using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Baseball.Editor.HistoricalDatabase
{
    public sealed partial class HistoricalDatabaseBrowserWindow
    {
        private ToolbarSearchField _teamSearch;
        private DropdownField _teamYearFilter;
        private ToolbarSearchField _awardSearch;
        private DropdownField _awardYearFilter;
        private DropdownField _awardTypeFilter;
        private DropdownField _awardPositionFilter;
        private DropdownField _awardFranchiseFilter;

        private void ConfigureTeamBrowser()
        {
            _teamSearch = Require<ToolbarSearchField>("team-search");
            _teamYearFilter = Require<DropdownField>("team-year-filter");
            BindTextColumn(_teamList.columns["year"], index => GetTeam(index)?.OriginYear.ToString());
            BindTextColumn(_teamList.columns["franchise"], index => GetTeam(index)?.FranchiseId);
            BindTextColumn(_teamList.columns["team"], index => GetTeam(index)?.TeamSeasonKey);
            BindTextColumn(_teamList.columns["pool"], index => GetTeam(index)?.AllNormalCardIds.Length.ToString());
            BindTextColumn(_teamList.columns["core"], index => GetTeam(index)?.Core25CardIds.Length.ToString());
            BindTextColumn(_teamList.columns["strength"], index => GetTeam(index)?.ReferenceStrength.ToString("0.000"));
            ConfigureTeamSort();
            _teamList.itemsSource = _visibleTeams;
            _teamList.selectionChanged += selection =>
            {
                HistoricalTeamSeason team = selection.OfType<HistoricalTeamSeason>().FirstOrDefault();
                if (team != null) SelectTeam(team);
            };
            _teamList.itemsChosen += selection =>
            {
                HistoricalTeamSeason team = selection.OfType<HistoricalTeamSeason>().FirstOrDefault();
                if (team != null) SelectTeam(team, true);
            };
            _teamList.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if (TryGetContextItemIndex(evt, _teamList, out int index))
                {
                    HistoricalTeamSeason contextTeam = GetTeam(index);
                    if (contextTeam != null)
                        SelectTeam(contextTeam);
                }
                evt.menu.AppendAction("팀 상세 열기", _ => SelectTeam(_selectedTeam, true), _ => _selectedTeam == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                evt.menu.AppendAction("TeamSeasonKey 복사", _ => CopyText(_selectedTeam.TeamSeasonKey, "TeamSeasonKey"), _ => _selectedTeam == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                evt.menu.AppendAction("JSON 경로 복사", _ => CopyText(_selectedTeam.SourcePath, "JSON 경로"), _ => _selectedTeam == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                evt.menu.AppendAction("Raw JSON 복사", _ => CopyEntityRawJson(_selectedTeam, "TeamSeason Raw JSON"), _ => _selectedTeam == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            }));
            _teamSearch.RegisterValueChangedCallback(_ => ApplyTeamFilters());
            _teamYearFilter.RegisterValueChangedCallback(_ => ApplyTeamFilters());
            ShowTeamEmptyState();
        }

        private void ConfigureTeamSort()
        {
            _teamList.sortingMode = ColumnSortingMode.Default;
            SetColumnComparison(_teamList.columns["year"], (left, right) => left.OriginYear.CompareTo(right.OriginYear), GetTeam);
            SetColumnComparison(_teamList.columns["franchise"], (left, right) => string.Compare(left.FranchiseId, right.FranchiseId, StringComparison.Ordinal), GetTeam);
            SetColumnComparison(_teamList.columns["team"], (left, right) => string.Compare(left.TeamSeasonKey, right.TeamSeasonKey, StringComparison.Ordinal), GetTeam);
            SetColumnComparison(_teamList.columns["pool"], (left, right) => left.AllNormalCardIds.Length.CompareTo(right.AllNormalCardIds.Length), GetTeam);
            SetColumnComparison(_teamList.columns["core"], (left, right) => left.Core25CardIds.Length.CompareTo(right.Core25CardIds.Length), GetTeam);
            SetColumnComparison(_teamList.columns["strength"], (left, right) => left.ReferenceStrength.CompareTo(right.ReferenceStrength), GetTeam);
        }

        private void ApplyTeamFilters()
        {
            if (_data == null)
                return;
            string query = _teamSearch.value?.Trim() ?? string.Empty;
            int? year = ParseChoiceInt(_teamYearFilter.value);
            _visibleTeams.Clear();
            for (int index = 0; index < _data.Teams.Count; index++)
            {
                HistoricalTeamSeason team = _data.Teams[index];
                if (year.HasValue && team.OriginYear != year.Value)
                    continue;
                if (!Contains(team.FranchiseId, query) && !Contains(team.TeamSeasonKey, query))
                    continue;
                _visibleTeams.Add(team);
            }
            _visibleTeams.Sort((left, right) =>
            {
                int byYear = left.OriginYear.CompareTo(right.OriginYear);
                return byYear != 0 ? byYear : string.CompareOrdinal(left.FranchiseId, right.FranchiseId);
            });
            _teamList.itemsSource = _visibleTeams;
            _teamList.Rebuild();
            SetLabel("team-result-count", $"{_visibleTeams.Count:N0}팀");
        }

        private void SelectTeam(HistoricalTeamSeason team, bool switchTab = false)
        {
            if (team == null)
                return;
            _selectedTeam = team;
            if (switchTab)
                ShowTab(BrowserTab.Teams);
            int index = _visibleTeams.IndexOf(team);
            if (index >= 0)
            {
                _teamList.SetSelectionWithoutNotify(new[] { index });
                _teamList.ScrollToItem(index);
            }
            BuildTeamDetail(team);
            _selectionLabel.text = $"{team.OriginYear} {team.FranchiseId} · {team.TeamSeasonKey}";
        }

        private void ShowTeamEmptyState()
        {
            _teamDetailContent.Clear();
            AddAbsent(_teamDetailContent, "왼쪽 목록에서 팀 시즌을 선택하세요.");
        }

        private void BuildTeamDetail(HistoricalTeamSeason team)
        {
            _teamDetailContent.Clear();
            var header = new Label($"{team.OriginYear} · {team.FranchiseId}");
            header.AddToClassList("detail-title");
            _teamDetailContent.Add(header);
            var id = new Label(team.TeamSeasonKey);
            id.AddToClassList("id-label");
            _teamDetailContent.Add(id);

            VisualElement overview = CreateTeamSection("팀 시즌");
            AddKeyValue(overview, "전체 선수", team.AllNormalCardIds.Length.ToString());
            AddKeyValue(overview, "대표 25인", team.Core25CardIds.Length.ToString());
            AddKeyValue(overview, "평균 환산 능력", team.ReferenceStrength.ToString("0.000") + " (파생)");

            HistoricalTeamValidationResult validation = _validationService.ValidateTeam(_data, team);
            bool isOriginalSource = string.Equals(
                _data.Manifest.SourceManifest?.NameDataPolicy,
                "editor-original-source-v2",
                StringComparison.Ordinal);
            VisualElement validationSection = CreateTeamSection(
                isOriginalSource
                    ? "원본 팀 대표 로스터"
                    : validation.IsValid ? "로스터 검증 · 통과" : "로스터 검증 · 오류");
            AddValidationMetric(validationSection, "전체", validation.TotalCount, 25);
            AddValidationMetric(validationSection, "타자", validation.HitterCount, 14);
            AddValidationMetric(validationSection, "투수", validation.PitcherCount, 11);
            AddValidationMetric(validationSection, "주전 타자", validation.StartingHitterCount, 9);
            AddValidationMetric(validationSection, "벤치", validation.BenchHitterCount, 5);
            AddValidationMetric(validationSection, "선발 투수", validation.StartingPitcherCount, 5);
            AddValidationMetric(validationSection, "불펜", validation.BullpenPitcherCount, 4);
            AddValidationMetric(validationSection, "셋업", validation.SetupPitcherCount, 1);
            AddValidationMetric(validationSection, "마무리", validation.CloserPitcherCount, 1);
            if (!isOriginalSource)
                AddValidationMetric(validationSection, "외국인", validation.ForeignPlayerCount, 3, isMaximum: true);
            AddValidationMetric(validationSection, "중복 선수", validation.DuplicatePersonCount, 0);
            foreach (HistoricalValidationIssue issue in validation.Issues.Where(issue => issue.Severity != HistoricalValidationSeverity.Pass))
                AddAbsent(validationSection, $"{FormatValidationSeverity(issue.Severity)}: {issue.Message}");

            IReadOnlyList<HistoricalPlayerRow> roster = _viewModel.FindCoreRoster(team.TeamSeasonKey);
            AddRosterSection("야수 · 주전 9명", roster.Where(row => row.RosterRole.StartsWith("StartingHitter", StringComparison.Ordinal)));
            AddRosterSection("야수 · 벤치 5명", roster.Where(row => row.RosterRole.StartsWith("BenchHitter", StringComparison.Ordinal)));
            AddRosterSection("투수 · 선발 5명", roster.Where(row => row.RosterRole.StartsWith("StartingPitcher", StringComparison.Ordinal)));
            AddRosterSection("투수 · 불펜 4명", roster.Where(row => row.RosterRole.StartsWith("Bullpen", StringComparison.Ordinal)));
            AddRosterSection("투수 · 셋업", roster.Where(row => string.Equals(row.RosterRole, "Setup", StringComparison.Ordinal)));
            AddRosterSection("투수 · 마무리", roster.Where(row => string.Equals(row.RosterRole, "Closer", StringComparison.Ordinal)));
            AddRosterSelectionTrace(team);
        }

        private void AddRosterSelectionTrace(HistoricalTeamSeason team)
        {
            HistoricalRosterSelectionTrace trace = team.RosterSelectionTrace;
            if (trace == null)
                return;

            VisualElement section = CreateTeamSection("대표 로스터 선택 Trace");
            AddKeyValue(section, "Builder", trace.RosterBuilderVersion);
            for (int index = 0; index < trace.StartingSlots.Count; index++)
            {
                HistoricalStartingSlotTrace slot = trace.StartingSlots[index];
                var detail = new Foldout
                {
                    text = $"{FormatPosition(slot.Slot)} · {FormatTracePlayer(slot.SelectedPlayerSeasonId)} · {slot.SelectionScore:0.000}",
                    value = false
                };
                AddKeyValue(detail, "선택 근거", slot.Reason);
                AddKeyValue(detail, "Fallback", slot.IsFallback ? "예" : "아니오");
                AddKeyValue(detail, "후보 수", slot.Candidates.Count.ToString());
                for (int candidateIndex = 0; candidateIndex < slot.Candidates.Count; candidateIndex++)
                {
                    HistoricalRosterCandidateTrace candidate = slot.Candidates[candidateIndex];
                    AddKeyValue(
                        detail,
                        FormatTracePlayer(candidate.PlayerSeasonId),
                        $"{FormatPosition(candidate.NaturalPosition)} · {candidate.Score:0.000}");
                }
                section.Add(detail);
            }

            if (trace.DesignatedHitter != null)
            {
                AddKeyValue(
                    section,
                    "DH",
                    $"{FormatTracePlayer(trace.DesignatedHitter.PlayerSeasonId)} · {trace.DesignatedHitter.SelectionScore:0.000}");
            }
            AddKeyValue(section, "Bench", string.Join(", ", trace.Bench.Select(candidate => FormatTracePlayer(candidate.PlayerSeasonId))));
            for (int index = 0; index < trace.PitchingStaff.Count; index++)
            {
                HistoricalPitchingStaffSelectionTrace staff = trace.PitchingStaff[index];
                AddKeyValue(
                    section,
                    staff.AssignedRole,
                    $"{string.Join(", ", staff.SelectedPlayerSeasonIds.Select(FormatTracePlayer))} · fallback {staff.FallbackCount}");
            }
            for (int index = 0; index < trace.ValidationWarnings.Count; index++)
            {
                HistoricalDerivationWarningTrace warning = trace.ValidationWarnings[index];
                AddAbsent(section, $"{warning.Code}: {warning.Message}");
            }
        }

        private string FormatTracePlayer(string playerSeasonId)
        {
            return _data.PlayersBySeasonId.TryGetValue(playerSeasonId ?? string.Empty, out HistoricalPlayerRow row)
                ? row.Name
                : playerSeasonId ?? string.Empty;
        }

        private VisualElement CreateTeamSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("detail-section");
            var label = new Label(title);
            label.AddToClassList("detail-section-title");
            section.Add(label);
            _teamDetailContent.Add(section);
            return section;
        }

        private static void AddValidationMetric(VisualElement parent, string name, int actual, int expected, bool isMaximum = false)
        {
            bool passed = isMaximum ? actual <= expected : actual == expected;
            string expectation = isMaximum ? $"≤ {expected}" : expected.ToString();
            var label = new Label($"{(passed ? "✓" : "✕")}  {name,-20}  {actual} / {expectation}");
            label.AddToClassList(passed ? "severity-pass" : "severity-error");
            parent.Add(label);
        }

        private static string FormatValidationSeverity(HistoricalValidationSeverity severity)
        {
            return severity == HistoricalValidationSeverity.Error
                ? "오류"
                : severity == HistoricalValidationSeverity.Warning
                    ? "경고"
                    : "통과";
        }

        private void AddRosterSection(string title, IEnumerable<HistoricalPlayerRow> rows)
        {
            List<HistoricalPlayerRow> roster = rows.OrderBy(row => row.RosterRole, StringComparer.Ordinal).ThenBy(row => row.Name, StringComparer.Ordinal).ToList();
            VisualElement section = CreateTeamSection($"{title} · {roster.Count}");
            for (int index = 0; index < roster.Count; index++)
            {
                HistoricalPlayerRow player = roster[index];
                var rosterRow = new VisualElement();
                rosterRow.AddToClassList("timeline-row");
                var role = new Label(FormatRosterRole(player.RosterRole)) { tooltip = player.RosterRole };
                role.AddToClassList("timeline-team");
                var name = new Button(() => SelectPlayer(player, true)) { text = player.Name };
                name.AddToClassList("link-button");
                name.style.flexGrow = 1f;
                var summary = new Label($"{FormatPosition(player.Position)}{FormatPlayerRoleSuffix(player)} · 비용 {player.Cost}");
                summary.AddToClassList("timeline-summary");
                rosterRow.Add(role);
                rosterRow.Add(name);
                rosterRow.Add(summary);
                section.Add(rosterRow);
            }
        }

        private HistoricalTeamSeason GetTeam(int index)
        {
            return index >= 0 && index < _visibleTeams.Count ? _visibleTeams[index] : null;
        }

        private void ConfigureAwardBrowser()
        {
            _awardSearch = Require<ToolbarSearchField>("award-search");
            _awardYearFilter = Require<DropdownField>("award-year-filter");
            _awardTypeFilter = Require<DropdownField>("award-type-filter");
            _awardPositionFilter = Require<DropdownField>("award-position-filter");
            _awardFranchiseFilter = Require<DropdownField>("award-franchise-filter");
            ConfigureLocalizedDropdown(_awardTypeFilter, FormatAwardType);
            ConfigureLocalizedDropdown(_awardPositionFilter, FormatPosition);
            BindTextColumn(_awardList.columns["year"], index => GetAward(index)?.Award.SeasonYear.ToString());
            BindTextColumn(_awardList.columns["award"], index => FormatAwardType(GetAward(index)?.Award.AwardType));
            BindTextColumn(_awardList.columns["player"], index => GetAward(index)?.Player?.Name ?? GetAward(index)?.Award.PlayerSeasonId);
            BindTextColumn(_awardList.columns["position"], index => FormatPosition(GetAward(index)?.Award.Position));
            BindTextColumn(_awardList.columns["team"], index => GetAward(index)?.Player?.OriginFranchiseId ?? "—");
            BindTextColumn(_awardList.columns["source"], index =>
                string.IsNullOrWhiteSpace(GetAward(index)?.Award.Source)
                    ? Path.GetFileName(GetAward(index)?.Award.SourcePath)
                    : GetAward(index)?.Award.Source);
            ConfigureAwardSort();
            _awardList.itemsSource = _visibleAwards;
            _awardList.itemsChosen += OpenAwardPlayer;
            _awardList.selectionChanged += selection =>
            {
                AwardViewRow row = selection.OfType<AwardViewRow>().FirstOrDefault();
                if (row != null) _selectionLabel.text = $"{row.Award.SeasonYear} {row.Award.AwardType} · {row.Player?.Name ?? row.Award.PlayerSeasonId}";
            };
            _awardList.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if (TryGetContextItemIndex(evt, _awardList, out int index))
                    _awardList.SetSelectionWithoutNotify(new[] { index });
                AwardViewRow row = _awardList.selectedItem as AwardViewRow;
                evt.menu.AppendAction("선수 상세 열기", _ => SelectPlayer(row.Player, true), _ => row?.Player == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                evt.menu.AppendAction("PlayerSeasonId 복사", _ => CopyText(row.Award.PlayerSeasonId, "PlayerSeasonId"), _ => row == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                evt.menu.AppendAction("Raw JSON 복사", _ => CopyEntityRawJson(row?.Award, "Award Raw JSON"), _ => row == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            }));
            _awardSearch.RegisterValueChangedCallback(_ => ApplyAwardFilters());
            _awardYearFilter.RegisterValueChangedCallback(_ => ApplyAwardFilters());
            _awardTypeFilter.RegisterValueChangedCallback(_ => ApplyAwardFilters());
            _awardPositionFilter.RegisterValueChangedCallback(_ => ApplyAwardFilters());
            _awardFranchiseFilter.RegisterValueChangedCallback(_ => ApplyAwardFilters());
        }

        private void ConfigureAwardSort()
        {
            _awardList.sortingMode = ColumnSortingMode.Default;
            SetColumnComparison(_awardList.columns["year"], (left, right) => left.Award.SeasonYear.CompareTo(right.Award.SeasonYear), GetAward);
            SetColumnComparison(_awardList.columns["award"], (left, right) => string.Compare(left.Award.AwardType, right.Award.AwardType, StringComparison.Ordinal), GetAward);
            SetColumnComparison(_awardList.columns["player"], (left, right) => string.Compare(left.Player?.Name, right.Player?.Name, StringComparison.Ordinal), GetAward);
            SetColumnComparison(_awardList.columns["position"], (left, right) => string.Compare(left.Award.Position, right.Award.Position, StringComparison.Ordinal), GetAward);
            SetColumnComparison(_awardList.columns["team"], (left, right) => string.Compare(left.Player?.OriginFranchiseId, right.Player?.OriginFranchiseId, StringComparison.Ordinal), GetAward);
        }

        private void ApplyAwardFilters()
        {
            if (_data == null)
                return;
            string search = _awardSearch.value?.Trim() ?? string.Empty;
            int? year = ParseChoiceInt(_awardYearFilter.value);
            string type = ChoiceValue(_awardTypeFilter.value);
            string position = ChoiceValue(_awardPositionFilter.value);
            string franchise = ChoiceValue(_awardFranchiseFilter.value);
            _visibleAwards.Clear();
            for (int index = 0; index < _data.Awards.Count; index++)
            {
                HistoricalAwardRecord award = _data.Awards[index];
                _data.PlayersBySeasonId.TryGetValue(award.PlayerSeasonId, out HistoricalPlayerRow player);
                if (year.HasValue && award.SeasonYear != year.Value) continue;
                if (!MatchesChoice(award.AwardType, type) || !MatchesChoice(award.Position, position)) continue;
                if (!MatchesChoice(player?.OriginFranchiseId, franchise)) continue;
                if (!Contains(award.AwardType, search) && !Contains(award.PlayerSeasonId, search) && !Contains(player?.Name, search) && !Contains(player?.OriginFranchiseId, search)) continue;
                _visibleAwards.Add(new AwardViewRow(award, player));
            }
            _visibleAwards.Sort((left, right) =>
            {
                int yearOrder = right.Award.SeasonYear.CompareTo(left.Award.SeasonYear);
                return yearOrder != 0 ? yearOrder : string.CompareOrdinal(left.Award.AwardType, right.Award.AwardType);
            });
            _awardList.itemsSource = _visibleAwards;
            _awardList.Rebuild();
            SetLabel("award-result-count", $"{_visibleAwards.Count:N0}건");
        }

        private void OpenAwardPlayer(IEnumerable<object> selection)
        {
            AwardViewRow row = selection.OfType<AwardViewRow>().FirstOrDefault();
            if (row?.Player != null)
                SelectPlayer(row.Player, true);
        }

        private AwardViewRow GetAward(int index)
        {
            return index >= 0 && index < _visibleAwards.Count ? _visibleAwards[index] : null;
        }

        private void CopyEntityRawJson(object entity, string label)
        {
            if (entity == null)
                return;
            if (_viewModel.TryGetRawJson(entity, out string rawJson, out string error))
            {
                CopyText(rawJson, label);
                return;
            }
            _statusLabel.text = error;
        }

        private static void SetColumnComparison<T>(Column column, Comparison<T> comparison, Func<int, T> itemProvider) where T : class
        {
            column.sortable = true;
            column.comparison = (leftIndex, rightIndex) =>
            {
                T left = itemProvider(leftIndex);
                T right = itemProvider(rightIndex);
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return 1;
                if (right == null) return -1;
                return comparison(left, right);
            };
        }

        private static bool MatchesChoice(string value, string choice)
        {
            return string.IsNullOrEmpty(choice) || string.Equals(value, choice, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string value, string query)
        {
            return string.IsNullOrWhiteSpace(query) || (!string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
