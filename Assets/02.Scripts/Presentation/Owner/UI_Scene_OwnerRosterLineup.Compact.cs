using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerRosterLineup
    {
        private static Sprite _rosterPortrait;

        private static Sprite GetRosterPortrait() => _rosterPortrait != null ? _rosterPortrait :
            _rosterPortrait = Resources.Load<Sprite>("UI/PlayerCards/PlayerPortrait_UpperSilhouette_V1");

        private static void CompactPanel(RectTransform panel)
        {
            panel.Find("HeaderAccent").gameObject.SetActive(false);
            RectTransform header = (RectTransform)panel.Find("HeaderSlot");
            header.offsetMin = new Vector2(6f, -24f);
            header.offsetMax = new Vector2(-6f, -2f);
            header.GetComponent<Text>().fontSize = 12;
            RectTransform surface = (RectTransform)panel.Find("HeaderSurface");
            surface.offsetMin = new Vector2(1f, -26f);
            RectTransform safe = (RectTransform)panel.Find("ContentSafeRect");
            safe.offsetMin = new Vector2(3f, 3f);
            safe.offsetMax = new Vector2(-3f, -27f);
            var layout = safe.GetComponentInChildren<VerticalLayoutGroup>();
            if (layout != null) { layout.padding = new RectOffset(3, 3, 3, 3); layout.spacing = 3f; }
        }

        private void RenderPositionFilters(RectTransform content, bool pitcher)
        {
            string[] labels = pitcher ? new[] { "전체", "선발", "불펜", "셋업", "마무리" } :
                new[] { "전체", "포수", "1루수", "2루수", "3루수", "유격수", "외야수", "지명타자" };
            RectTransform row = OwnerRuntimeUiFactory.CreateRect("PositionFilters", content);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            for (int index = 0; index < labels.Length; index++)
            {
                int filter = index;
                Button button = OwnerWorkspaceUiFactory.CreateButton(row, "Filter" + index, labels[index], () =>
                {
                    _positionFilter = filter;
                    RenderActivePlayerGroup();
                });
                var sizing = button.GetComponent<LayoutElement>();
                sizing.minWidth = 0; sizing.preferredWidth = 60; sizing.flexibleWidth = 1;
                Text text = button.GetComponentInChildren<Text>();
                text.fontSize = 11;
                text.color = filter == _positionFilter ? Color.white : CareerUiTheme.ReferenceText;
                button.image.color = filter == _positionFilter ? CareerUiTheme.ReferenceAccent : CareerUiTheme.ReferenceButton;
            }
        }

        private bool MatchesFilter(OwnerCollectionCardSnapshot card, bool pitcher)
        {
            if (IsPitcher(card) != pitcher) return false;
            if (_positionFilter == 0) return true;
            if (pitcher)
            {
                if (_positionFilter == 1) return card.Position == PlayerPosition.StartingPitcher;
                // 셋업·마무리는 현재 배치된 역할로 구분한다.
                if (_positionFilter == 3) return card.CardId == _model.Snapshot.Preset.SetupPitcherCardId;
                if (_positionFilter == 4) return card.CardId == _model.Snapshot.Preset.CloserPitcherCardId;
                return card.Position == PlayerPosition.ReliefPitcher;
            }
            return _positionFilter switch
            {
                1 => card.Position == PlayerPosition.Catcher,
                2 => card.Position == PlayerPosition.FirstBase,
                3 => card.Position == PlayerPosition.SecondBase,
                4 => card.Position == PlayerPosition.ThirdBase,
                5 => card.Position == PlayerPosition.Shortstop,
                6 => card.Position == PlayerPosition.LeftField || card.Position == PlayerPosition.CenterField || card.Position == PlayerPosition.RightField,
                7 => card.Position == PlayerPosition.DesignatedHitter,
                _ => true
            };
        }

        private static void RenderRosterChart(RectTransform content, IReadOnlyList<OwnerLineupSlotModel> slots, bool pitcher)
        {
            AddSectionTitle(content, pitcher ? "투수 분석 · 11명" : "타선 분석 · 타순별 컨디션");
            RectTransform chart = OwnerRuntimeUiFactory.CreateRect("RosterChart", content);
            chart.gameObject.AddComponent<LayoutElement>().preferredHeight = 250;
            RectTransform plot = OwnerRuntimeUiFactory.CreateRect("Plot", chart);
            OwnerRuntimeUiFactory.SetAnchors(plot, new Vector2(0.12f, 0.18f), new Vector2(0.99f, 0.94f), Vector2.zero, Vector2.zero);
            var graphic = plot.gameObject.AddComponent<UIRosterConditionPlot>();
            var values = new float[slots.Count];
            var valid = new bool[slots.Count];
            for (int index = 0; index < slots.Count; index++)
            {
                values[index] = slots[index].Player?.Condition ?? 0;
                valid[index] = slots[index].Player != null;
                float left = 0.12f + 0.87f * index / slots.Count;
                float right = 0.12f + 0.87f * (index + 1) / slots.Count;
                Text label = CreateAnalysisText(chart, "Order" + index,
                    FormatCompactRole(slots[index].Label), 10, FontStyle.Bold, TextAnchor.MiddleCenter);
                OwnerRuntimeUiFactory.SetAnchors(label.rectTransform, new Vector2(left, 0f), new Vector2(right, 0.08f), Vector2.zero, Vector2.zero);
                Text value = CreateAnalysisText(chart, "Value" + index, valid[index] ? values[index].ToString("0") : "—",
                    10, FontStyle.Bold, TextAnchor.MiddleCenter);
                OwnerRuntimeUiFactory.SetAnchors(value.rectTransform, new Vector2(left, 0.08f), new Vector2(right, 0.18f), Vector2.zero, Vector2.zero);
            }
            graphic.Bind(values, valid, pitcher);
            string[] bands = { "주의", "보통", "좋음" };
            float[] lows = { 0, 0.6f, 0.8f };
            float[] highs = { 0.6f, 0.8f, 1 };
            for (int i = 0; i < 3; i++)
            {
                Text band = CreateAnalysisText(chart, "Band" + i, bands[i], 11, FontStyle.Bold, TextAnchor.MiddleCenter);
                OwnerRuntimeUiFactory.SetAnchors(band.rectTransform, new Vector2(0, 0.18f + lows[i] * 0.76f),
                    new Vector2(0.12f, 0.18f + highs[i] * 0.76f), Vector2.zero, Vector2.zero);
            }
            AddSectionTitle(content, "저장 컨디션 0~100 · 배치·궁합 보정 전");
        }
    }
}
