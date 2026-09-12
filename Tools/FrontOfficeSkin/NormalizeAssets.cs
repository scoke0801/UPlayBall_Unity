using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Baseball.Tools.FrontOfficeSkin
{
    /// <summary>ImageGen 원본을 잘라 2배 크기와 상태별 동일 알파 외곽으로 정렬한다.</summary>
    public static class NormalizeAssets
    {
        /// <summary>상태별 외곽 알파가 Normal과 픽셀 단위로 같은지 확인한다.</summary>
        public static int CompareAlpha(string first, string second)
        {
            using (var a = new Bitmap(first)) using (var b = new Bitmap(second))
            {
                if (a.Size != b.Size) return -1;
                var left = Read(a); var right = Read(b); int difference = 0;
                for (int i = 3; i < left.Length; i += 4) if (left[i] != right[i]) difference++;
                return difference;
            }
        }

        public static string Process(string input, string output, int width, int height, int border,
            string normal, double brightnessRatio, double opacity, bool panel)
        {
            using (var source = new Bitmap(input))
            using (var result = Fit(source, width, height, border))
            {
                byte[] data = Read(result);
                byte[] baseline = null;
                if (!string.IsNullOrEmpty(normal))
                    using (var reference = new Bitmap(normal)) baseline = Read(reference);
                double mean = Mean(data, width, height);
                double gain = baseline != null && brightnessRatio > 0 ? Mean(baseline, width, height) * brightnessRatio / Math.Max(1, mean) : 1;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    if (baseline != null)
                    {
                        // 생성 상태의 외곽 오차는 Normal의 알파를 공유해 버튼 전환 시 흔들림을 막는다.
                        if (data[i + 3] < 64 && baseline[i + 3] > 0)
                            for (int c = 0; c < 3; c++) data[i + c] = baseline[i + c];
                        data[i + 3] = baseline[i + 3];
                    }
                    for (int c = 0; c < 3; c++) data[i + c] = (byte)Math.Min(255, Math.Round(data[i + c] * gain));
                    double alpha = opacity;
                    if (panel)
                    {
                        int edge = Math.Min(Math.Min(x, width - 1 - x), Math.Min(y, height - 1 - y));
                        // 프레임 테두리의 선명함은 유지하고 본문 면만 반투명하게 만든다.
                        alpha = 1 - (1 - opacity) * Math.Max(0, Math.Min(1, (edge - 8) / 16.0));
                    }
                    data[i + 3] = (byte)Math.Round(data[i + 3] * alpha);
                }
                // 완전 투명 영역의 RGB는 인접 전경색으로 채워 축소 필터의 색 매트를 막는다.
                for (int i = 0; i < data.Length; i += 4)
                    if (data[i + 1] > data[i + 2] + 55 && data[i + 1] > data[i] + 55)
                        data[i + 1] = Math.Max(data[i], data[i + 2]);
                if (panel && (output.Contains("MainDashboard") || output.Contains("ManagerCard")))
                    MoveHeaderLine(data, width, height, border);
                Bleed(data, width, height);
                Write(result, data);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                result.Save(output, ImageFormat.Png);
                int clear = 0, partial = 0, spill = 0;
                for (int i = 0; i < data.Length; i += 4)
                {
                    if (data[i + 3] == 0) clear++;
                    else if (data[i + 3] < 255) partial++;
                    if (data[i + 3] > 0 && data[i + 1] > data[i + 2] + 55 && data[i + 1] > data[i] + 55) spill++;
                }
                return width + "," + height + "," + clear + "," + partial + "," + spill + "," + Mean(data, width, height).ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static void MoveHeaderLine(byte[] data, int width, int height, int border)
        {
            int first = -1, last = -1;
            for (int y = 20; y < height / 4; y++)
            {
                int i = (y * width + width / 2) * 4;
                if (data[i + 2] > 95 && data[i + 2] > data[i] * 1.4) { if (first < 0) first = y; last = y; }
            }
            if (first < 0 || last - first > 12) return;
            int start = Math.Max(0, first - 2), end = last + 2;
            var strip = new byte[(end - start + 1) * width * 4];
            Array.Copy(data, start * width * 4, strip, 0, strip.Length);
            for (int y = start; y <= end; y++) for (int x = 18; x < width - 18; x++)
                for (int c = 0; c < 3; c++) data[(y * width + x) * 4 + c] = data[((end + 2) * width + x) * 4 + c];
            for (int y = 0; y <= end - start; y++) for (int x = border; x < width - border; x++)
                for (int c = 0; c < 3; c++) data[((124 + y) * width + x) * 4 + c] = strip[(y * width + x) * 4 + c];
        }

        private static Bitmap Fit(Bitmap source, int width, int height, int border)
        {
            byte[] data = Read(source);
            int left = source.Width, right = 0, top = source.Height, bottom = 0;
            for (int y = 0; y < source.Height; y++) for (int x = 0; x < source.Width; x++)
                if (data[(y * source.Width + x) * 4 + 3] > 16)
                { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
            if (right <= left || bottom <= top) throw new InvalidDataException("전경이 없는 생성 이미지: " + source);
            // 바깥쪽 한 픽셀은 완전 투명하게 남겨 실제 알파 경계를 보존한다.
            var crop = Rectangle.FromLTRB(Math.Max(0, left - 1), Math.Max(0, top - 1), Math.Min(source.Width, right + 2), Math.Min(source.Height, bottom + 2));
            double scale = Math.Min(1, Math.Min((double)width / crop.Width, (double)height / crop.Height));
            int scaledWidth = Math.Max(2, (int)Math.Round(crop.Width * scale));
            int scaledHeight = Math.Max(2, (int)Math.Round(crop.Height * scale));
            using (var scaled = new Bitmap(scaledWidth, scaledHeight, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(scaled))
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(source, new Rectangle(0, 0, scaledWidth, scaledHeight), crop.X, crop.Y, crop.Width, crop.Height, GraphicsUnit.Pixel, attributes);
                }
                var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                int bx = Math.Min(border, Math.Min(scaledWidth, width) / 3);
                int by = Math.Min(border, Math.Min(scaledHeight, height) / 3);
                int[] sx = { 0, bx, scaledWidth - bx, scaledWidth }, sy = { 0, by, scaledHeight - by, scaledHeight };
                int[] dx = { 0, bx, width - bx, width }, dy = { 0, by, height - by, height };
                using (var g = Graphics.FromImage(result))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                        if (dx[x + 1] > dx[x] && dy[y + 1] > dy[y] && sx[x + 1] > sx[x] && sy[y + 1] > sy[y])
                        g.DrawImage(scaled, Rectangle.FromLTRB(dx[x], dy[y], dx[x + 1], dy[y + 1]), Rectangle.FromLTRB(sx[x], sy[y], sx[x + 1], sy[y + 1]), GraphicsUnit.Pixel);
                }
                return result;
            }
        }

        private static double Mean(byte[] data, int width, int height)
        {
            double sum = 0; int count = 0;
            for (int y = height / 3; y < height * 2 / 3; y++) for (int x = width / 3; x < width * 2 / 3; x++)
            { int i = (y * width + x) * 4; sum += .2126 * data[i + 2] + .7152 * data[i + 1] + .0722 * data[i]; count++; }
            return sum / Math.Max(1, count);
        }

        private static void Bleed(byte[] data, int width, int height)
        {
            byte[] known = new byte[width * height];
            for (int i = 0; i < known.Length; i++) known[i] = data[i * 4 + 3] > 0 ? (byte)1 : (byte)0;
            for (int pass = 0; pass < 3; pass++)
            {
                var next = (byte[])known.Clone();
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    int p = y * width + x; if (known[p] != 0) continue;
                    int neighbor = x > 0 && known[p - 1] != 0 ? p - 1 : x + 1 < width && known[p + 1] != 0 ? p + 1
                        : y > 0 && known[p - width] != 0 ? p - width : y + 1 < height && known[p + width] != 0 ? p + width : -1;
                    if (neighbor < 0) continue;
                    for (int c = 0; c < 3; c++) data[p * 4 + c] = data[neighbor * 4 + c];
                    next[p] = 1;
                }
                known = next;
            }
        }

        private static byte[] Read(Bitmap image)
        {
            var bits = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var data = new byte[image.Width * image.Height * 4];
            for (int y = 0; y < image.Height; y++) Marshal.Copy(IntPtr.Add(bits.Scan0, y * bits.Stride), data, y * image.Width * 4, image.Width * 4);
            image.UnlockBits(bits); return data;
        }

        private static void Write(Bitmap image, byte[] data)
        {
            var bits = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < image.Height; y++) Marshal.Copy(data, y * image.Width * 4, IntPtr.Add(bits.Scan0, y * bits.Stride), image.Width * 4);
            image.UnlockBits(bits);
        }
    }
}
