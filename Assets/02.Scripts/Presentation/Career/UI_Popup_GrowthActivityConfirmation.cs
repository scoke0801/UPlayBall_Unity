using System;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Game.Manager;
using Baseball.Presentation.UI;
using Baseball.Simulation.Growth;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// 훈련과 유학이 공유하는 기간·비용·성장·부담 확정 팝업이다.
    /// </summary>
    public sealed partial class UI_Popup_GrowthActivityConfirmation : UIPopupBase
    {
        private static readonly Color OverlayColor = new(0.001f, 0.006f, 0.012f, 0.88f);
        private static readonly Color BackgroundColor = new(0.008f, 0.031f, 0.055f, 1f);
        private static readonly Color PanelColor = new(0.014f, 0.057f, 0.098f, 1f);
        private static readonly Color CardColor = new(0.019f, 0.077f, 0.128f, 1f);
        private static readonly Color SelectedColor = new(0.024f, 0.17f, 0.31f, 1f);
        private static readonly Color BorderColor = new(0.18f, 0.37f, 0.54f, 1f);
        private static readonly Color AccentColor = new(0.10f, 0.56f, 1f, 1f);
        private static readonly Color CyanColor = new(0.20f, 0.83f, 0.78f, 1f);
        private static readonly Color GoldColor = new(0.96f, 0.72f, 0.22f, 1f);
        private static readonly Color WarningColor = new(1f, 0.59f, 0.18f, 1f);
        private static readonly Color ErrorColor = new(1f, 0.34f, 0.34f, 1f);
        private static readonly Color PrimaryTextColor = new(0.94f, 0.97f, 1f, 1f);
        private static readonly Color SecondaryTextColor = new(0.64f, 0.73f, 0.82f, 1f);
        private static readonly Color MutedTextColor = new(0.38f, 0.47f, 0.56f, 1f);

        private CareerManager _manager;
        private RectTransform _content;
        private string _programId = string.Empty;
        private TrainingIntensity _intensity = TrainingIntensity.Standard;

        public static UI_Popup_GrowthActivityConfirmation CreateRuntime(Transform parent)
        {
            var gameObject = new GameObject(
                nameof(UI_Popup_GrowthActivityConfirmation),
                typeof(RectTransform),
                typeof(CanvasGroup));
            gameObject.transform.SetParent(parent, false);
            UI_Popup_GrowthActivityConfirmation popup =
                gameObject.AddComponent<UI_Popup_GrowthActivityConfirmation>();
            Stretch(gameObject.GetComponent<RectTransform>());
            return popup;
        }

        public void ShowProgram(string programId)
        {
            if (string.IsNullOrWhiteSpace(programId))
                return;
            _programId = programId;
            GrowthProgramView preview = _manager.BuildGrowthProgramPreview(
                programId,
                TrainingIntensity.Standard);
            CareerGrowthView dashboard = _manager.GrowthDashboard;
            _intensity = preview.SupportsIntensity &&
                         string.Equals(
                             dashboard.SelectedProgramId,
                             programId,
                             StringComparison.Ordinal)
                ? dashboard.SelectedTrainingIntensity
                : TrainingIntensity.Standard;
            if (IsVisible)
                Render();
            else
                Show();
        }

        protected override void OnInitialize()
        {
            _manager = GameManager.EnsureExists().EnsureManager<CareerManager>("CareerManager");
            _manager.CareerChanged += HandleCareerChanged;
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            RectTransform overlay = CreateImage(
                "Overlay",
                root,
                OverlayColor,
                Vector2.zero,
                Vector2.zero,
                stretch: true);
            overlay.GetComponent<Image>().raycastTarget = true;
            _content = CreateImage(
                "Content",
                root,
                BorderColor,
                new Vector2(1500f, 900f),
                Vector2.zero);
        }

        protected override void OnShow()
        {
            Render();
        }

        protected override void OnDestroy()
        {
            if (_manager != null)
                _manager.CareerChanged -= HandleCareerChanged;
            base.OnDestroy();
        }

        private void HandleCareerChanged()
        {
            if (_manager == null || !_manager.HasActiveCareer)
            {
                Hide();
                return;
            }
            if (IsVisible)
                Render();
        }

        private void Render()
        {
            if (_content == null || _manager == null || !_manager.HasActiveCareer ||
                string.IsNullOrEmpty(_programId))
            {
                return;
            }

            ClearChildren(_content);
            RectTransform surface = CreateImage(
                "Surface",
                _content,
                BackgroundColor,
                Vector2.zero,
                Vector2.zero,
                stretch: true);
            surface.offsetMin = new Vector2(2f, 2f);
            surface.offsetMax = new Vector2(-2f, -2f);

            CareerDashboardView dashboard = _manager.Dashboard;
            CareerGrowthView growth = _manager.GrowthDashboard;
            GrowthProgramView selected = _manager.BuildGrowthProgramPreview(_programId, _intensity);
            RenderHeader(selected);
            RenderPlayerSummary(dashboard, growth, selected);
            RenderCostSummary(selected);
            RenderProgramList(growth, selected);
            RenderDetails(selected);
            RenderTimeline(growth, selected);
            RenderFooter(growth, selected);
        }

        private void RenderHeader(GrowthProgramView selected)
        {
            string title = selected.ActivityType switch
            {
                OffseasonActivityType.Study => "유학 계획 확정",
                OffseasonActivityType.Rehabilitation => "회복 계획 확정",
                OffseasonActivityType.Rest => "휴식 계획 확정",
                _ => "훈련 계획 확정"
            };
            RectTransform header = CreateImage(
                "Header",
                _content,
                new Color(0.018f, 0.075f, 0.128f, 1f),
                new Vector2(1496f, 66f),
                new Vector2(0f, 415f));
            CreateText(
                "Title", header, title, 30, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(640f, 54f), Vector2.zero, PrimaryTextColor);
            Button close = CreateButton(
                "Close", header, "×", new Vector2(58f, 54f), new Vector2(705f, 0f),
                new Color(0.02f, 0.06f, 0.10f, 0.1f), out Text closeLabel);
            closeLabel.fontSize = 40;
            closeLabel.fontStyle = FontStyle.Normal;
            close.onClick.AddListener(Close);
        }

        private void RenderPlayerSummary(
            CareerDashboardView dashboard,
            CareerGrowthView growth,
            GrowthProgramView selected)
        {
            RectTransform panel = CreateImage(
                "PlayerSummary",
                _content,
                PanelColor,
                new Vector2(330f, 154f),
                new Vector2(-567f, 300f));
            RectTransform portrait = CreateImage(
                "Portrait",
                panel,
                Color.white,
                new Vector2(112f, 132f),
                new Vector2(-96f, 0f));
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.sprite = PlayerPortraitSprites.GetDefault(dashboard.Position);
            portraitImage.preserveAspect = true;
            CreateText(
                "Name", panel, dashboard.PlayerName, 25, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(170f, 38f), new Vector2(73f, 43f), PrimaryTextColor);
            CreateText(
                "Position", panel, GetPositionLabel(dashboard.Position), 15, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(170f, 28f), new Vector2(73f, 11f), AccentColor);
            RectTransform overall = CreateImage(
                "Overall",
                panel,
                new Color(0.025f, 0.08f, 0.12f, 1f),
                new Vector2(112f, 43f),
                new Vector2(44f, -43f));
            CreateText(
                "Label", overall, "OVR", 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(46f, 28f), new Vector2(-25f, 0f), SecondaryTextColor);
            CreateText(
                "Value", overall, dashboard.Overall.ToString(), 27, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(54f, 34f), new Vector2(25f, 0f), GoldColor);
            CreateText(
                "Season", panel,
                selected.ActivityType == OffseasonActivityType.Study
                    ? selected.CanUseThisOffseason
                        ? "유학 가능 횟수 1 / 1"
                        : "이번 오프시즌 유학 완료"
                    : growth.IsOffseason
                    ? $"{dashboard.SeasonYear} 오프시즌 {growth.CurrentWeek}주차"
                    : "오프시즌 전용",
                12, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(180f, 24f), new Vector2(75f, -71f), SecondaryTextColor);
        }

        private void RenderCostSummary(GrowthProgramView selected)
        {
            float startX = -280f;
            float gap = 252f;
            RenderSummaryCard(
                "Summary_Time",
                "▣  남은 기간",
                $"{selected.RemainingWeeksBefore}주  →  {selected.RemainingWeeksAfter}주",
                selected.PlannedWeeks > 0
                    ? $"계획 합계 {selected.PlannedWeeks + selected.DurationWeeks}주"
                    : $"{selected.DurationWeeks}주 소모",
                startX,
                CyanColor,
                selected.CanFitSchedule,
                selected.UsesMajorityOfRemainingTime ? "남은 기간의 절반 이상을 사용합니다." : string.Empty);
            RenderSummaryCard(
                "Summary_Cost",
                "●  비용",
                $"{FormatMoney(selected.MoneyBefore)} → {FormatMoney(selected.MoneyAfter)}",
                selected.PlannedCost > 0L
                    ? $"계획 합계 {FormatMoney(selected.PlannedCost + selected.MoneyCost)}"
                    : $"{FormatMoney(selected.MoneyCost)} 소모",
                startX + gap,
                GoldColor,
                selected.CanAfford,
                selected.CanAfford ? string.Empty : $"{FormatMoney(selected.MoneyShortfall)} 부족");
            string conditionValue = selected.ActivityType == OffseasonActivityType.Study &&
                                    selected.ConditionAfterWithDiscomfort != selected.ConditionAfter
                ? $"{selected.CurrentCondition} → {selected.ConditionAfterWithDiscomfort}~{selected.ConditionAfter}"
                : $"{selected.CurrentCondition}  →  {selected.ConditionAfter}";
            RenderSummaryCard(
                "Summary_Condition",
                selected.ActivityType == OffseasonActivityType.Study ? "♡  복귀 컨디션" : "♡  컨디션",
                conditionValue,
                FormatSigned(selected.ConditionAfter - selected.CurrentCondition),
                startX + gap * 2f,
                GetConditionColor(selected),
                selected.CanMeetCondition,
                selected.IsConditionDanger ? "완료 후 위험 구간에 진입합니다." : string.Empty);
            RenderSummaryCard(
                "Summary_Completion",
                selected.ActivityType == OffseasonActivityType.Study ? "⚑  귀국 예정" : "⚑  완료 예정",
                selected.ActivityType == OffseasonActivityType.Study
                    ? $"{selected.EndWeek + 1}주차"
                    : $"{selected.EndWeek + 1}주차 종료",
                selected.ActivityType == OffseasonActivityType.Study ? "평가·귀국 포함" : "즉시 결과 반영",
                startX + gap * 3f,
                PrimaryTextColor,
                selected.CanFitSchedule,
                string.Empty);
        }

        private void RenderSummaryCard(
            string name,
            string title,
            string value,
            string caption,
            float x,
            Color valueColor,
            bool isValid,
            string warning)
        {
            RectTransform frame = CreateImage(
                name,
                _content,
                isValid ? BorderColor : ErrorColor,
                new Vector2(236f, 154f),
                new Vector2(x, 300f));
            RectTransform card = CreateImage(
                "Surface",
                frame,
                CardColor,
                Vector2.zero,
                Vector2.zero,
                stretch: true);
            card.offsetMin = new Vector2(2f, 2f);
            card.offsetMax = new Vector2(-2f, -2f);
            CreateText(
                "Title", frame, title, 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(210f, 24f), new Vector2(0f, 52f), SecondaryTextColor);
            CreateText(
                "Value", frame, value, 21, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(224f, 40f), new Vector2(0f, 14f), isValid ? valueColor : ErrorColor);
            CreateText(
                "Caption", frame, caption, 13, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(210f, 24f), new Vector2(0f, -21f),
                isValid ? SecondaryTextColor : ErrorColor);
            if (!string.IsNullOrEmpty(warning))
            {
                CreateText(
                    "Warning", frame, warning, 10, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(220f, 30f), new Vector2(0f, -54f),
                    isValid ? WarningColor : ErrorColor);
            }
        }

        private void RenderProgramList(CareerGrowthView growth, GrowthProgramView selected)
        {
            RectTransform panel = CreatePanel(
                "ProgramList",
                selected.ActivityType == OffseasonActivityType.Study ? "유학 프로그램" : "훈련 프로그램",
                new Vector2(440f, 366f),
                new Vector2(-512f, 32f));
            int visibleIndex = 0;
            for (int index = 0; index < growth.Programs.Length; index++)
            {
                GrowthProgramView program = growth.Programs[index];
                if (!BelongsToPopup(program, selected.ActivityType))
                    continue;
                RenderProgramCard(panel, program, selected, visibleIndex++);
            }
        }

        private void RenderProgramCard(
            RectTransform panel,
            GrowthProgramView program,
            GrowthProgramView selected,
            int index)
        {
            bool isSelected = string.Equals(program.ProgramId, selected.ProgramId, StringComparison.Ordinal);
            Button button = CreateButton(
                "Program_" + program.ProgramId,
                panel,
                string.Empty,
                new Vector2(404f, 48f),
                new Vector2(0f, 98f - index * 50f),
                isSelected ? SelectedColor : CardColor,
                out _);
            button.interactable = program.CanUseThisOffseason && program.CanMeetCondition;
            string programId = program.ProgramId;
            button.onClick.AddListener(() => SelectProgram(programId));
            if (isSelected)
                CreateImage("SelectedBar", button.transform, AccentColor, new Vector2(5f, 44f), new Vector2(-198f, 0f));
            CreateText(
                "Name", button.transform, GetProgramLabel(program.ProgramId), 16, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(240f, 24f), new Vector2(-63f, 8f), PrimaryTextColor);
            CreateText(
                "Meta", button.transform,
                $"{program.DurationWeeks}주 · {FormatMoney(program.MoneyCost)} · 컨디션 {FormatSigned(program.ConditionChange)}",
                11, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(260f, 20f), new Vector2(-53f, -12f),
                program.CanSelect ? SecondaryTextColor : ErrorColor);
            CreateText(
                "Focus", button.transform, GetProgramFocus(program), 11, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(105f, 38f), new Vector2(139f, 0f), AccentColor);
        }

        private void RenderDetails(GrowthProgramView selected)
        {
            RectTransform panel = CreatePanel(
                "Details",
                GetProgramLabel(selected.ProgramId),
                new Vector2(928f, 366f),
                new Vector2(242f, 32f));
            CreateText(
                "Description", panel, GetProgramDescription(selected), 13, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(520f, 26f), new Vector2(-175f, 108f),
                SecondaryTextColor);

            if (selected.SupportsIntensity)
                RenderIntensitySelector(panel, selected);
            else
                RenderProgramBadge(panel, selected);

            CreateText(
                "EffectsTitle", panel,
                selected.ActivityType == OffseasonActivityType.Study ? "주요 예상 효과" : "핵심 훈련 효과",
                15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(370f, 25f), new Vector2(-245f, 77f), PrimaryTextColor);
            int rowCount = Math.Min(4, selected.AbilityRanges.Length);
            for (int index = 0; index < rowCount; index++)
                RenderAbilityRow(panel, selected.AbilityRanges[index], index);

            RenderBurdenPanel(panel, selected);
        }

        private void RenderIntensitySelector(RectTransform panel, GrowthProgramView selected)
        {
            CreateText(
                "IntensityLabel", panel, "훈련 강도", 12, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(100f, 32f), new Vector2(120f, 108f), SecondaryTextColor);
            TrainingIntensity[] intensities =
                { TrainingIntensity.Safe, TrainingIntensity.Standard, TrainingIntensity.Intensive };
            for (int index = 0; index < intensities.Length; index++)
            {
                TrainingIntensity intensity = intensities[index];
                bool isSelected = intensity == selected.Intensity;
                Button button = CreateButton(
                    "Intensity_" + intensity,
                    panel,
                    GetIntensityLabel(intensity),
                    new Vector2(94f, 38f),
                    new Vector2(220f + index * 96f, 108f),
                    isSelected ? AccentColor : CardColor,
                    out Text label);
                label.fontSize = 13;
                button.onClick.AddListener(() => SelectIntensity(intensity));
            }
        }

        private static void RenderProgramBadge(RectTransform panel, GrowthProgramView selected)
        {
            string label = selected.ActivityType == OffseasonActivityType.Study ? "고급 성장" : "고정 강도";
            RectTransform badge = CreateImage(
                "ProgramBadge",
                panel,
                new Color(0.32f, 0.18f, 0.03f, 1f),
                new Vector2(118f, 34f),
                new Vector2(385f, 108f));
            CreateText(
                "Label", badge, label, 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, GoldColor, stretch: true);
        }

        private static void RenderAbilityRow(
            RectTransform panel,
            AbilityGrowthRange range,
            int index)
        {
            float y = 43f - index * 52f;
            CreateText(
                "Ability_" + range.Ability,
                panel,
                GetAbilityLabel(range.Ability),
                14,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(75f, 28f),
                new Vector2(-330f, y),
                SecondaryTextColor);
            CreateProjectedBar(
                panel,
                range.CurrentValue,
                range.MaximumValue,
                new Vector2(170f, 12f),
                new Vector2(-205f, y));
            string value = range.MinimumValue == range.MaximumValue
                ? $"{range.CurrentValue} → {range.MaximumValue}"
                : $"{range.CurrentValue} → {range.MinimumValue}~{range.MaximumValue}";
            string gain = range.MinimumGain == range.MaximumGain
                ? FormatSigned(range.MaximumGain)
                : $"+{range.MinimumGain}~{range.MaximumGain}";
            CreateText(
                "Value_" + range.Ability,
                panel,
                value,
                15,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(125f, 28f),
                new Vector2(-70f, y),
                PrimaryTextColor);
            CreateText(
                "Gain_" + range.Ability,
                panel,
                gain,
                14,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                new Vector2(65f, 28f),
                new Vector2(40f, y),
                AccentColor);
        }

        private void RenderBurdenPanel(RectTransform parent, GrowthProgramView selected)
        {
            RectTransform panel = CreateImage(
                "Burden",
                parent,
                CardColor,
                new Vector2(350f, 225f),
                new Vector2(270f, -39f));
            CreateText(
                "Title", panel,
                selected.ActivityType == OffseasonActivityType.Study ? "유학 적합도와 가능성" : "부가 효과와 부담",
                15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(310f, 28f), new Vector2(0f, 84f), PrimaryTextColor);
            RenderFact(panel, "훈련 적합", GetFitLabel(selected.Fit), 48f, GetFitColor(selected.Fit));
            RenderFact(panel, "반복 효율", $"{selected.RepetitionMultiplier:P0}", 18f,
                selected.RepetitionMultiplier < 1d ? WarningColor : CyanColor);
            RenderFact(panel, "부상 위험", GetRiskLabel(selected.InjuryRisk), -12f,
                GetRiskColor(selected.InjuryRisk));
            string guarantee = selected.MinimumGuaranteedGain > 0
                ? $"총 +{selected.MinimumGuaranteedGain}"
                : "없음";
            if (selected.CanRaisePotential)
                guarantee += " · Potential";
            RenderFact(panel, "보장·특별", guarantee, -42f,
                selected.MinimumGuaranteedGain > 0 ? CyanColor : SecondaryTextColor);
            RenderConditionFlow(panel, selected);
        }

        private static void RenderConditionFlow(Transform parent, GrowthProgramView selected)
        {
            int[] values = new int[4];
            values[0] = selected.ConditionBefore;
            for (int index = 1; index < values.Length; index++)
            {
                values[index] = Mathf.RoundToInt(Mathf.Lerp(
                    selected.ConditionBefore,
                    selected.ConditionAfter,
                    index / 3f));
            }

            CreateText(
                "ConditionFlowLabel", parent,
                $"컨디션  {selected.ConditionBefore} → {selected.ConditionAfter}",
                10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(170f, 20f), new Vector2(-70f, -68f),
                GetConditionColor(selected));
            const float startX = -120f;
            const float stepX = 80f;
            for (int index = 0; index < values.Length; index++)
            {
                float x = startX + stepX * index;
                float y = -91f + (values[index] - selected.ConditionAfter) * 0.35f;
                if (index > 0)
                {
                    float previousX = startX + stepX * (index - 1);
                    float previousY = -91f +
                                      (values[index - 1] - selected.ConditionAfter) * 0.35f;
                    CreateLine(
                        parent,
                        new Vector2(previousX, previousY),
                        new Vector2(x, y),
                        GetConditionColor(selected));
                }
                RectTransform dot = CreateImage(
                    "ConditionPoint_" + index,
                    parent,
                    GetConditionColor(selected),
                    new Vector2(8f, 8f),
                    new Vector2(x, y));
                CreateText(
                    "Value", dot, values[index].ToString(), 9, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(38f, 18f), new Vector2(0f, 13f),
                    PrimaryTextColor);
            }
        }

        private static void CreateLine(
            Transform parent,
            Vector2 from,
            Vector2 to,
            Color color)
        {
            Vector2 direction = to - from;
            RectTransform line = CreateImage(
                "ConditionLine",
                parent,
                color,
                new Vector2(direction.magnitude, 2f),
                (from + to) * 0.5f);
            line.localEulerAngles = new Vector3(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private static void RenderFact(
            Transform parent,
            string label,
            string value,
            float y,
            Color valueColor)
        {
            CreateText(
                "Label_" + label, parent, label, 12, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(135f, 25f), new Vector2(-86f, y), SecondaryTextColor);
            CreateText(
                "Value_" + label, parent, value, 12, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(170f, 25f), new Vector2(66f, y), valueColor);
        }

        private void RenderTimeline(CareerGrowthView growth, GrowthProgramView selected)
        {
            RectTransform panel = CreatePanel(
                "Timeline",
                growth.PlannedActivities.Length > 0
                    ? $"오프시즌 일정 · 담긴 계획 {growth.PlannedActivities.Length}개"
                    : "오프시즌 일정",
                new Vector2(1404f, 112f),
                new Vector2(0f, -225f));
            float width = 1320f / growth.TotalWeeks;
            float startX = -660f + width * 0.5f;
            for (int week = 1; week <= growth.TotalWeeks; week++)
            {
                bool isSelected = week >= selected.StartWeek && week <= selected.EndWeek;
                GrowthPlanItemView planned = FindPlannedActivity(growth.PlannedActivities, week);
                bool isPlanned = planned.ActivityId > 0;
                bool isPast = week < growth.CurrentWeek;
                Color color = isSelected
                    ? new Color(0.025f, 0.31f, 0.62f, 1f)
                    : isPlanned
                        ? new Color(0.035f, 0.22f, 0.40f, 1f)
                    : isPast
                        ? new Color(0.07f, 0.10f, 0.13f, 1f)
                        : CardColor;
                RectTransform segment = CreateImage(
                    "Week_" + week,
                    panel,
                    color,
                    new Vector2(width - 4f, 48f),
                    new Vector2(startX + (week - 1) * width, -12f));
                CreateText(
                    "Number", segment, week.ToString(), 11, FontStyle.Bold,
                    TextAnchor.UpperCenter, new Vector2(width - 6f, 22f), new Vector2(0f, 12f),
                    week == growth.CurrentWeek ? CyanColor : SecondaryTextColor);
                CreateText(
                    "State", segment,
                    isSelected
                        ? GetTimelineActivityLabel(selected.ActivityType)
                        : isPlanned
                            ? GetTimelineActivityLabel(planned.ActivityType)
                            : isPast ? "완료" : "가능",
                    10, FontStyle.Normal, TextAnchor.LowerCenter,
                    new Vector2(width - 6f, 22f), new Vector2(0f, -12f),
                    isSelected || isPlanned ? PrimaryTextColor : MutedTextColor);
                if (isPlanned && week == planned.StartWeek)
                {
                    int activityId = planned.ActivityId;
                    Button remove = CreateButton(
                        "RemovePlan_" + activityId,
                        segment,
                        "×",
                        new Vector2(20f, 20f),
                        new Vector2(width * 0.5f - 14f, 12f),
                        new Color(0.48f, 0.08f, 0.10f, 1f),
                        out Text removeText);
                    removeText.fontSize = 13;
                    remove.onClick.AddListener(() => CancelPlannedActivity(activityId));
                }
            }
        }

        private void RenderFooter(CareerGrowthView growth, GrowthProgramView selected)
        {
            RectTransform footer = CreateImage(
                "Footer",
                _content,
                PanelColor,
                new Vector2(1404f, 92f),
                new Vector2(0f, -366f));
            string result = BuildResultSummary(selected);
            CreateText(
                "SummaryLabel", footer, "예상 결과", 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(105f, 54f), new Vector2(-639f, 0f), PrimaryTextColor);
            CreateText(
                "Summary", footer, result, 14, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(680f, 58f), new Vector2(-232f, 0f), SecondaryTextColor);

            string warning = GetBlockingOrWarning(growth, selected);
            if (!string.IsNullOrEmpty(warning))
            {
                CreateText(
                    "Warning", footer, warning, 11, FontStyle.Normal, TextAnchor.UpperLeft,
                    new Vector2(680f, 25f), new Vector2(-232f, -27f),
                    selected.CanSelect ? WarningColor : ErrorColor);
            }

            Button plan = CreateButton(
                "Plan",
                footer,
                growth.PlannedActivities.Length > 0
                    ? $"계획에 담기 ({growth.PlannedActivities.Length + 1}개)"
                    : "성장 계획에 담기",
                new Vector2(205f, 58f),
                new Vector2(340f, 0f),
                new Color(0.11f, 0.16f, 0.22f, 1f),
                out Text planLabel);
            planLabel.fontSize = 15;
            plan.interactable = selected.CanSelect;
            plan.onClick.AddListener(AddToPlan);

            int executionCount = growth.PlannedActivities.Length + 1;
            string confirmLabel = executionCount > 1
                ? $"{executionCount}개 성장 계획 실행"
                : GetSingleActivityConfirmLabel(selected);
            Button confirm = CreateButton(
                "Confirm",
                footer,
                selected.CanSelect ? confirmLabel : GetDisabledButtonLabel(selected),
                new Vector2(235f, 58f),
                new Vector2(570f, 0f),
                new Color(0.025f, 0.31f, 0.67f, 1f),
                out Text confirmText);
            confirmText.fontSize = 16;
            confirm.interactable = selected.CanSelect;
            confirm.onClick.AddListener(ConfirmActivity);
        }

        private void SelectProgram(string programId)
        {
            _programId = programId;
            _intensity = TrainingIntensity.Standard;
            Render();
        }

        private void SelectIntensity(TrainingIntensity intensity)
        {
            _intensity = intensity;
            Render();
        }

        private void AddToPlan()
        {
            if (_manager.AddGrowthProgramToPlan(_programId, _intensity))
                Close();
        }

        private void ConfirmActivity()
        {
            if (_manager.AddAndExecuteGrowthPlan(_programId, _intensity))
                Close();
        }

        private void CancelPlannedActivity(int activityId)
        {
            _manager.CancelGrowthPlanActivity(activityId);
        }

        private static GrowthPlanItemView FindPlannedActivity(
            GrowthPlanItemView[] activities,
            int week)
        {
            for (int index = 0; index < activities.Length; index++)
            {
                if (week >= activities[index].StartWeek && week <= activities[index].EndWeek)
                    return activities[index];
            }
            return default;
        }

        private static bool BelongsToPopup(
            GrowthProgramView program,
            OffseasonActivityType selectedType)
        {
            if (selectedType == OffseasonActivityType.Study)
                return program.ActivityType == OffseasonActivityType.Study;
            if (selectedType == OffseasonActivityType.PersonalTraining)
                return program.ActivityType == OffseasonActivityType.PersonalTraining;
            return program.ActivityType == selectedType;
        }

        private static string GetSingleActivityConfirmLabel(GrowthProgramView selected)
        {
            return selected.ActivityType switch
            {
                OffseasonActivityType.Study => $"{selected.DurationWeeks}주 유학 확정",
                OffseasonActivityType.Rehabilitation => $"{selected.DurationWeeks}주 회복 시작",
                OffseasonActivityType.Rest => $"{selected.DurationWeeks}주 휴식 시작",
                OffseasonActivityType.TrainingPartner => $"{selected.DurationWeeks}주 합동 훈련 시작",
                _ => $"{selected.DurationWeeks}주 훈련 시작"
            };
        }

        private static string BuildResultSummary(GrowthProgramView selected)
        {
            string abilities = string.Empty;
            int count = Math.Min(3, selected.AbilityRanges.Length);
            for (int index = 0; index < count; index++)
            {
                if (index > 0)
                    abilities += " · ";
                AbilityGrowthRange range = selected.AbilityRanges[index];
                abilities += GetAbilityLabel(range.Ability) + " " +
                             (range.MinimumGain == range.MaximumGain
                                 ? FormatSigned(range.MaximumGain)
                                 : $"+{range.MinimumGain}~{range.MaximumGain}");
            }
            if (string.IsNullOrEmpty(abilities))
                abilities = $"컨디션 {FormatSigned(selected.ConditionChange)}";
            return $"{abilities} · 잔여 오프시즌 {selected.RemainingWeeksAfter}주";
        }

        private static string GetBlockingOrWarning(
            CareerGrowthView growth,
            GrowthProgramView selected)
        {
            if (!selected.CanAfford)
                return $"비용이 {FormatMoney(selected.MoneyShortfall)} 부족합니다.";
            if (!selected.CanFitSchedule)
                return $"남은 기간이 {selected.WeeksShortfall}주 부족합니다.";
            if (!selected.CanMeetCondition)
            {
                return selected.PlannedWeeks > 0
                    ? $"앞선 계획 후 예상 컨디션이 {selected.ConditionBefore}입니다. " +
                      $"컨디션 {selected.MinimumCondition} 이상이 되도록 회복 활동을 먼저 담아 주세요."
                    : $"시작하려면 컨디션 {selected.MinimumCondition} 이상이 필요합니다.";
            }
            if (!selected.CanUseThisOffseason)
            {
                return HasPlannedStudy(growth.PlannedActivities)
                    ? "성장 계획에 이미 유학이 포함되어 있습니다. 유학은 오프시즌당 한 번만 가능합니다."
                    : "이번 오프시즌에는 유학을 이미 완료했습니다.";
            }
            if (selected.IsConditionDanger)
                return "완료 후 위험 구간입니다. 다음 활동으로 컨디션 관리를 권장합니다.";
            if (selected.RepetitionMultiplier < 1d)
                return $"동일 계열 반복으로 성장 효율이 {selected.RepetitionMultiplier:P0}로 감소합니다.";
            if (selected.UsesMajorityOfRemainingTime)
                return "남은 오프시즌의 절반 이상을 사용하는 활동입니다.";
            return string.Empty;
        }

        private static bool HasPlannedStudy(GrowthPlanItemView[] activities)
        {
            for (int index = 0; index < activities.Length; index++)
            {
                if (activities[index].ActivityType == OffseasonActivityType.Study)
                    return true;
            }
            return false;
        }

        private static string GetDisabledButtonLabel(GrowthProgramView selected)
        {
            if (!selected.CanFitSchedule) return "기간 부족";
            if (!selected.CanAfford) return "비용 부족";
            if (!selected.CanMeetCondition) return "컨디션 부족";
            return "활동 확정 불가";
        }

        private static string GetProgramFocus(GrowthProgramView program)
        {
            if (program.AbilityRanges.Length == 0)
                return program.ConditionChange > 0 ? "회복" : "휴식";
            string first = GetAbilityLabel(program.AbilityRanges[0].Ability);
            if (program.AbilityRanges.Length == 1)
                return first;
            return first + " · " + GetAbilityLabel(program.AbilityRanges[1].Ability);
        }

        private RectTransform CreatePanel(
            string name,
            string title,
            Vector2 size,
            Vector2 position)
        {
            RectTransform panel = CreateImage(name, _content, BorderColor, size, position);
            RectTransform surface = CreateImage(
                "Surface", panel, PanelColor, Vector2.zero, Vector2.zero, stretch: true);
            surface.offsetMin = new Vector2(2f, 2f);
            surface.offsetMax = new Vector2(-2f, -2f);
            CreateText(
                "Title", panel, title, 19, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 34f, 40f), new Vector2(0f, size.y * 0.5f - 27f),
                PrimaryTextColor);
            CreateImage(
                "Divider", panel, BorderColor, new Vector2(size.x - 28f, 1f),
                new Vector2(0f, size.y * 0.5f - 51f));
            return panel;
        }

        private static void CreateProjectedBar(
            Transform parent,
            int current,
            int projectedMaximum,
            Vector2 size,
            Vector2 position)
        {
            RectTransform track = CreateImage(
                "Track", parent, new Color(0.07f, 0.12f, 0.16f, 1f), size, position);
            float currentWidth = Mathf.Max(2f, size.x * Mathf.Clamp01(current / 100f));
            RectTransform currentFill = CreateImage(
                "Current", track, AccentColor, new Vector2(currentWidth, size.y - 4f), Vector2.zero);
            currentFill.anchorMin = currentFill.anchorMax = new Vector2(0f, 0.5f);
            currentFill.pivot = new Vector2(0f, 0.5f);
            currentFill.anchoredPosition = new Vector2(2f, 0f);
            float projectedWidth = size.x * Mathf.Clamp01((projectedMaximum - current) / 100f);
            if (projectedWidth <= 0f)
                return;
            RectTransform projected = CreateImage(
                "Projected", track, CyanColor,
                new Vector2(projectedWidth, size.y - 4f), Vector2.zero);
            projected.anchorMin = projected.anchorMax = new Vector2(0f, 0.5f);
            projected.pivot = new Vector2(0f, 0.5f);
            projected.anchoredPosition = new Vector2(2f + currentWidth, 0f);
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 size,
            Vector2 position,
            bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Vector2 size,
            Vector2 position,
            Color color,
            bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            Vector2 position,
            Color color,
            out Text text)
        {
            RectTransform rect = CreateImage(name, parent, color, size, position);
            Button button = rect.gameObject.AddComponent<Button>();
            rect.GetComponent<Image>().raycastTarget = true;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.72f);
            button.colors = colors;
            text = CreateText(
                "Label", rect, label, 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor, stretch: true);
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(parent.GetChild(index).gameObject);
                else
#endif
                    Destroy(parent.GetChild(index).gameObject);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
