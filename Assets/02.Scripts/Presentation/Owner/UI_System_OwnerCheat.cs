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

        /// <summary>타입별 일괄 지급 대상. 테스트 수요가 큰 특수 카드를 앞에 두고 일반 계열은 뒤에 둔다.</summary>
        private static readonly PlayerCardEdition[] GrantableEditions =
        {
            PlayerCardEdition.Ex,
            PlayerCardEdition.CareerHigh,
            PlayerCardEdition.Legend,
            PlayerCardEdition.Rare,
            PlayerCardEdition.Mvp,
            PlayerCardEdition.GoldenGlove,
            PlayerCardEdition.AllStar,
            PlayerCardEdition.Normal
        };

        private static UI_System_OwnerCheat _instance;

        private readonly List<CardOption> _cardOptions = new List<CardOption>();
        private readonly List<FranchiseOption> _franchiseOptions = new List<FranchiseOption>();
        private readonly List<int> _years = new List<int>();
        private readonly List<SkillBlockDefinition> _skillOptions = new List<SkillBlockDefinition>();
        private readonly List<string> _skillLabels = new List<string>();
        private readonly List<int> _visibleCardIndices = new List<int>();
        private readonly List<int> _visibleSkillIndices = new List<int>();

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
        private int _selectedEditionIndex;
        private bool _isEditionYearLimited;
        private string _editionBatchCount = "1";
        private string _cardSearch = string.Empty;
        private string _skillSearch = string.Empty;
        private string _filteredCardSearch;
        private string _filteredSkillSearch;
        private string _yearInput = string.Empty;
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
            DrawYearSelector(210f);
            DrawCycleSelector("원 구단", _franchiseOptions, ref _selectedFranchiseIndex, option => option.DisplayName, 340f);
            DrawTextField("장씩 (1~1,000)", ref _cardBatchCount, 130f);
            GUI.enabled = _years.Count > 0 && _franchiseOptions.Count > 0;
            if (GUILayout.Button("조건 카드 N장씩 획득", GUILayout.Width(190f), GUILayout.Height(42f)))
                AcquireCardBatch(manager);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            DrawCycleSelector("카드 타입", GrantableEditions, ref _selectedEditionIndex, DescribeEdition, 260f);
            GUILayout.BeginVertical(GUILayout.Width(170f));
            GUILayout.Label("연도 범위");
            _isEditionYearLimited = GUILayout.Toggle(
                _isEditionYearLimited,
                _isEditionYearLimited && _years.Count > 0 ? $"선택 연도({_years[_selectedYearIndex]})만" : "전체 연도",
                GUILayout.Height(24f));
            GUILayout.EndVertical();
            DrawTextField("장씩 (1~1,000)", ref _editionBatchCount, 130f);
            GUI.enabled = !_isEditionYearLimited || _years.Count > 0;
            if (GUILayout.Button("타입 카드 N장씩 획득", GUILayout.Width(190f), GUILayout.Height(42f)))
                AcquireEditionCardBatch(manager);
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
                _skillOptions.Count == 0 ? "선택 가능한 스킬 블록 없음" : "선택: " + _skillLabels[_selectedSkillIndex],
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

        /// <summary>◀▶ 순환과 직접 입력을 함께 받는 연도 선택기다. 카탈로그에 있는 연도를 입력한 경우에만 선택을 바꾼다.</summary>
        private void DrawYearSelector(float width)
        {
            GUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label("원 연도 (직접 입력 가능)");
            GUILayout.BeginHorizontal();
            GUI.enabled = _years.Count > 0;
            if (GUILayout.Button("◀", GUILayout.Width(30f), GUILayout.Height(24f)))
                SelectYearIndex(_selectedYearIndex <= 0 ? _years.Count - 1 : _selectedYearIndex - 1);
            string typed = GUILayout.TextField(_yearInput ?? string.Empty, 4, GUILayout.ExpandWidth(true), GUILayout.Height(24f));
            if (!string.Equals(typed, _yearInput, StringComparison.Ordinal))
            {
                _yearInput = typed;
                ApplyTypedYear();
            }
            if (GUILayout.Button("▶", GUILayout.Width(30f), GUILayout.Height(24f)))
                SelectYearIndex((_selectedYearIndex + 1) % _years.Count);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label(DescribeYearInputState(), _mutedStyle);
            GUILayout.EndVertical();
        }

        private void ApplyTypedYear()
        {
            if (!TryParseNonNegativeInt(_yearInput, out int year))
                return;
            int yearIndex = _years.IndexOf(year);
            if (yearIndex < 0 || yearIndex == _selectedYearIndex)
                return;
            _selectedYearIndex = yearIndex;
            RefreshFranchiseOptions();
        }

        private string DescribeYearInputState()
        {
            if (_years.Count == 0)
                return "카드 연도 없음";
            if (TryParseNonNegativeInt(_yearInput, out int year) && _years.Contains(year))
                return $"{_years[_years.Count - 1]}~{_years[0]}년";
            return $"없는 연도 · 현재 {_years[_selectedYearIndex]}년 ({_years[_years.Count - 1]}~{_years[0]})";
        }

        private void SelectYearIndex(int yearIndex)
        {
            if (_years.Count == 0)
                return;
            _selectedYearIndex = ClampIndex(yearIndex, _years.Count);
            _yearInput = _years[_selectedYearIndex].ToString(CultureInfo.InvariantCulture);
            RefreshFranchiseOptions();
        }

        private void DrawCardSearchResults()
        {
            if (_cardOptions.Count == 0)
                return;
            RefreshVisibleCardsIfSearchChanged();
            for (int visible = 0; visible < _visibleCardIndices.Count; visible++)
            {
                int index = _visibleCardIndices[visible];
                bool isSelected = index == _selectedCardIndex;
                if (GUILayout.Button((isSelected ? "● " : "○ ") + _cardOptions[index].Label, GUILayout.Height(24f)))
                    _selectedCardIndex = index;
            }
            if (_visibleCardIndices.Count == 0)
                GUILayout.Label("일치하는 카드가 없습니다.", _mutedStyle);
        }

        private void DrawSkillSearchResults()
        {
            if (_skillOptions.Count == 0)
                return;
            RefreshVisibleSkillsIfSearchChanged();
            for (int visible = 0; visible < _visibleSkillIndices.Count; visible++)
            {
                int index = _visibleSkillIndices[visible];
                bool isSelected = index == _selectedSkillIndex;
                if (GUILayout.Button((isSelected ? "● " : "○ ") + _skillLabels[index], GUILayout.Height(24f)))
                    _selectedSkillIndex = index;
            }
            if (_visibleSkillIndices.Count == 0)
                GUILayout.Label("일치하는 스킬 블록이 없습니다.", _mutedStyle);
        }

        /// <summary>
        /// OnGUI는 한 프레임에 Layout·Repaint·입력 이벤트마다 여러 번 호출되므로, 전체 카드 필터링은
        /// 검색어가 바뀐 순간에만 수행하고 표시 인덱스를 캐시한다.
        /// </summary>
        private void RefreshVisibleCardsIfSearchChanged()
        {
            if (string.Equals(_filteredCardSearch, _cardSearch, StringComparison.Ordinal))
                return;
            _filteredCardSearch = _cardSearch;
            _visibleCardIndices.Clear();
            string query = NormalizeQuery(_cardSearch);
            for (int index = 0; index < _cardOptions.Count && _visibleCardIndices.Count < MaximumVisibleSearchResults; index++)
            {
                if (MatchesSearch(_cardOptions[index].SearchText, query))
                    _visibleCardIndices.Add(index);
            }
        }

        private void RefreshVisibleSkillsIfSearchChanged()
        {
            if (string.Equals(_filteredSkillSearch, _skillSearch, StringComparison.Ordinal))
                return;
            _filteredSkillSearch = _skillSearch;
            _visibleSkillIndices.Clear();
            string query = NormalizeQuery(_skillSearch);
            for (int index = 0; index < _skillOptions.Count && _visibleSkillIndices.Count < MaximumVisibleSearchResults; index++)
            {
                if (MatchesSearch(_skillLabels[index].ToLowerInvariant(), query))
                    _visibleSkillIndices.Add(index);
            }
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

        private void AcquireEditionCardBatch(OwnerModeManager manager)
        {
            if (!TryParseBatchCount(_editionBatchCount, out int count))
                return;
            PlayerCardEdition edition = GrantableEditions[_selectedEditionIndex];
            int? year = _isEditionYearLimited ? _years[_selectedYearIndex] : (int?)null;
            string scope = year.HasValue ? $"{year.Value}년 " : "전체 연도 ";
            ExecuteGrant(
                () => manager.CheatAcquireCardsByEdition(edition, year, count),
                $"{scope}{DescribeEdition(edition)} 카드");
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
                string skipped = result.SkippedCardCount > 0
                    ? $" · 단일 소유·영입 예약 규칙으로 {result.SkippedCardCount:N0}장 제외"
                    : string.Empty;
                if (result.ItemCount == 0 && result.SkippedCardCount > 0)
                {
                    SetStatus(subject + ": 추가 지급 대상이 없습니다." + skipped, false);
                    return;
                }
                if (result.ItemCount == 0)
                {
                    SetStatus(subject + " 조건에 맞는 정의가 없습니다.", true);
                    return;
                }
                string cardBreakdown = subject.Contains("카드")
                    ? $" (신규 {result.NewCardCount:N0}, 중복 {result.DuplicateCardCount:N0})"
                    : string.Empty;
                SetStatus(
                    $"{subject} 획득 완료: {result.DefinitionCount:N0}종, 총 {result.ItemCount:N0}개{cardBreakdown}{skipped}",
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
            _skillLabels.Clear();
            _filteredCardSearch = null;
            _filteredSkillSearch = null;

            WorldCardCatalog catalog = manager.Runtime.WorldCardCatalog;
            var yearSet = new HashSet<int>();
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = catalog.Cards[index];
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                string franchiseName = manager.Runtime.IdentityRegistry.GetPresentationTeamSeasonName(
                    season.OriginTeamSeasonKey,
                    season.OriginFranchiseId);
                franchiseName = OwnerClubDisplayNameFormatter.Format(franchiseName, season.OriginYear);
                string playerName = manager.Runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId);
                string label = $"{season.OriginYear} · {franchiseName} · {playerName} · {DescribeEdition(card.Edition)} · {card.CardId}";
                _cardOptions.Add(new CardOption(card, label));
                if (yearSet.Add(season.OriginYear))
                    _years.Add(season.OriginYear);
            }
            _cardOptions.Sort(CardOption.Compare);
            _years.Sort((left, right) => right.CompareTo(left));

            SkillBlockDefinition[] definitions = manager.Balance.Growth.SkillBlocks;
            for (int index = 0; index < definitions.Length; index++)
                _skillOptions.Add(definitions[index]);
            _skillOptions.Sort(CompareSkillBlocks);
            for (int index = 0; index < _skillOptions.Count; index++)
                _skillLabels.Add(DescribeSkillBlock(_skillOptions[index]));

            _selectedCardIndex = ClampIndex(_selectedCardIndex, _cardOptions.Count);
            _selectedSkillIndex = ClampIndex(_selectedSkillIndex, _skillOptions.Count);
            SelectYearIndex(_selectedYearIndex);
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
            if (yearIndex >= 0)
            {
                _selectedYearIndex = yearIndex;
                _yearInput = season.OriginYear.ToString(CultureInfo.InvariantCulture);
            }
            RefreshFranchiseOptions(season.OriginFranchiseId);
        }

        private void RefreshFranchiseOptions(string preferredFranchiseId = null)
        {
            if (preferredFranchiseId == null && _franchiseOptions.Count > 0)
                preferredFranchiseId = _franchiseOptions[_selectedFranchiseIndex].FranchiseId;
            _franchiseOptions.Clear();
            _selectedFranchiseIndex = 0;
            if (_years.Count == 0)
                return;

            IReadOnlyList<PlayerSeasonDefinition> origins = OwnerCheatService.GetCardOrigins(
                _boundRuntime.WorldCardCatalog, _years[_selectedYearIndex]);
            for (int index = 0; index < origins.Count; index++)
            {
                PlayerSeasonDefinition season = origins[index];
                string name = _boundRuntime.IdentityRegistry.GetPresentationTeamSeasonName(
                    season.OriginTeamSeasonKey, season.OriginFranchiseId);
                name = OwnerClubDisplayNameFormatter.Format(name, season.OriginYear);
                _franchiseOptions.Add(new FranchiseOption(season.OriginFranchiseId, name));
            }
            _franchiseOptions.Sort(FranchiseOption.Compare);
            for (int index = 0; index < _franchiseOptions.Count; index++)
            {
                if (!string.Equals(
                        _franchiseOptions[index].FranchiseId,
                        preferredFranchiseId,
                        StringComparison.Ordinal))
                    continue;
                _selectedFranchiseIndex = index;
                break;
            }
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
            // 월드 카드 카탈로그는 런타임 수명 동안 바뀌지 않으므로 같은 런타임이면 재생성하지 않는다.
            if (HasActiveOwnerRuntime(manager))
            {
                if (!ReferenceEquals(_boundRuntime, manager.Runtime))
                    RefreshCatalog(manager);
            }
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

        private static string NormalizeQuery(string query)
        {
            return string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim().ToLowerInvariant();
        }

        /// <summary>source와 query는 모두 소문자로 정규화된 값이어야 한다. culture 비교는 수만 장 카탈로그에서 느리므로 ordinal을 쓴다.</summary>
        private static bool MatchesSearch(string source, string normalizedQuery)
        {
            return normalizedQuery.Length == 0 ||
                   source.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0;
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
            return Baseball.Game.Historical.PlayerCardEditionText.Get(edition);
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
                SearchText = label.ToLowerInvariant();
            }

            public PlayerCardDefinition Definition { get; }
            public string Label { get; }
            public string SearchText { get; }

            public static int Compare(CardOption left, CardOption right) =>
                string.CompareOrdinal(left.Label, right.Label);
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
