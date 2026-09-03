using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baseball.Editor.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Baseball.Editor.HistoricalDatabase
{
    /// <summary>Historical JSON Archive를 읽기 전용으로 탐색·분석·검증하는 UI Toolkit 창이다.</summary>
    public sealed partial class HistoricalDatabaseBrowserWindow : EditorWindow
    {
        private const string LayoutPath = "Assets/02.Scripts/Editor/HistoricalDatabase/UI/HistoricalDatabaseBrowserWindow.uxml";
        private const string LastSourcePreference = "Baseball.Editor.HistoricalDatabase.LastSource";
        private const double SourcePollIntervalSeconds = 2d;
        private const string AllChoice = "전체";

        private readonly HistoricalDatabaseAnalyzer _analyzer = new HistoricalDatabaseAnalyzer();
        private readonly HistoricalDatabaseValidationService _validationService = new HistoricalDatabaseValidationService();
        private readonly List<HistoricalPlayerRow> _visiblePlayers = new List<HistoricalPlayerRow>();
        private readonly List<HistoricalTeamSeason> _visibleTeams = new List<HistoricalTeamSeason>();
        private readonly List<AwardViewRow> _visibleAwards = new List<AwardViewRow>();
        private readonly List<HistoricalValidationIssue> _validationIssues = new List<HistoricalValidationIssue>();
        private readonly List<HistoricalPlayerRow> _comparePlayers = new List<HistoricalPlayerRow>(4);

        private HistoricalDatabaseViewModel _viewModel;
        private HistoricalArchiveData _data;
        private HistoricalPlayerRow _selectedPlayer;
        private HistoricalTeamSeason _selectedTeam;
        private CancellationTokenSource _loadCancellation;
        private int _loadGeneration;
        private int _searchGeneration;
        private double _nextSourcePollTime;
        private bool _isRawMode;
        private BrowserTab _activeTab;
        private DateTime _loadedManifestWriteUtc;

        private TextField _sourcePathField;
        private VisualElement _workspace;
        private VisualElement _emptyState;
        private VisualElement _loadingPanel;
        private ProgressBar _loadProgress;
        private VisualElement _sourceChangedBanner;
        private VisualElement _sourceErrorBanner;
        private Label _sourceErrorLabel;
        private Label _archiveSummary;
        private Label _archiveHealthBadge;
        private Label _statusLabel;
        private Label _selectionLabel;
        private MultiColumnListView _playerList;
        private MultiColumnListView _teamList;
        private MultiColumnListView _awardList;
        private MultiColumnListView _validationList;
        private ScrollView _playerDetailScroll;
        private VisualElement _playerDetailContent;
        private TextField _playerRawJson;
        private VisualElement _teamDetailContent;

        [MenuItem("Baseball/Historical Database Browser", priority = 10)]
        [BaseballEditorTool(
            "데이터",
            "Historical Database Browser",
            "역사 JSON 아카이브를 검색·분석·검증합니다. 원본 데이터는 변경하지 않습니다.",
            order: 0,
            impact: ToolImpact.ReadOnly)]
        public static void Open()
        {
            HistoricalDatabaseBrowserWindow window = GetWindow<HistoricalDatabaseBrowserWindow>("Historical Database");
            window.minSize = new Vector2(900f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            _viewModel = new HistoricalDatabaseViewModel();
            EditorApplication.update += PollSourceChanges;
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollSourceChanges;
            CancelLoad();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (layout == null)
            {
                rootVisualElement.Add(new HelpBox($"UI Layout을 찾을 수 없습니다.\n{LayoutPath}", HelpBoxMessageType.Error));
                return;
            }

            layout.CloneTree(rootVisualElement);
            rootVisualElement.EnableInClassList("unity-editor-theme--light", !EditorGUIUtility.isProSkin);
            CacheVisualElements();
            ConfigureSourceControls();
            ConfigureTabs();
            ConfigurePlayerBrowser();
            ConfigureTeamBrowser();
            ConfigureAwardBrowser();
            ConfigureAnalysis();
            ConfigureValidation();
            Require<Button>("validate-button").SetEnabled(false);
            ShowTab(BrowserTab.Overview);

            string previousSource = EditorPrefs.GetString(LastSourcePreference, string.Empty);
            _sourcePathField.SetValueWithoutNotify(previousSource);
            if (!string.IsNullOrWhiteSpace(previousSource))
                BeginLoad(previousSource);
        }

        private void CacheVisualElements()
        {
            _sourcePathField = Require<TextField>("source-path-field");
            _workspace = Require<VisualElement>("workspace");
            _emptyState = Require<VisualElement>("empty-state");
            _loadingPanel = Require<VisualElement>("loading-panel");
            _loadProgress = Require<ProgressBar>("load-progress");
            _sourceChangedBanner = Require<VisualElement>("source-changed-banner");
            _sourceErrorBanner = Require<VisualElement>("source-error-banner");
            _sourceErrorLabel = Require<Label>("source-error-label");
            _archiveSummary = Require<Label>("archive-summary");
            _archiveHealthBadge = Require<Label>("archive-health-badge");
            _statusLabel = Require<Label>("status-label");
            _selectionLabel = Require<Label>("selection-label");
            _playerList = Require<MultiColumnListView>("player-list");
            _teamList = Require<MultiColumnListView>("team-list");
            _awardList = Require<MultiColumnListView>("award-list");
            _validationList = Require<MultiColumnListView>("validation-list");
            _playerDetailScroll = Require<ScrollView>("player-detail-scroll");
            _playerDetailContent = Require<VisualElement>("player-detail-content");
            _playerRawJson = Require<TextField>("player-raw-json");
            _teamDetailContent = Require<VisualElement>("team-detail-content");
        }

        private void ConfigureSourceControls()
        {
            Require<Button>("browse-button").clicked += SelectFolder;
            Require<Button>("empty-select-folder-button").clicked += SelectFolder;
            Require<Button>("reload-button").clicked += Reload;
            Require<Button>("reload-changed-button").clicked += Reload;
            Require<Button>("validate-button").clicked += RunValidation;
            Require<Button>("cancel-load-button").clicked += CancelLoad;

            VisualElement sourcePanel = rootVisualElement.Q(className: "source-panel");
            sourcePanel.RegisterCallback<DragUpdatedEvent>(OnFolderDragUpdated);
            sourcePanel.RegisterCallback<DragPerformEvent>(OnFolderDragPerformed);
        }

        private void ConfigureTabs()
        {
            BindTab("overview-tab", BrowserTab.Overview);
            BindTab("players-tab", BrowserTab.Players);
            BindTab("teams-tab", BrowserTab.Teams);
            BindTab("awards-tab", BrowserTab.Awards);
            BindTab("analysis-tab", BrowserTab.Analysis);
            BindTab("validation-tab", BrowserTab.Validation);
        }

        private void BindTab(string buttonName, BrowserTab tab)
        {
            Require<ToolbarButton>(buttonName).clicked += () => ShowTab(tab);
        }

        private void ShowTab(BrowserTab tab)
        {
            _activeTab = tab;
            SetDisplayed("overview-panel", tab == BrowserTab.Overview);
            SetDisplayed("players-panel", tab == BrowserTab.Players);
            SetDisplayed("teams-panel", tab == BrowserTab.Teams);
            SetDisplayed("awards-panel", tab == BrowserTab.Awards);
            SetDisplayed("analysis-panel", tab == BrowserTab.Analysis);
            SetDisplayed("validation-panel", tab == BrowserTab.Validation);

            SetSelectedTab("overview-tab", tab == BrowserTab.Overview);
            SetSelectedTab("players-tab", tab == BrowserTab.Players);
            SetSelectedTab("teams-tab", tab == BrowserTab.Teams);
            SetSelectedTab("awards-tab", tab == BrowserTab.Awards);
            SetSelectedTab("analysis-tab", tab == BrowserTab.Analysis);
            SetSelectedTab("validation-tab", tab == BrowserTab.Validation);

            if (_data == null)
                return;
            if (tab == BrowserTab.Analysis)
                RefreshAnalysis();
        }

        private void SetSelectedTab(string name, bool selected)
        {
            Require<ToolbarButton>(name).EnableInClassList("selected-tab", selected);
        }

        private void SelectFolder()
        {
            string current = _sourcePathField.value;
            string start = Directory.Exists(current) ? current : Application.dataPath;
            string selected = EditorUtility.OpenFolderPanel("역사 아카이브 폴더 선택", start, string.Empty);
            if (string.IsNullOrEmpty(selected))
                return;
            _sourcePathField.SetValueWithoutNotify(selected);
            BeginLoad(selected);
        }

        private void Reload()
        {
            BeginLoad(_sourcePathField.value);
        }

        private void BeginLoad(string sourcePath)
        {
            CancelLoad();
            _validationGeneration++;
            int generation = ++_loadGeneration;
            HistoricalArchivePathValidation pathValidation = new HistoricalArchiveRepository().ValidatePath(sourcePath);
            if (!pathValidation.IsValid)
            {
                ShowLoadError(pathValidation.Message);
                return;
            }

            string normalizedPath = pathValidation.NormalizedPath;
            _sourcePathField.SetValueWithoutNotify(normalizedPath);
            _sourceErrorBanner.AddToClassList("hidden");
            _sourceChangedBanner.AddToClassList("hidden");
            _loadingPanel.RemoveFromClassList("hidden");
            _loadProgress.value = 0f;
            _loadProgress.title = "아카이브 준비 중...";
            _statusLabel.text = "역사 아카이브를 불러오는 중입니다.";
            _workspace.SetEnabled(false);
            Require<Button>("cancel-load-button").SetEnabled(true);
            Require<Button>("run-validation-button").SetEnabled(false);
            SetSourceControlsEnabled(false);

            _loadCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = _loadCancellation.Token;
            var progress = new Progress<HistoricalLoadProgress>(value =>
            {
                if (generation != _loadGeneration || cancellationToken.IsCancellationRequested || _loadProgress == null)
                    return;
                _loadProgress.value = GetOverallLoadRatio(value);
                _loadProgress.title = string.IsNullOrWhiteSpace(value.Message) ? "불러오는 중..." : value.Message;
            });

            Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var viewModel = new HistoricalDatabaseViewModel();
                viewModel.Load(normalizedPath, progress);
                cancellationToken.ThrowIfCancellationRequested();
                return viewModel;
            }, cancellationToken).ContinueWith(task =>
            {
                EditorApplication.delayCall += () => CompleteLoad(task, generation, normalizedPath);
            }, TaskScheduler.Default);
        }

        private void CompleteLoad(Task<HistoricalDatabaseViewModel> task, int generation, string normalizedPath)
        {
            if (this == null || generation != _loadGeneration)
                return;

            _loadCancellation?.Dispose();
            _loadCancellation = null;

            _loadingPanel.AddToClassList("hidden");
            if (task.IsCanceled)
            {
                _statusLabel.text = "아카이브 로드를 취소했습니다.";
                _workspace.SetEnabled(true);
                SetSourceControlsEnabled(true);
                return;
            }
            if (task.IsFaulted)
            {
                Exception error = task.Exception?.GetBaseException();
                ShowLoadError(error?.Message ?? "알 수 없는 아카이브 로드 오류입니다.");
                return;
            }

            string selectedPlayerId = _selectedPlayer?.PlayerSeasonId;
            string selectedTeamKey = _selectedTeam?.TeamSeasonKey;
            string[] comparePlayerIds = _comparePlayers.Select(row => row.PlayerSeasonId).ToArray();
            _viewModel = task.Result;
            _data = _viewModel.Data;
            _selectedPlayer = _viewModel.FindPlayer(selectedPlayerId);
            _selectedTeam = _viewModel.FindTeam(selectedTeamKey);
            _playerRawJson.SetValueWithoutNotify(string.Empty);
            _comparePlayers.Clear();
            for (int index = 0; index < comparePlayerIds.Length; index++)
            {
                HistoricalPlayerRow comparePlayer = _viewModel.FindPlayer(comparePlayerIds[index]);
                if (comparePlayer != null)
                    _comparePlayers.Add(comparePlayer);
            }
            string manifestPath = Path.Combine(_data.SourceFolder, "manifest.json");
            _loadedManifestWriteUtc = File.Exists(manifestPath) ? File.GetLastWriteTimeUtc(manifestPath) : DateTime.MinValue;
            SetSourceControlsEnabled(true);
            EditorPrefs.SetString(LastSourcePreference, normalizedPath);
            _emptyState.AddToClassList("hidden");
            _workspace.RemoveFromClassList("hidden");
            _workspace.SetEnabled(true);
            _sourceChangedBanner.AddToClassList("hidden");
            _nextSourcePollTime = EditorApplication.timeSinceStartup + SourcePollIntervalSeconds;
            PopulateArchive();
            RestoreDetailsAfterReload();
            _statusLabel.text = $"로드 완료 · {_data.LoadElapsed.TotalMilliseconds:N0} ms · JSON 원본만 사용";
        }

        private void CancelLoad()
        {
            if (_loadCancellation == null)
                return;
            _loadCancellation.Cancel();
            _loadCancellation.Dispose();
            _loadCancellation = null;
            _loadGeneration++;
            if (_loadingPanel != null)
                _loadingPanel.AddToClassList("hidden");
            if (_sourcePathField != null)
                SetSourceControlsEnabled(true);
        }

        private void ShowLoadError(string message)
        {
            ClearLoadedArchiveState();
            _loadingPanel?.AddToClassList("hidden");
            _sourceErrorLabel.text = string.IsNullOrWhiteSpace(message) ? "아카이브를 읽을 수 없습니다." : message;
            _sourceErrorBanner.RemoveFromClassList("hidden");
            _archiveHealthBadge.text = "INVALID";
            SetBadgeClass("status-error");
            _statusLabel.text = "아카이브 로드 실패";
            SetSourceControlsEnabled(true);
        }

        private void ClearLoadedArchiveState()
        {
            _data = null;
            _viewModel = new HistoricalDatabaseViewModel();
            _selectedPlayer = null;
            _selectedTeam = null;
            _comparePlayers.Clear();
            _visiblePlayers.Clear();
            _visibleTeams.Clear();
            _visibleAwards.Clear();
            _validationIssues.Clear();
            _playerList?.SetSelectionWithoutNotify(Array.Empty<int>());
            _teamList?.SetSelectionWithoutNotify(Array.Empty<int>());
            _playerList?.Rebuild();
            _teamList?.Rebuild();
            _awardList?.Rebuild();
            _validationList?.Rebuild();
            Button runValidationButton = rootVisualElement.Q<Button>("run-validation-button");
            if (runValidationButton != null)
                runValidationButton.SetEnabled(false);
            _playerRawJson?.SetValueWithoutNotify(string.Empty);
            if (_playerDetailContent != null)
                ShowPlayerEmptyState();
            if (_teamDetailContent != null)
                ShowTeamEmptyState();
            _workspace?.SetEnabled(true);
            _workspace?.AddToClassList("hidden");
            _emptyState?.RemoveFromClassList("hidden");
            if (_selectionLabel != null)
                _selectionLabel.text = "선택 없음";
        }

        private void RestoreDetailsAfterReload()
        {
            if (_selectedPlayer != null)
            {
                BuildPlayerDetail();
                if (_isRawMode)
                    LoadSelectedRawJson();
            }
            else
            {
                _playerRawJson.SetValueWithoutNotify(string.Empty);
                ShowPlayerEmptyState();
            }

            if (_selectedTeam != null)
                BuildTeamDetail(_selectedTeam);
            else
                ShowTeamEmptyState();

            if (_activeTab == BrowserTab.Players && _selectedPlayer != null)
                _selectionLabel.text = $"{_selectedPlayer.OriginYear} {_selectedPlayer.Name} · {_selectedPlayer.PlayerSeasonId}";
            else if (_activeTab == BrowserTab.Teams && _selectedTeam != null)
                _selectionLabel.text = $"{_selectedTeam.OriginYear} {_selectedTeam.FranchiseId} · {_selectedTeam.TeamSeasonKey}";
            else if (_selectedPlayer == null && _selectedTeam == null)
                _selectionLabel.text = "선택 없음";
        }

        private void SetSourceControlsEnabled(bool enabled)
        {
            _sourcePathField.SetEnabled(enabled);
            Require<Button>("browse-button").SetEnabled(enabled);
            Require<Button>("reload-button").SetEnabled(enabled);
            Require<Button>("validate-button").SetEnabled(enabled && _data != null);
        }

        private void PopulateArchive()
        {
            PopulateOverview();
            PopulateFilterChoices();
            ApplyPlayerFilters();
            ApplyTeamFilters();
            ApplyAwardFilters();
            ClearValidation();
            RefreshAnalysis();
            _archiveHealthBadge.text = "로드됨";
            SetBadgeClass("status-pass");
        }

        private void PopulateOverview()
        {
            HistoricalArchiveSummary summary = _data.Manifest.Summary;
            SetLabel("overview-seasons", summary?.YearCount ?? _data.YearSourcePaths.Count);
            SetLabel("overview-persons", _data.Persons.Count);
            SetLabel("overview-player-seasons", _data.PlayerRows.Count);
            SetLabel("overview-cards-count", _data.Cards.Count);
            SetLabel("overview-teams", _data.Teams.Count);
            SetLabel("overview-awards", _data.Awards.Count);

            int minimumYear = _data.YearSourcePaths.Count == 0 ? 0 : _data.YearSourcePaths.Keys.Min();
            int maximumYear = _data.YearSourcePaths.Count == 0 ? 0 : _data.YearSourcePaths.Keys.Max();
            _archiveSummary.text = $"{minimumYear}–{maximumYear} · 시즌 {_data.YearSourcePaths.Count:N0}개 · 인물 {_data.Persons.Count:N0}명 · 선수 시즌 {_data.PlayerRows.Count:N0}건";

            VisualElement manifestDetails = Require<VisualElement>("manifest-details");
            manifestDetails.Clear();
            AddKeyValue(manifestDetails, "아카이브 유형", IsOriginalSourceArchive(_data) ? "에디터 원본 1:1" : "인게임 가공 데이터");
            AddKeyValue(manifestDetails, "에셋 형식", _data.Manifest.AssetFormatVersion.ToString());
            AddKeyValue(manifestDetails, "콘텐츠 스키마", _data.Manifest.ContentSchemaVersion.ToString());
            AddKeyValue(manifestDetails, "아카이브 해시", ShortHash(_data.Manifest.AssetArchiveHash));
            HistoricalSourceManifest source = _data.Manifest.SourceManifest;
            if (source != null)
            {
                AddKeyValue(manifestDetails, "참조 데이터 버전", source.ReferenceDataVersion);
                AddKeyValue(manifestDetails, "생성기 버전", source.GeneratorVersion);
                AddKeyValue(manifestDetails, "밸런스 버전", source.BalanceVersion);
                AddKeyValue(manifestDetails, "생성 시드", source.GenerationSeed.ToString());
                AddKeyValue(manifestDetails, "이름 정책", source.NamePolicyVersion);
                AddKeyValue(manifestDetails, "이름 데이터 정책", source.NameDataPolicy);
                AddKeyValue(manifestDetails, "콘텐츠 해시", ShortHash(source.ContentHash));
            }
        }

        private static bool IsOriginalSourceArchive(HistoricalArchiveData archive)
        {
            return string.Equals(
                archive?.Manifest?.SourceManifest?.NameDataPolicy,
                "editor-original-source-v2",
                StringComparison.Ordinal);
        }

        private void PollSourceChanges()
        {
            if (_data == null || EditorApplication.timeSinceStartup < _nextSourcePollTime)
                return;
            _nextSourcePollTime = EditorApplication.timeSinceStartup + SourcePollIntervalSeconds;
            try
            {
                string manifestPath = Path.Combine(_data.SourceFolder, "manifest.json");
                if (!File.Exists(manifestPath) || File.GetLastWriteTimeUtc(manifestPath) != _loadedManifestWriteUtc)
                {
                    _sourceChangedBanner.RemoveFromClassList("hidden");
                    return;
                }
                for (int index = 0; index < _data.SourceFiles.Count; index++)
                {
                    HistoricalSourceFileInfo file = _data.SourceFiles[index];
                    if (!File.Exists(file.FullPath) || File.GetLastWriteTimeUtc(file.FullPath) != file.LastWriteUtc)
                    {
                        _sourceChangedBanner.RemoveFromClassList("hidden");
                        return;
                    }
                }
            }
            catch (IOException)
            {
                _sourceChangedBanner.RemoveFromClassList("hidden");
            }
            catch (UnauthorizedAccessException)
            {
                _sourceChangedBanner.RemoveFromClassList("hidden");
            }
        }

        private void OnFolderDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = GetDraggedFolder() == null ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private void OnFolderDragPerformed(DragPerformEvent evt)
        {
            string folder = GetDraggedFolder();
            if (folder == null)
                return;
            DragAndDrop.AcceptDrag();
            _sourcePathField.SetValueWithoutNotify(folder);
            BeginLoad(folder);
            evt.StopPropagation();
        }

        private static string GetDraggedFolder()
        {
            if (DragAndDrop.paths == null || DragAndDrop.paths.Length != 1)
                return null;
            string path = Path.GetFullPath(DragAndDrop.paths[0]);
            return Directory.Exists(path) ? path : null;
        }

        private T Require<T>(string name) where T : VisualElement
        {
            T element = rootVisualElement.Q<T>(name);
            if (element == null)
                throw new InvalidOperationException($"UXML 요소를 찾을 수 없습니다: {name}");
            return element;
        }

        private void SetDisplayed(string name, bool isDisplayed)
        {
            Require<VisualElement>(name).EnableInClassList("hidden", !isDisplayed);
        }

        private void SetLabel(string name, object value)
        {
            Require<Label>(name).text = Convert.ToString(value) ?? string.Empty;
        }

        private void SetBadgeClass(string className)
        {
            _archiveHealthBadge.RemoveFromClassList("status-neutral");
            _archiveHealthBadge.RemoveFromClassList("status-pass");
            _archiveHealthBadge.RemoveFromClassList("status-warning");
            _archiveHealthBadge.RemoveFromClassList("status-error");
            _archiveHealthBadge.AddToClassList(className);
        }

        private static string ShortHash(string hash)
        {
            return string.IsNullOrEmpty(hash) || hash.Length <= 14 ? hash ?? string.Empty : hash.Substring(0, 14) + "…";
        }

        private static float GetOverallLoadRatio(HistoricalLoadProgress progress)
        {
            switch (progress.Stage)
            {
                case "Manifest": return 0.02f;
                case "Persons": return 0.06f;
                case "Years": return 0.08f + progress.Ratio * 0.84f;
                case "Joining": return 0.94f;
                case "Completed": return 1f;
                default: return Mathf.Clamp01(progress.Ratio);
            }
        }

        private static void AddKeyValue(VisualElement parent, string key, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("key-value-row");
            var keyLabel = new Label(key);
            keyLabel.AddToClassList("key-label");
            var valueLabel = new Label(string.IsNullOrEmpty(value) ? "—" : value);
            valueLabel.AddToClassList("key-value");
            row.Add(keyLabel);
            row.Add(valueLabel);
            parent.Add(row);
        }

        private static Label MakeTableLabel()
        {
            var label = new Label();
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.overflow = Overflow.Hidden;
            return label;
        }

        private static void BindTextColumn(Column column, Func<int, string> valueProvider)
        {
            column.makeCell = MakeTableLabel;
            column.bindCell = (element, index) =>
            {
                element.userData = index;
                ((Label)element).text = valueProvider(index) ?? string.Empty;
            };
        }

        private static bool TryGetContextItemIndex(
            ContextualMenuPopulateEvent evt,
            VisualElement list,
            out int index)
        {
            VisualElement current = evt.target as VisualElement;
            while (current != null && current != list)
            {
                if (current.userData is int itemIndex)
                {
                    index = itemIndex;
                    return true;
                }
                current = current.parent;
            }
            index = -1;
            return false;
        }

        private enum BrowserTab
        {
            Overview,
            Players,
            Teams,
            Awards,
            Analysis,
            Validation
        }

        private sealed class AwardViewRow
        {
            public AwardViewRow(HistoricalAwardRecord award, HistoricalPlayerRow player)
            {
                Award = award;
                Player = player;
            }

            public HistoricalAwardRecord Award { get; }
            public HistoricalPlayerRow Player { get; }
        }
    }
}
