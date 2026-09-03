using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Baseball.Editor.HistoricalDatabase
{
    public sealed partial class HistoricalDatabaseBrowserWindow
    {
        private const string AnyAwardChoice = "수상 있음";
        private const string NoAwardChoice = "수상 없음";

        private ToolbarSearchField _playerSearch;
        private DropdownField _playerYearFilter;
        private DropdownField _playerFranchiseFilter;
        private DropdownField _playerTeamFilter;
        private DropdownField _playerPositionFilter;
        private DropdownField _playerRoleFilter;
        private DropdownField _playerTypeFilter;
        private DropdownField _playerRegistrationFilter;
        private DropdownField _playerAwardFilter;
        private DropdownField _playerAbilityFilter;
        private IntegerField _playerCostMin;
        private IntegerField _playerCostMax;
        private IntegerField _playerAbilityMin;
        private IntegerField _playerAbilityMax;
        private readonly Dictionary<Column, HistoricalPlayerSortField> _playerSortFields = new Dictionary<Column, HistoricalPlayerSortField>();

        private void ConfigurePlayerBrowser()
        {
            _playerSearch = Require<ToolbarSearchField>("player-search");
            _playerYearFilter = Require<DropdownField>("player-year-filter");
            _playerFranchiseFilter = Require<DropdownField>("player-franchise-filter");
            _playerTeamFilter = Require<DropdownField>("player-team-filter");
            _playerPositionFilter = Require<DropdownField>("player-position-filter");
            _playerRoleFilter = Require<DropdownField>("player-role-filter");
            _playerTypeFilter = Require<DropdownField>("player-type-filter");
            _playerRegistrationFilter = Require<DropdownField>("player-registration-filter");
            _playerAwardFilter = Require<DropdownField>("player-award-filter");
            _playerAbilityFilter = Require<DropdownField>("player-ability-filter");
            _playerCostMin = Require<IntegerField>("player-cost-min");
            _playerCostMax = Require<IntegerField>("player-cost-max");
            _playerAbilityMin = Require<IntegerField>("player-ability-min");
            _playerAbilityMax = Require<IntegerField>("player-ability-max");
            ConfigureLocalizedDropdown(_playerPositionFilter, FormatPosition);
            ConfigureLocalizedDropdown(_playerRoleFilter, FormatPitcherRole);
            ConfigureLocalizedDropdown(_playerTypeFilter, FormatPlayerType);
            ConfigureLocalizedDropdown(_playerRegistrationFilter, FormatRegistrationType);
            ConfigureLocalizedDropdown(_playerAwardFilter, FormatAwardType);
            ConfigureLocalizedDropdown(_playerAbilityFilter, FormatAbilityName);

            BindTextColumn(_playerList.columns["player"], index => GetPlayer(index)?.Name);
            BindTextColumn(_playerList.columns["year"], index => GetPlayer(index)?.OriginYear.ToString());
            BindTextColumn(_playerList.columns["team"], index => GetPlayer(index)?.OriginFranchiseId);
            BindTextColumn(_playerList.columns["position"], index => FormatPosition(GetPlayer(index)?.Position));
            BindTextColumn(_playerList.columns["role"], index => FormatPlayerRole(GetPlayer(index)));
            BindTextColumn(_playerList.columns["cost"], index => GetPlayer(index)?.Cost.ToString());
            BindTextColumn(_playerList.columns["primary"], index => FormatPrimaryAbility(GetPlayer(index)));
            BindTextColumn(_playerList.columns["summary"], index => FormatSeasonSummary(GetPlayer(index)));
            BindTextColumn(_playerList.columns["awards"], index => GetPlayer(index)?.AwardCount.ToString());
            _playerList.columns["primary"].sortable = false;
            _playerList.columns["summary"].sortable = false;
            AddOptionalPlayerColumns();

            _playerList.itemsSource = _visiblePlayers;
            _playerList.selectionChanged += OnPlayerSelectionChanged;
            _playerList.itemsChosen += OnPlayerItemsChosen;
            _playerList.columnSortingChanged += OnPlayerSortingChanged;
            _playerList.AddManipulator(new ContextualMenuManipulator(BuildPlayerContextMenu));

            _playerSearch.RegisterValueChangedCallback(_ => SchedulePlayerFilter());
            RegisterFilter(_playerYearFilter);
            RegisterFilter(_playerFranchiseFilter);
            RegisterFilter(_playerTeamFilter);
            RegisterFilter(_playerPositionFilter);
            RegisterFilter(_playerRoleFilter);
            RegisterFilter(_playerTypeFilter);
            RegisterFilter(_playerRegistrationFilter);
            RegisterFilter(_playerAwardFilter);
            RegisterFilter(_playerAbilityFilter);
            _playerCostMin.RegisterValueChangedCallback(_ => ApplyPlayerFilters());
            _playerCostMax.RegisterValueChangedCallback(_ => ApplyPlayerFilters());
            _playerAbilityMin.RegisterValueChangedCallback(_ => ApplyPlayerFilters());
            _playerAbilityMax.RegisterValueChangedCallback(_ => ApplyPlayerFilters());
            Require<Button>("clear-player-filter-button").clicked += ClearPlayerFilters;
            Require<ToolbarButton>("player-details-mode-button").clicked += () => SetPlayerRawMode(false);
            Require<ToolbarButton>("player-raw-mode-button").clicked += () => SetPlayerRawMode(true);
            Require<ToolbarButton>("copy-player-button").clicked += CopySelectedPlayerSummary;
            Require<ToolbarButton>("pin-compare-button").clicked += ToggleComparePlayer;
            Require<ToolbarButton>("player-columns-button").clicked += ShowPlayerColumnMenu;
            ShowPlayerEmptyState();
        }

        private void RegisterFilter(DropdownField field)
        {
            field.RegisterValueChangedCallback(_ => ApplyPlayerFilters());
        }

        private void AddOptionalPlayerColumns()
        {
            AddPlayerColumn("contact", "컨택", 72, HistoricalPlayerSortField.Contact, row => FormatAbility(row, 0, false));
            AddPlayerColumn("power", "장타력", 65, HistoricalPlayerSortField.Power, row => FormatAbility(row, 1, false));
            AddPlayerColumn("speed", "주력", 65, HistoricalPlayerSortField.Speed, row => FormatAbility(row, 2, false));
            AddPlayerColumn("arm", "송구", 58, HistoricalPlayerSortField.Arm, row => FormatAbility(row, 3, false));
            AddPlayerColumn("defense", "수비", 72, HistoricalPlayerSortField.Defense, row => FormatAbility(row, 4, false));
            AddPlayerColumn("batter-mental", "타자 멘탈", 82, HistoricalPlayerSortField.BatterMental, row => FormatAbility(row, 5, false));
            AddPlayerColumn("stamina", "체력", 72, HistoricalPlayerSortField.Stamina, row => FormatAbility(row, 6, true));
            AddPlayerColumn("velocity", "구속", 72, HistoricalPlayerSortField.Velocity, row => FormatAbility(row, 7, true));
            AddPlayerColumn("stuff", "구위", 62, HistoricalPlayerSortField.Stuff, row => FormatAbility(row, 8, true));
            AddPlayerColumn("breaking", "변화구", 78, HistoricalPlayerSortField.Breaking, row => FormatAbility(row, 9, true));
            AddPlayerColumn("control", "제구", 72, HistoricalPlayerSortField.Control, row => FormatAbility(row, 10, true));
            AddPlayerColumn("pitcher-mental", "투수 멘탈", 82, HistoricalPlayerSortField.PitcherMental, row => FormatAbility(row, 11, true));
            AddPlayerColumn("pa", "타석", 58, HistoricalPlayerSortField.PlateAppearances, row => row.IsHitter ? row.Record?.PlateAppearances.ToString() ?? "—" : "—");
            AddPlayerColumn("hits", "안타", 52, HistoricalPlayerSortField.Hits, row => row.IsHitter ? row.Record?.Hits.ToString() ?? "—" : "—");
            AddPlayerColumn("hr", "홈런", 52, HistoricalPlayerSortField.HomeRuns, row => row.IsHitter ? row.Record?.HomeRuns.ToString() ?? "—" : "—");
            AddPlayerColumn("walks", "볼넷", 52, HistoricalPlayerSortField.Walks, row => row.IsHitter ? row.Record?.Walks.ToString() ?? "—" : "—");
            AddPlayerColumn("strikeouts", "삼진", 52, HistoricalPlayerSortField.Strikeouts, row => row.IsHitter ? row.Record?.Strikeouts.ToString() ?? "—" : "—");
            AddPlayerColumn("average", "타율", 62, HistoricalPlayerSortField.BattingAverage, row => FormatRate(row.BattingAverage));
            AddPlayerColumn("on-base", "출루율", 66, HistoricalPlayerSortField.OnBasePercentage, row => FormatRate(row.OnBasePercentage));
            AddPlayerColumn("slugging", "장타율", 66, HistoricalPlayerSortField.SluggingPercentage, row => FormatRate(row.SluggingPercentage));
            AddPlayerColumn("ops", "출루율+장타율", 100, HistoricalPlayerSortField.OnBasePlusSlugging, row => FormatRate(row.OnBasePlusSlugging));
            AddPlayerColumn("hpa", "타석당 안타", 82, HistoricalPlayerSortField.HitsPerPlateAppearance, row => FormatRate(row.HitsPerPlateAppearance));
            AddPlayerColumn("innings", "이닝", 58, HistoricalPlayerSortField.PitchingOuts, row => row.IsPitcher && row.Record != null ? FormatInnings(row.Record.PitchingOuts) : "—");
            AddPlayerColumn("earned-runs", "자책점", 58, HistoricalPlayerSortField.EarnedRuns, row => row.IsPitcher ? row.Record?.EarnedRuns.ToString() ?? "—" : "—");
            AddPlayerColumn("pitching-strikeouts", "투수 삼진", 72, HistoricalPlayerSortField.PitchingStrikeouts, row => row.IsPitcher ? row.Record?.PitchingStrikeouts.ToString() ?? "—" : "—");
            AddPlayerColumn("era", "평균자책점", 82, HistoricalPlayerSortField.EarnedRunAverage, row => FormatDecimal(row.EarnedRunAverage, "0.00"));
            AddPlayerColumn("whip", "이닝당 출루허용", 105, HistoricalPlayerSortField.WalksAndHitsPerInningPitched, row => FormatDecimal(row.WalksAndHitsPerInningPitched, "0.00"));
            AddPlayerColumn("k9", "9이닝당 삼진", 95, HistoricalPlayerSortField.StrikeoutsPerNine, row => FormatDecimal(row.StrikeoutsPerNine, "0.0"));
        }

        private void AddPlayerColumn(
            string name,
            string title,
            float width,
            HistoricalPlayerSortField sortField,
            Func<HistoricalPlayerRow, string> formatter)
        {
            var column = new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = Math.Min(width, 50f),
                optional = true,
                visible = false,
                sortable = true
            };
            BindTextColumn(column, index =>
            {
                HistoricalPlayerRow row = GetPlayer(index);
                return row == null ? string.Empty : formatter(row);
            });
            _playerSortFields[column] = sortField;
            _playerList.columns.Add(column);
        }

        private void ShowPlayerColumnMenu()
        {
            var menu = new GenericMenu();
            foreach (Column column in _playerList.columns)
            {
                if (!column.optional)
                    continue;
                Column captured = column;
                menu.AddItem(
                    new GUIContent(captured.title),
                    captured.visible,
                    () =>
                    {
                        captured.visible = !captured.visible;
                        _playerList.Rebuild();
                    });
            }
            menu.ShowAsContext();
        }

        private void PopulateFilterChoices()
        {
            SetDropdownChoices(_playerYearFilter, _data.PlayerRows.Select(row => row.OriginYear.ToString()));
            SetDropdownChoices(_playerFranchiseFilter, _data.PlayerRows.Select(row => row.OriginFranchiseId));
            SetDropdownChoices(_playerTeamFilter, _data.PlayerRows.Select(row => row.OriginTeamSeasonKey));
            SetDropdownChoices(_playerPositionFilter, _data.PlayerRows.Select(row => row.Position));
            SetDropdownChoices(_playerRoleFilter, _data.PlayerRows.Where(row => row.IsPitcher).Select(row => row.PitcherRole));
            SetDropdownChoices(_playerTypeFilter, _data.PlayerRows.Select(row => row.PlayerType));
            SetDropdownChoices(_playerRegistrationFilter, _data.PlayerRows.Select(row => row.RegistrationType));
            SetDropdownChoices(_playerAwardFilter, _data.Awards.Select(award => award.AwardType));
            _playerAwardFilter.choices.Insert(1, NoAwardChoice);
            _playerAwardFilter.choices.Insert(1, AnyAwardChoice);
            SetDropdownChoices(_playerAbilityFilter, HistoricalPlayerRow.AbilityNames);

            SetDropdownChoices(Require<DropdownField>("team-year-filter"), _data.Teams.Select(team => team.OriginYear.ToString()));
            SetDropdownChoices(Require<DropdownField>("award-year-filter"), _data.Awards.Select(award => award.SeasonYear.ToString()));
            SetDropdownChoices(Require<DropdownField>("award-type-filter"), _data.Awards.Select(award => award.AwardType));
            SetDropdownChoices(Require<DropdownField>("award-position-filter"), _data.Awards.Select(award => award.Position));
            SetDropdownChoices(Require<DropdownField>("award-franchise-filter"), _data.PlayerRows.Select(row => row.OriginFranchiseId));
            SetDropdownChoices(Require<DropdownField>("analysis-year-filter"), _data.PlayerRows.Select(row => row.OriginYear.ToString()));
            SetDropdownChoices(Require<DropdownField>("analysis-position-filter"), _data.PlayerRows.Select(row => row.Position));
            SetDropdownChoices(Require<DropdownField>("analysis-role-filter"), _data.PlayerRows.Where(row => row.IsPitcher).Select(row => row.PitcherRole));
            SetDropdownChoices(Require<DropdownField>("analysis-franchise-filter"), _data.PlayerRows.Select(row => row.OriginFranchiseId));
            SetDropdownChoices(Require<DropdownField>("analysis-type-filter"), _data.PlayerRows.Select(row => row.PlayerType));
        }

        private static void SetDropdownChoices(DropdownField field, IEnumerable<string> values)
        {
            var choices = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            choices.Insert(0, AllChoice);
            field.choices = choices;
            field.SetValueWithoutNotify(AllChoice);
        }

        private static void ConfigureLocalizedDropdown(DropdownField field, Func<string, string> formatter)
        {
            field.formatSelectedValueCallback = value => FormatDropdownChoice(value, formatter);
            field.formatListItemCallback = value => FormatDropdownChoice(value, formatter);
        }

        private static string FormatDropdownChoice(string value, Func<string, string> formatter)
        {
            return string.Equals(value, AllChoice, StringComparison.Ordinal) ? AllChoice : formatter(value);
        }

        private void SchedulePlayerFilter()
        {
            int generation = ++_searchGeneration;
            _playerSearch.schedule.Execute(() =>
            {
                if (generation == _searchGeneration)
                    ApplyPlayerFilters();
            }).StartingIn(180);
        }

        private void ApplyPlayerFilters()
        {
            if (_data == null || _viewModel == null)
                return;

            HistoricalPlayerFilter filter = _viewModel.Filter;
            filter.SearchText = _playerSearch.value?.Trim() ?? string.Empty;
            filter.Year = ParseChoiceInt(_playerYearFilter.value);
            filter.FranchiseId = ChoiceValue(_playerFranchiseFilter.value);
            filter.TeamSeasonKey = ChoiceValue(_playerTeamFilter.value);
            filter.Position = ChoiceValue(_playerPositionFilter.value);
            filter.PitcherRole = ChoiceValue(_playerRoleFilter.value);
            filter.PlayerType = ChoiceValue(_playerTypeFilter.value);
            filter.RegistrationType = ChoiceValue(_playerRegistrationFilter.value);
            filter.MinimumCost = Mathf.Clamp(_playerCostMin.value, 1, 10);
            filter.MaximumCost = Mathf.Clamp(_playerCostMax.value, 1, 10);
            filter.AbilityIndex = ResolveAbilityIndex(_playerAbilityFilter.value);
            filter.MinimumAbility = Mathf.Clamp(_playerAbilityMin.value, 0, 100);
            filter.MaximumAbility = Mathf.Clamp(_playerAbilityMax.value, 0, 100);
            filter.HasAnyAward = _playerAwardFilter.value == AnyAwardChoice
                ? true
                : _playerAwardFilter.value == NoAwardChoice
                    ? false
                    : (bool?)null;
            filter.AwardType = filter.HasAnyAward.HasValue
                ? string.Empty
                : ChoiceValue(_playerAwardFilter.value);
            _viewModel.ApplyQuery();

            _visiblePlayers.Clear();
            _visiblePlayers.AddRange(_viewModel.VisiblePlayers);
            _playerList.itemsSource = _visiblePlayers;
            _playerList.Rebuild();
            SetLabel("player-result-count", $"{_visiblePlayers.Count:N0}명");
            RestorePlayerSelection();
        }

        private void ClearPlayerFilters()
        {
            _searchGeneration++;
            _playerSearch.SetValueWithoutNotify(string.Empty);
            _playerYearFilter.SetValueWithoutNotify(AllChoice);
            _playerFranchiseFilter.SetValueWithoutNotify(AllChoice);
            _playerTeamFilter.SetValueWithoutNotify(AllChoice);
            _playerPositionFilter.SetValueWithoutNotify(AllChoice);
            _playerRoleFilter.SetValueWithoutNotify(AllChoice);
            _playerTypeFilter.SetValueWithoutNotify(AllChoice);
            _playerRegistrationFilter.SetValueWithoutNotify(AllChoice);
            _playerAwardFilter.SetValueWithoutNotify(AllChoice);
            _playerAbilityFilter.SetValueWithoutNotify(AllChoice);
            _playerCostMin.SetValueWithoutNotify(1);
            _playerCostMax.SetValueWithoutNotify(10);
            _playerAbilityMin.SetValueWithoutNotify(0);
            _playerAbilityMax.SetValueWithoutNotify(100);
            _viewModel.ResetFilter();
            ApplyPlayerFilters();
        }

        private void OnPlayerSortingChanged()
        {
            SortColumnDescription description = _playerList.sortedColumns.FirstOrDefault();
            if (description?.column == null)
                return;
            if (!TryResolvePlayerSortField(description.column, out HistoricalPlayerSortField field))
                return;

            _viewModel.SortField = field;
            _viewModel.SortDirection = description.direction == SortDirection.Descending
                ? HistoricalSortDirection.Descending
                : HistoricalSortDirection.Ascending;
            ApplyPlayerFilters();
        }

        private bool TryResolvePlayerSortField(Column column, out HistoricalPlayerSortField field)
        {
            if (_playerSortFields.TryGetValue(column, out field))
                return true;

            switch (column.name)
            {
                case "player": field = HistoricalPlayerSortField.Name; return true;
                case "year": field = HistoricalPlayerSortField.Year; return true;
                case "team": field = HistoricalPlayerSortField.Franchise; return true;
                case "position": field = HistoricalPlayerSortField.Position; return true;
                case "role": field = HistoricalPlayerSortField.PitcherRole; return true;
                case "cost": field = HistoricalPlayerSortField.Cost; return true;
                case "awards": field = HistoricalPlayerSortField.AwardCount; return true;
                default: field = default; return false;
            }
        }

        private void OnPlayerSelectionChanged(IEnumerable<object> selection)
        {
            HistoricalPlayerRow row = selection.OfType<HistoricalPlayerRow>().FirstOrDefault();
            if (row != null)
                SelectPlayer(row);
        }

        private void OnPlayerItemsChosen(IEnumerable<object> selection)
        {
            HistoricalPlayerRow row = selection.OfType<HistoricalPlayerRow>().FirstOrDefault();
            if (row == null)
                return;
            SelectPlayer(row);
            SetPlayerRawMode(false);
        }

        private void SelectPlayer(HistoricalPlayerRow row, bool switchTab = false)
        {
            if (row == null)
                return;
            _selectedPlayer = row;
            _playerRawJson.SetValueWithoutNotify(string.Empty);
            if (switchTab)
                ShowTab(BrowserTab.Players);
            int index = _visiblePlayers.IndexOf(row);
            if (index >= 0)
            {
                _playerList.SetSelectionWithoutNotify(new[] { index });
                _playerList.ScrollToItem(index);
            }
            BuildPlayerDetail();
            if (_isRawMode)
                LoadSelectedRawJson();
            _selectionLabel.text = $"{row.OriginYear} {row.Name} · {row.PlayerSeasonId}";
        }

        private void RestorePlayerSelection()
        {
            if (_selectedPlayer == null)
                return;
            int index = _visiblePlayers.FindIndex(row => row.PlayerSeasonId == _selectedPlayer.PlayerSeasonId);
            if (index >= 0)
                _playerList.SetSelectionWithoutNotify(new[] { index });
        }

        private HistoricalPlayerRow GetPlayer(int index)
        {
            return index >= 0 && index < _visiblePlayers.Count ? _visiblePlayers[index] : null;
        }

        private void BuildPlayerContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (TryGetContextItemIndex(evt, _playerList, out int index))
            {
                HistoricalPlayerRow contextPlayer = GetPlayer(index);
                if (contextPlayer != null)
                    SelectPlayer(contextPlayer);
            }
            HistoricalPlayerRow row = _selectedPlayer;
            evt.menu.AppendAction("상세 열기", _ => SelectPlayer(row, true), _ => row == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("비교에 추가/제거", _ => ToggleComparePlayer(), _ => row == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("PlayerSeasonId 복사", _ => CopyText(row.PlayerSeasonId, "PlayerSeasonId"), _ => row == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("PlayerPersonId 복사", _ => CopyText(row.PlayerPersonId, "PlayerPersonId"), _ => row == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("JSON 경로 복사", _ => CopyText(row.Season.SourcePath, "JSON 경로"), _ => row == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Raw JSON 복사", _ => CopyRawJson(), _ => row == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
        }

        private void ToggleComparePlayer()
        {
            if (_selectedPlayer == null)
                return;
            int existing = _comparePlayers.FindIndex(row => row.PlayerSeasonId == _selectedPlayer.PlayerSeasonId);
            if (existing >= 0)
            {
                _comparePlayers.RemoveAt(existing);
                _statusLabel.text = $"비교 목록에서 제거했습니다. ({_comparePlayers.Count}/4)";
            }
            else if (_comparePlayers.Count >= 4)
            {
                _statusLabel.text = "비교 목록은 최대 4명입니다.";
            }
            else
            {
                _comparePlayers.Add(_selectedPlayer);
                _statusLabel.text = $"비교 목록에 추가했습니다. ({_comparePlayers.Count}/4)";
            }
            BuildPlayerDetail();
        }

        private void CopySelectedPlayerSummary()
        {
            if (_selectedPlayer != null)
                CopyText(BuildPlayerSummary(_selectedPlayer), "선수 요약");
        }

        private void CopyRawJson()
        {
            if (_selectedPlayer == null)
                return;
            if (string.IsNullOrEmpty(_playerRawJson.value))
                LoadSelectedRawJson();
            CopyText(_playerRawJson.value, "Raw JSON");
        }

        private void SetPlayerRawMode(bool isRaw)
        {
            _isRawMode = isRaw;
            _playerDetailScroll.EnableInClassList("hidden", isRaw);
            _playerRawJson.EnableInClassList("hidden", !isRaw);
            Require<ToolbarButton>("player-details-mode-button").EnableInClassList("selected-detail-mode", !isRaw);
            Require<ToolbarButton>("player-raw-mode-button").EnableInClassList("selected-detail-mode", isRaw);
            if (isRaw)
                LoadSelectedRawJson();
        }

        private void LoadSelectedRawJson()
        {
            if (_selectedPlayer == null)
            {
                _playerRawJson.SetValueWithoutNotify(string.Empty);
                return;
            }

            if (_viewModel.TryGetRawJson(_selectedPlayer, out string raw, out string error))
            {
                _playerRawJson.SetValueWithoutNotify(raw);
            }
            else
            {
                _playerRawJson.SetValueWithoutNotify($"원본 JSON을 추출하지 못했습니다.\n{error}");
            }
        }

        private void CopyText(string text, string label)
        {
            EditorGUIUtility.systemCopyBuffer = text ?? string.Empty;
            ShowNotification(new GUIContent($"{label} 복사 완료"));
        }

        private static string ChoiceValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value == AllChoice ? string.Empty : value;
        }

        private static int? ParseChoiceInt(string value)
        {
            return int.TryParse(ChoiceValue(value), out int result) ? result : (int?)null;
        }

        private static int? ResolveAbilityIndex(string value)
        {
            string abilityName = ChoiceValue(value);
            if (string.IsNullOrEmpty(abilityName))
                return null;
            for (int index = 0; index < HistoricalPlayerRow.AbilityNames.Count; index++)
                if (string.Equals(HistoricalPlayerRow.AbilityNames[index], abilityName, StringComparison.Ordinal))
                    return index;
            return null;
        }

        private static string FormatRate(double? value)
        {
            return value.HasValue ? value.Value.ToString(".000") : "—";
        }

        private static string FormatDecimal(double? value, string format)
        {
            return value.HasValue ? value.Value.ToString(format) : "—";
        }

        private static string FormatPrimaryAbility(HistoricalPlayerRow row)
        {
            if (row == null)
                return string.Empty;
            int index = row.IsPitcher ? 7 : 0;
            return $"{FormatAbilityName(index)} {row.GetBaseAbility(index)}";
        }

        private static string FormatAbility(HistoricalPlayerRow row, int abilityIndex, bool isPitcherAbility)
        {
            if (row == null || row.IsPitcher != isPitcherAbility || row.BaseAttributes.Length <= abilityIndex)
                return "—";
            return row.GetBaseAbility(abilityIndex).ToString();
        }

        private static string FormatSeasonSummary(HistoricalPlayerRow row)
        {
            if (row?.Record == null)
                return "기록 없음";
            HistoricalSeasonRecord record = row.Record;
            if (row.IsPitcher)
                return $"{FormatInnings(record.PitchingOuts)}이닝 · 평균자책점 {FormatDecimal(row.EarnedRunAverage, "0.00")}";
            return $"타율 {FormatRate(row.BattingAverage)} · {record.HomeRuns}홈런 · {record.RunsBattedIn}타점";
        }

        private static string FormatInnings(int outs)
        {
            return $"{outs / 3}.{outs % 3}";
        }
    }
}
