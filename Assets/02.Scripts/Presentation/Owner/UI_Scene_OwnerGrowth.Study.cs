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
            StudyLabel(_content, "StudyMapHeading", "유학지 선택", 15, 30, 92, 300, 25);
            var map = new GameObject("StudyWorldMap", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            map.transform.SetParent(_content, false);
            Place(map.rectTransform, 30, 121, 690, 337);
            map.texture = Resources.Load<Texture2D>("UI/OwnerPowerUp/study_world_map_v1");
            // 원본의 장식 테두리를 제외하고 지도 영역만 사용한다.
            map.uvRect = new Rect(.025f, .035f, .95f, .93f);
            map.raycastTarget = true;
            AddStudyDismissAction(map);
            OwnerGrowthCardSnapshot card = SelectedCard();
            OwnerStudyOption option = null;
            if (card != null && card.Studies.Length > 0)
            {
                foreach (OwnerStudyOption study in card.Studies)
                    if (study.Program.ProgramId == _programId) option = study;
                for (int index = 0; index < card.Studies.Length; index++)
                    RenderStudyDestinationNode(card.Studies[index]);
            }
            OwnerDashboardStyle.ApplyInset(Surface(_content, "MapLegendSurface", 30, 462, 690, 32, OwnerDashboardStyle.InsetSurface));
            StudyLabel(_content, "MapLegend", "비행기  이용 가능    ·    ?  해금 조건 확인    ·    금색 테두리  선택", 11, 35, 466, 685, 26);
            Image informationFrame = Frame(_content, "StudyInformation", 742, 86, 338, 416);
            AddStudyDismissAction(informationFrame);
            StudyLabel(_content, "StudyInformationTitle", "유학지 정보", 15, 754, 92, 310, 25);
            Surface(_content, "StudyTitleRule", 753, 119, 315, 1, Border);
            OwnerDashboardStyle.ApplyInset(Surface(_content, "StudyProgramSummary", 753, 124, 315, 172, OwnerDashboardStyle.InsetSurface));
            StudyLabel(_content, "StudyName", option?.Program.DisplayName ?? "목적지를 선택하세요", 16, 757, 126, 305, 28);
            StudyLabel(_content, "StudyDestination", option == null ? "" : "목적지  " + option.Program.DestinationName,
                12, 757, 153, 305, 22);
            Text unlock = StudyLabel(_content, "StudyUnlock", option?.UnlockText ?? "", 11, 757, 174, 305, 32);
            unlock.color = option?.IsUnlocked == false ? Baseball.Presentation.UI.CareerUiTheme.Error : OwnerDashboardStyle.Gold;
            StudyLabel(_content, "StudyReward", option == null
                ? "지도 위 비행기를 누르면 성장 효과와\n해금 조건을 확인할 수 있습니다."
                : "유학 성장 효과\n" + option.RewardText, 13, 757, 208, 305, 48);
            StudyLabel(_content, "StudyCost", option == null ? "" :
                $"유학 비용    {option.CostText}\n유학 기간    {option.Program.DurationWeeks}주",
                12, 757, 258, 305, 38);
            Surface(_content, "StudyCardRule", 753, 300, 315, 1, Border);
            if (card != null)
            {
                PlayerMiniCardView selected = PlayerMiniCardView.CreateRuntime(_content, "StudyPlayerCard");
                selected.UseLineupSlotLayout();
                Place(selected.GetComponent<RectTransform>(), 758, 309, 82, 112);
                selected.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(card.DetailCard, false));
                selected.SetPortrait(Baseball.Presentation.UI.PlayerPortraitSprites.GetDefault(card.Card.Position));
                FitCompactCardText(selected);
                // 공용 카드가 이미 생성한 그룹을 재사용한다. 중복 추가하면 여기서 렌더링이 중단된다.
                CanvasGroup cardCanvas = selected.GetComponent<CanvasGroup>();
                cardCanvas.blocksRaycasts = false;
                StudyLabel(_content, "StudyPlayer", card.Card.DisplayName + "\n" +
                    (string.IsNullOrEmpty(card.Card.StudyStatus) ? "유학 대기" : card.Card.StudyStatus),
                    13, 850, 313, 210, 48);
            }
            Button choosePlayer = Tab(_content, "ChooseStudyPlayer", "선수 선택", () =>
            {
                _isChoosingStudyPlayer = true;
                _studyDraftCardId = _cardId;
                _studyPickerPage = 0;
                _pendingStudy = string.Empty;
                Render();
                FocusRosterControl("StudySearch");
            }, false, 850, 374, 205, 30);
            OwnerUiButtonSkin.Apply(choosePlayer, OwnerButtonRole.Secondary);
            if (option == null)
            {
                if (_isChoosingStudyPlayer) RenderStudyPlayerPicker();
                return;
            }
            string reason = option?.BlockedReason ?? "보유 선수를 선택하세요.";
            bool isPending = option != null && _pendingStudy == _cardId + ":" + _programId;
            Text status = StudyLabel(_content, "StudyBlockedReason", isPending
                ? $"{card.Card.DisplayName} · {option.Program.DurationWeeks}주 · {option.CostText} 사용. 확정하면 시작합니다."
                : reason.Length == 0 ? "시즌당 한 번 참가할 수 있습니다." : reason, 11, 757, 425, 305, 34);
            status.alignment = TextAnchor.UpperLeft;
            status.color = reason.Length == 0 ? OwnerDashboardStyle.Ivory : Baseball.Presentation.UI.CareerUiTheme.Error;
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

        private void RenderStudyDestinationNode(OwnerStudyOption study)
        {
            Vector2 point = GetStudyMapPoint(study.Program.MapXPermille, study.Program.MapYPermille);
            bool isSelected = _programId == study.Program.ProgramId;
            Action select = () =>
            {
                _programId = isSelected ? string.Empty : study.Program.ProgramId;
                _pendingStudy = string.Empty;
                Render();
                FocusRosterControl("StudyPin_" + study.Program.ProgramId);
            };

            // 작은 원형 핀과 별개로 44px 입력 영역을 확보한다.
            Image hitArea = Surface(_content, "StudyPin_" + study.Program.ProgramId,
                point.x - 22, point.y - 22, 44, 44, Color.clear);
            hitArea.raycastTarget = true;
            var pin = new GameObject("StudyPinSymbol_" + study.Program.ProgramId,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(UIStudyDestinationPin))
                .GetComponent<UIStudyDestinationPin>();
            pin.transform.SetParent(hitArea.transform, false);
            Place(pin.rectTransform, 3, 3, 38, 38);
            pin.Configure(study.IsUnlocked, isSelected);
            pin.raycastTarget = false;
            if (study.IsUnlocked)
            {
                var airplane = new GameObject("StudyAirplane", typeof(RectTransform), typeof(RawImage))
                    .GetComponent<RawImage>();
                airplane.transform.SetParent(hitArea.transform, false);
                Place(airplane.rectTransform, 3, 3, 38, 38);
                airplane.texture = Resources.Load<Texture2D>("UI/OwnerPowerUp/study_airplane_figurine_v2");
                airplane.raycastTarget = false;
            }
            if (!study.IsUnlocked)
            {
                Text locked = Label(hitArea.transform, "LockedSymbol", "?", 18, 7, 7, 30, 30);
                locked.alignment = TextAnchor.MiddleCenter;
                locked.color = Color.white;
            }
            Button markerButton = hitArea.gameObject.AddComponent<Button>();
            markerButton.targetGraphic = pin;
            markerButton.onClick.AddListener(() => select());

            const float labelWidth = 116;
            float labelX = Mathf.Clamp(point.x - labelWidth * .5f, 38, 712 - labelWidth);
            float labelY = point.y + 32;
            Surface(_content, "StudyLabelConnector_" + study.Program.ProgramId,
                point.x - .5f, point.y + 19, 1, 13, new Color32(206, 229, 251, 255));
            Button nameplate = Tab(_content, "StudyPinLabel_" + study.Program.ProgramId,
                study.Program.DestinationName, select, false, labelX, labelY, labelWidth, 28);
            // 지도 점무늬가 글자 뒤로 비치지 않도록 밑줄 탭 대신 공용 면 버튼을 쓴다.
            OwnerUiButtonSkin.Apply(nameplate, OwnerButtonRole.Secondary);
            OwnerUiButtonSkin.SetSelected(nameplate, isSelected);
            Text label = nameplate.GetComponentInChildren<Text>();
            label.fontSize = 14;
            OwnerDashboardStyle.SetTypography(label, true);
        }

        private static Text StudyLabel(Transform parent, string name, string value, int size,
            float x, float y, float width, float height)
        {
            Text text = Label(parent, name, value, Mathf.Max(14, size), x, y, width, height);
            // 본문 서체는 유지하고 공용 기본 글자색으로 대비를 확보한다.
            OwnerDashboardStyle.SetTypography(text, size >= 15);
            text.color = OwnerDashboardStyle.Ivory;
            return text;
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
            UIOwnerFrontOfficePanel.Apply(image.rectTransform, "ManagerReport");
            Image data = OwnerRuntimeUiFactory.CreateImage("DataSurface", image.transform, OwnerDashboardStyle.TableSurface);
            OwnerRuntimeUiFactory.Stretch(data.rectTransform, new Vector2(8, 8), new Vector2(-8, -8));
            OwnerDashboardStyle.ApplySection(data, 28f);
            return image;
        }

        private static Text Label(Transform parent, string name, string value, int size, float x, float y, float width, float height)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            Place(text.rectTransform, x, y, width, height);
            text.color = UIOwnerFrontOfficePanel.HasDarkSurface(parent) ? OwnerDashboardStyle.Ivory : Ink;
            OwnerDashboardStyle.SetDataText(text, size >= 14);
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
            if (!string.IsNullOrEmpty(text))
            {
                outline.enabled = false;
                OwnerUiButtonSkin.Apply(button, name == "TraitTraining" || name == "DevelopmentOffice" || name == "OffseasonCalendar" ? OwnerButtonRole.Secondary : OwnerButtonRole.Tab);
            }
            OwnerUiButtonSkin.SetSelected(button, selected);
            return button;
        }

        private static void AddScrollbar(ScrollRect scroll)
        {
            Image track = OwnerRuntimeUiFactory.CreateImage("Scrollbar", scroll.transform, new Color32(228, 232, 235, 255));
            OwnerRuntimeUiFactory.SetAnchors(track.rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-10, 0), Vector2.zero);
            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            Image handle = OwnerRuntimeUiFactory.CreateImage("Handle", track.transform, Border);
            OwnerDashboardStyle.SetDataSurface(track, OwnerDashboardStyle.TableHeader, true);
            OwnerDashboardStyle.SetDataSurface(handle, OwnerDashboardStyle.TableSecondary, true);
            Image surface = scroll.GetComponent<Image>();
            if (surface != null) OwnerDashboardStyle.ApplyInset(surface, true);
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.viewport.offsetMax = new Vector2(-12, 0);
        }
    }
}
