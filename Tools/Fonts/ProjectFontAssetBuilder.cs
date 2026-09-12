using System;
using System.IO;
using System.Linq;
using Baseball.Presentation.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Baseball.Editor.Fonts
{
    /// <summary>원본 TTF에서 동적 SDF와 공통 Medium 기본 설정을 생성한다.</summary>
    public static class ProjectFontAssetBuilder
    {
        /// <summary>빈 작업 프로젝트에 폰트와 기본 설정을 생성하고 글자 지원을 검증한다.</summary>
        public static void Build()
        {
            const string root = "Assets/08.Fonts/Resources";
            if (Directory.Exists(root + "/Fonts") && Directory.GetFiles(root + "/Fonts", "*.asset").Length > 0)
                throw new InvalidOperationException("생성 대상에 기존 에셋이 있습니다. 빈 작업 프로젝트를 사용하세요.");
            Directory.CreateDirectory(root + "/Fonts");
            AssetDatabase.Refresh();
            var fonts = new TMP_FontAsset[3];
            string[] weights = { "Light", "Medium", "Bold" };
            for (int i = 0; i < weights.Length; i++)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/08.Fonts/esamanru " + weights[i] + ".ttf");
                var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                if (font == null) throw new InvalidOperationException("폰트 생성 실패: " + weights[i]);
                font.name = "esamanru " + weights[i] + " SDF";
                AssetDatabase.CreateAsset(font, root + "/Fonts/" + font.name + ".asset");
                font.material.name = font.name + " Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
                // 한글 전체를 고정 베이크하지 않고 필요한 글자를 동적 멀티 아틀라스로 보충한다.
                string sample = string.Concat(Enumerable.Range(32, 95).Select(x => (char)x)) + "선수구단주경기시즌훈련계약은퇴승패무타율홈런삼진안녕하세요대한민국";
                if (!font.TryAddCharacters(sample, out string missing))
                    throw new InvalidOperationException("누락 글자: " + missing);
                foreach (var texture in font.atlasTextures)
                {
                    texture.name = font.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(texture, font);
                }
                EditorUtility.SetDirty(font);
                fonts[i] = font;
            }
            foreach (var font in fonts)
            {
                var table = font.fontWeightTable;
                table[3].regularTypeface = fonts[0];
                table[4].regularTypeface = fonts[1];
                table[5].regularTypeface = fonts[1];
                table[7].regularTypeface = fonts[2];
                EditorUtility.SetDirty(font);
            }
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = fonts[1];
            serialized.FindProperty("m_defaultFontAssetPath").stringValue = "Fonts/";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var definition = ScriptableObject.CreateInstance<UIProjectFonts>();
            var definitionData = new SerializedObject(definition);
            definitionData.FindProperty("_medium").objectReferenceValue = fonts[1].sourceFontFile;
            definitionData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, root + "/Fonts/DefaultFonts.asset");
            AssetDatabase.SaveAssets();
            if (TMP_Settings.defaultFontAsset != fonts[1] || UIProjectFonts.Default != fonts[1].sourceFontFile)
                throw new InvalidOperationException("기본 폰트 연결 검증 실패");
            File.WriteAllText("font-validation.txt", "PASS: Light/Medium/Bold SDF, Korean/ASCII glyphs, dynamic multi-atlas, TMP Medium default, uGUI Medium default");
        }
    }
}
