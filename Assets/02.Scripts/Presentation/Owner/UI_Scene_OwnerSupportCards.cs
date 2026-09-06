using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>서포트 카드 Runtime 도입 전에도 레퍼런스의 편성 구조와 잠금 사유를 보여주는 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerSupportCards : MonoBehaviour
    {
        private RectTransform _root;

        public static UI_Scene_OwnerSupportCards CreateRuntime(Transform parent)
        {
            var host = new GameObject(nameof(UI_Scene_OwnerSupportCards), typeof(RectTransform));
            host.transform.SetParent(parent, false);
            OwnerWorkspaceUiFactory.Stretch(host.GetComponent<RectTransform>());
            var view = host.AddComponent<UI_Scene_OwnerSupportCards>();
            view.Build();
            return view;
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);
        }

        private void Build()
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(transform, "OwnerSupportCardsWorkspace", true);

            RectTransform selected = OwnerDugoutDetailUiFactory.CreatePanel(
                _root, "SelectedCards", 0.015f, 0.43f, 0.315f, 0.975f);
            RectTransform catalog = OwnerDugoutDetailUiFactory.CreatePanel(
                _root, "CardCatalog", 0.33f, 0.43f, 0.68f, 0.975f);
            RectTransform active = OwnerDugoutDetailUiFactory.CreatePanel(
                _root, "ActiveCards", 0.695f, 0.43f, 0.985f, 0.975f);
            RectTransform information = OwnerDugoutDetailUiFactory.CreatePanel(
                _root, "CardInformation", 0.015f, 0.10f, 0.985f, 0.405f);

            CreatePanelTitle(selected, "사용 중인 서포트 카드");
            CreateEmptyCardSlot(selected, "SelectedSlot0", 0.57f, 0.84f);
            CreateEmptyCardSlot(selected, "SelectedSlot1", 0.23f, 0.50f);

            CreatePanelTitle(catalog, "보유 서포트 카드");
            Text catalogMessage = OwnerDugoutDetailUiFactory.CreateLabel(
                catalog,
                "CatalogMessage",
                "서포트 카드 정의와 보유 목록이\n아직 Runtime에 연결되지 않았습니다.",
                0.08f,
                0.25f,
                0.92f,
                0.78f,
                16,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            catalogMessage.color = CareerUiTheme.TextMuted;

            CreatePanelTitle(active, "현재 발동 효과");
            CreateEmptyCardSlot(active, "ActiveSlot0", 0.57f, 0.84f);
            CreateEmptyCardSlot(active, "ActiveSlot1", 0.23f, 0.50f);

            OwnerDugoutDetailUiFactory.CreateLabel(
                information,
                "Title",
                "서포트 카드 정보",
                0.025f,
                0.78f,
                0.45f,
                0.96f,
                18,
                FontStyle.Bold);
            Text informationMessage = OwnerDugoutDetailUiFactory.CreateLabel(
                information,
                "Message",
                "서포트 카드는 현재 시뮬레이션 효과와 저장 Command가 없습니다. " +
                "효과를 임의로 만들지 않고, 카드 정의·검증·저장이 마련될 때 이 편성 화면에 연결합니다.",
                0.025f,
                0.24f,
                0.975f,
                0.76f,
                14,
                FontStyle.Normal,
                TextAnchor.UpperLeft);
            informationMessage.horizontalOverflow = HorizontalWrapMode.Wrap;

            Button lockedAction = OwnerDugoutDetailUiFactory.CreateButton(
                information,
                "LockedAction",
                "편성 기능 준비 중",
                0.76f,
                0.04f,
                0.975f,
                0.20f,
                null);
            lockedAction.interactable = false;

            Text status = OwnerDugoutDetailUiFactory.CreateLabel(
                _root,
                "Status",
                "UI 레이아웃만 제공됩니다 · 경기 능력치에는 영향을 주지 않습니다.",
                0.02f,
                0.025f,
                0.98f,
                0.085f,
                13);
            status.color = CareerUiTheme.TextMuted;
        }

        private static void CreatePanelTitle(Transform panel, string title)
        {
            OwnerDugoutDetailUiFactory.CreateLabel(
                panel, "Title", title, 0.06f, 0.87f, 0.94f, 0.98f, 18, FontStyle.Bold);
        }

        private static void CreateEmptyCardSlot(Transform panel, string name, float bottom, float top)
        {
            Button slot = OwnerDugoutDetailUiFactory.CreateButton(
                panel, name, "EMPTY", 0.08f, bottom, 0.92f, top, null);
            slot.interactable = false;
            Text label = slot.transform.Find("Label").GetComponent<Text>();
            label.fontSize = 18;
            label.color = CareerUiTheme.TextMuted;
        }

        private void OnDestroy()
        {
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
        }
    }
}
