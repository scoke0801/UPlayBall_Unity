using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단주 화면의 역할별 V2 자원과 재적용 진입점을 제공한다.</summary>
    public static class UIOwnerFrontOfficeSkin
    {
        public const string ResourceRoot = "UI/BaseballFrontOffice/V2/";
        private static readonly Dictionary<string, Sprite> Sprites = new();
        // 모드를 선택하지 않은 독립 구단주 화면도 저작·검수할 수 있다.
        public static bool IsOwnerContext => !Baseball.Presentation.SharedUI.UiGameModeSession.CurrentMode.HasValue
            || Baseball.Presentation.SharedUI.UiGameModeSession.IsSelected(Baseball.Presentation.SharedUI.UiGameMode.OwnerCareer);

        /// <summary>상태 데이터로 표시 여부를 제어하는 비입력 배지를 생성한다.</summary>
        public static Image CreateBadge(Transform parent, string name, string kind, float size)
        {
            var badge = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            badge.transform.SetParent(parent, false);
            badge.sprite = Load("Badges/UI_Badge_" + kind);
            badge.type = Image.Type.Simple;
            badge.preserveAspect = true;
            badge.raycastTarget = false;
            badge.rectTransform.sizeDelta = new Vector2(size, size);
            badge.gameObject.AddComponent<Baseball.Presentation.UI.CareerUiVisualElement>()
                .Initialize(Baseball.Presentation.UI.CareerUiVisualRole.DataImage);
            return badge;
        }

        /// <summary>누락된 상태를 포함해 한 번 읽은 자원을 재사용한다.</summary>
        public static Sprite Load(string path)
        {
            if (!Sprites.TryGetValue(path, out var sprite))
                Sprites[path] = sprite = Resources.Load<Sprite>(ResourceRoot + path);
            return sprite;
        }

        /// <summary>이름이 아닌 생성 지점의 역할로 실제 버튼 스킨을 선택한다.</summary>
        public static void ApplyButton(Button button, OwnerButtonRole role)
        {
            bool row = role == OwnerButtonRole.ListItem;
            bool tab = role == OwnerButtonRole.Tab || role == OwnerButtonRole.Navigation;
            bool primary = role == OwnerButtonRole.Primary;
            string folder = row ? "ListItems" : tab ? "Tabs" : "Buttons";
            string prefix = row ? "UI_ListItem" : tab ? "UI_Tab" : primary ? "UI_Button_Primary"
                : role == OwnerButtonRole.Utility ? "UI_Button_Utility" : "UI_Button_Secondary";
            var skin = button.GetComponent<UIOwnerFrontOfficeButton>() ?? button.gameObject.AddComponent<UIOwnerFrontOfficeButton>();
            skin.enabled = true;
            skin.Configure(button, folder, prefix, primary, tab, role == OwnerButtonRole.Quiet ? .18f : 1f);
        }

        /// <summary>검수 도구의 명시적 재적용 진입점이다. 실제 화면은 생성·상태 변경 시 역할을 연결한다.</summary>
        public static void Apply(Transform root)
        {
            if (root == null) return;
            foreach (var panel in root.GetComponentsInChildren<UIOwnerFrontOfficePanel>(true)) panel.Refresh();
            foreach (var button in root.GetComponentsInChildren<OwnerUiButtonSkin>(true)) button.Refresh();
        }
    }
}
