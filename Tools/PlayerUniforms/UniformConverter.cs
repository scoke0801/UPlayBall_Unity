using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Baseball.Tools.PlayerUniforms
{
    /// <summary>정면 네이비 모자·흰 셔츠 원본의 의상 영역만 변환한다.</summary>
    public static class UniformConverter
    {
        /// <summary>마스크: 빨강=모자, 초록=셔츠, 파랑=파이핑/이너. 검정은 보존한다.</summary>
        public static void CreateMask(string sourcePath, string maskPath)
        {
            using (var source = new Bitmap(sourcePath))
            using (var mask = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            {
                for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                {
                    Color c = source.GetPixel(x, y);
                    double row = (double)y / source.Height;
                    bool blue = c.B > c.R + 12 && c.B > c.G + 5;
                    // 눈·눈썹·머리·피부를 포함하는 중앙부에는 의상 마스크를 만들지 않는다.
                    int cap = row < 0.48 && blue ? 255 : 0;
                    int shirt = 0, trim = 0;
                    if (row > 0.74 && c.A > 0 && c.R <= c.B + 20 && c.G <= c.B + 20)
                    {
                        // 파이핑 경계의 밝은 혼합 픽셀도 연속 가중치로 처리한다.
                        double navy = Math.Max(0, Math.Min(1, (c.B - c.R - 8) / 28.0))
                            * Math.Max(0, Math.Min(1, (190 - Math.Min(c.R, c.G)) / 95.0));
                        if (Math.Min(c.R, c.G) > 80 || blue)
                        {
                            trim = (int)Math.Round(255 * navy);
                            shirt = 255 - trim;
                        }
                    }
                    if (c.A == 0) cap = shirt = trim = 0;
                    mask.SetPixel(x, y, Color.FromArgb(255, cap, shirt, trim));
                }
                mask.Save(maskPath, ImageFormat.Png);
            }
        }

        /// <summary>마스크 밖 RGBA와 전체 알파를 픽셀 단위로 보존하며 명암을 유지한다.</summary>
        public static long Convert(string sourcePath, string maskPath, string outputPath,
            string capHex, string jerseyHex, string trimHex, bool pinstripes)
        {
            Color cap = ColorTranslator.FromHtml(capHex), jersey = ColorTranslator.FromHtml(jerseyHex), trim = ColorTranslator.FromHtml(trimHex);
            using (var source = new Bitmap(sourcePath))
            using (var mask = new Bitmap(maskPath))
            using (var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            {
                byte[] input = Read(source), masks = Read(mask), output = (byte[])Read(source).Clone();
                long preserved = 0;
                for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                {
                    int p = (y * source.Width + x) * 4;
                    Color s = Pixel(input, p), m = Pixel(masks, p);
                    Color color = s;
                    if (m.R > 0) color = Shade(s, cap, 0.40);
                    else if (m.G + m.B > 0)
                    {
                        Color body = Shade(s, jersey, 0.94);
                        if (pinstripes && m.G > 220)
                        {
                            // 배율을 바꿔도 같은 비율의 가는 세로줄을 유지한다. 피부와 파이핑에는 그리지 않는다.
                            double phase = (double)x / source.Width * 20;
                            double distance = Math.Abs(phase - Math.Round(phase));
                            double weight = Math.Max(0, 1 - distance / 0.055) * 0.65;
                            body = Blend(body, Shade(s, trim, 0.94), weight);
                        }
                        Color piping = Shade(s, trim, 0.40);
                        color = Blend(body, piping, m.B / 255.0);
                    }
                    else preserved++;
                    output[p] = color.B; output[p + 1] = color.G; output[p + 2] = color.R;
                }
                Write(result, output);
                result.Save(outputPath, ImageFormat.Png);
                // 저장된 PNG를 다시 읽어 보존 계약을 검사한다.
                using (var saved = new Bitmap(outputPath))
                {
                    byte[] actualBytes = Read(saved);
                    for (int y = 0; y < source.Height; y++)
                    for (int x = 0; x < source.Width; x++)
                    {
                        int p = (y * source.Width + x) * 4;
                        Color s = Pixel(input, p), m = Pixel(masks, p), actual = Pixel(actualBytes, p);
                        bool face = (double)y / source.Height >= 0.48 && (double)y / source.Height <= 0.74;
                        bool warmSkinOrHair = s.R > s.B + 24 && (double)y / source.Height > 0.40;
                        if (actual.A != s.A || ((face || warmSkinOrHair || m.R + m.G + m.B == 0) && actual.ToArgb() != s.ToArgb()))
                            throw new InvalidOperationException("얼굴/알파 보존 검증 실패: " + outputPath);
                    }
                }
                return preserved;
            }
        }

        private static Color Shade(Color source, Color target, double sourceMid)
        {
            double light = (source.R * 0.2126 + source.G * 0.7152 + source.B * 0.0722) / 255;
            double factor = light / sourceMid;
            if (factor <= 1) return Color.FromArgb(Byte(target.R * factor), Byte(target.G * factor), Byte(target.B * factor));
            double highlight = Math.Min(0.65, (factor - 1) * 0.45);
            return Blend(target, Color.White, highlight);
        }

        private static Color Blend(Color first, Color second, double weight)
        {
            return Color.FromArgb(Byte(first.R * (1 - weight) + second.R * weight),
                Byte(first.G * (1 - weight) + second.G * weight), Byte(first.B * (1 - weight) + second.B * weight));
        }

        private static int Byte(double value) { return Math.Max(0, Math.Min(255, (int)Math.Round(value))); }

        /// <summary>전경·투명 배경 존재와 원본의 녹색 잔여를 독립적으로 집계한다.</summary>
        public static long[] Inspect(string path)
        {
            using (var bitmap = new Bitmap(path))
            {
                byte[] pixels = Read(bitmap);
                long transparent = 0, opaque = 0, green = 0;
                for (int p = 0; p < pixels.Length; p += 4)
                {
                    if (pixels[p + 3] == 0) transparent++;
                    if (pixels[p + 3] == 255) opaque++;
                    if (pixels[p + 3] > 16 && pixels[p + 1] > pixels[p] + 40 && pixels[p + 1] > pixels[p + 2] + 40) green++;
                }
                bool corners = pixels[3] == 0 && pixels[(bitmap.Width - 1) * 4 + 3] == 0
                    && pixels[(bitmap.Height - 1) * bitmap.Width * 4 + 3] == 0 && pixels[pixels.Length - 1] == 0;
                return new[] { transparent, opaque, green, corners ? 1L : 0L };
            }
        }

        private static Color Pixel(byte[] data, int p) { return Color.FromArgb(data[p + 3], data[p + 2], data[p + 1], data[p]); }

        private static byte[] Read(Bitmap bitmap)
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try { var bytes = new byte[bitmap.Width * bitmap.Height * 4]; for (int y = 0; y < bitmap.Height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), bytes, y * bitmap.Width * 4, bitmap.Width * 4); return bytes; }
            finally { bitmap.UnlockBits(data); }
        }

        private static void Write(Bitmap bitmap, byte[] bytes)
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { for (int y = 0; y < bitmap.Height; y++) Marshal.Copy(bytes, y * bitmap.Width * 4, IntPtr.Add(data.Scan0, y * data.Stride), bitmap.Width * 4); }
            finally { bitmap.UnlockBits(data); }
        }
    }
}
