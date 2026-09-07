#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Game.Input;
using Baseball.Presentation.SharedUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Baseball.Presentation.Owner
{
    /// <summary>] 키로 여는 Editor·Development Build 전용 구단주 치트 UI다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_System_OwnerCheat : MonoBehaviour
    {
        private const int WindowId = 2_026_090_7;
        private const int MaximumVisibleSearchResults = 8;
        private const int MaximumUiBatchCount = 1_000;
        private const float WindowWidth = 1_040f;

        private static UI_System_OwnerCheat _instance;

        private readonly List<CardOption> _cardOptions = new List<CardOption>();
        private readonly List<FranchiseOption> _franchiseOptions = new List<FranchiseOption>();
        private readonly List<int> _years = new List<int>();
        private readonly List<SkillBlockDefinition> _skillOptions = new List<SkillBlockDefinition>();

        private ManagerHistoricalRuntimeState _boundRuntime;
        private InputContextLease _inputLease;
        private InputManager _leasedInputManager;
        private EventSystem _suspendedEventSystem;
        private bool _wasEventSystemEnabled;
        private bool _isVisible;
        private Rect _windowRect;
        private Vector2 _scrollPosition;
        private int _selectedCardIndex;
        private int _selectedSkillIndex;
        private int _selectedYearIndex;
        private int _selectedFranchiseIndex;
        private int _selectedRarityIndex;
        private string _cardSearch = string.Empty;
        private string _skillSearch = string.Empty;
        private string _moneyAmount = "100000000";
        private string _scoutingPointAmount = "10000";
        private string _developmentPointAmount = "10000";
        private string _cardBatchCount = "1";
        private string _skillBatchCount = "1";
        private string _status = "] 키로 닫을 수 있습니다.";
        private bool _isError;
        private GUIStyle _sectionStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _mutedStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeInstance()
        {
            if (_instance != null)
                return;
            var root = new GameObject(nameof(UI_System_OwnerCheat));
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<UI_System_OwnerCheat>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            gameObject.hideFlags = HideFlags.DontSave;
        }

        private void OnDestroy()
        {
            ReleaseInput();
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rightBracketKey.wasPressedThisFrame)
                SetVisible(!_isVisible);
            if (_isVisible)
                EnsureInputSuspended();
        }

        private void OnGUI()
        {
            if (!_isVisible)
                return;

            EnsureStyles();
            float height = Mathf.Min(920f, Mathf.Max(540f, Screen.height - 40f));
            if (_windowRect.width <= 0f)
                _windowRect = new Rect((Screen.width - WindowWidth) * 0.5f, 20f, WindowWidth, height);
            _windowRect.width = Mathf.Min(WindowWidth, Screen.width - 20f);
            _windowRect.height = height;
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));

            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            _windowRect = GUI.ModalWindow(WindowId, _windowRect, DrawWindow, "구단주 개발 치트  ·  Editor / Development Build");
        }

        private void DrawWindow(int windowId)
        {
            if (GUI.Button(new Rect(_windowRect.width - 36f, 3f, 30f, 22f), "×"))
            {
                SetVisible(false);
                return;
            }

            GUILayout.Space(8f);
            OwnerModeManager manager = OwnerModeManager.Instance;
            if (!HasActiveOwnerRuntime(manager))
            {
                GUILayout.Space(24f);
                GUILayout.Label("구단주 모드 게임 화면에서만 사용할 수 있습니다.", _sectionStyle);
                GUILayout.Label("치트 UI 자체는 ] 키 또는 우측 상단 × 버튼으로 닫을 수 있습니다.", _mutedStyle);
                GUI.DragWindow(new Rect(0f, 0f, _windowRect.width - 44f, 28f));
                return;
            }

            if (!ReferenceEquals(_boundRuntime, manager.Runtime))
                RefreshCatalog(manager);

            ManagerEconomyState economy = manager.Runtime.Economy;
            GUILayout.Label(
                $"현재 보유  Money {OwnerMoneyFormatter.Format(economy.Money)}  ·  SP {economy.ScoutingPoints:N0}  ·  DP {economy.DevelopmentPoints:N0}  ·  선수 카드 {manager.Runtime.OwnedCards.Count:N0}종  ·  스킬 블록 {manager.Runtime.PlayerGrowth.Inventory.Blocks.Count:N0}개",
                _mutedStyle);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            DrawResourceSection(manager);
            GUILayout.Space(12f);
            DrawCardSection(manager);
            GUILayout.Space(12f);
            DrawSkillBlockSection(manager);
            GUILayout.Space(14f);
            GUILayout.Label(_status, _statusStyle);
            GUILayout.Space(8f);
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width - 44f, 28f));
        }

        private void DrawResourceSection(OwnerModeManager manager)
        {
            GUILayout.Label("보유 자원 증가", _sectionStyle);
            GUILayout.BeginHorizontal();
            DrawTextField("Money (원)", ref _moneyAmount, 190f);
            DrawTextField("SP", ref _scoutingPointAmount, 130f);
            DrawTextField("DP", ref _developmentPointAmount, 130f);
            if (GUILayout.Button("입력한 자원 증가", GUILayout.Width(180f), GUILayout.Height(42f)))
                IncreaseResources(manager);
            GUILayout.EndHorizontal();
        }

        private void DrawCardSection(OwnerModeManager manager)
        {
            GUILayout.Label("선수 카드 획득", _sectionStyle);
            GUILayout.Label("선수명·구단·연도·Edition·CardId로 검색한 뒤 한 장을 선택합니다.", _mutedStyle);
            _cardSearch = GUILayout.TextField(_cardSearch ?? string.Empty, GUILayout.Height(26f));
            DrawCardSearchResults();

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                _cardOptions.Count == 0 ? "선택 가능한 카드 없음" : "선택: " + _cardOptions[_selectedCardIndex].Label,
                GUILayout.ExpandWidth(true));
            GUI.enabled = _cardOptions.Count > 0;
            if (GUILayout.Button("선택 카드 1장 획득", GUILayout.Width(180f), GUILayout.Height(30f)))
                ExecuteGrant(() => manager.CheatAcquireCard(_cardOptions[_selectedCardIndex].Definition.CardId), "선수 카드");
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            DrawCycleSelector("원 연도", _years, ref _selectedYearIndex, year => year + "년", 210f);
            DrawCycleSelector("원 구단", _franchiseOptions, ref _selectedFranchiseIndex, option => option.DisplayName, 340f);
            DrawTextField("장씩 (1~1,000)", ref _cardBatchCount, 130f);
            GUI.enabled = _years.Count > 0 && _franchiseOptions.Count > 0;
            if (GUILayout.Button("조건 카드 N장씩 획득", GUILayout.Width(190f), GUILayout.Height(42f)))
                AcquireCardBatch(manager);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("현재 월드의 모든 카드 1장씩 획득", GUILayout.Width(310f), GUILayout.Height(34f)))
                ExecuteGrant(manager.CheatAcquireAllCards, "전체 선수 카드");
            GUILayout.EndHorizontal();
        }

        private void DrawSkillBlockSection(OwnerModeManager manager)
        {
            GUILayout.Label("스킬 블록 획득", _sectionStyle);
            GUILayout.Label("계통·등급·DefinitionId로 검색한 뒤 하나를 선택합니다. 치트 지급은 Gacha 보장 카운트를 바꾸지 않습니다.", _mutedStyle);
            _skillSearch = GUILayout.TextField(_skillSearch ?? string.Empty, GUILayout.Height(26f));
            DrawSkillSearchResults();

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                _skillOptions.Count == 0 ? "선택 가능한 스킬 블록 없음" : "선택: " + DescribeSkillBlock(_skillOptions[_selectedSkillIndex]),
                GUILayout.ExpandWidth(true));
            GUI.enabled = _skillOptions.Count > 0;
            if (GUILayout.Button("선택 블록 1개 획득", GUILayout.Width(180f), GUILayout.Height(30f)))
                ExecuteGrant(
                    () => manager.CheatAcquireSkillBlock(_skillOptions[_selectedSkillIndex].BlockId),
                    "스킬 블록");
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            SkillBlockRarity[] rarities = (SkillBlockRarity[])Enum.GetValues(typeof(SkillBlockRarity));
            DrawCycleSelector(
                "등급",
                rarities,
                ref _selectedRarityIndex,
                DescribeRarity,
                260f);
            DrawTextField("개씩 (1~1,000)", ref _skillBatchCount, 150f);
            if (GUILayout.Button("해당 등급 모두 N개씩", GUILayout.Width(200f), GUILayout.Height(42f)))
                AcquireRaritySkillBlocks(manager, rarities[_selectedRarityIndex]);
            if (GUILayout.Button("모든 블록 N개씩", GUILayout.Width(180f), GUILayout.Height(42f)))
                AcquireAllSkillBlocks(manager);
            GUILayout.EndHorizontal();
        }

        private void DrawCardSearchResults()
        {
            if (_cardOptions.Count == 0)
                return;
            int visibleCount = 0;
            for (int index = 0; index < _cardOptions.Count && visibleCount < MaximumVisibleSearchResults; index++)
            {
                CardOption option = _cardOptions[index];
                if (!MatchesSearch(option.SearchText, _cardSearch))
                    continue;
                bool isSelected = index == _selectedCardIndex;
                if (GUILayout.Button((isSelected ? "● " : "○ ") + option.Label, GUILayout.Height(24f)))
                    _selectedCardIndex = index;
                visibleCount++;
            }
            if (visibleCount == 0)
                GUILayout.Label("일치하는 카드가 없습니다.", _mutedStyle);
        }

        private void DrawSkillSearchResults()
        {
            if (_skillOptions.Count == 0)
                return;
            int visibleCount = 0;
            for (int index = 0; index < _skillOptions.Count && visibleCount < MaximumVisibleSearchResults; index++)
            {
                SkillBlockDefinition definition = _skillOptions[index];
                string label = DescribeSkillBlock(definition);
                if (!MatchesSearch(label, _skillSearch))
                    continue;
                bool isSelected = index == _selectedSkillIndex;
                if (GUILayout.Button((isSelected ? "● " : "○ ") + label, GUILayout.Height(24f)))
                    _selectedSkillIndex = index;
                visibleCount++;
            }
            if (visibleCount == 0)
                GUILayout.Label("일치하는 스킬 블록이 없습니다.", _mutedStyle);
        }

        private void IncreaseResources(OwnerModeManager manager)
        {
            if (!TryParseNonNegativeLong(_moneyAmount, out long money) ||
                !TryParseNonNegativeInt(_scoutingPointAmount, out int scoutingPoints) ||
                !TryParseNonNegativeInt(_developmentPointAmount, out int developmentPoints))
            {
                SetStatus("자원 증가량은 0 이상의 정수여야 합니다.", true);
                return;
            }
            Execute(() =>
            {
                manager.CheatIncreaseResources(money, scoutingPoints, developmentPoints);
                SetStatus($"자원 증가 완료: Money +{OwnerMoneyFormatter.Format(money)}, SP +{scoutingPoints:N0}, DP +{developmentPoints:N0}", false);
            });
        }

        private void AcquireCardBatch(OwnerModeManager manager)
        {
            if (!TryParseBatchCount(_cardBatchCount, out int count))
                return;
            int year = _years[_selectedYearIndex];
            FranchiseOption franchise = _franchiseOptions[_selectedFranchiseIndex];
            ExecuteGrant(
                () => manager.CheatAcquireCards(year, franchise.FranchiseId, count),
                $"{year}년 {franchise.DisplayName} 카드");
        }

        private void AcquireRaritySkillBlocks(OwnerModeManager manager, SkillBlockRarity rarity)
        {
            if (!TryParseBatchCount(_skillBatchCount, out int count))
                return;
            ExecuteGrant(
                () => manager.CheatAcquireSkillBlocks(rarity, count),
                DescribeRarity(rarity) + " 스킬 블록");
        }

        private void AcquireAllSkillBlocks(OwnerModeManager manager)
        {
            if (!TryParseBatchCount(_skillBatchCount, out int count))
                return;
            ExecuteGrant(
                () => manager.CheatAcquireAllSkillBlocks(count),
                "전체 스킬 블록");
        }

        private void ExecuteGrant(Func<OwnerCheatGrantResult> action, string subject)
        {
            Execute(() =>
            {
                OwnerCheatGrantResult result = action();
                if (result.ItemCount == 0)
                {
                    SetStatus(subject + " 조건에 맞는 정의가 없습니다.", true);
                    return;
                }
                string cardBreakdown = subject.Contains("카드")
                    ? $" (신규 {result.NewCardCount:N0}, 중복 {result.DuplicateCardCount:N0})"
                    : string.Empty;
                SetStatus(
                    $"{subject} 획득 완료: {result.DefinitionCount:N0}종, 총 {result.ItemCount:N0}개{cardBreakdown}",
                    false);
            });
        }

        private void Execute(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("치트 적용 실패: " + exception.Message, true);
            }
        }

        private void RefreshCatalog(OwnerModeManager manager)
        {
            _boundRuntime = manager.Runtime;
            _cardOptions.Clear();
            _franchiseOptions.Clear();
            _years.Clear();
            _skillOptions.Clear();

            WorldCardCatalog catalog = manager.Runtime.WorldCardCatalog;
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = catalog.Cards[index];
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
            string franchiseName = manager.Runtime.IdentityRegistry.GetPresentationFranchiseName(season.OriginFranchiseId);
            string playerName = manager.Runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId);
                string label = $"{season.OriginYear} · {franchiseName} · {playerName} · {DescribeEdition(card.Edition)} · {card.CardId}";
                _cardOptions.Add(new CardOption(card, label));
                AddYear(season.OriginYear);
                AddFranchise(season.OriginFranchiseId, franchiseName);
            }
            _cardOptions.Sort(CardOption.Compare);
            _years.Sort((left, right) => right.CompareTo(left));
            _franchiseOptions.Sort(FranchiseOption.Compare);

            SkillBlockDefinition[] definitions = manager.Balance.Growth.SkillBlocks;
            for (int index = 0; index < definitions.Length; index++)
                _skillOptions.Add(definitions[index]);
            _skillOptions.Sort(CompareSkillBlocks);

            _selectedCardIndex = ClampIndex(_selectedCardIndex, _cardOptions.Count);
            _selectedSkillIndex = ClampIndex(_selectedSkillIndex, _skillOptions.Count);
            SelectCurrentTeamOrigin(manager.Runtime);
        }

        private void SelectCurrentTeamOrigin(ManagerHistoricalRuntimeState runtime)
        {
            CurrentRosterState roster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            if (roster.Entries.Count == 0 ||
                !runtime.WorldCardCatalog.TryGetCard(roster.Entries[0].CardId, out PlayerCardDefinition card))
                return;
            PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            int yearIndex = _years.IndexOf(season.OriginYear);
            if (yearIndex >= 0) _selectedYearIndex = yearIndex;
            for (int index = 0; index < _franchiseOptions.Count; index++)
            {
                if (!string.Equals(
                        _franchiseOptions[index].FranchiseId,
                        season.OriginFranchiseId,
                        StringComparison.Ordinal))
                    continue;
                _selectedFranchiseIndex = index;
                break;
            }
        }

        private void AddYear(int year)
        {
            if (!_years.Contains(year))
                _years.Add(year);
        }

        private void AddFranchise(string franchiseId, string displayName)
        {
            for (int index = 0; index < _franchiseOptions.Count; index++)
                if (string.Equals(_franchiseOptions[index].FranchiseId, franchiseId, StringComparison.Ordinal))
                    return;
            _franchiseOptions.Add(new FranchiseOption(franchiseId, displayName));
        }

        private void SetVisible(bool isVisible)
        {
            if (_isVisible == isVisible)
                return;
            _isVisible = isVisible;
            if (_isVisible)
            {
                RefreshCatalogIfPossible();
                EnsureInputSuspended();
                return;
            }
            ReleaseInput();
        }

        private void RefreshCatalogIfPossible()
        {
            OwnerModeManager manager = OwnerModeManager.Instance;
            if (HasActiveOwnerRuntime(manager))
                RefreshCatalog(manager);
            else
                _boundRuntime = null;
        }

        private static bool HasActiveOwnerRuntime(OwnerModeManager manager)
        {
            return manager != null &&
                   manager.HasActiveRuntime &&
                   UiGameModeSession.IsSelected(UiGameMode.OwnerCareer);
        }

        private void EnsureInputSuspended()
        {
            InputManager currentInput = InputManager.Instance;
            if (currentInput != null && currentInput != _leasedInputManager)
            {
                _inputLease?.Dispose();
                _leasedInputManager = currentInput;
                _inputLease = currentInput.PushContext(InputContext.Modal);
            }

            if (_suspendedEventSystem == null)
            {
                EventSystem currentEventSystem = EventSystem.current;
                if (currentEventSystem != null)
                {
                    _suspendedEventSystem = currentEventSystem;
                    _wasEventSystemEnabled = currentEventSystem.enabled;
                    currentEventSystem.enabled = false;
                }
            }
        }

        private void ReleaseInput()
        {
            _inputLease?.Dispose();
            _inputLease = null;
            _leasedInputManager = null;
            if (_suspendedEventSystem != null)
                _suspendedEventSystem.enabled = _wasEventSystemEnabled;
            _suspendedEventSystem = null;
        }

        private bool TryParseBatchCount(string value, out int count)
        {
            if (!TryParseNonNegativeInt(value, out count) || count <= 0 || count > MaximumUiBatchCount)
            {
                SetStatus($"N은 1~{MaximumUiBatchCount:N0} 범위의 정수여야 합니다.", true);
                return false;
            }
            return true;
        }

        private void SetStatus(string message, bool isError)
        {
            _status = message ?? string.Empty;
            _isError = isError;
            if (_statusStyle != null)
                _statusStyle.normal.textColor = isError
                    ? new Color(1f, 0.48f, 0.42f)
                    : new Color(0.45f, 0.95f, 0.55f);
        }

        private void EnsureStyles()
        {
            if (_sectionStyle == null)
            {
                _sectionStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    margin = new RectOffset(0, 0, 4, 6)
                };
                _mutedStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    wordWrap = true,
                    normal = { textColor = new Color(0.75f, 0.78f, 0.82f) }
                };
                _statusStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true,
                    padding = new RectOffset(12, 12, 8, 8)
                };
            }
            _statusStyle.normal.textColor = _isError
                ? new Color(1f, 0.48f, 0.42f)
                : new Color(0.45f, 0.95f, 0.55f);
        }

        private static void DrawTextField(string label, ref string value, float width)
        {
            GUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label(label);
            value = GUILayout.TextField(value ?? string.Empty, GUILayout.Height(24f));
            GUILayout.EndVertical();
        }

        private static void DrawCycleSelector<T>(
            string label,
            IReadOnlyList<T> values,
            ref int selectedIndex,
            Func<T, string> formatter,
            float width)
        {
            GUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label(label);
            GUILayout.BeginHorizontal();
            GUI.enabled = values.Count > 0;
            if (GUILayout.Button("◀", GUILayout.Width(30f), GUILayout.Height(24f)))
                selectedIndex = selectedIndex <= 0 ? values.Count - 1 : selectedIndex - 1;
            string current = values.Count == 0 ? "없음" : formatter(values[selectedIndex]);
            GUILayout.Label(current, GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.Height(24f));
            if (GUILayout.Button("▶", GUILayout.Width(30f), GUILayout.Height(24f)))
                selectedIndex = (selectedIndex + 1) % values.Count;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static bool TryParseNonNegativeLong(string value, out long result)
        {
            string normalized = (value ?? string.Empty).Replace(",", string.Empty).Trim();
            return long.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= 0L;
        }

        private static bool TryParseNonNegativeInt(string value, out int result)
        {
            string normalized = (value ?? string.Empty).Replace(",", string.Empty).Trim();
            return int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= 0;
        }

        private static bool MatchesSearch(string source, string query)
        {
            return string.IsNullOrWhiteSpace(query) ||
                   source.IndexOf(query.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static int ClampIndex(int index, int count)
        {
            return count == 0 ? 0 : Mathf.Clamp(index, 0, count - 1);
        }

        private static int CompareSkillBlocks(SkillBlockDefinition left, SkillBlockDefinition right)
        {
            int rarity = right.Rarity.CompareTo(left.Rarity);
            if (rarity != 0) return rarity;
            int category = left.Category.CompareTo(right.Category);
            return category != 0 ? category : string.CompareOrdinal(left.BlockId, right.BlockId);
        }

        private static string DescribeEdition(PlayerCardEdition edition)
        {
            return edition switch
            {
                PlayerCardEdition.Normal => "일반",
                PlayerCardEdition.AllStar => "올스타",
                PlayerCardEdition.GoldenGlove => "골든글러브",
                PlayerCardEdition.Mvp => "MVP",
                _ => edition.ToString()
            };
        }

        private static string DescribeRarity(SkillBlockRarity rarity)
        {
            return rarity switch
            {
                SkillBlockRarity.Normal => "일반",
                SkillBlockRarity.Rare => "희귀",
                SkillBlockRarity.Elite => "정예",
                SkillBlockRarity.Unique => "고유",
                SkillBlockRarity.Legendary => "전설",
                _ => rarity.ToString()
            };
        }

        private static string DescribeSkillBlock(SkillBlockDefinition definition)
        {
            var builder = new StringBuilder(96);
            builder.Append(DescribeRarity(definition.Rarity));
            builder.Append(" · ");
            builder.Append(definition.Category);
            builder.Append(" · ");
            builder.Append(definition.BlockId);
            return builder.ToString();
        }

        private sealed class CardOption
        {
            public CardOption(PlayerCardDefinition definition, string label)
            {
                Definition = definition;
                Label = label;
                SearchText = label;
            }

            public PlayerCardDefinition Definition { get; }
            public string Label { get; }
            public string SearchText { get; }

            public static int Compare(CardOption left, CardOption right) =>
                string.Compare(left.Label, right.Label, StringComparison.CurrentCulture);
        }

        private sealed class FranchiseOption
        {
            public FranchiseOption(string franchiseId, string displayName)
            {
                FranchiseId = franchiseId;
                DisplayName = displayName;
            }

            public string FranchiseId { get; }
            public string DisplayName { get; }

            public static int Compare(FranchiseOption left, FranchiseOption right)
            {
                int displayName = string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCulture);
                return displayName != 0 ? displayName : string.CompareOrdinal(left.FranchiseId, right.FranchiseId);
            }
        }
    }
}
#endif
