using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerTeamColor
    {
        private void BuildDiscoveryControls(RectTransform parent)
        {
            GameObject host = DefaultControls.CreateInputField(new DefaultControls.Resources());
            host.name = "Search";
            host.transform.SetParent(parent, false);
            OwnerDugoutDetailUiFactory.Place(host.GetComponent<RectTransform>(), .035f, .81f, .73f, .885f);
            host.AddComponent<CareerUiPreserveTextColor>();
            _search = host.GetComponent<InputField>();
            foreach (Text label in host.GetComponentsInChildren<Text>(true))
            {
                label.font = UIProjectFonts.Default;
                label.fontSize = 14;
                label.color = CareerUiTheme.ReferenceText;
                label.supportRichText = false;
                label.rectTransform.offsetMin = new Vector2(CareerUiTheme.Space2, CareerUiTheme.Space1 / 2f);
                label.rectTransform.offsetMax = -label.rectTransform.offsetMin;
            }
            ((Text)_search.placeholder).text = "이름·효과 검색 (예: 제구, 올 스탯)";
            // 행을 재사용하므로 검색 중에도 입력 필드와 한글 조합 상태를 보존한다.
            _search.onValueChanged.AddListener(_ => RefreshDiscovery());
            CreateBoardButton(parent, "ResetFilters", "초기화", .75f, .81f, .965f, .885f, ResetDiscovery);
            _targetFilter = OwnerCardFilters.CreateDropdown(parent, "TargetFilter",
                new List<string> { "대상: 전체", "대상: 타자", "대상: 투수" }, 0);
            OwnerDugoutDetailUiFactory.Place(_targetFilter.GetComponent<RectTransform>(), .035f, .72f, .32f, .795f);
            _targetFilter.onValueChanged.AddListener(_ => RefreshDiscovery());
            _sort = OwnerCardFilters.CreateDropdown(parent, "Sort",
                new List<string> { "장착 가능 → 등급순", "등급 높은 순", "효과 합계 높은 순", "부족 인원 적은 순", "이름순" }, 0);
            OwnerDugoutDetailUiFactory.Place(_sort.GetComponent<RectTransform>(), .34f, .72f, .965f, .795f);
            _sort.onValueChanged.AddListener(_ => RefreshDiscovery());
            _resultCount = CreateBoardLabel(parent, "ResultCount", string.Empty, .035f, .66f, .965f, .715f, 13);
        }

        private void ResetDiscovery()
        {
            _search.SetTextWithoutNotify(string.Empty);
            _targetFilter.SetValueWithoutNotify(0);
            _sort.SetValueWithoutNotify(0);
            SetFilter(false);
        }

        private void RefreshDiscovery()
        {
            if (_snapshot == null) return;
            RebuildCandidates();
            _candidateScroll.StopMovement();
            _candidateContent.anchoredPosition = Vector2.zero;
        }

        private bool MatchesDiscovery(OwnerTeamColorCandidateSnapshot candidate)
        {
            if (_targetFilter.value == 1 && candidate.Definition.HitterBonus.Total == 0) return false;
            if (_targetFilter.value == 2 && candidate.Definition.PitcherBonus.Total == 0) return false;
            string query = _search.text.Trim();
            if (query.Length == 0) return true;
            // '전체 능력치'로 요약된 효과도 개별 능력치 검색에서 빠뜨리지 않는다.
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
            {
                var ability = (PlayerAbility)index;
                if (candidate.Definition.HitterBonus.Get(ability) + candidate.Definition.PitcherBonus.Get(ability) <= 0) continue;
                if (OwnerDugoutLoadoutPresentationBuilder.GetAbilityName(ability).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            if (query == "올 스탯") query = "전체 능력치";
            return candidate.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                OwnerTeamColorCardView.DescribeComparisonEffect(candidate.Definition)
                    .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int CompareCandidates(OwnerTeamColorCandidateSnapshot left, OwnerTeamColorCandidateSnapshot right)
        {
            int order = 0;
            if (_sort.value == 0) order = right.IsActive.CompareTo(left.IsActive);
            if (order != 0) return order;
            if (_sort.value <= 1) order = GetGradeOrder(left.Grade).CompareTo(GetGradeOrder(right.Grade));
            if (_sort.value == 2)
                order = (right.Definition.HitterBonus.Total + right.Definition.PitcherBonus.Total)
                    .CompareTo(left.Definition.HitterBonus.Total + left.Definition.PitcherBonus.Total);
            if (_sort.value == 3)
                order = Math.Max(0, left.Definition.RequiredCount - left.EligibleCount)
                    .CompareTo(Math.Max(0, right.Definition.RequiredCount - right.EligibleCount));
            if (order != 0) return order;
            order = string.Compare(left.Name, right.Name, StringComparison.Ordinal);
            return order != 0 ? order : string.CompareOrdinal(left.Id, right.Id);
        }

        private static int GetGradeOrder(string grade)
        {
            switch (grade)
            {
                case "S": return 0;
                case "A": return 1;
                case "B": return 2;
                default: return 3;
            }
        }

    }
}
