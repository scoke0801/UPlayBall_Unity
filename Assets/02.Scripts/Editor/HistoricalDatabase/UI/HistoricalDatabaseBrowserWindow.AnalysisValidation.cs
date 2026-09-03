using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Baseball.Editor.HistoricalDatabase
{
    public sealed partial class HistoricalDatabaseBrowserWindow
    {
        private DropdownField _analysisYearFilter;
        private DropdownField _analysisPositionFilter;
        private DropdownField _analysisRoleFilter;
        private DropdownField _analysisFranchiseFilter;
        private DropdownField _analysisTypeFilter;
        private IntegerField _analysisCostMin;
        private IntegerField _analysisCostMax;
        private int _validationGeneration;

        private void ConfigureAnalysis()
        {
            _analysisYearFilter = Require<DropdownField>("analysis-year-filter");
            _analysisPositionFilter = Require<DropdownField>("analysis-position-filter");
            _analysisRoleFilter = Require<DropdownField>("analysis-role-filter");
            _analysisFranchiseFilter = Require<DropdownField>("analysis-franchise-filter");
            _analysisTypeFilter = Require<DropdownField>("analysis-type-filter");
            _analysisCostMin = Require<IntegerField>("analysis-cost-min");
            _analysisCostMax = Require<IntegerField>("analysis-cost-max");
            ConfigureLocalizedDropdown(_analysisPositionFilter, FormatPosition);
            ConfigureLocalizedDropdown(_analysisRoleFilter, FormatPitcherRole);
            ConfigureLocalizedDropdown(_analysisTypeFilter, FormatPlayerType);
            _analysisYearFilter.RegisterValueChangedCallback(_ => RefreshAnalysis());
            _analysisPositionFilter.RegisterValueChangedCallback(_ => RefreshAnalysis());
            _analysisRoleFilter.RegisterValueChangedCallback(_ => RefreshAnalysis());
            _analysisFranchiseFilter.RegisterValueChangedCallback(_ => RefreshAnalysis());
            _analysisTypeFilter.RegisterValueChangedCallback(_ => RefreshAnalysis());
            _analysisCostMin.RegisterValueChangedCallback(_ => RefreshAnalysis());
            _analysisCostMax.RegisterValueChangedCallback(_ => RefreshAnalysis());
            Require<Button>("refresh-analysis-button").clicked += RefreshAnalysis;
        }

        private void RefreshAnalysis()
        {
            if (_data == null)
                return;
            var filter = new HistoricalAnalysisFilter
            {
                Year = ParseChoiceInt(_analysisYearFilter.value),
                FranchiseId = ChoiceValue(_analysisFranchiseFilter.value),
                Position = ChoiceValue(_analysisPositionFilter.value),
                PitcherRole = ChoiceValue(_analysisRoleFilter.value),
                PlayerType = ChoiceValue(_analysisTypeFilter.value),
                MinimumCost = Mathf.Clamp(_analysisCostMin.value, 1, 10),
                MaximumCost = Mathf.Clamp(_analysisCostMax.value, 1, 10)
            };
            HistoricalDatabaseAnalysisResult result = _analyzer.Analyze(_data, filter);
            PopulateDistribution(Require<VisualElement>("cost-distribution"), result.CostDistribution, "비용 ");
            PopulatePositionAndRoleDistribution(Require<VisualElement>("position-distribution"), result);
            PopulateAwardDistribution(Require<VisualElement>("award-distribution"), result);
            PopulateAbilitySummary(Require<VisualElement>("ability-summary"), result.Abilities, result.PlayerCount);
            PopulateStatisticSummary(Require<VisualElement>("season-statistics-summary"), result.SeasonStatistics);
            _statusLabel.text = $"분석 갱신 · 선수 시즌 {result.PlayerCount:N0}건";
        }

        private static void PopulatePositionAndRoleDistribution(
            VisualElement parent,
            HistoricalDatabaseAnalysisResult result)
        {
            parent.Clear();
            AddDistributionGroup(parent, "포지션", result.PositionDistribution, string.Empty, FormatPosition);
            AddDistributionGroup(parent, "투수 역할", result.PitcherRoleDistribution, string.Empty, FormatPitcherRole);
        }

        private static void PopulateAwardDistribution(
            VisualElement parent,
            HistoricalDatabaseAnalysisResult result)
        {
            parent.Clear();
            AddDistributionGroup(parent, "수상 종류", result.AwardDistribution, string.Empty, FormatAwardType);
            AddDistributionGroup(parent, "연도", result.AwardsByYear, string.Empty);
            AddDistributionGroup(parent, "포지션", result.AwardsByPosition, string.Empty, FormatPosition);
            AddDistributionGroup(parent, "비용", result.AwardsByCost, "비용 ");
            AddDistributionGroup(parent, "구단", result.AwardsByFranchise, string.Empty);
        }

        private static void AddDistributionGroup(
            VisualElement parent,
            string title,
            IReadOnlyList<HistoricalDistributionBucket> buckets,
            string prefix,
            Func<string, string> keyFormatter = null)
        {
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("analysis-subheading");
            parent.Add(titleLabel);
            var content = new VisualElement();
            PopulateDistribution(content, buckets, prefix, keyFormatter);
            parent.Add(content);
        }

        private static void PopulateDistribution(
            VisualElement parent,
            IReadOnlyList<HistoricalDistributionBucket> buckets,
            string prefix,
            Func<string, string> keyFormatter = null)
        {
            parent.Clear();
            if (buckets.Count == 0)
            {
                AddAbsent(parent, "현재 조건에 해당하는 데이터가 없습니다.");
                return;
            }
            double maximum = Math.Max(1d, buckets.Max(bucket => bucket.Count));
            for (int index = 0; index < buckets.Count; index++)
            {
                HistoricalDistributionBucket bucket = buckets[index];
                var row = new VisualElement();
                row.AddToClassList("distribution-row");
                string key = keyFormatter == null ? bucket.Key : keyFormatter(bucket.Key);
                var name = new Label(prefix + key);
                name.AddToClassList("distribution-name");
                var track = new VisualElement();
                track.AddToClassList("distribution-track");
                var fill = new VisualElement();
                fill.AddToClassList("distribution-fill");
                fill.style.width = Length.Percent((float)(bucket.Count * 100d / maximum));
                track.Add(fill);
                var value = new Label($"{bucket.Count:N0}  {bucket.Percentage:0.0}%");
                value.AddToClassList("distribution-value");
                row.Add(name);
                row.Add(track);
                row.Add(value);
                parent.Add(row);
            }
        }

        private static void PopulateAbilitySummary(
            VisualElement parent,
            IReadOnlyList<HistoricalAbilitySummary> summaries,
            int playerCount)
        {
            parent.Clear();
            var header = new VisualElement();
            header.AddToClassList("summary-row");
            AddSummaryCell(header, $"능력치 (선수 시즌 {playerCount:N0}건)", "summary-name summary-header");
            AddSummaryCell(header, "표본 수", "summary-number summary-header");
            AddSummaryCell(header, "최솟값", "summary-number summary-header");
            AddSummaryCell(header, "평균", "summary-number summary-header");
            AddSummaryCell(header, "중앙값", "summary-number summary-header");
            AddSummaryCell(header, "하위 10%", "summary-number summary-header");
            AddSummaryCell(header, "상위 10%", "summary-number summary-header");
            AddSummaryCell(header, "최댓값", "summary-number summary-header");
            AddSummaryCell(header, "표준편차", "summary-number summary-header");
            parent.Add(header);
            for (int index = 0; index < summaries.Count; index++)
            {
                HistoricalAbilitySummary summary = summaries[index];
                var row = new VisualElement();
                row.AddToClassList("summary-row");
                AddSummaryCell(row, summary.AbilityName, "summary-name");
                AddSummaryCell(row, summary.Count.ToString("N0"), "summary-number");
                AddSummaryCell(row, summary.Minimum.ToString("0.0"), "summary-number");
                AddSummaryCell(row, summary.Mean.ToString("0.0"), "summary-number");
                AddSummaryCell(row, summary.Median.ToString("0.0"), "summary-number");
                AddSummaryCell(row, summary.Percentile10.ToString("0.0"), "summary-number");
                AddSummaryCell(row, summary.Percentile90.ToString("0.0"), "summary-number");
                AddSummaryCell(row, summary.Maximum.ToString("0.0"), "summary-number");
                AddSummaryCell(row, summary.StandardDeviation.ToString("0.0"), "summary-number");
                parent.Add(row);
            }
        }

        private static void PopulateStatisticSummary(
            VisualElement parent,
            IReadOnlyList<HistoricalStatisticSummary> summaries)
        {
            parent.Clear();
            if (summaries.Count == 0)
            {
                AddAbsent(parent, "현재 조건에서 계산 가능한 시즌 기록이 없습니다.");
                return;
            }

            var header = new VisualElement();
            header.AddToClassList("summary-row");
            AddSummaryCell(header, "지표", "summary-name summary-header");
            AddSummaryCell(header, "표본 수", "summary-number summary-header");
            AddSummaryCell(header, "최솟값", "summary-number summary-header");
            AddSummaryCell(header, "평균", "summary-number summary-header");
            AddSummaryCell(header, "중앙값", "summary-number summary-header");
            AddSummaryCell(header, "하위 10%", "summary-number summary-header");
            AddSummaryCell(header, "상위 10%", "summary-number summary-header");
            AddSummaryCell(header, "최댓값", "summary-number summary-header");
            AddSummaryCell(header, "표준편차", "summary-number summary-header");
            parent.Add(header);

            for (int index = 0; index < summaries.Count; index++)
            {
                HistoricalStatisticSummary summary = summaries[index];
                var row = new VisualElement();
                row.AddToClassList("summary-row");
                AddSummaryCell(row, summary.StatisticName, "summary-name");
                AddSummaryCell(row, summary.Count.ToString("N0"), "summary-number");
                AddSummaryCell(row, summary.Minimum.ToString("0.000"), "summary-number");
                AddSummaryCell(row, summary.Mean.ToString("0.000"), "summary-number");
                AddSummaryCell(row, summary.Median.ToString("0.000"), "summary-number");
                AddSummaryCell(row, summary.Percentile10.ToString("0.000"), "summary-number");
                AddSummaryCell(row, summary.Percentile90.ToString("0.000"), "summary-number");
                AddSummaryCell(row, summary.Maximum.ToString("0.000"), "summary-number");
                AddSummaryCell(row, summary.StandardDeviation.ToString("0.000"), "summary-number");
                parent.Add(row);
            }
        }

        private static void AddSummaryCell(VisualElement parent, string text, string classes)
        {
            var label = new Label(text);
            string[] classNames = classes.Split(' ');
            for (int index = 0; index < classNames.Length; index++)
                label.AddToClassList(classNames[index]);
            parent.Add(label);
        }

        private void ConfigureValidation()
        {
            BindValidationSeverityColumn();
            BindTextColumn(_validationList.columns["year"], index => GetValidationIssue(index)?.Year?.ToString() ?? "—");
            BindTextColumn(_validationList.columns["category"], index => GetValidationIssue(index)?.Category);
            BindTextColumn(_validationList.columns["entity"], index => GetValidationIssue(index)?.EntityId);
            BindTextColumn(_validationList.columns["message"], index => GetValidationIssue(index)?.Message);
            ConfigureValidationSort();
            _validationList.itemsSource = _validationIssues;
            _validationList.itemsChosen += OpenValidationSelection;
            _validationList.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if (TryGetContextItemIndex(evt, _validationList, out int index))
                    _validationList.SetSelectionWithoutNotify(new[] { index });
                HistoricalValidationIssue issue = _validationList.selectedItem as HistoricalValidationIssue;
                evt.menu.AppendAction("Entity 열기", _ => NavigateValidationIssue(issue), _ => issue == null || issue.NavigationKind == HistoricalNavigationKind.None ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                evt.menu.AppendAction("메시지 복사", _ => CopyText(issue.Message, "검증 메시지"), _ => issue == null ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            }));
            Require<Button>("run-validation-button").clicked += RunValidation;
            ClearValidation();
        }

        private void ConfigureValidationSort()
        {
            _validationList.sortingMode = ColumnSortingMode.Default;
            SetColumnComparison(_validationList.columns["severity"], (left, right) => right.Severity.CompareTo(left.Severity), GetValidationIssue);
            SetColumnComparison(_validationList.columns["year"], (left, right) => Nullable.Compare(left.Year, right.Year), GetValidationIssue);
            SetColumnComparison(_validationList.columns["category"], (left, right) => string.Compare(left.Category, right.Category, StringComparison.Ordinal), GetValidationIssue);
            SetColumnComparison(_validationList.columns["entity"], (left, right) => string.Compare(left.EntityId, right.EntityId, StringComparison.Ordinal), GetValidationIssue);
            SetColumnComparison(_validationList.columns["message"], (left, right) => string.Compare(left.Message, right.Message, StringComparison.Ordinal), GetValidationIssue);
        }

        private void RunValidation()
        {
            if (_data == null)
                return;
            int generation = ++_validationGeneration;
            HistoricalArchiveData archive = _data;
            Button runButton = Require<Button>("run-validation-button");
            runButton.SetEnabled(false);
            Require<Button>("validate-button").SetEnabled(false);
            Require<Button>("cancel-load-button").SetEnabled(false);
            _loadingPanel.RemoveFromClassList("hidden");
            _loadProgress.value = 0.25f;
            _loadProgress.title = "아카이브 검증 중...";
            _statusLabel.text = "아카이브 전체 검증을 실행 중입니다.";

            Task.Run(() => _validationService.Validate(archive)).ContinueWith(task =>
            {
                EditorApplication.delayCall += () =>
                {
                    if (this == null || generation != _validationGeneration || archive != _data)
                        return;
                    _loadingPanel.AddToClassList("hidden");
                    runButton.SetEnabled(true);
                    Require<Button>("validate-button").SetEnabled(_data != null);
                    Require<Button>("cancel-load-button").SetEnabled(true);
                    if (task.IsFaulted)
                    {
                        string message = task.Exception?.GetBaseException().Message ?? "검증 중 오류가 발생했습니다.";
                        _sourceErrorLabel.text = $"아카이브 검증을 완료하지 못했습니다: {message}";
                        _sourceErrorBanner.RemoveFromClassList("hidden");
                        _archiveHealthBadge.text = "검증 실패";
                        SetBadgeClass("status-error");
                        _statusLabel.text = "아카이브 검증 실행 실패";
                        return;
                    }
                    ApplyValidationReport(task.Result);
                    ShowTab(BrowserTab.Validation);
                };
            }, TaskScheduler.Default);
        }

        private void ApplyValidationReport(HistoricalDatabaseValidationReport report)
        {
            _validationIssues.Clear();
            _validationIssues.AddRange(report.Issues);
            _validationList.itemsSource = _validationIssues;
            _validationList.Rebuild();
            SetLabel("validation-pass-count", $"통과 {report.PassCount:N0}");
            SetLabel("validation-warning-count", $"경고 {report.WarningCount:N0}");
            SetLabel("validation-error-count", $"오류 {report.ErrorCount:N0}");
            _archiveHealthBadge.text = report.ErrorCount > 0 ? "오류" : report.WarningCount > 0 ? "경고" : "통과";
            SetBadgeClass(report.ErrorCount > 0 ? "status-error" : report.WarningCount > 0 ? "status-warning" : "status-pass");
            string detailNotice = report.ArePassDetailsTruncated
                ? $" · 통과 상세 {report.DetailedPassCount:N0}건만 표시"
                : string.Empty;
            _validationList.tooltip = report.ArePassDetailsTruncated
                ? $"전체 통과 {report.PassCount:N0}건 중 앞쪽 {report.DetailedPassCount:N0}건과 모든 경고/오류를 표시합니다."
                : string.Empty;
            _statusLabel.text = $"검증 완료 · 통과 {report.PassCount:N0} / 경고 {report.WarningCount:N0} / 오류 {report.ErrorCount:N0} · {report.Elapsed.TotalMilliseconds:N0}밀리초{detailNotice}";
        }

        private void ClearValidation()
        {
            _validationGeneration++;
            _validationIssues.Clear();
            if (_validationList != null)
            {
                _validationList.itemsSource = _validationIssues;
                _validationList.Rebuild();
            }
            if (rootVisualElement.Q<Label>("validation-pass-count") != null)
            {
                SetLabel("validation-pass-count", "통과 0");
                SetLabel("validation-warning-count", "경고 0");
                SetLabel("validation-error-count", "오류 0");
            }
            Button runButton = rootVisualElement.Q<Button>("run-validation-button");
            if (runButton != null)
                runButton.SetEnabled(_data != null);
        }

        private void OpenValidationSelection(IEnumerable<object> selection)
        {
            NavigateValidationIssue(selection.OfType<HistoricalValidationIssue>().FirstOrDefault());
        }

        private void NavigateValidationIssue(HistoricalValidationIssue issue)
        {
            if (issue == null)
                return;
            switch (issue.NavigationKind)
            {
                case HistoricalNavigationKind.Player:
                    HistoricalPlayerRow player = _viewModel.FindPlayer(issue.NavigationId);
                    if (player == null)
                    {
                        IReadOnlyList<HistoricalPlayerRow> career = _viewModel.FindPersonCareer(issue.NavigationId);
                        player = career.FirstOrDefault();
                    }
                    if (player != null)
                        SelectPlayer(player, true);
                    else
                        _statusLabel.text = $"Player Entity를 찾을 수 없습니다: {issue.NavigationId}";
                    break;
                case HistoricalNavigationKind.Team:
                    SelectTeam(_viewModel.FindTeam(issue.NavigationId), true);
                    break;
                case HistoricalNavigationKind.Award:
                    HistoricalPlayerRow awardPlayer = _viewModel.FindPlayer(issue.NavigationId);
                    if (awardPlayer != null) SelectPlayer(awardPlayer, true);
                    else ShowTab(BrowserTab.Awards);
                    break;
                case HistoricalNavigationKind.File:
                    _statusLabel.text = $"원본 파일: {issue.NavigationId}";
                    break;
                default:
                    _statusLabel.text = issue.Message;
                    break;
            }
        }

        private HistoricalValidationIssue GetValidationIssue(int index)
        {
            return index >= 0 && index < _validationIssues.Count ? _validationIssues[index] : null;
        }

        private void BindValidationSeverityColumn()
        {
            Column column = _validationList.columns["severity"];
            column.makeCell = MakeTableLabel;
            column.bindCell = (element, index) =>
            {
                element.userData = index;
                var label = (Label)element;
                label.RemoveFromClassList("severity-pass");
                label.RemoveFromClassList("severity-warning");
                label.RemoveFromClassList("severity-error");
                HistoricalValidationIssue issue = GetValidationIssue(index);
                if (issue == null)
                {
                    label.text = string.Empty;
                    return;
                }
                label.text = issue.Severity == HistoricalValidationSeverity.Error
                    ? "오류"
                    : issue.Severity == HistoricalValidationSeverity.Warning
                        ? "경고"
                        : "통과";
                label.AddToClassList(issue.Severity == HistoricalValidationSeverity.Error
                    ? "severity-error"
                    : issue.Severity == HistoricalValidationSeverity.Warning
                        ? "severity-warning"
                        : "severity-pass");
            };
        }
    }
}
