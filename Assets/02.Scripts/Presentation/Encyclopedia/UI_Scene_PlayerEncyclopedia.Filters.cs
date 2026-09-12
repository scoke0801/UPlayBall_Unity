using System;
using System.Collections.Generic;
using Baseball.Presentation.Owner;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Encyclopedia
{
    public sealed partial class UI_Scene_PlayerEncyclopedia
    {
        private void BuildFilters()
        {
            ReleaseSearchInput();
            OwnerRuntimeUiFactory.ClearChildren(_filterRoot);
            RectTransform first = Row(_filterRoot, "PrimaryFilters", 0, 32);
            Image surface = OwnerRuntimeUiFactory.CreateImage("NameSearch", first, CareerUiTheme.ReferencePanel);
            surface.raycastTarget = true;
            var size = surface.gameObject.AddComponent<LayoutElement>();
            size.minWidth = 120; size.flexibleWidth = 1;
            _search = surface.gameObject.AddComponent<InputField>();
            Text value = OwnerWorkspaceUiFactory.CreateText(surface.transform, "Value", string.Empty, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
            OwnerRuntimeUiFactory.Stretch(value.rectTransform, new Vector2(8, 2), new Vector2(-8, -2));
            Text placeholder = OwnerWorkspaceUiFactory.CreateText(surface.transform, "Placeholder", "선수 이름 검색", 12, FontStyle.Italic, TextAnchor.MiddleLeft);
            OwnerRuntimeUiFactory.Stretch(placeholder.rectTransform, new Vector2(8, 2), new Vector2(-8, -2));
            _search.textComponent = value; _search.placeholder = placeholder; _search.targetGraphic = surface;
            OwnerDashboardStyle.SetDataInput(_search);
            _search.SetTextWithoutNotify(_filter.Search);
            _lastImeComposition = string.Empty;
            _search.onValueChanged.AddListener(text => { _filter.Search = text; Refresh(); });
            StringFilter(first, "Franchise", "전체 구단", entry => entry.FranchiseId, entry => entry.FranchiseDisplayName,
                _filter.FranchiseId, valueId => _filter.FranchiseId = valueId);
            StringFilter(first, "Year", "전체 연도", entry => entry.OriginYear.ToString(), entry => entry.OriginYear + "년",
                _filter.OriginYear == 0 ? "" : _filter.OriginYear.ToString(), valueId => _filter.OriginYear = Parse(valueId));
            StringFilter(first, "Position", "전체 포지션", entry => entry.Position, entry => entry.Position,
                _filter.Position, valueId => _filter.Position = valueId);
            ListFilter(first, "Cost", new List<string> { "전체 Cost", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" }, _filter.Cost, valueId => _filter.Cost = valueId);
            RectTransform second = Row(_filterRoot, "SecondaryFilters", 38, 32);
            StringFilter(second, "Edition", "전체 Edition", entry => entry.EditionId, entry => entry.EditionDisplayName,
                _filter.EditionId, valueId => { _filter.EditionId = valueId; if (valueId.Length > 0) _tab = 1; });
            if (!_snapshot.IsReadOnly)
                ListFilter(second, "CollectionState", new List<string> { "전체 수집 상태", "현재 보유", "획득 · 현재 미보유", "미획득", "위시", "위시 아님" }, _filter.CollectionState, valueId => _filter.CollectionState = valueId);
            var sorts = new List<string> { "기본 정렬", "이름", "연도 ↑", "연도 ↓", "Cost ↑", "Cost ↓", "포지션", "Edition" };
            if (!_snapshot.IsReadOnly) sorts.AddRange(new[] { "보유 우선", "미획득 우선", "위시 우선", "최근 위시 등록", "오래된 위시 등록" });
            ListFilter(second, "Sort", sorts, Math.Min(_filter.Sort, sorts.Count - 1), valueId => _filter.Sort = valueId);
            Button advanced = OwnerWorkspaceUiFactory.CreateButton(second, "Advanced", _advanced ? "세부필터 닫기" : "세부필터", () => { _advanced = !_advanced; BuildFilters(); });
            OwnerUiButtonSkin.SetSelected(advanced, _advanced);
            OwnerWorkspaceUiFactory.CreateButton(second, "ResetFilters", "초기화", () => { _filter = new EncyclopediaScreenFilter { Sort = _wishlistOnly ? 11 : 0 }; BuildFilters(); Refresh(); });
            CompactButtons(_filterRoot);
            if (!_advanced) return;
            RectTransform third = Row(_filterRoot, "AdvancedFilters", 76, 32);
            StringFilter(third, "Decade", "전체 연대", entry => (entry.OriginYear / 10 * 10).ToString(), entry => entry.OriginYear / 10 * 10 + "년대",
                _filter.Decade == 0 ? "" : _filter.Decade.ToString(), valueId => _filter.Decade = Parse(valueId));
            ListFilter(third, "PlayerType", new List<string> { "타자 / 투수", "타자", "투수" }, _filter.PlayerType, valueId => _filter.PlayerType = valueId);
            StringFilter(third, "PitcherRole", "투수 역할", entry => entry.PitcherRole, entry => entry.PitcherRole,
                _filter.PitcherRole, valueId => _filter.PitcherRole = valueId);
            StringFilter(third, "Bats", "타격 손", entry => entry.Bats, entry => entry.Bats,
                _filter.Bats, valueId => _filter.Bats = valueId);
            StringFilter(third, "Throws", "투구 손", entry => entry.Throws, entry => entry.Throws,
                _filter.Throws, valueId => _filter.Throws = valueId);
        }

        private void StringFilter(RectTransform parent, string name, string all, Func<EncyclopediaScreenEntry, string> key,
            Func<EncyclopediaScreenEntry, string> label, string selected, Action<string> changed)
        {
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in _snapshot.Cards) if (!string.IsNullOrEmpty(key(entry))) values[key(entry)] = label(entry);
            foreach (var entry in _snapshot.Seasons) if (!string.IsNullOrEmpty(key(entry))) values[key(entry)] = label(entry);
            var ids = new List<string> { string.Empty };
            var labels = new List<string> { all };
            foreach (var value in values) { ids.Add(value.Key); labels.Add(value.Value); }
            int index = Math.Max(0, ids.IndexOf(selected));
            ListFilter(parent, name, labels, index, i => changed(ids[i]));
        }

        private void ListFilter(RectTransform parent, string name, List<string> labels, int selected, Action<int> changed)
        {
            Dropdown dropdown = OwnerCardFilters.CreateDropdown(parent, name, labels, selected);
            OwnerDashboardStyle.SetDataDropdown(dropdown);
            LayoutElement layout = dropdown.GetComponent<LayoutElement>();
            layout.minWidth = 74; layout.preferredWidth = 120;
            dropdown.onValueChanged.AddListener(index => { changed(index); Refresh(); });
        }

        private static int Parse(string value) => int.TryParse(value, out int result) ? result : 0;
    }
}
