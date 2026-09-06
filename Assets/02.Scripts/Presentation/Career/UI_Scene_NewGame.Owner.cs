using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
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

            CreateImage("OwnerBackground", _content, BackgroundColor, new Vector2(1920f, 1080f), Vector2.zero);
            RectTransform panel = CreateImage(
                "OwnerNewGamePanel", _content, PanelColor, new Vector2(1720f, 940f), Vector2.zero);
            ApplyFramedCardSkin(panel);
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
            CareerUiSkin.Apply(panel);
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
                int column = local % 6;
                int row = local / 6;
                string selectionMark = card.IsSelected ? "✓ " : string.Empty;
                Button button = CreateButton(
                    "MainCard_" + card.CardId,
                    panel,
                    $"{selectionMark}{card.DisplayName}\n{card.OriginYear} · {GetPositionLabel(card.Position)} · 비용 {card.Cost}",
                    new Vector2(245f, 100f),
                    new Vector2(-625f + column * 250f, 190f - row * 112f),
                    card.IsSelected ? SelectedColor : CardColor,
                    out Text label);
                label.fontSize = 15;
                label.color = card.IsSelected ? GoldColor : PrimaryTextColor;
                string cardId = card.CardId;
                button.onClick.AddListener(() => RunOwnerFlowAction(() => flow.ToggleMainCard(cardId)));
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
            Button year = CreateButton("OwnerCardYearFilter", panel,
                "연도  " + (_ownerCardYearFilter?.ToString() ?? "전체"), new Vector2(205f, 42f),
                new Vector2(-625f, 300f), CareerUiTheme.SecondaryAction, out Text yearLabel);
            yearLabel.fontSize = 14;
            year.onClick.AddListener(() => CycleOwnerCardYearFilter(allCards));

            Button position = CreateButton("OwnerCardPositionFilter", panel,
                "포지션  " + (_ownerCardPositionFilter.HasValue
                    ? GetPositionLabel(_ownerCardPositionFilter.Value)
                    : "전체"), new Vector2(205f, 42f),
                new Vector2(-400f, 300f), CareerUiTheme.SecondaryAction, out Text positionLabel);
            positionLabel.fontSize = 14;
            position.onClick.AddListener(() => CycleOwnerCardPositionFilter(allCards));

            Button cost = CreateButton("OwnerCardCostFilter", panel,
                "비용  " + (_ownerCardCostFilter?.ToString() ?? "전체"), new Vector2(170f, 42f),
                new Vector2(-192f, 300f), CareerUiTheme.SecondaryAction, out Text costLabel);
            costLabel.fontSize = 14;
            cost.onClick.AddListener(() => CycleOwnerCardCostFilter(allCards));

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
            string portraitKey = managerId == FrontManagerIds.DefaultTest
                ? "FM_02_NEUTRAL"
                : "FM_01_NEUTRAL";
            RectTransform portraitRect = CreateImage(
                "Portrait", button.transform, Color.white,
                new Vector2(300f, 300f), new Vector2(0f, 28f));
            Image portrait = portraitRect.GetComponent<Image>();
            portrait.sprite = FrontManagerPortraitSprites.Load(portraitKey);
            portrait.preserveAspect = true;
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
            var summary = new StringBuilder(720);
            summary.AppendLine("메인 카드");
            AppendOwnerCardNames(summary, flow, roster.MainCardIds);
            summary.AppendLine().AppendLine().AppendLine("자동 보충 카드");
            AppendOwnerCardNames(summary, flow, roster.FillerCardIds);
            CreateText("RosterSummary", panel, summary.ToString(), 17, FontStyle.Normal,
                TextAnchor.UpperLeft, new Vector2(1360f, 560f), new Vector2(0f, 40f), PrimaryTextColor);
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

        private static void AppendOwnerCardNames(
            StringBuilder builder,
            OwnerNewGameFlow flow,
            IReadOnlyList<string> cardIds)
        {
            for (int index = 0; index < cardIds.Count; index++)
            {
                string displayName = cardIds[index];
                if (flow.CardCatalog.TryGetCard(cardIds[index], out PlayerCardDefinition card))
                {
                    PlayerSeasonDefinition season = flow.CardCatalog.GetPlayerSeason(card);
                    displayName = flow.Identities.GetPlayerDisplayName(season.PlayerPersonId);
                }
                if (index > 0) builder.Append(index % 5 == 0 ? "\n" : "   ·   ");
                builder.Append(displayName);
            }
        }

        private void CycleOwnerCardYearFilter(IReadOnlyList<OwnerNewGameCardView> cards)
        {
            var values = new List<int>();
            for (int index = 0; index < cards.Count; index++)
                if (!values.Contains(cards[index].OriginYear)) values.Add(cards[index].OriginYear);
            values.Sort((left, right) => right.CompareTo(left));
            _ownerCardYearFilter = GetNextOwnerFilterValue(values, _ownerCardYearFilter);
            _ownerCardPage = 0;
            Render();
        }

        private void CycleOwnerCardPositionFilter(IReadOnlyList<OwnerNewGameCardView> cards)
        {
            var values = new List<PlayerPosition>();
            for (int index = 0; index < cards.Count; index++)
                if (!values.Contains(cards[index].Position)) values.Add(cards[index].Position);
            values.Sort((left, right) => ((int)left).CompareTo((int)right));
            _ownerCardPositionFilter = GetNextOwnerFilterValue(values, _ownerCardPositionFilter);
            _ownerCardPage = 0;
            Render();
        }

        private void CycleOwnerCardCostFilter(IReadOnlyList<OwnerNewGameCardView> cards)
        {
            var values = new List<int>();
            for (int index = 0; index < cards.Count; index++)
                if (!values.Contains(cards[index].Cost)) values.Add(cards[index].Cost);
            values.Sort((left, right) => right.CompareTo(left));
            _ownerCardCostFilter = GetNextOwnerFilterValue(values, _ownerCardCostFilter);
            _ownerCardPage = 0;
            Render();
        }

        private static T? GetNextOwnerFilterValue<T>(IReadOnlyList<T> values, T? current)
            where T : struct
        {
            if (values == null || values.Count == 0)
                return null;
            if (!current.HasValue)
                return values[0];
            for (int index = 0; index < values.Count; index++)
            {
                if (EqualityComparer<T>.Default.Equals(values[index], current.Value))
                    return index + 1 < values.Count ? values[index + 1] : null;
            }
            return values[0];
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
