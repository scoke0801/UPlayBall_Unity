using Baseball.Core.Growth;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerRosterLineup
    {
        private RectTransform _comparisonContent;
        private GameObject _conditionScroll;
        private GameObject _comparisonScroll;
        private Button _conditionTab;
        private Button _comparisonTab;
        private bool _isComparisonTab;

        private void BuildAnalysisTabs(RectTransform panel)
        {
            var safe = (RectTransform)panel.Find("ContentSafeRect");
            RectTransform tabs = CreateInspectorControlRow(safe, "AnalysisSubTabs");
            OwnerRuntimeUiFactory.SetAnchors(tabs, Vector2.up, Vector2.one,
                new Vector2(0f, -40f), Vector2.zero);
            _conditionTab = CreateToolbarButton(tabs, "ConditionAnalysisTab", "컨디션 분석", 0f,
                () => SelectAnalysisTab(false));
            _comparisonTab = CreateToolbarButton(tabs, "PlayerComparisonTab", "선수비교", 0f,
                () => SelectAnalysisTab(true));
            _conditionTab.GetComponent<LayoutElement>().flexibleWidth = 1f;
            _comparisonTab.GetComponent<LayoutElement>().flexibleWidth = 1f;
            _conditionScroll = safe.Find("RoleScroll").gameObject;
            ((RectTransform)_conditionScroll.transform).offsetMax = new Vector2(0f, -44f);
            ScrollRect comparison = OwnerRuntimeUiFactory.CreateVerticalScroll(
                "ComparisonScroll", safe, out _comparisonContent);
            _comparisonScroll = comparison.gameObject;
            OwnerRuntimeUiFactory.Stretch((RectTransform)comparison.transform);
            ((RectTransform)comparison.transform).offsetMax = new Vector2(0f, -44f);
            ClearComparison();
        }

        private void SelectAnalysisTab(bool comparison)
        {
            if (_positionSourceIndex >= 0) ClosePositionEditor();
            _isComparisonTab = comparison;
            _conditionScroll.SetActive(!comparison);
            _comparisonScroll.SetActive(comparison);
            SetPlayerGroupTabVisual(_conditionTab, !comparison);
            SetPlayerGroupTabVisual(_comparisonTab, comparison);
            SetAnalysisTitle(comparison ? "교체 전후 비교" : "편성 분석");
        }

        private void ClearComparison()
        {
            if (_comparisonContent == null) return;
            OwnerRuntimeUiFactory.ClearChildren(_comparisonContent);
            AddComparisonText(_comparisonContent, "ComparisonEmptyTitle",
                "교체할 자리와 후보 선수를 선택하세요", 44f);
            RectTransform slots = OwnerRuntimeUiFactory.CreateRect("ComparisonEmptySlots", _comparisonContent);
            slots.gameObject.AddComponent<LayoutElement>().minHeight = 144f;
            var layout = slots.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = CareerUiTheme.Space2;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            CreateComparisonEmptySlot(slots, "OutgoingSlot", "교체 전", "현재 배치된 선수", "교체할 자리를 선택하세요");
            CreateComparisonEmptySlot(slots, "IncomingSlot", "교체 후", "새로 기용할 선수", "보유 선수에서 선택하세요");
            _comparisonScroll.GetComponent<ScrollRect>().verticalNormalizedPosition = 1f;
            SelectAnalysisTab(_isComparisonTab);
        }

        private static void CreateComparisonEmptySlot(Transform parent, string name, string role,
            string title, string instruction)
        {
            Image surface = OwnerRuntimeUiFactory.CreateImage(name, parent, OwnerDashboardStyle.InsetSurface);
            OwnerDashboardStyle.ApplyInset(surface);
            var sizing = surface.gameObject.AddComponent<LayoutElement>();
            sizing.minWidth = 0f;
            sizing.flexibleWidth = 1f;
            var layout = surface.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = CareerUiTheme.Space2;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            AddComparisonText(surface.transform, name + "Role", role, 24f);
            AddComparisonText(surface.transform, name + "Title", title, 32f);
            AddComparisonText(surface.transform, name + "Instruction", instruction, 40f);
        }

        private void ShowComparison(OwnerCollectionCardSnapshot outgoing, OwnerCollectionCardSnapshot incoming)
        {
            var cards = ResolveCardDetails(new[] { outgoing, incoming });
            if (cards == null || cards.Count != 2) return;
            OwnerRuntimeUiFactory.ClearChildren(_comparisonContent);
            bool pitcher = IsPitcher(incoming);
            PlayerAbility[] abilities = pitcher
                ? new[] { PlayerAbility.Stamina, PlayerAbility.Velocity, PlayerAbility.Stuff, PlayerAbility.Breaking, PlayerAbility.Control, PlayerAbility.PitcherMental }
                : new[] { PlayerAbility.Contact, PlayerAbility.Power, PlayerAbility.Speed, PlayerAbility.Bunt, PlayerAbility.Defense, PlayerAbility.BatterMental };
            string[] labels = pitcher ? new[] { "체력", "구속", "구위", "변화구", "제구력", "정신력" }
                : new[] { "교타력", "장타력", "주력", "번트", "수비력", "정신력" };
            RectTransform visual = OwnerRuntimeUiFactory.CreateRect("ComparisonVisual", _comparisonContent);
            visual.gameObject.AddComponent<LayoutElement>().minHeight = 192f;
            CreateComparisonCard(visual, cards[0], false);
            CreateComparisonCard(visual, cards[1], true);
            RectTransform chart = OwnerRuntimeUiFactory.CreateRect("ComparisonRadar", visual);
            OwnerRuntimeUiFactory.SetAnchors(chart, new Vector2(.25f, 0f), new Vector2(.75f, 1f),
                new Vector2(20f, 32f), new Vector2(-20f, -24f));
            var before = new float[6];
            var after = new float[6];
            float maximum = 100f;
            for (int i = 0; i < abilities.Length; i++)
            {
                before[i] = cards[0].GetEffectiveAbility(abilities[i]) ?? float.NaN;
                after[i] = cards[1].GetEffectiveAbility(abilities[i]) ?? float.NaN;
                if (!float.IsNaN(before[i])) maximum = Mathf.Max(maximum, before[i]);
                if (!float.IsNaN(after[i])) maximum = Mathf.Max(maximum, after[i]);
                float angle = (90f - i * 60f) * Mathf.Deg2Rad;
                Vector2 anchor = new Vector2(.5f + Mathf.Cos(angle) * .5f, .5f + Mathf.Sin(angle) * .5f);
                Text label = CreateConditionChartText(chart, "Axis" + i, labels[i], 12, OwnerDashboardStyle.Ivory);
                label.gameObject.AddComponent<CareerUiPreserveTextColor>();
                OwnerRuntimeUiFactory.SetAnchors(label.rectTransform, anchor, anchor,
                    new Vector2(-24f, -10f), new Vector2(24f, 10f));
            }
            chart.gameObject.AddComponent<UIOpponentRadar>().Bind(before, after, 6, maximum);
            RectTransform table = OwnerRuntimeUiFactory.CreateRect("ComparisonStats", _comparisonContent);
            table.gameObject.AddComponent<LayoutElement>().minHeight = 108f;
            for (int side = 0; side < 2; side++)
            {
                Color accent = side == 0 ? CareerUiTheme.RosterAccent : CareerUiTheme.Error;
                Color surface = Color.Lerp(CareerUiTheme.RosterSurface, accent, .35f);
                RectTransform band = CreateAnalysisSurface(table, "StatBand" + side, surface);
                OwnerRuntimeUiFactory.SetAnchors(band, new Vector2(side * .5f, 0f),
                    new Vector2((side + 1) * .5f, 1f), new Vector2(2f, 0f), new Vector2(-2f, 0f));
                for (int i = 0; i < abilities.Length; i++)
                {
                    int row = i % 3;
                    int column = i / 3;
                    int? value = cards[side].GetEffectiveAbility(abilities[i]);
                    Text cell = CreateConditionChartText(table, $"Stat{side}_{i}",
                        $"{labels[i]}  {value?.ToString() ?? "—"}", 13, CareerUiTheme.TextPrimary);
                    cell.fontStyle = FontStyle.Bold;
                    float left = side * .5f + column * .25f;
                    OwnerRuntimeUiFactory.SetAnchors(cell.rectTransform,
                        new Vector2(left, 1f - (row + 1) / 3f), new Vector2(left + .25f, 1f - row / 3f),
                        Vector2.zero, Vector2.zero);
                }
            }
            AddComparisonText(_comparisonContent, "ComparisonSaveHint",
                "저장 전 미리보기", 40f);
            SelectAnalysisTab(true);
        }

        private void CreateComparisonCard(RectTransform parent, OwnerCollectionCardSnapshot player, bool incoming)
        {
            RectTransform side = OwnerRuntimeUiFactory.CreateRect(incoming ? "IncomingPlayer" : "OutgoingPlayer", parent);
            OwnerRuntimeUiFactory.SetAnchors(side, new Vector2(incoming ? .76f : 0f, 0f),
                new Vector2(incoming ? 1f : .24f, 1f), Vector2.zero, Vector2.zero);
            Text role = CreateConditionChartText(side, "ComparisonRole", incoming ? "교체 후" : "교체 전", 13,
                incoming ? CareerUiTheme.Error : CareerUiTheme.RosterAccent);
            OwnerRuntimeUiFactory.SetAnchors(role.rectTransform, Vector2.up, Vector2.one,
                new Vector2(0f, -24f), Vector2.zero);
            RectTransform cardSlot = OwnerRuntimeUiFactory.CreateRect("ComparisonCardSlot", side);
            OwnerRuntimeUiFactory.SetAnchors(cardSlot, Vector2.zero, Vector2.one,
                new Vector2(4f, 32f), new Vector2(-4f, -28f));
            PlayerMiniCardView card = PlayerMiniCardView.CreateRuntime(cardSlot, "ComparisonCard");
            card.UseLineupSlotLayout();
            card.UseRosterPresentation();
            BindOwnedPlayerCard(card, player);
            var rect = (RectTransform)card.transform;
            OwnerRuntimeUiFactory.Stretch(rect);
            var aspect = card.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = PlayerMiniCardView.LineupSlotWidth / PlayerMiniCardView.LineupSlotHeight;
            card.Selected += ShowCardDetail;
            card.DetailRequested += ShowCardDetail;
            Text name = CreateConditionChartText(side, "ComparisonName", player.DisplayName, 12,
                incoming ? CareerUiTheme.Error : CareerUiTheme.RosterAccent);
            OwnerRuntimeUiFactory.SetAnchors(name.rectTransform, Vector2.zero, Vector2.right,
                Vector2.zero, new Vector2(0f, 28f));
        }

        private static void AddComparisonText(Transform parent, string name, string value, float height)
        {
            Text text = CreateConditionChartText(parent, name, value, 13, OwnerDashboardStyle.Ivory);
            OwnerDashboardStyle.SetTypography(text);
            if (text.GetComponent<CareerUiPreserveTextColor>() == null)
                text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.gameObject.AddComponent<LayoutElement>().minHeight = height;
        }
    }
}
