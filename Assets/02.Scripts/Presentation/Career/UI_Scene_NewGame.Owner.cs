using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_NewGame
    {
        private const int OwnerCardPageSize = 24;
        private int _ownerCardPage;
        private string _ownerNicknameDraft = "구단주";
        private int? _ownerCardYearFilter;
        private PlayerPosition? _ownerCardPositionFilter;
        private int? _ownerCardCostFilter;
        private string _ownerCardNameDraft = string.Empty;
        private string _ownerCardNameFilter = string.Empty;

        /// <summary>구단 선택부터 스타터 로스터 확인까지 기존 구단주 Draft를 단계별로 표시한다.</summary>
        private void RenderOwnerNewGame()
        {
            OwnerModeManager ownerManager = OwnerModeManager.Instance;
            OwnerNewGameFlow flow = ownerManager?.NewGameFlow;
            if (flow == null)
            {
                RenderTitle();
                return;
            }

            RectTransform background = OwnerWorkspaceUiFactory.CreateRoot(_content, "OwnerBackground", true);
            background.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            RectTransform panel = CreateImage(
                "OwnerNewGamePanel", _content, CareerUiTheme.ReferencePanel, new Vector2(1720f, 940f), Vector2.zero);
            panel.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            int step = Math.Min(5, (int)flow.CurrentStep + 1);
            CreateText("Eyebrow", panel, $"구단주 새 게임  {step}/5", 15, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(650f, 30f), new Vector2(-455f, 420f), GoldColor);
            CreateText("Title", panel, GetOwnerStepTitle(flow.CurrentStep), 32, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(920f, 52f), new Vector2(-320f, 375f), PrimaryTextColor);

            Button cancel = CreateButton(
                "CancelOwnerNewGame", panel, "새 게임 취소", new Vector2(160f, 44f),
                new Vector2(735f, 410f), CareerUiTheme.SecondaryAction, out _);
            cancel.onClick.AddListener(() =>
            {
                ownerManager.CancelNewGameFlow();
                _titleNotice = string.Empty;
                Render();
            });
            if (flow.CurrentStep != OwnerNewGameStep.Team)
            {
                Button back = CreateButton(
                    "OwnerBack", panel, "← 이전", new Vector2(140f, 44f),
                    new Vector2(-735f, -410f), CareerUiTheme.SecondaryAction, out _);
                back.onClick.AddListener(() => RunOwnerFlowAction(flow.GoBack));
            }

            switch (flow.CurrentStep)
            {
                case OwnerNewGameStep.Team:
                    RenderOwnerTeams(panel, flow);
                    break;
                case OwnerNewGameStep.MainCards:
                    RenderOwnerMainCards(panel, flow);
                    break;
                case OwnerNewGameStep.FrontManager:
                    RenderOwnerFrontManager(panel, flow);
                    break;
                case OwnerNewGameStep.Nickname:
                    RenderOwnerNickname(panel, flow);
                    break;
                case OwnerNewGameStep.StarterRosterReview:
                    RenderOwnerStarterRoster(panel, ownerManager, flow);
                    break;
            }

            if (!string.IsNullOrEmpty(_titleNotice))
                CreateText("OwnerNotice", panel, _titleNotice, 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(1040f, 38f), new Vector2(0f, -410f), ErrorColor);
            ApplyOwnerNewGameSkin(panel);
            CareerUiSkin.Apply(panel);
            ApplyOwnerFilterDropdownVisuals(panel);
        }

        private void RenderOwnerTeams(RectTransform panel, OwnerNewGameFlow flow)
        {
            IReadOnlyList<OwnerNewGameTeamView> teams = flow.GetTeamCandidates();
            for (int index = 0; index < teams.Count; index++)
            {
                OwnerNewGameTeamView team = teams[index];
                int column = index % 4;
                int row = index / 4;
                Button button = CreateButton(
                    "Team_" + team.TeamSeasonKey,
                    panel,
                    team.DisplayName,
                    new Vector2(350f, 128f),
                    new Vector2(-555f + column * 370f, 225f - row * 150f),
                    CardColor,
                    out _);
                string teamSeasonKey = team.TeamSeasonKey;
                button.onClick.AddListener(() =>
                {
                    _ownerCardPage = 0;
                    ResetOwnerCardFilters();
                    RunOwnerFlowAction(() => flow.SelectTeam(teamSeasonKey));
                });
            }
        }

        private void RenderOwnerMainCards(RectTransform panel, OwnerNewGameFlow flow)
        {
            IReadOnlyList<OwnerNewGameCardView> allCards = flow.GetMainCardCandidates();
            IReadOnlyList<OwnerNewGameCardView> cards = flow.GetMainCardCandidates(
                new OwnerMainCardCandidateFilter(
                    _ownerCardYearFilter,
                    _ownerCardPositionFilter,
                    _ownerCardCostFilter,
                    _ownerCardNameFilter));
            OwnerMainCardSelectionStatus status = flow.GetMainCardSelectionStatus();
            RenderOwnerCardFilters(panel, allCards, cards.Count);
            int pageCount = Math.Max(1, (cards.Count + OwnerCardPageSize - 1) / OwnerCardPageSize);
            _ownerCardPage = Math.Max(0, Math.Min(_ownerCardPage, pageCount - 1));
            int start = _ownerCardPage * OwnerCardPageSize;
            int end = Math.Min(cards.Count, start + OwnerCardPageSize);
            for (int index = start; index < end; index++)
            {
                OwnerNewGameCardView card = cards[index];
                int local = index - start;
                int column = local % 8;
                int row = local / 8;
                string cardId = card.CardId;
                flow.CardCatalog.TryGetCard(cardId, out PlayerCardDefinition definition);
                var model = new PlayerMiniCardModel(cardId, card.DisplayName, GetPositionLabel(card.Position),
                    card.OriginYear.ToString(), $"비용 {card.Cost}",
                    OwnerCollectionPresentationBuilder.FormatEdition(definition.Edition),
                    card.IsSelected ? "✓ 선택" : string.Empty, card.PlayerPersonId,
                    visualState: card.IsSelected ? PlayerMiniCardVisualState.Selected : PlayerMiniCardVisualState.Normal,
                    frameEdition: definition.Edition, cost: card.Cost);
                PlayerMiniCardView view = PlayerMiniCardView.CreateRuntime(panel, "MainCard_" + cardId);
                view.UseLineupSlotLayout();
                view.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
                view.Bind(model, PlayerPortraitSprites.GetDefault(card.Position));
                RectTransform rect = view.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(128f, 166f);
                rect.anchoredPosition = new Vector2(-647.5f + column * 185f, 190f - row * 170f);
                view.Selected += _ => RunOwnerFlowAction(() => flow.ToggleMainCard(cardId));
                view.DetailRequested += _ => ShowOwnerNewGameCard(view.transform, flow, cardId);
            }

            if (cards.Count == 0)
            {
                CreateText("NoFilteredCards", panel, "조건에 맞는 선수가 없습니다. 필터를 바꿔 보세요.", 17,
                    FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(900f, 80f),
                    new Vector2(0f, 35f), SecondaryTextColor);
            }

            CreateText("SelectionStatus", panel,
                $"선택 {status.SelectedCount}/{flow.Rule.MainCardCount} · 타자 {status.HitterCount}/{flow.Rule.MainHitterCount} · " +
                $"투수 {status.PitcherCount}/{flow.Rule.MainPitcherCount} · 비용 {status.TotalCost}/{flow.Rule.MaximumMainCost}",
                16, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(1050f, 30f), new Vector2(0f, -255f),
                status.IsValid ? AccentColor : SecondaryTextColor);
            string ruleStatus = status.IsValid
                ? "선택 조건을 충족했습니다."
                : status.ErrorCode == "CARD_COUNT"
                    ? "타자 6명·투수 4명을 선택하세요. 선택할 수 없는 카드는 즉시 사유를 알려드립니다."
                    : status.Message;
            CreateText("SelectionRuleStatus", panel, ruleStatus, 14, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Vector2(1200f, 26f), new Vector2(0f, -283f),
                status.IsValid ? AccentColor : SecondaryTextColor);
            CreateText("SelectedCardNames", panel, BuildOwnerSelectedCardSummary(allCards), 13,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(1450f, 30f),
                new Vector2(0f, -310f), GoldColor);
            CreateText("CardInputHint", panel, "좌클릭: 선택/해제 · 우클릭: 선수 상세정보", 14,
                FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(500f, 30f),
                new Vector2(-480f, -355f), SecondaryTextColor);
            if (_ownerCardPage > 0)
            {
                Button previous = CreateButton("CardPagePrevious", panel, "◀", new Vector2(54f, 40f),
                    new Vector2(-140f, -355f), CareerUiTheme.SecondaryAction, out _);
                previous.onClick.AddListener(() => { _ownerCardPage--; Render(); });
            }
            CreateText("CardPage", panel, $"{_ownerCardPage + 1} / {pageCount}", 14, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(120f, 40f), new Vector2(0f, -355f), SecondaryTextColor);
            if (_ownerCardPage + 1 < pageCount)
            {
                Button nextPage = CreateButton("CardPageNext", panel, "▶", new Vector2(54f, 40f),
                    new Vector2(140f, -355f), CareerUiTheme.SecondaryAction, out _);
                nextPage.onClick.AddListener(() => { _ownerCardPage++; Render(); });
            }
            Button confirm = CreateButton("ConfirmMainCards", panel, "메인 카드 확정", new Vector2(220f, 48f),
                new Vector2(620f, -355f), AccentColor, out _);
            confirm.interactable = status.IsValid;
            confirm.onClick.AddListener(() => RunOwnerFlowAction(flow.ContinueFromMainCards));
        }

        private void RenderOwnerCardFilters(
            RectTransform panel,
            IReadOnlyList<OwnerNewGameCardView> allCards,
            int filteredCount)
        {
            List<int> years = CollectOwnerCardYears(allCards);
            List<string> yearOptions = new List<string>(years.Count + 1) { "연도 전체" };
            int selectedYearIndex = 0;
            for (int index = 0; index < years.Count; index++)
            {
                yearOptions.Add(years[index].ToString());
                if (_ownerCardYearFilter == years[index]) selectedYearIndex = index + 1;
            }
            Dropdown year = CreateOwnerFilterDropdown("OwnerCardYearFilter", panel, yearOptions,
                selectedYearIndex, new Vector2(205f, 42f), new Vector2(-625f, 300f));
            year.onValueChanged.AddListener(index =>
            {
                _ownerCardYearFilter = index == 0 ? null : years[index - 1];
                _ownerCardPage = 0;
                Render();
            });

            List<PlayerPosition> positions = CollectOwnerCardPositions(allCards);
            List<string> positionOptions = new List<string>(positions.Count + 1) { "포지션 전체" };
            int selectedPositionIndex = 0;
            for (int index = 0; index < positions.Count; index++)
            {
                positionOptions.Add(GetPositionLabel(positions[index]));
                if (_ownerCardPositionFilter == positions[index]) selectedPositionIndex = index + 1;
            }
            Dropdown position = CreateOwnerFilterDropdown("OwnerCardPositionFilter", panel, positionOptions,
                selectedPositionIndex, new Vector2(225f, 42f), new Vector2(-400f, 300f));
            position.onValueChanged.AddListener(index =>
            {
                _ownerCardPositionFilter = index == 0 ? null : positions[index - 1];
                _ownerCardPage = 0;
                Render();
            });

            List<int> costs = CollectOwnerCardCosts(allCards);
            List<string> costOptions = new List<string>(costs.Count + 1) { "비용 전체" };
            int selectedCostIndex = 0;
            for (int index = 0; index < costs.Count; index++)
            {
                costOptions.Add(costs[index].ToString());
                if (_ownerCardCostFilter == costs[index]) selectedCostIndex = index + 1;
            }
            Dropdown cost = CreateOwnerFilterDropdown("OwnerCardCostFilter", panel, costOptions,
                selectedCostIndex, new Vector2(170f, 42f), new Vector2(-192f, 300f));
            cost.onValueChanged.AddListener(index =>
            {
                _ownerCardCostFilter = index == 0 ? null : costs[index - 1];
                _ownerCardPage = 0;
                Render();
            });

            InputField nameSearch = CreateInputField("OwnerCardNameSearch", panel, "선수 이름 검색",
                _ownerCardNameDraft, new Vector2(310f, 42f), new Vector2(65f, 300f));
            nameSearch.textComponent.fontSize = 15;
            nameSearch.onValueChanged.AddListener(value => _ownerCardNameDraft = value);
            Button search = CreateButton("ApplyOwnerCardNameSearch", panel, "검색", new Vector2(78f, 42f),
                new Vector2(274f, 300f), CareerUiTheme.SecondaryAction, out Text searchLabel);
            searchLabel.fontSize = 14;
            search.onClick.AddListener(() =>
            {
                _ownerCardNameFilter = _ownerCardNameDraft.Trim();
                _ownerCardPage = 0;
                Render();
            });
            Button reset = CreateButton("ResetOwnerCardFilters", panel, "초기화", new Vector2(92f, 42f),
                new Vector2(374f, 300f), CareerUiTheme.SecondaryAction, out Text resetLabel);
            resetLabel.fontSize = 14;
            reset.onClick.AddListener(() =>
            {
                ResetOwnerCardFilters();
                Render();
            });
            CreateText("OwnerCardFilterResult", panel, $"{filteredCount} / {allCards.Count}명", 14,
                FontStyle.Bold, TextAnchor.MiddleRight, new Vector2(230f, 38f),
                new Vector2(600f, 300f), SecondaryTextColor);
        }

        private void RenderOwnerFrontManager(RectTransform panel, OwnerNewGameFlow flow)
        {
            CreateText("Guide", panel, "프런트 매니저 외형은 능력치 효과가 없는 연출 선택입니다.", 18,
                FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(1000f, 48f),
                new Vector2(0f, 250f), SecondaryTextColor);
            CreateOwnerManagerChoice(panel, flow, FrontManagerIds.DefaultAnalysis, "분석형 매니저", -245f);
            CreateOwnerManagerChoice(panel, flow, FrontManagerIds.DefaultTest, "현장형 매니저", 245f);
        }

        private void CreateOwnerManagerChoice(
            RectTransform panel,
            OwnerNewGameFlow flow,
            string managerId,
            string label,
            float positionX)
        {
            Button button = CreateButton("FrontManager_" + managerId, panel, label,
                new Vector2(420f, 390f), new Vector2(positionX, 5f), CardColor, out Text buttonLabel);
            buttonLabel.alignment = TextAnchor.LowerCenter;
            buttonLabel.rectTransform.offsetMin = new Vector2(12f, 14f);
            buttonLabel.rectTransform.offsetMax = new Vector2(-12f, -325f);
            RectTransform portraitRect = CreateImage(
                "Portrait", button.transform, Color.white,
                new Vector2(300f, 300f), new Vector2(0f, 28f));
            Image portrait = portraitRect.GetComponent<Image>();
            portrait.sprite = FrontManagerPortraitSprites.LoadForManager(managerId, "FM_NEUTRAL");
            portrait.preserveAspect = true;
            portrait.color = portrait.sprite != null ? Color.white : CareerUiTheme.Warning;
            portrait.raycastTarget = false;
            button.onClick.AddListener(() => RunOwnerFlowAction(() => flow.SelectFrontManager(managerId)));
        }

        private void RenderOwnerNickname(RectTransform panel, OwnerNewGameFlow flow)
        {
            CreateText("Guide", panel, "구단 기사와 프런트 매니저 대화에서 사용할 이름입니다. (2~12자)", 18,
                FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(1000f, 48f),
                new Vector2(0f, 155f), SecondaryTextColor);
            InputField input = CreateInputField("OwnerNickname", panel, "구단주 닉네임",
                _ownerNicknameDraft, new Vector2(560f, 64f), new Vector2(0f, 55f));
            input.characterLimit = 12;
            input.onValueChanged.AddListener(value => _ownerNicknameDraft = value);
            Button confirm = CreateButton("ConfirmNickname", panel, "스타터 로스터 생성",
                new Vector2(260f, 54f), new Vector2(0f, -70f), AccentColor, out _);
            confirm.onClick.AddListener(() => RunOwnerFlowAction(() => flow.SetNickname(_ownerNicknameDraft)));
        }

        private void RenderOwnerStarterRoster(
            RectTransform panel,
            OwnerModeManager ownerManager,
            OwnerNewGameFlow flow)
        {
            OwnerStarterRosterResult roster = flow.StarterRoster;
            if (roster == null)
            {
                _titleNotice = "스타터 로스터가 생성되지 않았습니다.";
                return;
            }
            CreateText("MainRosterLabel", panel, "메인 카드 10장", 16, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(1400f, 28f), new Vector2(0f, 286f), GoldColor);
            CreateOwnerRosterCardRow(panel, flow, roster.MainCardIds, 0, roster.MainCardIds.Count,
                190f, true);
            CreateText("FillerRosterLabel", panel, "자동 보충 카드 15장", 16, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(1400f, 28f), new Vector2(0f, 94f), GoldColor);
            CreateOwnerRosterCardRow(panel, flow, roster.FillerCardIds, 0, 10, 5f, false);
            CreateOwnerRosterCardRow(panel, flow, roster.FillerCardIds, 10,
                roster.FillerCardIds.Count - 10, -145f, false);
            CreateText("RerollState", panel,
                $"보충 리롤 {roster.RerollIndex}/{flow.Rule.MaximumFillerRerolls} · 확정 후 25인 로스터로 저장됩니다.",
                15, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(900f, 36f),
                new Vector2(0f, -280f), SecondaryTextColor);
            Button reroll = CreateButton("RerollFiller", panel, "보충 선수 다시 뽑기",
                new Vector2(240f, 50f), new Vector2(-150f, -345f), CareerUiTheme.SecondaryAction, out _);
            reroll.interactable = roster.RerollIndex < flow.Rule.MaximumFillerRerolls;
            reroll.onClick.AddListener(() => RunOwnerFlowAction(() => flow.RerollFiller()));
            Button start = CreateButton("CompleteOwnerNewGame", panel, "이 구단으로 시작",
                new Vector2(240f, 50f), new Vector2(150f, -345f), AccentColor, out _);
            start.onClick.AddListener(() =>
            {
                if (!ownerManager.CompleteNewGameFlow())
                {
                    _titleNotice = ownerManager.LastError;
                    Render();
                    return;
                }
                UiGameModeSession.Select(UiGameMode.OwnerCareer);
                Hide();
            });
        }

        private static void CreateOwnerRosterCardRow(
            RectTransform panel,
            OwnerNewGameFlow flow,
            IReadOnlyList<string> cardIds,
            int startIndex,
            int count,
            float positionY,
            bool isMainCard)
        {
            const float CardWidth = 132f;
            const float CardHeight = 138f;
            const float CardSpacing = 142f;
            float firstX = -(count - 1) * CardSpacing * 0.5f;
            for (int localIndex = 0; localIndex < count; localIndex++)
            {
                string cardId = cardIds[startIndex + localIndex];
                if (!flow.CardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                    continue;
                PlayerSeasonDefinition season = flow.CardCatalog.GetPlayerSeason(card);
                var model = new PlayerMiniCardModel(
                    cardId,
                    flow.Identities.GetPlayerDisplayName(season.PlayerPersonId),
                    GetPositionLabel(season.Position),
                    season.OriginYear.ToString(),
                    $"비용 {season.Cost}",
                    isMainCard ? "MAIN" : "AUTO",
                    isMainCard ? "메인" : "보충",
                    visualState: isMainCard
                        ? PlayerMiniCardVisualState.Highlighted
                        : PlayerMiniCardVisualState.Normal, frameEdition: card.Edition, cost: season.Cost);
                PlayerMiniCardView cardView = PlayerMiniCardView.CreateRuntime(
                    panel, $"StarterRoster_{startIndex + localIndex}");
                cardView.UseLineupSlotLayout();
                cardView.GetComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
                cardView.Bind(model, PlayerPortraitSprites.GetDefault(season.Position));
                cardView.DetailRequested += _ => ShowOwnerNewGameCard(cardView.transform, flow, cardId);
                RectTransform cardRect = cardView.GetComponent<RectTransform>();
                cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);
                cardRect.anchoredPosition = new Vector2(firstX + localIndex * CardSpacing, positionY);
            }
        }

        private static List<int> CollectOwnerCardYears(IReadOnlyList<OwnerNewGameCardView> cards)
        {
            var values = new List<int>();
            for (int index = 0; index < cards.Count; index++)
                if (!values.Contains(cards[index].OriginYear)) values.Add(cards[index].OriginYear);
            values.Sort((left, right) => right.CompareTo(left));
            return values;
        }

        private static List<PlayerPosition> CollectOwnerCardPositions(IReadOnlyList<OwnerNewGameCardView> cards)
        {
            var values = new List<PlayerPosition>();
            for (int index = 0; index < cards.Count; index++)
                if (!values.Contains(cards[index].Position)) values.Add(cards[index].Position);
            values.Sort((left, right) => ((int)left).CompareTo((int)right));
            return values;
        }

        private static List<int> CollectOwnerCardCosts(IReadOnlyList<OwnerNewGameCardView> cards)
        {
            var values = new List<int>();
            for (int index = 0; index < cards.Count; index++)
                if (!values.Contains(cards[index].Cost)) values.Add(cards[index].Cost);
            values.Sort((left, right) => right.CompareTo(left));
            return values;
        }

        private static Dropdown CreateOwnerFilterDropdown(
            string name,
            Transform parent,
            List<string> options,
            int selectedIndex,
            Vector2 size,
            Vector2 position)
        {
            GameObject dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = name;
            dropdownObject.transform.SetParent(parent, false);
            RectTransform rect = dropdownObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image surface = dropdownObject.GetComponent<Image>();
            surface.color = new Color(0.08f, 0.12f, 0.17f, 0.96f);
            CareerUiVisualElement visual = dropdownObject.AddComponent<CareerUiVisualElement>();
            visual.Initialize(CareerUiVisualRole.FlatSurface);
            dropdownObject.AddComponent<CareerUiPreserveTextColor>();

            Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, options.Count - 1));

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text[] labels = dropdownObject.GetComponentsInChildren<Text>(true);
            for (int index = 0; index < labels.Length; index++)
            {
                labels[index].font = font;
                labels[index].fontSize = 14;
                labels[index].color = PrimaryTextColor;
            }
            dropdown.captionText.alignment = TextAnchor.MiddleLeft;
            dropdown.captionText.rectTransform.offsetMin = new Vector2(14f, 2f);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-34f, -2f);
            dropdown.itemText.alignment = TextAnchor.MiddleLeft;

            RectTransform template = dropdown.template;
            template.sizeDelta = new Vector2(0f, Mathf.Min(8, options.Count) * 32f + 8f);
            template.anchoredPosition = new Vector2(0f, -2f);
            Image templateImage = template.GetComponent<Image>();
            if (templateImage != null)
            {
                templateImage.color = new Color(0.06f, 0.09f, 0.14f, 0.99f);
                CareerUiVisualElement templateVisual = template.gameObject.AddComponent<CareerUiVisualElement>();
                templateVisual.Initialize(CareerUiVisualRole.FlatSurface);
            }
            if (template.GetComponent<CareerUiPreserveTextColor>() == null)
                template.gameObject.AddComponent<CareerUiPreserveTextColor>();
            RectTransform item = dropdown.itemText.transform.parent as RectTransform;
            if (item != null) item.sizeDelta = new Vector2(item.sizeDelta.x, 32f);
            Toggle itemToggle = dropdown.itemText.GetComponentInParent<Toggle>(true);
            if (itemToggle != null)
            {
                if (itemToggle.targetGraphic is Image itemBackground)
                    itemBackground.color = new Color(0.10f, 0.15f, 0.22f, 1f);
                if (itemToggle.graphic != null)
                    itemToggle.graphic.color = AccentColor;
                ColorBlock itemColors = itemToggle.colors;
                itemColors.normalColor = Color.white;
                itemColors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                itemColors.selectedColor = itemColors.highlightedColor;
                itemColors.pressedColor = new Color(0.78f, 0.84f, 0.92f, 1f);
                itemToggle.colors = itemColors;
            }
            ScrollRect scroll = template.GetComponent<ScrollRect>();
            if (scroll != null && scroll.viewport != null &&
                scroll.viewport.TryGetComponent(out Image viewportImage))
            {
                // Mask는 알파로 표시 영역을 기록하므로 투명하게 만들면 자식 항목도 전부 가려진다.
                viewportImage.color = Color.white;
                scroll.viewport.GetComponent<Mask>().showMaskGraphic = false;
            }

            Transform arrow = dropdownObject.transform.Find("Arrow");
            if (arrow != null && arrow.TryGetComponent(out Image arrowImage))
                arrowImage.color = Color.clear;
            Text arrowText = CreateText("ArrowLabel", dropdownObject.transform, "▼", 13, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(28f, 40f), new Vector2(size.x * 0.5f - 18f, 0f),
                SecondaryTextColor);
            arrowText.raycastTarget = false;
            return dropdown;
        }

        /// <summary>공통 스킨 적용 뒤에도 펼침 목록이 높은 대비를 유지하도록 Dropdown 전용 팔레트를 확정한다.</summary>
        private static void ApplyOwnerFilterDropdownVisuals(RectTransform panel)
        {
            Dropdown[] dropdowns = panel.GetComponentsInChildren<Dropdown>(true);
            for (int index = 0; index < dropdowns.Length; index++)
            {
                Dropdown dropdown = dropdowns[index];
                if (dropdown == null || !dropdown.name.StartsWith("OwnerCard", StringComparison.Ordinal))
                    continue;

                Image surface = dropdown.GetComponent<Image>();
                if (surface != null)
                    surface.color = new Color(0.08f, 0.12f, 0.17f, 1f);

                if (dropdown.captionText != null)
                {
                    dropdown.captionText.color = PrimaryTextColor;
                    dropdown.captionText.fontSize = 15;
                }

                RectTransform template = dropdown.template;
                if (template == null)
                    continue;

                Image templateImage = template.GetComponent<Image>();
                if (templateImage != null)
                    templateImage.color = new Color(0.035f, 0.055f, 0.085f, 1f);

                if (dropdown.itemText != null)
                {
                    dropdown.itemText.color = PrimaryTextColor;
                    dropdown.itemText.fontSize = 15;
                    dropdown.itemText.fontStyle = FontStyle.Bold;
                    dropdown.itemText.alignment = TextAnchor.MiddleLeft;
                    dropdown.itemText.rectTransform.offsetMin = new Vector2(12f, 1f);
                    dropdown.itemText.rectTransform.offsetMax = new Vector2(-8f, -1f);
                    if (dropdown.itemText.GetComponent<CareerUiPreserveTextColor>() == null)
                        dropdown.itemText.gameObject.AddComponent<CareerUiPreserveTextColor>();
                }

                Toggle itemToggle = dropdown.itemText != null
                    ? dropdown.itemText.GetComponentInParent<Toggle>(true)
                    : null;
                if (itemToggle != null)
                {
                    if (itemToggle.targetGraphic is Image itemBackground)
                        itemBackground.color = new Color(0.08f, 0.12f, 0.18f, 1f);
                    if (itemToggle.graphic != null)
                        itemToggle.graphic.color = AccentColor;

                    ColorBlock colors = itemToggle.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
                    colors.selectedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
                    colors.pressedColor = new Color(0.76f, 0.86f, 1f, 1f);
                    itemToggle.colors = colors;
                }

                ScrollRect scroll = template.GetComponent<ScrollRect>();
                if (scroll != null && scroll.viewport != null &&
                    scroll.viewport.TryGetComponent(out Image viewportImage))
                    viewportImage.color = Color.white;

                Scrollbar scrollbar = template.GetComponentInChildren<Scrollbar>(true);
                if (scrollbar == null)
                    continue;
                Image scrollbarTrack = scrollbar.GetComponent<Image>();
                if (scrollbarTrack != null)
                    scrollbarTrack.color = new Color(0.025f, 0.04f, 0.065f, 1f);
                if (scrollbar.targetGraphic != null)
                    scrollbar.targetGraphic.color = new Color(0.34f, 0.48f, 0.62f, 1f);
            }
        }

        private static void ShowOwnerNewGameCard(Transform source, OwnerNewGameFlow flow, string cardId)
        {
            UI_Popup_OwnerPlayerCard.Show(source,
                OwnerModeRuntimeSnapshotFactory.CreateNewGameCard(OwnerModeManager.Instance, flow, cardId));
        }

        private static void ApplyOwnerNewGameSkin(RectTransform panel)
        {
            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponentInParent<PlayerMiniCardView>() != null) continue;
                OwnerUiButtonSkin.Apply(button, button.GetComponent<Image>().color == AccentColor
                    ? OwnerButtonRole.Primary : OwnerButtonRole.Secondary);
            }
            Text[] texts = panel.GetComponentsInChildren<Text>(true);
            for (int index = 0; index < texts.Length; index++)
            {
                Text text = texts[index];
                if (text.GetComponentInParent<Button>() != null ||
                    text.GetComponentInParent<InputField>() != null ||
                    text.GetComponentInParent<Dropdown>() != null)
                    continue;
                if (text.color != ErrorColor)
                    text.color = CareerUiTheme.ReferenceText;
                if (text.GetComponent<CareerUiPreserveTextColor>() == null)
                    text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            }
        }

        private static string BuildOwnerSelectedCardSummary(IReadOnlyList<OwnerNewGameCardView> cards)
        {
            var summary = new StringBuilder(320);
            summary.Append("선택 명단  ");
            int selectedCount = 0;
            for (int index = 0; index < cards.Count; index++)
            {
                if (!cards[index].IsSelected)
                    continue;
                if (selectedCount > 0)
                    summary.Append("  ·  ");
                summary.Append(cards[index].DisplayName).Append('(').Append(cards[index].OriginYear).Append(')');
                selectedCount++;
            }
            if (selectedCount == 0)
                summary.Append("없음");
            return summary.ToString();
        }

        private void ResetOwnerCardFilters()
        {
            _ownerCardYearFilter = null;
            _ownerCardPositionFilter = null;
            _ownerCardCostFilter = null;
            _ownerCardNameDraft = string.Empty;
            _ownerCardNameFilter = string.Empty;
            _ownerCardPage = 0;
        }

        private void RunOwnerFlowAction(Action action)
        {
            try
            {
                action();
                _titleNotice = string.Empty;
            }
            catch (Exception exception) when (
                exception is ArgumentException || exception is InvalidOperationException)
            {
                _titleNotice = exception.Message;
            }
            Render();
        }

        private static string GetOwnerStepTitle(OwnerNewGameStep step)
        {
            return step switch
            {
                OwnerNewGameStep.Team => "운영할 구단을 선택하세요",
                OwnerNewGameStep.MainCards => "팀의 중심이 될 메인 카드 10장을 고르세요",
                OwnerNewGameStep.FrontManager => "프런트 매니저를 선택하세요",
                OwnerNewGameStep.Nickname => "구단주 이름을 정하세요",
                OwnerNewGameStep.StarterRosterReview => "첫 시즌 25인 로스터를 확인하세요",
                _ => "구단주 새 게임"
            };
        }
    }
}
