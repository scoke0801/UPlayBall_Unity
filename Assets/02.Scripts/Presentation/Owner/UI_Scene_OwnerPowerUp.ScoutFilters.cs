using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerPowerUp
    {
        private InputField _scoutScopeSearch;
        private Dropdown _scoutTeamFilter;
        private Dropdown _scoutYearFilter;
        private Button _scoutPolicyScopeButton;
        private bool _hasScoutPolicyScopeFilter;
        private string _scoutPolicyScope = string.Empty;
        private readonly List<string> _scoutTeamIds = new List<string>();
        private readonly List<int> _scoutYears = new List<int>();
        private readonly List<OwnerScoutProductSnapshot> _scoutScopes = new List<OwnerScoutProductSnapshot>();
        private OwnerScoutScreenSnapshot _scoutFilterSource;

        private void BuildScoutScopeFilters(Transform board)
        {
            Image search = ScoutSurface(board, "ScopeSearch", 24, 128, 184, 28, ScoutSilver);
            search.raycastTarget = true;
            _scoutScopeSearch = search.gameObject.AddComponent<InputField>();
            _scoutScopeSearch.targetGraphic = search;
            _scoutScopeSearch.textComponent = ScoutLabel(search.transform, "ScopeSearchText", "", 12, 8, 2, 168, 24);
            _scoutScopeSearch.placeholder = ScoutLabel(search.transform, "ScopePlaceholder", "구단·연도 검색", 12, 8, 2, 168, 24);
            _scoutScopeSearch.onValueChanged.AddListener(_ => RefreshScoutScopeFilters());
            ScoutButton(board, "ResetScopeFilters", "초기화", ResetScoutScopeFilters, 216, 128, 52, 28);
            _scoutTeamFilter = OwnerCardFilters.CreateDropdown(board, "ScoutTeamFilter", new List<string> { "전체 구단" }, 0);
            PlaceReference(_scoutTeamFilter.GetComponent<RectTransform>(), 24, 164, 244, 28);
            _scoutYearFilter = OwnerCardFilters.CreateDropdown(board, "ScoutYearFilter", new List<string> { "전체 연도" }, 0);
            PlaceReference(_scoutYearFilter.GetComponent<RectTransform>(), 24, 200, 152, 28);
            ScoutButton(board, "ShowSelectedScope", "현재 선택", () =>
            {
                RevealScoutScope(FindScoutProduct(_selectedScoutProductId));
                BindScout();
                FocusSelectedScoutScope();
            }, 184, 200, 84, 28);
            _scoutTeamFilter.onValueChanged.AddListener(_ => RefreshScoutScopeFilters());
            _scoutYearFilter.onValueChanged.AddListener(_ => RefreshScoutScopeFilters());
        }

        private void RefreshScoutScopeFilters()
        {
            _scoutMapPage = 0;
            if (_snapshot != null) BindScout();
        }

        private void ResetScoutScopeFilters()
        {
            _scoutScopeSearch.SetTextWithoutNotify(string.Empty);
            _scoutTeamFilter.SetValueWithoutNotify(0);
            _scoutYearFilter.SetValueWithoutNotify(0);
            RefreshScoutScopeFilters();
        }

        private void BindScoutScopeFilterOptions(OwnerScoutScreenSnapshot screen)
        {
            if (ReferenceEquals(_scoutFilterSource, screen)) return;
            string previousTeam = _scoutTeamFilter.value == 0 ? string.Empty : _scoutTeamIds[_scoutTeamFilter.value - 1];
            int previousYear = _scoutYearFilter.value == 0 ? 0 : _scoutYears[_scoutYearFilter.value - 1];
            _scoutFilterSource = screen;
            var teams = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var years = new SortedSet<int>();
            foreach (OwnerScoutProductSnapshot product in screen.Products)
            {
                if (product.TargetFranchiseId.Length > 0) teams[product.TargetFranchiseId] = product.TargetFranchiseName;
                if (product.TargetYear.HasValue) years.Add(product.TargetYear.Value);
            }
            _scoutTeamIds.Clear();
            var teamLabels = new List<string> { "전체 구단" };
            var franchises = new List<KeyValuePair<string, string>>(teams);
            franchises.Sort((left, right) =>
            {
                int labelComparison = StringComparer.CurrentCulture.Compare(left.Value, right.Value);
                return labelComparison != 0
                    ? labelComparison
                    : StringComparer.Ordinal.Compare(left.Key, right.Key);
            });
            foreach (KeyValuePair<string, string> team in franchises)
            {
                _scoutTeamIds.Add(team.Key);
                teamLabels.Add(team.Value);
            }
            _scoutYears.Clear();
            _scoutYears.AddRange(years);
            _scoutYears.Reverse();
            var yearLabels = new List<string> { "전체 연도" };
            foreach (int year in _scoutYears) yearLabels.Add(year + "년");
            SetScoutFilterOptions(_scoutTeamFilter, teamLabels, _scoutTeamIds.IndexOf(previousTeam) + 1);
            SetScoutFilterOptions(_scoutYearFilter, yearLabels, _scoutYears.IndexOf(previousYear) + 1);
        }

        private static void SetScoutFilterOptions(Dropdown dropdown, List<string> labels, int selected)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(labels);
            dropdown.SetValueWithoutNotify(selected);
            dropdown.RefreshShownValue();
            dropdown.template.sizeDelta = new Vector2(0, Math.Min(7, labels.Count) * 28 + 8);
        }

        private void BindScoutScopes(OwnerScoutScreenSnapshot screen)
        {
            BindScoutScopeFilterOptions(screen);
            CollectScoutScopes(screen);
            _scoutMapPage = Mathf.Clamp(_scoutMapPage, 0, Math.Max(0, (_scoutScopes.Count - 1) / ScoutMapPageSize));
            OwnerScoutProductSnapshot selected = FindScoutProduct(_selectedScoutProductId);
            int start = _scoutMapPage * ScoutMapPageSize;
            for (int index = start; index < Math.Min(_scoutScopes.Count, start + ScoutMapPageSize); index++)
            {
                OwnerScoutProductSnapshot scope = _scoutScopes[index];
                CreateScoutReferencePin(_scoutList, "Scout_" + scope.ProductId, scope.Scope,
                    () => SelectScoutScope(scope), selected != null && scope.Scope == selected.Scope,
                    index - start, _scoutScopes.Count);
            }
            if (_scoutScopes.Count == 0)
                ScoutLabel(_scoutList, "NoScopeResults", "일치하는 파견 범위가 없습니다.\n검색어나 필터를 초기화해 주세요.", 12, 4, 28, 236, 80);
            BindScoutMapPagination(_scoutScopes.Count);
        }

        private void CollectScoutScopes(OwnerScoutScreenSnapshot screen)
        {
            _scoutScopes.Clear();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string teamId = _scoutTeamFilter.value == 0 ? string.Empty : _scoutTeamIds[_scoutTeamFilter.value - 1];
            int year = _scoutYearFilter.value == 0 ? 0 : _scoutYears[_scoutYearFilter.value - 1];
            foreach (OwnerScoutProductSnapshot product in screen.Products)
            {
                if (teamId.Length > 0 && product.TargetFranchiseId != teamId) continue;
                if (year != 0 && product.TargetYear != year) continue;
                if (!MatchesScoutQuery(product, _scoutScopeSearch.text)) continue;
                if (seen.Add(product.Scope)) _scoutScopes.Add(product);
            }
        }

        private static bool MatchesScoutQuery(OwnerScoutProductSnapshot product, string query)
        {
            string text = product.Scope + " " + product.Title + " " + product.TargetFranchiseName;
            // 입력 순서와 관계없이 모든 단어를 만족한다. 예: "KT 2025"와 "2025 KT".
            foreach (string term in query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                if (text.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0) return false;
            return true;
        }

        private void SelectScoutScope(OwnerScoutProductSnapshot scope)
        {
            OwnerScoutProductSnapshot current = FindScoutProduct(_selectedScoutProductId);
            OwnerScoutProductSnapshot next = scope;
            foreach (OwnerScoutProductSnapshot product in _snapshot.Scout.Products)
            {
                if (product.Scope != scope.Scope || current == null) continue;
                if (product.DrawCount == current.DrawCount && DescribeScoutPolicy(product) == DescribeScoutPolicy(current))
                { next = product; break; }
            }
            _selectedScoutProductId = next.ProductId;
            BindScout();
            FocusSelectedScoutScope();
        }

        private void FocusSelectedScoutScope()
        {
            OwnerScoutProductSnapshot selected = FindScoutProduct(_selectedScoutProductId);
            foreach (OwnerScoutProductSnapshot scope in _scoutScopes)
                if (selected != null && scope.Scope == selected.Scope)
                { FocusScoutControl(_scoutList, "Scout_" + scope.ProductId); return; }
        }

        private void RevealScoutScope(OwnerScoutProductSnapshot selected)
        {
            if (selected == null) return;
            BindScoutScopeFilterOptions(_snapshot.Scout);
            _scoutScopeSearch.SetTextWithoutNotify(string.Empty);
            _scoutTeamFilter.SetValueWithoutNotify(_scoutTeamIds.IndexOf(selected.TargetFranchiseId) + 1);
            _scoutYearFilter.SetValueWithoutNotify(selected.TargetYear.HasValue ? _scoutYears.IndexOf(selected.TargetYear.Value) + 1 : 0);
            CollectScoutScopes(_snapshot.Scout);
            int index = _scoutScopes.FindIndex(product => product.Scope == selected.Scope);
            _scoutMapPage = Math.Max(0, index) / ScoutMapPageSize;
        }

        private void ToggleScoutPolicyScope()
        {
            _hasScoutPolicyScopeFilter = !_hasScoutPolicyScopeFilter;
            _scoutPolicyScopeButton.transform.Find("Label").GetComponent<Text>().text =
                _hasScoutPolicyScopeFilter ? "선택 범위 ▾" : "전체 범위 ▾";
            _scoutPolicyPage = 0;
            RebuildScoutPolicyOptions();
        }
    }
}
