using System;
using System.IO;
using System.Reflection;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Presentation.Owner;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Editor.Tools
{
    /// <summary>실제 카드 앞뒷면 Builder를 테스트 데이터로 렌더링한다.</summary>
    public static class PlayerCardBackVisualExporter
    {
        [BaseballEditorTool("UI", "선수 카드 시각 검증", "실제 Runtime Builder로 카드 앞뒷면 PNG를 docs/reports/player-card-back-visuals에 출력합니다.", impact: ToolImpact.DataWrite)]
        public static void Export()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("카드 PNG 출력에는 그래픽 장치가 필요합니다. -nographics 없이 실행하세요.");
            string output = Path.GetFullPath("docs/reports/player-card-back-visuals");
            Directory.CreateDirectory(output);
            foreach (int height in new[] { 960, 640 })
            {
                RenderFixture(CreateFixture(0), height, Path.Combine(output, "hitter-front-" + height + ".png"), "BuildFront");
                RenderFixture(CreateFixture(4), height, Path.Combine(output, "pitcher-front-" + height + ".png"), "BuildFront");
                RenderFixture(CreateFixture(0), height, Path.Combine(output, "hitter-" + height + ".png"));
                for (int count = 2; count <= 6; count++)
                    RenderFixture(CreateFixture(count), height, Path.Combine(output, "pitcher-" + count + "-" + height + ".png"));
            }
            Debug.Log("카드 앞뒷면 테스트 데이터 PNG 16장 출력: " + output);
        }

        private static void RenderFixture(OwnerCollectionCardSnapshot snapshot, int height, string path, string builder = "BuildReferenceBack")
        {
            int width = Mathf.RoundToInt(height * 228f / 320f);
            var cameraObject = new GameObject("CardBackVisualCamera", typeof(Camera));
            var root = new GameObject("CardBackVisualFixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.04f, .07f, .12f);
                camera.orthographic = true;
                camera.targetTexture = renderTexture;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                RectTransform rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var view = root.AddComponent<UI_Popup_OwnerPlayerCard>();
                view.enabled = false;
                MethodInfo build = typeof(UI_Popup_OwnerPlayerCard).GetMethod(builder, BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new MissingMethodException("Runtime 카드 Builder를 찾을 수 없습니다: " + builder);
                build.Invoke(view, new object[] { rect, snapshot, snapshot.Pitches.Count > 0 });
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static OwnerCollectionCardSnapshot CreateFixture(int count)
        {
            PitchType[] types = { PitchType.FourSeamFastball, PitchType.Slider, PitchType.CircleChangeup,
                PitchType.KnuckleCurve, PitchType.Splitter, PitchType.Curveball };
            string[] names = { "포심", "슬라이더", "서클 체인지업", "너클 커브", "스플리터", "커브" };
            string[] grades = { "A", "SS", "D", "B", "S", "A" };
            var pitches = new OwnerPitchCardSnapshot[count];
            for (int index = 0; index < count; index++)
                pitches[index] = new OwnerPitchCardSnapshot(types[index], names[index], grades[index], 151 - index * 5);
            var records = count > 0 ? new[] { new OwnerCardRecordFieldSnapshot("G", "42"),
                new OwnerCardRecordFieldSnapshot("W-L", "16-7"), new OwnerCardRecordFieldSnapshot("ERA", "2.81"),
                new OwnerCardRecordFieldSnapshot("IP", "192.2"), new OwnerCardRecordFieldSnapshot("SO", "198"),
                new OwnerCardRecordFieldSnapshot("WHIP", "1.09") } : new[] {
                new OwnerCardRecordFieldSnapshot("G", "144"), new OwnerCardRecordFieldSnapshot("PA", "625"),
                new OwnerCardRecordFieldSnapshot("AVG", ".312"), new OwnerCardRecordFieldSnapshot("HR", "31"),
                new OwnerCardRecordFieldSnapshot("RBI", "112"), new OwnerCardRecordFieldSnapshot("OPS", ".942") };
            return new OwnerCollectionCardSnapshot("visual-fixture", "visual-person", "가상선수 시각검증", 2025,
                count > 0 ? PlayerPosition.StartingPitcher : PlayerPosition.Shortstop, 8, PlayerCardEdition.Normal,
                0, 0, false, false, new AbilityRatings(72), "검증용 시즌 기록", "visual-season",
                count > 0 ? PitcherRole.Starter : (PitcherRole?)null, Handedness.Right, Handedness.Left, pitches, records);
        }
    }
}
