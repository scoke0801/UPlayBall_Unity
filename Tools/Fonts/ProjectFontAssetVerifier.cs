using System;
using System.IO;
using Baseball.Presentation.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Editor.Fonts
{
    /// <summary>저장된 기본 폰트의 참조와 실제 글자 렌더링을 확인한다.</summary>
    public static class ProjectFontAssetVerifier
    {
        /// <summary>기본 에셋을 다시 불러와 uGUI와 TMP의 한글·숫자 표본을 렌더링한다.</summary>
        public static void Verify()
        {
            if (TMP_Settings.defaultFontAsset.name != "esamanru Medium SDF" || UIProjectFonts.Default.name != "esamanru Medium")
                throw new InvalidOperationException("저장된 기본 폰트가 Medium이 아닙니다.");
            var camera = new GameObject("Font verification camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            camera.orthographic = true;
            var canvas = new GameObject("Font verification", typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            string sample = "선수 구단주 경기 시즌 훈련 계약 은퇴  타율 0.321  홈런 48  1,250,000";
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Sample " + i, typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.offsetMin = new Vector2(40, -110 - i * 130);
                rect.offsetMax = new Vector2(-40, -30 - i * 130);
                if (i == 0)
                {
                    var label = go.AddComponent<Text>();
                    label.font = UIProjectFonts.Default;
                    label.fontSize = 28;
                    label.text = "uGUI Medium 기본 폰트\n" + sample;
                    label.color = Color.white;
                }
                else
                {
                    string weight = new[] { "", "Light", "Medium", "Bold" }[i];
                    var label = go.AddComponent<TextMeshProUGUI>();
                    label.font = Resources.Load<TMP_FontAsset>("Fonts/esamanru " + weight + " SDF");
                    label.fontSize = 28;
                    label.text = "TMP " + weight + "\n" + sample;
                    label.color = Color.white;
                    label.ForceMeshUpdate();
                }
            }
            Canvas.ForceUpdateCanvases();
            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes("../font-preview.png", image.EncodeToPNG());
            File.WriteAllText("../persisted-validation.txt", "PASS: persisted TMP/uGUI Medium references and four rendered font specimens");
        }
    }
}
