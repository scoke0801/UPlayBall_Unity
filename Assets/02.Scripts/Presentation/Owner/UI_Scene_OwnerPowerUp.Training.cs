using System.Collections.Generic;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerPowerUp
    {
        private readonly List<TrainingProgramView> _trainingPrograms = new List<TrainingProgramView>();
        private Text _trainingOutcome;
        private Text _trainingBalance;

        private sealed class TrainingProgramView
        {
            public OwnerCardTrainingProgramSnapshot Program;
            public Button Button;
            public Text Value;
            public Text Cost;
            public Image Marker;
            public UIOwnerMetricBar Progress;
        }

        private GameObject _trainingConfirmationFocus;

        private void FocusTrainingConfirmation()
        {
            if ((!_trainingRoot.gameObject.activeInHierarchy && !_enhancementRoot.gameObject.activeInHierarchy)
                || EventSystem.current == null) return;
            _trainingConfirmationFocus = EventSystem.current.currentSelectedGameObject;
            Button cancel = _confirmRoot.GetComponentsInChildren<Button>()[0];
            EventSystem.current.SetSelectedGameObject(cancel.gameObject);
        }

        private void RestoreTrainingConfirmationFocus()
        {
            if (_trainingConfirmationFocus != null && _trainingConfirmationFocus.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_trainingConfirmationFocus);
            _trainingConfirmationFocus = null;
        }
        private void BuildTraining()
        {
            _trainingRoot.gameObject.AddComponent<CareerUiPreserveTextColor>();
            var columns = OwnerWorkspaceUiFactory.AddHorizontalLayout(_trainingRoot, CareerUiTheme.Space3);
            columns.padding = new RectOffset(0, 0, 0, 36);
            RectTransform left = CreateTrainingColumn("TrainingTargets", .34f, "보유 선수");
            // 세 열의 실제 카드 폭에 맞춰 목록의 상한을 고정하고 남는 폭은 상세에 배분한다.
            var inventoryWidth = left.parent.GetComponent<LayoutElement>();
            inventoryWidth.minWidth = 320;
            inventoryWidth.preferredWidth = 432;
            inventoryWidth.flexibleWidth = 0;
            OwnerWorkspaceUiFactory.AddVerticalLayout(left, 8);
            BuildTrainingFilters(left);
            BuildTrainingListToolbar(left);
            _trainingCardList = CreateGridScrollContent(left, 3, new Vector2(102, 153));
            StyleTrainingScroll(_trainingCardList);
            _trainingGrid = new CardGrid(_trainingCardList, SelectTrainingCard, null);
            BuildTrainingEmptyState();

            RectTransform center = CreateTrainingColumn("TrainingCard", .28f, "훈련 대상");
            OwnerWorkspaceUiFactory.AddVerticalLayout(center, 12);
            Text cardHint = TrainingText(center, "CardHint", 28, 12);
            cardHint.text = "선택한 선수의 현재 능력치";
            _trainingCard = OwnerRuntimeUiFactory.CreateRect("SelectedTrainingCard", center);
            SetPreferred(_trainingCard, 160, 1);
            _trainingCardFront = OwnerRuntimeUiFactory.CreateRect("CardFront", _trainingCard);
            OwnerRuntimeUiFactory.Stretch(_trainingCardFront);
            _trainingCardFront.gameObject.AddComponent<CareerUiPreserveTextColor>();
            var aspect = _trainingCardFront.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 2f / 3f;
            _trainingDetails = TrainingText(center, "TrainingCardDetails", 64, 15, TextAnchor.MiddleCenter);
            _trainingDetails.color = CareerUiTheme.RosterText;

            RectTransform right = CreateTrainingColumn("TrainingPrograms", .38f, "개인 훈련");
            OwnerWorkspaceUiFactory.AddVerticalLayout(right, 8);
            _trainingWallet = TrainingText(right, "TrainingWallet", 32, 19, TextAnchor.MiddleRight);
            _trainingWallet.color = CareerUiTheme.AccentGold;
            ScrollRect scroll = OwnerRuntimeUiFactory.CreateVerticalScroll("ProgramScroll", right, out _trainingProgramList);
            StyleTrainingScroll(_trainingProgramList);
            SetPreferred(scroll.GetComponent<RectTransform>(), 120, 1);
            _trainingProgramList.GetComponent<VerticalLayoutGroup>().spacing = 4;
            _trainingOutcome = TrainingText(right, "TrainingOutcome", 32, 18);
            _trainingOutcome.color = CareerUiTheme.RosterAccent;
            _trainingBalance = TrainingText(right, "TrainingBalance", 36, 12);
            _trainingExecuteButton = OwnerWorkspaceUiFactory.CreateButton(right, "TrainingExecute", "훈련 선택", RequestTraining);
            SetPreferred(_trainingExecuteButton.GetComponent<RectTransform>(), 48);
            OwnerUiButtonSkin.Apply(_trainingExecuteButton, OwnerButtonRole.Primary);
        }

        // 기존 구단주 보드 토큰을 사용해 스킨 재적용에도 명암과 안전 영역을 유지한다.
        private RectTransform CreateTrainingColumn(string name, float width, string title)
        {
            RectTransform content = CreateColumn(_trainingRoot, name, width, title);
            Transform panel = content.parent;
            SetTrainingSurface(panel.GetComponent<Image>(), CareerUiTheme.RosterSurface);
            SetTrainingSurface(panel.Find("HeaderSurface").GetComponent<Image>(), CareerUiTheme.RosterSurface);
            SetTrainingSurface(panel.Find("HeaderAccent").GetComponent<Image>(), CareerUiTheme.RosterDivider);
            // 투명한 전면 Image의 Outline은 원본 알파를 무시하면 패널 전체를 덮는다.
            panel.Find("ThinBorder").gameObject.SetActive(false);
            Text header = panel.GetComponent<CareerUiFrame>().HeaderRoot.GetComponent<Text>();
            header.color = CareerUiTheme.RosterText;
            header.fontStyle = FontStyle.Normal;
            header.fontSize = 17;
            return content;
        }

        private void ApplyTrainingChrome(bool active)
        {
            Transform panel = _root.Find("PowerUpPanel");
            if (panel == null) return;
            panel.Find("ThinBorder").gameObject.SetActive(!active);
            Image background = panel.GetComponent<Image>();
            background.GetComponent<CareerUiVisualElement>().Initialize(
                active ? CareerUiVisualRole.DataImage : CareerUiVisualRole.TexturedPanel);
            if (active) SetTrainingSurface(background, CareerUiTheme.RosterBoard);
            else CareerUiSkin.ApplyVisualElement(background);
            Text header = panel.GetComponent<CareerUiFrame>().HeaderRoot.GetComponent<Text>();
            if (header.GetComponent<CareerUiPreserveTextColor>() == null)
                header.gameObject.AddComponent<CareerUiPreserveTextColor>();
            header.text = active ? "카드 훈련   /   선수의 다음 성장을 준비하세요" : "전력보강 센터";
            header.color = OwnerDashboardStyle.Ivory;
            SetTrainingSurface(panel.Find("HeaderSurface").GetComponent<Image>(),
                active ? CareerUiTheme.RosterBoard : CareerUiTheme.ReferencePanelHeader);
            SetTrainingSurface(panel.Find("HeaderAccent").GetComponent<Image>(),
                active ? CareerUiTheme.RosterDivider : CareerUiTheme.ReferenceAccent);
        }

        private static void SetTrainingSurface(Image image, Color color)
        {
            var visual = image.GetComponent<CareerUiVisualElement>() ?? image.gameObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(CareerUiVisualRole.DataImage);
            var frontOffice = image.GetComponent<UIOwnerFrontOfficePanel>();
            if (frontOffice != null) { frontOffice.Refresh(); return; }
            image.sprite = null;
            image.color = color;
            image.raycastTarget = false;
        }

        private static Text TrainingText(Transform parent, string name, float height, int size,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Text text = CreateFixedText(parent, name, height, size, FontStyle.Normal, alignment);
            text.color = CareerUiTheme.RosterTextSecondary;
            return text;
        }

        private static void StyleTrainingScroll(RectTransform content)
        {
            Image surface = content.GetComponentInParent<ScrollRect>().GetComponent<Image>();
            SetTrainingSurface(surface, CareerUiTheme.RosterBoard);
            surface.raycastTarget = true;
            OwnerDashboardStyle.SetDataSurface(surface, OwnerDashboardStyle.TableSurface, true);
        }

        private void ClearTrainingPrograms()
        {
            _trainingPrograms.Clear();
            _trainingProgramList.anchoredPosition = Vector2.zero;
            OwnerRuntimeUiFactory.ClearChildren(_trainingProgramList);
            _trainingOutcome.text = string.Empty;
            _trainingBalance.text = string.Empty;
        }

        private void CreateTrainingProgramButton(OwnerCardTrainingProgramSnapshot program)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(_trainingProgramList,
                "Program_" + program.ProgramId, program.Title, () => SelectTrainingProgram(program.ProgramId));
            SetPreferred(button.GetComponent<RectTransform>(), 72);
            OwnerDashboardStyle.SetDataRow(button, false, OwnerDashboardStyle.TableSurface);
            Text title = button.transform.Find("Label").GetComponent<Text>();
            title.fontStyle = FontStyle.Normal;
            title.fontSize = 15;
            title.alignment = TextAnchor.MiddleLeft;
            OwnerRuntimeUiFactory.SetAnchors(title.rectTransform, new Vector2(0, .48f), new Vector2(.46f, 1),
                new Vector2(16, 0), new Vector2(0, -4));
            Text value = OwnerWorkspaceUiFactory.CreateText(button.transform, "GrowthPreview", "", 19, FontStyle.Normal, TextAnchor.MiddleRight);
            OwnerRuntimeUiFactory.SetAnchors(value.rectTransform, new Vector2(.46f, .48f), Vector2.one,
                Vector2.zero, new Vector2(-16, -4));
            Text cost = OwnerWorkspaceUiFactory.CreateText(button.transform, "TrainingCost", "", 12, FontStyle.Normal, TextAnchor.MiddleLeft);
            OwnerRuntimeUiFactory.SetAnchors(cost.rectTransform, new Vector2(0, .14f), new Vector2(1, .48f),
                new Vector2(16, 2), new Vector2(-16, 0));
            var progress = UIOwnerMetricBar.Create(button.transform, "GrowthBar");
            OwnerRuntimeUiFactory.SetAnchors((RectTransform)progress.transform, Vector2.zero, Vector2.right,
                new Vector2(16, 4), new Vector2(-16, 8));
            Image marker = OwnerRuntimeUiFactory.CreateImage("SelectionMarker", button.transform, CareerUiTheme.RosterAccent);
            SetTrainingSurface(marker, CareerUiTheme.RosterAccent);
            OwnerRuntimeUiFactory.SetAnchors(marker.rectTransform, Vector2.zero, new Vector2(0, 1),
                new Vector2(4, 12), new Vector2(7, -12));
            button.gameObject.AddComponent<UICardGridFocusRelay>().Selected = () => RevealTrainingProgram(button);
            _trainingPrograms.Add(new TrainingProgramView { Program = program, Button = button, Value = value, Cost = cost, Marker = marker, Progress = progress });
        }

        private void ResizeTrainingPrograms()
        {
            if (!_trainingRoot.gameObject.activeInHierarchy) return;
            var inventory = _trainingCardList.GetComponentInParent<ScrollRect>();
            var grid = _trainingCardList.GetComponent<GridLayoutGroup>();
            float width = Mathf.Max(1, (inventory.viewport.rect.width - grid.padding.horizontal - grid.spacing.x * 2) / 3);
            grid.cellSize = new Vector2(width, width * 1.5f);
            if (_trainingPrograms.Count == 0) return;
            ScrollRect scroll = _trainingProgramList.GetComponentInParent<ScrollRect>();
            float height = Mathf.Clamp((scroll.viewport.rect.height - _trainingProgramList.GetComponent<VerticalLayoutGroup>().padding.vertical - 4 * (_trainingPrograms.Count - 1)) / _trainingPrograms.Count, 48, 80);
            foreach (TrainingProgramView view in _trainingPrograms)
            {
                var element = view.Button.GetComponent<LayoutElement>();
                if (Mathf.Abs(element.preferredHeight - height) > .5f)
                    SetPreferred(view.Button.GetComponent<RectTransform>(), height);
            }
        }

        private void RevealTrainingProgram(Button button)
        {
            ScrollRect scroll = _trainingProgramList.GetComponentInParent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            RectTransform rect = button.GetComponent<RectTransform>();
            float top = -rect.anchoredPosition.y - rect.rect.height * (1 - rect.pivot.y);
            float offset = _trainingProgramList.anchoredPosition.y;
            if (top < offset) offset = top;
            else if (top + rect.rect.height > offset + scroll.viewport.rect.height)
                offset = top + rect.rect.height - scroll.viewport.rect.height;
            float maximum = Mathf.Max(0, _trainingProgramList.rect.height - scroll.viewport.rect.height);
            _trainingProgramList.anchoredPosition = new Vector2(0, Mathf.Clamp(offset, 0, maximum));
        }
        private void RefreshTrainingSelection()
        {
            OwnerCardTrainingTargetSnapshot target = FindTrainingTarget(_selectedTrainingCardId);
            if (target == null) return;
            foreach (TrainingProgramView view in _trainingPrograms)
            {
                var program = view.Program;
                bool selected = program.ProgramId == _selectedTrainingProgramId;
                OwnerDashboardStyle.SetDataRow(view.Button, selected, OwnerDashboardStyle.TableSurface);
                view.Marker.gameObject.SetActive(selected);
                view.Value.text = program.CanTrain ? $"{program.Current} → {program.Current + program.GainedPoints}  (+{program.GainedPoints})" : program.Current.ToString();
                view.Value.color = program.CanTrain ? OwnerDashboardStyle.Success : OwnerDashboardStyle.Muted;
                view.Progress.Bind(program.Current, program.Current + (program.CanTrain ? program.GainedPoints : 0), program.Ceiling);
                view.Cost.text = program.CanTrain
                    ? $"훈련 상한 {program.Ceiling}   ·   {program.DpCost:N0} 포인트"
                    : program.BlockedReason.Replace("DP", "육성 포인트");
                view.Cost.color = program.CanTrain ? CareerUiTheme.RosterTextSecondary : CareerUiTheme.AccentGold;
            }
            _trainingDetails.text = target.Card.DisplayName + "\n" +
                OwnerCollectionPresentationBuilder.FormatPlayerRole(target.Card.Position,
                    target.Card.PitcherRole, target.Card.IsPositionEvidenceMissing) + "  ·  코스트 " + target.Card.Cost
                + (target.Card.IsActiveRoster ? "  ·  배치 중" : "  ·  미배치");
            var current = FindTrainingProgram(target, _selectedTrainingProgramId);
            _trainingExecuteButton.interactable = current != null && current.CanTrain;
            OwnerUiButtonSkin.Apply(_trainingExecuteButton, OwnerButtonRole.Primary);
            _trainingOutcome.text = current == null ? "훈련 과정 없음" :
                current.CanTrain ? $"{current.Title}  {current.Current} → {current.Current + current.GainedPoints}  (+{current.GainedPoints})" : "현재 훈련할 수 없습니다";
            _trainingBalance.text = current == null ? "이 선수에게 적용할 수 있는 훈련이 없습니다." :
                current.CanTrain ? $"사용 {current.DpCost:N0}  ·  훈련 후 잔액 {_snapshot.Training.DevelopmentPoints - current.DpCost:N0} 포인트"
                : current.BlockedReason.Replace("DP", "육성 포인트");
            _trainingExecuteButton.transform.Find("Label").GetComponent<Text>().text =
                current != null && current.CanTrain ? $"{current.Title} 훈련 시작" : "훈련 불가";
        }
    }
}
