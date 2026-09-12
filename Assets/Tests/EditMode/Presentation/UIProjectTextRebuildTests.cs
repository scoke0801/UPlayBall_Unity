using System;
using System.Reflection;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>최초 폰트 텍스처 갱신에 중첩된 다른 글자의 메시가 섞이지 않는지 검증한다.</summary>
    public sealed class UIProjectTextRebuildTests
    {
        [TestCase(1f)]
        [TestCase(2.2f)]
        public void FirstGlyphPopulation_DiscardsNestedTextGeometry(float scale)
        {
            var host = new GameObject("Font rebuild", typeof(Canvas));
            var font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/08.Fonts/esamanru Medium.ttf");
            using var sharedMesh = new VertexHelper();
            var populate = typeof(UIProjectText).GetMethod("OnPopulateMesh",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            int nestedRebuilds = 0;
            bool isNested = false;
            Action<Font> onRebuilt = null;
            try
            {
                Assert.That(font.dynamic, Is.True);
                var heading = CreateText(host.transform, font, "파견 범위 · 탐색 방침 · 비용", 13, scale);
                populate.Invoke(heading, new object[] { sharedMesh });
                // 짧은 문구는 기존 아틀라스의 빈 공간에 들어가 갱신 이벤트가 생기지 않을 수 있다.
                // 여러 화면이 처음 열릴 때처럼 아직 없는 한글을 한 번에 요청해 재배치를 유발한다.
                var characters = new char[2048];
                for (int i = 0; i < characters.Length; i++) characters[i] = (char)('\uac00' + i);
                int fontSize = 31;
                while (font.GetCharacterInfo(characters[0], out _, Mathf.RoundToInt(fontSize * scale))) fontSize++;
                var choice = CreateText(host.transform, font, new string(characters), fontSize, scale);
                onRebuilt = rebuilt =>
                {
                    if (rebuilt != font || isNested) return;
                    isNested = true;
                    try
                    {
                        // uGUI가 폰트 갱신 중 다른 Text를 같은 공유 버퍼로 그리는 재진입을 재현한다.
                        heading.cachedTextGenerator.Invalidate();
                        populate.Invoke(heading, new object[] { sharedMesh });
                        nestedRebuilds++;
                    }
                    finally { isNested = false; }
                };
                Font.textureRebuilt += onRebuilt;
                populate.Invoke(choice, new object[] { sharedMesh });
                Assert.That(choice.cachedTextGenerator.vertexCount, Is.GreaterThan(0));
                Assert.That(nestedRebuilds, Is.GreaterThan(0), "실제 최초 글리프 생성으로 폰트 텍스처 갱신을 일으켜야 한다.");
                Assert.That(sharedMesh.currentVertCount, Is.EqualTo(choice.cachedTextGenerator.verts.Count),
                    "최초 메시에는 현재 텍스트의 정점만 있어야 한다.");
                int firstCount = sharedMesh.currentVertCount;
                populate.Invoke(choice, new object[] { sharedMesh });
                Assert.That(sharedMesh.currentVertCount, Is.EqualTo(firstCount), "최초 표시와 재표시의 글자 수가 같아야 한다.");
                choice.text = string.Empty;
                populate.Invoke(choice, new object[] { sharedMesh });
                Assert.That(sharedMesh.currentVertCount, Is.Zero);
            }
            finally
            {
                if (onRebuilt != null) Font.textureRebuilt -= onRebuilt;
                Object.DestroyImmediate(host);
            }
        }

        private static UIProjectText CreateText(Transform parent, Font font, string value, int size, float scale)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<UIProjectText>();
            label.font = font;
            label.fontSize = size;
            label.text = value;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.sizeDelta = new Vector2(360, 100);
            label.transform.localScale = Vector3.one * scale;
            return label;
        }
    }
}
