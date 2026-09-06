using System;
using Baseball.Presentation.SharedUI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerGrowth
    {
        private bool _isChoosingStudyPlayer;

        private void RenderStudy()
        {
            Frame(_content, "StudyMapFrame", 20, 86, 710, 416);
            Label(_content, "StudyMapHeading", "유학지 선택", 15, 30, 92, 300, 25);
            var map = new GameObject("StudyWorldMap", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            map.transform.SetParent(_content, false);
            Place(map.rectTransform, 30, 121, 690, 337);
            map.texture = Resources.Load<Texture2D>("UI/OwnerPowerUp/study_world_map_v1");
            map.raycastTarget = false;
            OwnerGrowthCardSnapshot card = SelectedCard();
            OwnerStudyOption option = null;
            if (card != null && card.Studies.Length > 0)
            {
                foreach (OwnerStudyOption study in card.Studies)
                    if (study.Program.ProgramId == _programId) option = study;
                if (option == null) { option = card.Studies[0]; _programId = option.Program.ProgramId; }
                for (int index = 0; index < card.Studies.Length; index++)
                {
                    OwnerStudyOption study = card.Studies[index];
                    // 핀은 실제 국가를 뜻하지 않는 과정 선택 표식이다. 과정의 경제·효과는 정적 정의를 따른다.
                    float x = 84 + index % 2 * 310;
                    float y = 224 + index / 2 * 95;
                    Tab(_content, "StudyPin_" + study.Program.ProgramId, "●  " + study.Program.DisplayName, () =>
                    {
                        _programId = study.Program.ProgramId;
                        _pendingStudy = string.Empty;
                        Render();
                    }, _programId == study.Program.ProgramId, x, y, 219, 31);
                }
            }
            Label(_content, "MapLegend", "● 과정을 선택하면 성장 효과와 참가 조건을 확인합니다.", 12, 35, 466, 685, 26);
            Frame(_content, "StudyInformation", 742, 86, 338, 416);
            Label(_content, "StudyInformationTitle", "유학지 정보", 15, 754, 92, 310, 25);
            Surface(_content, "StudyTitleRule", 753, 119, 315, 1, Border);
            Label(_content, "StudyName", option?.Program.DisplayName ?? "선수를 선택하세요", 17, 757, 128, 305, 30);
            Label(_content, "StudyReward", option == null ? "" : "유학 성장 효과\n" + option.RewardText, 14, 757, 166, 305, 74);
            Label(_content, "StudyCost", option == null ? "" :
                $"유학 비용    육성 포인트 {option.Program.DevelopmentPointCost:N0}\n유학 기간    {option.Program.DurationWeeks}주",
                13, 757, 244, 305, 46);
            Surface(_content, "StudyCardRule", 753, 297, 315, 1, Border);
            if (card != null)
            {
                PlayerMiniCardView selected = PlayerMiniCardView.CreateRuntime(_content, "StudyPlayerCard");
                selected.UseLineupSlotLayout();
                Place(selected.GetComponent<RectTransform>(), 758, 310, 73, 99);
                selected.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(card.Card, false));
                selected.SetPortrait(Baseball.Presentation.UI.PlayerPortraitSprites.GetDefault(card.Card.Position));
                FitCompactCardText(selected);
                Label(_content, "StudyPlayer", card.Card.DisplayName + "\n" +
                    (string.IsNullOrEmpty(card.Card.StudyStatus) ? "유학 대기" : card.Card.StudyStatus),
                    13, 842, 310, 218, 48);
            }
            Tab(_content, "ChooseStudyPlayer", "선수 선택", () =>
            {
                _isChoosingStudyPlayer = true;
                _pendingStudy = string.Empty;
                Render();
            }, false, 847, 372, 208, 30);
            string reason = option?.BlockedReason ?? "보유 선수를 선택하세요.";
            bool isPending = option != null && _pendingStudy == _cardId + ":" + _programId;
            Text status = Label(_content, "StudyBlockedReason", isPending
                ? $"{card.Card.DisplayName} · {option.Program.DurationWeeks}주 · 육성 포인트 {option.Program.DevelopmentPointCost} 사용. 확정하면 시작합니다."
                : reason.Length == 0 ? "시즌당 한 번 참가할 수 있습니다." : reason, 12, 757, 411, 305, 48);
            status.color = reason.Length == 0 ? Ink : new Color32(176, 50, 39, 255);
            Button start = Tab(_content, "StartStudy", isPending ? "유학 확정" : "유학지 결정", () =>
            {
                if (isPending)
                {
                    _pendingStudy = string.Empty;
                    StudyRequested?.Invoke(_cardId, _programId);
                    Render();
                }
                else { _pendingStudy = _cardId + ":" + _programId; Render(); }
            }, true, 832, 463, 160, 29);
            start.interactable = option != null && option.CanStart;
            if (isPending) Tab(_content, "CancelStudy", "취소", () =>
                { _pendingStudy = string.Empty; Render(); }, false, 998, 463, 65, 29);
            if (_isChoosingStudyPlayer) RenderStudyPlayerPicker();
        }

        private void RenderStudyPlayerPicker()
        {
            Frame(_content, "StudyPlayerPicker", 42, 142, 667, 296);
            Label(_content, "PickerHeading", "유학 대상 선수 선택 · 1군 미등록 선수만 신청 가능", 15, 56, 152, 590, 30);
            RenderRoster(_content, 52, 190, 646, 198, 8);
            Tab(_content, "CloseStudyPlayerPicker", "선택 완료", () =>
            {
                _isChoosingStudyPlayer = false;
                _pendingStudy = string.Empty;
                Render();
            }, true, 545, 399, 148, 29);
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Image Surface(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            Image image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            Place(image.rectTransform, x, y, width, height);
            image.raycastTarget = false;
            return image;
        }

        private static void Frame(Transform parent, string name, float x, float y, float width, float height)
        {
            Image image = Surface(parent, name, x, y, width, height, new Color32(252, 252, 251, 246));
            image.raycastTarget = true;
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1, -1);
        }

        private static Text Label(Transform parent, string name, string value, int size, float x, float y, float width, float height)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            Place(text.rectTransform, x, y, width, height);
            text.raycastTarget = false;
            return text;
        }

        private static Button Tab(Transform parent, string name, string text, Action action, bool selected,
            float x, float y, float width, float height)
        {
            Image image = Surface(parent, name, x, y, width, height, selected ? Blue : new Color32(239, 242, 245, 255));
            image.raycastTarget = true;
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = selected ? new Color32(100, 156, 215, 255) : Border;
            outline.effectDistance = new Vector2(1, -1);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(() => action());
            var layout = image.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            Text label = Label(image.transform, "Label", text, 13, 3, 0, width - 6, height);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = selected ? Color.white : Ink;
            return button;
        }

        private static void AddScrollbar(ScrollRect scroll)
        {
            Image track = OwnerRuntimeUiFactory.CreateImage("Scrollbar", scroll.transform, new Color32(228, 232, 235, 255));
            OwnerRuntimeUiFactory.SetAnchors(track.rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-10, 0), Vector2.zero);
            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            Image handle = OwnerRuntimeUiFactory.CreateImage("Handle", track.transform, Border);
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.viewport.offsetMax = new Vector2(-12, 0);
        }
    }
}
