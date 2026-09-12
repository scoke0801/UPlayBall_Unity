using System;
using System.IO;
using Baseball.Presentation.UI;
using Baseball.Presentation.SharedUI;
using Baseball.Core.Historical;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>공용 글자의 부모 확대 보정과 실제 카드의 한글 표시를 검증한다.</summary>
    public sealed class UIProjectTextTests
    {
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void ScaledPanels_RenderAtDisplayDensity(int width, int height)
        {
            var host = new GameObject("Font samples", typeof(Canvas));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.12f, .14f, .17f);
                camera.orthographic = true;
                camera.targetTexture = target;
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                var panel = new GameObject("Scaled panel", typeof(RectTransform)).GetComponent<RectTransform>();
                panel.SetParent(host.transform, false);
                panel.sizeDelta = new Vector2(1100, 560);
                float scale = Mathf.Min(width / 1100f, height / 560f);
                panel.localScale = Vector3.one * scale;
                Canvas.ForceUpdateCanvases();
                AddText<Text>(panel, "기존 · 선수 선택  함창건 좌익수 2025년  육성 포인트 3,000", 24, 220);
                var revised = AddText<UIProjectText>(panel, "수정 · 선수 선택  함창건 좌익수 2025년  육성 포인트 3,000", 24, 170);
                AddText<Text>(panel, "기존 작은 글자 · 함창건 좌익수 2025  코스트 3  훈련 대기", 9, 125);
                AddText<UIProjectText>(panel, "수정 작은 글자 · 함창건 좌익수 2025  코스트 3  훈련 대기", 9, 100);
                for (int i = 0; i < 5; i++)
                {
                    var card = PlayerMiniCardView.CreateRuntime(panel);
                    card.UseLineupSlotLayout();
                    card.Bind(new PlayerMiniCardModel("sample" + i, "함창건", "좌익수", "25", "3", "일반",
                        frameEdition: PlayerCardEdition.Normal, cost: 3));
                    var rect = card.GetComponent<RectTransform>();
                    rect.anchoredPosition = new Vector2(-430 + i * 170, -60);
                    rect.sizeDelta = new Vector2(130, 200);
                }
                Canvas.ForceUpdateCanvases();
                Assert.That(revised.RasterPixelsPerUnit, Is.EqualTo(canvas.scaleFactor * scale).Within(.001f));
                Assert.That(revised.cachedTextGenerator.vertexCount, Is.GreaterThan(0));
                Assert.That(revised.preferredHeight, Is.LessThan(40));
                float oldWidth = revised.preferredWidth;
                panel.localScale = Vector3.one;
                Assert.That(revised.RasterPixelsPerUnit, Is.EqualTo(canvas.scaleFactor).Within(.001f));
                Assert.That(revised.preferredWidth, Is.EqualTo(oldWidth).Within(24),
                    "생성 해상도를 바꿔도 레이아웃의 논리 크기는 유지한다.");
                panel.localScale = Vector3.one * scale;
                revised.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var prior = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    image.Apply();
                    string folder = Environment.GetEnvironmentVariable("BASEBALL_FONT_CAPTURE");
                    if (!string.IsNullOrEmpty(folder))
                    {
                        Directory.CreateDirectory(folder);
                        File.WriteAllBytes(Path.Combine(folder, width + "x" + height + ".png"), image.EncodeToPNG());
                    }
                }
                finally { RenderTexture.active = prior; Object.DestroyImmediate(image); }
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static T AddText<T>(Transform parent, string value, int size, float y) where T : Text
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<T>();
            label.font = UIProjectFonts.Default;
            label.text = value;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.sizeDelta = new Vector2(1000, 40);
            label.rectTransform.anchoredPosition = new Vector2(0, y);
            return label;
        }
    }
}
