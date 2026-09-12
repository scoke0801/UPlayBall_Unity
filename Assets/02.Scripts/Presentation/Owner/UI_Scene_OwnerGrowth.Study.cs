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
            Image mapFrame = Frame(_content, "StudyMapFrame", 20, 86, 710, 416);
            AddStudyDismissAction(mapFrame);
            Label(_content, "StudyMapHeading", "유학지 선택", 15, 30, 92, 300, 25);
            var map = new GameObject("StudyWorldMap", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            map.transform.SetParent(_content, false);
            Place(map.rectTransform, 30, 121, 690, 337);
            map.texture = Resources.Load<Texture2D>("UI/OwnerPowerUp/study_world_map_v1");
            map.raycastTarget = true;
            AddStudyDismissAction(map);
            OwnerGrowthCardSnapshot card = SelectedCard();
            OwnerStudyOption option = null;
            if (card != null && card.Studies.Length > 0)
            {
                foreach (OwnerStudyOption study in card.Studies)
                    if (study.Program.ProgramId == _programId) option = study;
                for (int index = 0; index < card.Studies.Length; index++)
                    RenderStudyRouteLine(card.Studies[index]);
                RenderStudyHomeNode();
                for (int index = 0; index < card.Studies.Length; index++)
                    RenderStudyDestinationNode(card.Studies[index]);
            }
            Label(_content, "MapLegend", "비행기를 눌러 유학지 선택    ·    흐린 비행기  잠김    ·    금색 밑줄  선택", 11, 35, 466, 685, 26);
            Image informationFrame = Frame(_content, "StudyInformation", 742, 86, 338, 416);
            AddStudyDismissAction(informationFrame);
            Label(_content, "StudyInformationTitle", "유학지 정보", 15, 754, 92, 310, 25);
            Surface(_content, "StudyTitleRule", 753, 119, 315, 1, Border);
            Label(_content, "StudyName", option?.Program.DisplayName ?? "목적지를 선택하세요", 16, 757, 126, 305, 28);
            Label(_content, "StudyDestination", option == null ? "" : "목적지  " + option.Program.DestinationName,
                12, 757, 153, 305, 22);
            Text unlock = Label(_content, "StudyUnlock", option?.UnlockText ?? "", 11, 757, 174, 305, 26);
            unlock.color = option?.IsUnlocked == false ? new Color32(191, 77, 62, 255) : Blue;
            Label(_content, "StudyReward", option == null
                ? "지도 위 비행기를 누르면 성장 효과와\n해금 조건을 확인할 수 있습니다."
                : "유학 성장 효과\n" + option.RewardText, 13, 757, 198, 305, 48);
            Label(_content, "StudyCost", option == null ? "" :
                $"유학 비용    육성 포인트 {option.Program.DevelopmentPointCost:N0}\n유학 기간    {option.Program.DurationWeeks}주",
                12, 757, 248, 305, 42);
            Surface(_content, "StudyCardRule", 753, 300, 315, 1, Border);
            if (card != null)
            {
                PlayerMiniCardView selected = PlayerMiniCardView.CreateRuntime(_content, "StudyPlayerCard");
                selected.UseLineupSlotLayout();
                Place(selected.GetComponent<RectTransform>(), 758, 309, 82, 112);
                selected.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(card.DetailCard, false));
                selected.SetPortrait(Baseball.Presentation.UI.PlayerPortraitSprites.GetDefault(card.Card.Position));
                selected.UsePlayerPickerLayout();
                FitCompactCardText(selected);
                // 공용 카드가 이미 생성한 그룹을 재사용한다. 중복 추가하면 여기서 렌더링이 중단된다.
                CanvasGroup cardCanvas = selected.GetComponent<CanvasGroup>();
                cardCanvas.blocksRaycasts = false;
                Label(_content, "StudyPlayer", card.Card.DisplayName + "\n" +
                    (string.IsNullOrEmpty(card.Card.StudyStatus) ? "유학 대기" : card.Card.StudyStatus),
                    13, 850, 313, 210, 48);
            }
            Tab(_content, "ChooseStudyPlayer", "선수 선택", () =>
            {
                _isChoosingStudyPlayer = true;
                _pendingStudy = string.Empty;
                Render();
                FocusRosterControl("Card_" + _cardId);
            }, false, 850, 374, 205, 30);
            if (option == null)
            {
                if (_isChoosingStudyPlayer) RenderStudyPlayerPicker();
                return;
            }
            string reason = option?.BlockedReason ?? "보유 선수를 선택하세요.";
            bool isPending = option != null && _pendingStudy == _cardId + ":" + _programId;
            Text status = Label(_content, "StudyBlockedReason", isPending
                ? $"{card.Card.DisplayName} · {option.Program.DurationWeeks}주 · 육성 포인트 {option.Program.DevelopmentPointCost} 사용. 확정하면 시작합니다."
                : reason.Length == 0 ? "시즌당 한 번 참가할 수 있습니다." : reason, 11, 757, 409, 305, 50);
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

        private void RenderStudyRouteLine(OwnerStudyOption study)
        {
            Vector2 home = GetStudyMapPoint(790, 430);
            Vector2 destination = GetStudyMapPoint(
                study.Program.MapXPermille,
                study.Program.MapYPermille);
            Vector2 delta = destination - home;
            Image line = OwnerRuntimeUiFactory.CreateImage(
                "StudyRoute_" + study.Program.ProgramId,
                _content,
                study.IsUnlocked
                    ? new Color32(111, 196, 245, 150)
                    : new Color32(132, 145, 160, 100));
            RectTransform rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(
                (home.x + destination.x) * .5f,
                -(home.y + destination.y) * .5f);
            rect.sizeDelta = new Vector2(
                delta.magnitude,
                study.Program.ProgramId == _programId ? 2.5f : 1.5f);
            rect.localEulerAngles = new Vector3(
                0,
                0,
                -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.raycastTarget = false;
        }

        private void RenderStudyHomeNode()
        {
            Vector2 point = GetStudyMapPoint(790, 430);
            Surface(
                _content,
                "StudyHomeNode",
                point.x - 3,
                point.y - 3,
                6,
                6,
                Color.white);
            Text label = Label(_content, "StudyHomeLabel", "출발 · 한국", 11, point.x - 44, point.y + 12, 88, 24);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            Shadow shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(4, 17, 35, 240);
            shadow.effectDistance = new Vector2(1, -1);
        }

        private void RenderStudyDestinationNode(OwnerStudyOption study)
        {
            Vector2 point = GetStudyMapPoint(
                study.Program.MapXPermille,
                study.Program.MapYPermille);
            bool isSelected = _programId == study.Program.ProgramId;
            Action select = () =>
            {
                _programId = isSelected ? string.Empty : study.Program.ProgramId;
                _pendingStudy = string.Empty;
                Render();
                FocusRosterControl("StudyPin_" + study.Program.ProgramId);
            };

            var marker = new GameObject(
                "StudyPin_" + study.Program.ProgramId,
                typeof(RectTransform),
                typeof(RawImage)).GetComponent<RawImage>();
            marker.transform.SetParent(_content, false);
            Place(marker.rectTransform, point.x - 30, point.y - 30, 60, 60);
            // 목적지 중심에서 회전해야 아이콘이 지리 좌표와 클릭 위치를 벗어나지 않는다.
            marker.rectTransform.pivot = new Vector2(.5f, .5f);
            marker.rectTransform.anchoredPosition = new Vector2(point.x, -point.y);
            marker.texture = Resources.Load<Texture2D>("UI/OwnerPowerUp/study_airplane_icon_v1");
            marker.color = study.IsUnlocked ? Color.white : new Color32(90, 101, 116, 210);
            marker.raycastTarget = true;
            Vector2 home = GetStudyMapPoint(790, 430);
            Vector2 heading = new Vector2(point.x - home.x, home.y - point.y);
            marker.rectTransform.localEulerAngles = new Vector3(
                0,
                0,
                Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg - 24f);
            // RawImage Outline은 알파 윤곽선 대신 비행기 전체를 복제하므로 선택은 라벨 아래에 표시한다.
            Button markerButton = marker.gameObject.AddComponent<Button>();
            markerButton.targetGraphic = marker;
            markerButton.transition = Selectable.Transition.None;
            markerButton.onClick.AddListener(() => select());

            bool placeRight = study.Program.MapXPermille < 420;
            float labelX = placeRight ? point.x + 29 : point.x - 169;
            float labelY = point.y - 17;
            Text label = Label(
                _content,
                "StudyPinLabel_" + study.Program.ProgramId,
                study.Program.DestinationName + "\n" + (study.IsUnlocked ? "" : "잠김 · ") +
                study.Program.DisplayName,
                10,
                labelX,
                labelY,
                140,
                38);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
            label.color = !study.IsUnlocked
                ? new Color32(157, 170, 185, 255)
                : isSelected
                    ? new Color32(255, 211, 98, 255)
                    : Color.white;
            Shadow labelShadow = label.gameObject.AddComponent<Shadow>();
            labelShadow.effectColor = new Color32(4, 17, 35, 240);
            labelShadow.effectDistance = new Vector2(1, -1);
            if (isSelected)
                Surface(_content, "StudySelection_" + study.Program.ProgramId,
                    labelX + 8, labelY + 40, 124, 3, new Color32(255, 211, 98, 255));
        }

        private void AddStudyDismissAction(Graphic graphic)
        {
            Button button = graphic.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (string.IsNullOrEmpty(_programId)) return;
                _programId = string.Empty;
                _pendingStudy = string.Empty;
                Render();
            });
        }

        private static Vector2 GetStudyMapPoint(int xPermille, int yPermille) =>
            new Vector2(30f + 690f * xPermille / 1000f, 121f + 337f * yPermille / 1000f);

        private void RenderStudyPlayerPicker()
        {
            Frame(_content, "StudyPlayerPicker", 42, 110, 667, 386);
            Label(_content, "PickerHeading", "유학 대상 선수 선택 · 1군 미등록 선수만 신청 가능", 15, 56, 120, 590, 30);
            RenderRoster(_content, 52, 154, 646, 292, 8, cardHeight: 120);
            Tab(_content, "CloseStudyPlayerPicker", "선택 완료", () =>
            {
                _isChoosingStudyPlayer = false;
                _pendingStudy = string.Empty;
                Render();
                FocusRosterControl("ChooseStudyPlayer");
            }, true, 545, 456, 148, 29);
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

        private static Image Frame(Transform parent, string name, float x, float y, float width, float height)
        {
            Image image = Surface(parent, name, x, y, width, height, new Color32(252, 252, 251, 246));
            image.raycastTarget = true;
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(1, -1);
            return image;
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
