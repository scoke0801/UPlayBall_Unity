using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Baseball.Tools.ImageBackground
{
    /// <summary>단색 크로마키를 제거하고 혼합 경계의 알파와 전경색을 복원한다.</summary>
    public static class ChromaKeyRemoval
    {
        /// <summary>분리된 전경과 내부 틈을 모두 처리하되 원본 알파를 증가시키지 않는다.</summary>
        public static void Run(string input, string output, Color key, int tolerance, int opaqueDistance, int radius)
        {
            using (var source = new Bitmap(input))
            using (var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    graphics.DrawImageUnscaled(source, 0, 0);
                }
                int width = bitmap.Width, height = bitmap.Height;
                var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                int transparent = 0, partial = 0, keyed = 0;
                try
                {
                    var original = new byte[data.Stride * height];
                    Marshal.Copy(data.Scan0, original, 0, original.Length);
                    var result = (byte[])original.Clone();
                    var distance = new int[width * height];
                    var keyBytes = new[] { (double)key.B, key.G, key.R };
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int p = y * data.Stride + x * 4, index = y * width + x;
                        bool isKey = GetColorDistance(original, p, keyBytes) <= tolerance;
                        if (isKey && original[p + 3] > 0) keyed++;
                        distance[index] = isKey || original[p + 3] == 0 ? 0 : radius + 1;
                    }
                    // 두 번의 고정 순회로 배경에서 가까운 경계만 찾는다. 의상 내부 색은 보정하지 않는다.
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        if (x > 0) distance[i] = Math.Min(distance[i], distance[i - 1] + 1);
                        if (y > 0) distance[i] = Math.Min(distance[i], distance[i - width] + 1);
                    }
                    for (int y = height - 1; y >= 0; y--)
                    for (int x = width - 1; x >= 0; x--)
                    {
                        int i = y * width + x;
                        if (x + 1 < width) distance[i] = Math.Min(distance[i], distance[i + 1] + 1);
                        if (y + 1 < height) distance[i] = Math.Min(distance[i], distance[i + width] + 1);
                    }
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int p = y * data.Stride + x * 4;
                        int edgeDistance = distance[y * width + x];
                        if (edgeDistance == 0)
                            result[p] = result[p + 1] = result[p + 2] = result[p + 3] = 0;
                        else if (radius > 0 && (edgeDistance <= radius || IsKeyTinted(original, p, keyBytes, 40)) && IsKeyTinted(original, p, keyBytes) && GetColorDistance(original, p, keyBytes) < opaqueDistance)
                            RestoreEdge(original, result, p, x, y, width, height, data.Stride, keyBytes, radius + 4);
                        if (result[p + 3] == 0) transparent++;
                        else if (result[p + 3] < 255) partial++;
                    }
                    if (keyed == 0 || transparent == width * height)
                        throw new InvalidOperationException("키 색 배경 또는 전경이 없습니다. 입력과 KeyColor를 확인하세요.");
                    Marshal.Copy(result, 0, data.Scan0, result.Length);
                }
                finally { bitmap.UnlockBits(data); }
                bitmap.Save(output, ImageFormat.Png);
                Console.WriteLine("Chroma RGBA: {0}x{1}; transparent: {2}; partial: {3}", width, height, transparent, partial);
            }
        }

        private static double GetColorDistance(byte[] pixels, int p, double[] key)
        {
            return Math.Max(Math.Abs(pixels[p] - key[0]), Math.Max(Math.Abs(pixels[p + 1] - key[1]), Math.Abs(pixels[p + 2] - key[2])));
        }

        // 키의 높은 채널 모두가 우세해야 한다. 평균을 쓰면 붉은 피부와 갈색 머리까지 키로 오인한다.
        private static bool IsKeyTinted(byte[] pixels, int p, double[] key, int minimumDominance = 10)
        {
            double mean = (key[0] + key[1] + key[2]) / 3;
            double high = 255, low = 0;
            for (int channel = 0; channel < 3; channel++)
            {
                if (key[channel] > mean) high = Math.Min(high, pixels[p + channel]);
                else low = Math.Max(low, pixels[p + channel]);
            }
            return high - low > minimumDominance;
        }
        private static void RestoreEdge(byte[] original, byte[] result, int p, int x, int y, int width, int height, int stride, double[] key, int searchRadius)
        {
            double bestScore = double.MaxValue, bestAlpha = 1;
            int bestForeground = -1;
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                int q = ny * stride + nx * 4;
                if (original[q + 3] == 0 || IsKeyTinted(original, q, key)) continue;
                double numerator = 0, denominator = 0;
                for (int channel = 0; channel < 3; channel++)
                {
                    double foreground = original[q + channel] - key[channel];
                    numerator += (original[p + channel] - key[channel]) * foreground;
                    denominator += foreground * foreground;
                }
                double alpha = Math.Max(0, Math.Min(1, numerator / Math.Max(1, denominator)));
                double residual = 0;
                for (int channel = 0; channel < 3; channel++)
                {
                    double error = original[p + channel] - (alpha * original[q + channel] + (1 - alpha) * key[channel]);
                    residual += error * error;
                }
                // 색 복원이 비슷하면 가까운 전경을 택해 가는 머리카락에 피부색이 섞이지 않게 한다.
                double score = residual + dx * dx + dy * dy;
                if (score >= bestScore) continue;
                bestScore = score;
                bestAlpha = alpha;
                bestForeground = q;
            }
            if (bestScore == double.MaxValue) return;
            byte alphaByte = (byte)Math.Round(original[p + 3] * bestAlpha);
            for (int channel = 0; channel < 3; channel++)
            {
                // 생성 이미지의 경계 압축 오차를 낮은 알파로 나누면 노랑·청록 반점이 생긴다.
                // 혼합식에 가장 잘 맞는 근처 전경색을 사용해 키 색을 제거한다.
                double foreground = original[bestForeground + channel];
                result[p + channel] = alphaByte == 0 ? (byte)0 : (byte)Math.Max(0, Math.Min(255, Math.Round(foreground)));
            }
            result[p + 3] = alphaByte;
        }
    }
}
