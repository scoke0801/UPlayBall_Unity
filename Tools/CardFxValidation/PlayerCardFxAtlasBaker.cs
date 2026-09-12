using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Baseball.Editor.CardFx
{
    /// <summary>Imagegen 원화 한 칸의 좌표를 고정하고 광량만 다른 플립북을 셰이더로 베이크한다.</summary>
    public static class PlayerCardFxAtlasBaker
    {
        [Serializable]
        private sealed class Settings
        {
            public float sweepStrength;
            public float sparkleStrength;
            public float sweepWidth;
        }

        /// <summary>격리 Unity 프로젝트에서 시트와 위치 안정성 검증 결과를 내보낸다.</summary>
        public static void Build()
        {
            string output = Environment.GetEnvironmentVariable("BASEBALL_CARD_FX_BAKE_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("베이크 출력 경로가 필요합니다.");
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Source.png");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/PlayerCardFxBake.shader");
            if (source == null || shader == null || !shader.isSupported)
                throw new InvalidOperationException("고정 원화 또는 베이크 셰이더를 사용할 수 없습니다.");
            var settings = JsonUtility.FromJson<Settings>(File.ReadAllText("Assets/StableFlipbook.json"));
            var material = new Material(shader);
            var target = RenderTexture.GetTemporary(1024, 1536, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var image = new Texture2D(1024, 1536, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                material.SetFloat("_SweepStrength", settings.sweepStrength);
                material.SetFloat("_SparkleStrength", settings.sparkleStrength);
                material.SetFloat("_SweepWidth", settings.sweepWidth);
                ShaderUtil.CompilePass(material, 0, true);
                Graphics.Blit(source, target, material, 0);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1024, 1536), 0, 0);
                image.Apply();
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "bake-review.png"), image.EncodeToPNG());
                string report = Validate(image.GetPixels32());
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "PlayerCardFX_GoldHolographic_Flipbook_v2.png"), image.EncodeToPNG());
                File.WriteAllText(Path.Combine(output, "alignment.txt"), report);
                Debug.Log(report);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static string Validate(Color32[] pixels)
        {
            var report = new StringBuilder("고정 원화 플립북 위치 검증\n");
            int animatedFrames = 0;
            for (int frame = 0; frame < 16; frame++)
            {
                int changed = 0;
                for (int y = 0; y < 384; y++)
                for (int x = 0; x < 256; x++)
                {
                    Color32 before = GetPixel(pixels, 0, x, y);
                    Color32 after = GetPixel(pixels, frame, x, y);
                    if (before.r != after.r || before.g != after.g || before.b != after.b) changed++;
                    if (after.a != 255) throw new InvalidOperationException($"불투명 배경에 알파 누락: frame={frame}, x={x}, y={y}, pixel={after}");
                }
                if (changed > 0) animatedFrames++;
                if (frame == 15 && changed != 0) throw new InvalidOperationException("루프 양 끝의 원화가 다릅니다.");
                report.Append("frame ").Append(frame).Append(": ");
                // 전체 화면의 중심이 같아도 모서리가 흔들리는 문제를 잡도록 네 구역을 따로 검사한다.
                for (int region = 0; region < 4; region++)
                {
                    Vector2Int shift = FindShift(pixels, frame, region, 0);
                    if (shift != Vector2Int.zero) throw new InvalidOperationException($"프레임 {frame}, 구역 {region}에 {shift} 이동이 있습니다.");
                    report.Append("region ").Append(region).Append("=(0,0) ");
                }
                report.Append("changed=").Append(changed).AppendLine();
            }
            if (animatedFrames != 14) throw new InvalidOperationException("중간 프레임의 광량 변화가 누락됐습니다.");
            // 한 픽셀 밀린 비교 대조군을 실제로 찾아야 정렬 검사를 신뢰할 수 있다.
            if (FindShift(pixels, 0, 0, 1) != new Vector2Int(-1, 0))
                throw new InvalidOperationException("정렬 검사가 한 픽셀 이동 대조군을 검출하지 못했습니다.");
            return report.AppendLine("PASS: 64개 구역 위치 오차 0px / 14개 중간 프레임 광량 변화 / 첫·마지막 프레임 동일 / 1px 이동 대조군 검출").ToString();
        }

        private static Vector2Int FindShift(Color32[] pixels, int frame, int region, int controlOffset)
        {
            double best = double.NegativeInfinity;
            var result = Vector2Int.zero;
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                double dot = 0, lengthA = 0, lengthB = 0;
                int left = 8 + region % 2 * 128;
                int bottom = 8 + region / 2 * 192;
                for (int y = bottom; y < bottom + 176; y += 3)
                for (int x = left; x < left + 112; x += 3)
                {
                    Vector2 a = Gradient(pixels, 0, x, y);
                    Vector2 b = Gradient(pixels, frame, x + dx + controlOffset, y + dy);
                    dot += Vector2.Dot(a, b);
                    lengthA += a.sqrMagnitude;
                    lengthB += b.sqrMagnitude;
                }
                double score = dot / Math.Sqrt(Math.Max(1e-12, lengthA * lengthB));
                if (score <= best) continue;
                best = score;
                result = new Vector2Int(dx, dy);
            }
            return result;
        }

        private static Vector2 Gradient(Color32[] pixels, int frame, int x, int y) => new Vector2(
            Luminance(GetPixel(pixels, frame, x + 1, y)) - Luminance(GetPixel(pixels, frame, x - 1, y)),
            Luminance(GetPixel(pixels, frame, x, y + 1)) - Luminance(GetPixel(pixels, frame, x, y - 1)));

        private static float Luminance(Color32 pixel) => pixel.r * .2126f + pixel.g * .7152f + pixel.b * .0722f;

        private static Color32 GetPixel(Color32[] pixels, int frame, int x, int y) =>
            pixels[((3 - frame / 4) * 384 + y) * 1024 + frame % 4 * 256 + x];
    }
}
