using System;
using System.Collections.Generic;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>보유 카드의 원 연도·원 구단을 교차 필터링하고 같은 드롭다운을 제공한다.</summary>
    internal sealed class OwnerCardFilters
    {
        private int _year;
        private string _team = string.Empty;

        /// <summary>검색 결과가 없을 때 연도와 구단 조건을 함께 해제한다.</summary>
        public void Reset() { _year = 0; _team = string.Empty; }

        public bool Matches(OwnerCollectionCardSnapshot card) =>
            (_year == 0 || card.OriginYear == _year) &&
            (string.IsNullOrEmpty(_team) || string.Equals(card.TeamDisplayName, _team, StringComparison.Ordinal));

        public void Build(Transform parent, IReadOnlyList<OwnerCollectionCardSnapshot> cards, Action changed)
        {
            var yearSet = new HashSet<int>();
            var teamSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (OwnerCollectionCardSnapshot card in cards)
            {
                yearSet.Add(card.OriginYear);
                if (!string.IsNullOrWhiteSpace(card.TeamDisplayName)) teamSet.Add(card.TeamDisplayName);
            }
            var years = new List<int>(yearSet);
            var teams = new List<string>(teamSet);
            years.Sort((a, b) => b.CompareTo(a));
            teams.Sort(StringComparer.CurrentCulture);
            if (!years.Contains(_year)) _year = 0;
            if (!teams.Contains(_team)) _team = string.Empty;
            var yearLabels = new List<string> { "전체 연도" };
            foreach (int year in years) yearLabels.Add(year + "년");
            var teamLabels = new List<string> { "전체 구단" };
            teamLabels.AddRange(teams);
            Dropdown yearDropdown = CreateDropdown(parent, "YearFilter", yearLabels, years.IndexOf(_year) + 1);
            Dropdown teamDropdown = CreateDropdown(parent, "TeamFilter", teamLabels, teams.IndexOf(_team) + 1);
            yearDropdown.onValueChanged.AddListener(index => { _year = index == 0 ? 0 : years[index - 1]; changed(); });
            teamDropdown.onValueChanged.AddListener(index => { _team = index == 0 ? string.Empty : teams[index - 1]; changed(); });
        }

        /// <summary>선수 카드 목록의 필터·정렬에 공통 드롭다운 모양을 적용한다.</summary>
        internal static Dropdown CreateDropdown(Transform parent, string name, List<string> options, int selected)
        {
            GameObject root = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            root.name = name;
            root.transform.SetParent(parent, false);
            root.AddComponent<CareerUiPreserveTextColor>();
            root.GetComponent<Image>().color = new Color32(228, 233, 237, 255);
            LayoutElement size = root.AddComponent<LayoutElement>();
            size.minWidth = 100;
            size.preferredWidth = name == "YearFilter" ? 130 : 180;
            size.flexibleWidth = 1;
            size.minHeight = 28;
            Dropdown dropdown = root.GetComponent<Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(selected);
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 12;
                text.color = new Color32(33, 45, 57, 255);
                text.alignment = TextAnchor.MiddleLeft;
                text.raycastTarget = false;
            }
            dropdown.captionText.rectTransform.offsetMin = new Vector2(10, 1);
            dropdown.captionText.rectTransform.offsetMax = new Vector2(-26, -1);
            root.transform.Find("Arrow").gameObject.SetActive(false);
            Text arrow = OwnerWorkspaceUiFactory.CreateText(root.transform, "DropdownArrow", "▾", 14,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Color32(43, 57, 73, 255));
            OwnerRuntimeUiFactory.SetAnchors(arrow.rectTransform, new Vector2(1, 0), Vector2.one,
                new Vector2(-24, 0), Vector2.zero);
            dropdown.template.GetComponent<Image>().color = new Color32(240, 243, 246, 255);
            dropdown.template.sizeDelta = new Vector2(0, Mathf.Min(7, options.Count) * 28 + 8);
            dropdown.itemText.transform.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 28);
            Toggle toggle = dropdown.itemText.GetComponentInParent<Toggle>(true);
            toggle.targetGraphic.color = new Color32(215, 225, 235, 255);
            toggle.graphic.color = new Color32(40, 82, 126, 255);
            return dropdown;
        }
    }
}
