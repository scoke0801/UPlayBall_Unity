using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    public enum OwnerNewGameStep
    {
        Team,
        MainCards,
        FrontManager,
        Nickname,
        StarterRosterReview,
        Completed
    }

    /// <summary>효과가 없는 프런트 매니저 외형 선택의 안정 ID를 정의한다.</summary>
    public static class FrontManagerIds
    {
        public const string DefaultAnalysis = "FRONT_MANAGER_DEFAULT_01";
        public const string DefaultTest = "FRONT_MANAGER_DEFAULT_02";
        public const string DefaultEnergetic = "FRONT_MANAGER_DEFAULT_03";

        public static bool IsSupported(string managerId) =>
            string.Equals(managerId, DefaultAnalysis, StringComparison.Ordinal) ||
            string.Equals(managerId, DefaultTest, StringComparison.Ordinal) ||
            string.Equals(managerId, DefaultEnergetic, StringComparison.Ordinal);
    }

    /// <summary>구단명·구단주 닉네임과 프런트 매니저 외형을 Save 범위로 보관한다.</summary>
    public sealed class OwnerProfileState
    {
        public OwnerProfileState(string nickname, string frontManagerId, string clubName = null)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                throw new ArgumentException("구단주 닉네임이 필요합니다.", nameof(nickname));
            string trimmed = nickname.Trim();
            if (trimmed.Length < 2 || trimmed.Length > 12)
                throw new ArgumentOutOfRangeException(nameof(nickname), "닉네임은 2~12자로 입력해야 합니다.");
            if (!FrontManagerIds.IsSupported(frontManagerId))
                throw new ArgumentException("지원하지 않는 프런트 매니저입니다.", nameof(frontManagerId));
            if (clubName != null)
            {
                string trimmedClubName = clubName.Trim();
                if (trimmedClubName.Length < 2 || trimmedClubName.Length > 16)
                    throw new ArgumentOutOfRangeException(nameof(clubName), "구단명은 2~16자로 입력해야 합니다.");
                ClubName = trimmedClubName;
            }
            Nickname = trimmed;
            FrontManagerId = frontManagerId.Trim();
        }

        public string Nickname { get; }
        public string FrontManagerId { get; private set; }

        /// <summary>지원하는 프런트 매니저로 교체하며 구단주와 구단 정보는 유지한다.</summary>
        public void ChangeFrontManager(string managerId)
        {
            if (!FrontManagerIds.IsSupported(managerId))
                throw new ArgumentException("지원하지 않는 프런트 매니저입니다.", nameof(managerId));
            FrontManagerId = managerId;
        }
        public string ClubName { get; } = string.Empty;

        public static OwnerProfileState CreateLegacyDefault() =>
            new OwnerProfileState("구단주", FrontManagerIds.DefaultAnalysis);
    }

    /// <summary>실제·가상 Identity와 무관하게 구단명을 원본 연도와 함께 표시한다.</summary>
    public static class OwnerClubDisplayNameFormatter
    {
        public static string Format(string identityName, int? originYear)
        {
            string displayName = identityName?.Trim() ?? string.Empty;
            if (!originYear.HasValue || originYear.Value <= 0 || displayName.Length == 0)
                return displayName;

            string year = originYear.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (displayName.StartsWith(year, StringComparison.Ordinal) &&
                (displayName.Length == year.Length ||
                 displayName[year.Length] == ' ' ||
                 displayName[year.Length] == '년'))
            {
                return displayName;
            }
            return year + " " + displayName;
        }

        /// <summary>
        /// 플레이어 구단은 새 게임에서 직접 입력한 구단명을 그대로 쓴다.
        /// 원본 연도는 역사 Identity를 구분하기 위한 표기이므로, 플레이어가 새로 이름 붙인 구단에는 붙이지 않는다.
        /// </summary>
        public static string Format(
            string identityName,
            int? originYear,
            bool isPlayerTeam,
            string playerClubName)
        {
            if (isPlayerTeam && !string.IsNullOrWhiteSpace(playerClubName))
                return playerClubName.Trim();
            return Format(identityName, originYear);
        }
    }

    /// <summary>새 게임에서 실제 지급한 카드와 리롤 횟수를 이후 감사·문의에 남긴다.</summary>
    public sealed class OwnerNewGameReceipt
    {
        private readonly string[] _mainCardIds;
        private readonly string[] _fillerCardIds;

        public OwnerNewGameReceipt(
            IReadOnlyList<string> mainCardIds,
            IReadOnlyList<string> fillerCardIds,
            int fillerRerollCount,
            ulong starterRosterSeed)
        {
            _mainCardIds = Copy(mainCardIds, nameof(mainCardIds));
            _fillerCardIds = Copy(fillerCardIds, nameof(fillerCardIds));
            if (_mainCardIds.Length + _fillerCardIds.Length != ActiveRosterCompositionRule.ActiveRosterSize)
                throw new ArgumentException("새 게임 지급 카드는 정확히 25장이어야 합니다.");
            if (fillerRerollCount < 0)
                throw new ArgumentOutOfRangeException(nameof(fillerRerollCount));
            FillerRerollCount = fillerRerollCount;
            StarterRosterSeed = starterRosterSeed;
        }

        public IReadOnlyList<string> MainCardIds => _mainCardIds;
        public IReadOnlyList<string> FillerCardIds => _fillerCardIds;
        public int FillerRerollCount { get; }
        public ulong StarterRosterSeed { get; }

        private static string[] Copy(IReadOnlyList<string> source, string name)
        {
            if (source == null) throw new ArgumentNullException(name);
            var result = new string[source.Count];
            for (int index = 0; index < result.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(source[index]))
                    throw new ArgumentException("CardId는 비어 있을 수 없습니다.", name);
                result[index] = source[index].Trim();
            }
            return result;
        }
    }

    /// <summary>프런트 매니저의 첫 로스터 배정 가이드 진행도를 Save 범위로 보관한다.</summary>
    public sealed class OwnerOnboardingState
    {
        public OwnerOnboardingState(int currentStep, bool isCompleted)
        {
            if (currentStep < 0 || currentStep > 4)
                throw new ArgumentOutOfRangeException(nameof(currentStep));
            CurrentStep = currentStep;
            IsCompleted = isCompleted;
        }

        public int CurrentStep { get; private set; }
        public bool IsCompleted { get; private set; }

        public void Advance()
        {
            if (IsCompleted) return;
            CurrentStep++;
            if (CurrentStep >= 4)
            {
                CurrentStep = 4;
                IsCompleted = true;
            }
        }

        public void Skip()
        {
            CurrentStep = 4;
            IsCompleted = true;
        }
    }

    public readonly struct OwnerNewGameTeamView
    {
        public OwnerNewGameTeamView(
            string teamSeasonKey,
            string franchiseId,
            string displayName,
            int originYear)
        {
            TeamSeasonKey = teamSeasonKey;
            FranchiseId = franchiseId;
            DisplayName = displayName;
            OriginYear = originYear;
        }

        public string TeamSeasonKey { get; }
        public string FranchiseId { get; }
        public string DisplayName { get; }
        public int OriginYear { get; }
    }

    public readonly struct OwnerNewGameCardView
    {
        public OwnerNewGameCardView(
            string cardId,
            string playerPersonId,
            string displayName,
            int originYear,
            int cost,
            PlayerType playerType,
            PlayerPosition position,
            bool isSelected,
            PitcherRole? pitcherRole = null)
        {
            CardId = cardId;
            PlayerPersonId = playerPersonId;
            DisplayName = displayName;
            OriginYear = originYear;
            Cost = cost;
            PlayerType = playerType;
            Position = position;
            IsSelected = isSelected;
            PitcherRole = pitcherRole;
        }

        public string CardId { get; }
        public string PlayerPersonId { get; }
        public string DisplayName { get; }
        public int OriginYear { get; }
        public int Cost { get; }
        public PlayerType PlayerType { get; }
        public PlayerPosition Position { get; }
        public bool IsSelected { get; }
        public PitcherRole? PitcherRole { get; }
    }

    /// <summary>구단주 새 게임 카드 Browser의 표시 범위를 정하는 순수 검색 조건이다.</summary>
    public readonly struct OwnerMainCardCandidateFilter
    {
        public OwnerMainCardCandidateFilter(
            int? originYear = null,
            PlayerPosition? position = null,
            int? cost = null,
            string playerName = null,
            PitcherRole? pitcherRole = null)
        {
            if (originYear.HasValue && originYear.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));
            if (cost.HasValue && cost.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(cost));
            OriginYear = originYear;
            Position = position;
            Cost = cost;
            PlayerName = playerName?.Trim() ?? string.Empty;
            PitcherRole = pitcherRole;
        }

        public int? OriginYear { get; }
        public PlayerPosition? Position { get; }
        public int? Cost { get; }
        public string PlayerName { get; }
        public PitcherRole? PitcherRole { get; }

        public bool Matches(OwnerNewGameCardView card)
        {
            if (OriginYear.HasValue && card.OriginYear != OriginYear.Value)
                return false;
            if (Position.HasValue && card.Position != Position.Value)
                return false;
            if (Cost.HasValue && card.Cost != Cost.Value)
                return false;
            if (PitcherRole.HasValue && card.PitcherRole != PitcherRole)
                return false;
            return string.IsNullOrEmpty(PlayerName) ||
                (!string.IsNullOrEmpty(card.DisplayName) &&
                 card.DisplayName.IndexOf(PlayerName, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    /// <summary>선택한 메인 카드에서 첫 시즌의 기준이 될 최고 Cost 선수 시즌을 결정론적으로 고른다.</summary>
    public static class OwnerStartingSeasonResolver
    {
        public static PlayerSeasonDefinition Resolve(
            IReadOnlyList<string> mainCardIds,
            WorldCardCatalog catalog)
        {
            if (mainCardIds == null) throw new ArgumentNullException(nameof(mainCardIds));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (mainCardIds.Count == 0)
                throw new ArgumentException("첫 시즌을 정할 메인 카드가 필요합니다.", nameof(mainCardIds));

            PlayerSeasonDefinition selectedSeason = null;
            string selectedCardId = string.Empty;
            for (int index = 0; index < mainCardIds.Count; index++)
            {
                PlayerCardDefinition card = catalog.GetRequiredCard(mainCardIds[index]);
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                if (selectedSeason == null ||
                    season.Cost > selectedSeason.Cost ||
                    (season.Cost == selectedSeason.Cost && season.OriginYear > selectedSeason.OriginYear) ||
                    (season.Cost == selectedSeason.Cost && season.OriginYear == selectedSeason.OriginYear &&
                     string.CompareOrdinal(card.CardId, selectedCardId) < 0))
                {
                    selectedSeason = season;
                    selectedCardId = card.CardId;
                }
            }
            return selectedSeason;
        }
    }

    /// <summary>화면 순서와 무관하게 구단·10장·매니저·닉네임·보충 로스터를 한 Draft로 관리한다.</summary>
    public sealed class OwnerNewGameFlow
    {
        private readonly IHistoricalContentProvider _contentProvider;
        private readonly HistoricalWorldRuntimeBuilder _worldBuilder;
        private readonly int _originYear;
        private readonly ulong _worldSeed;
        private readonly OwnerStarterRosterRule _rule;
        private readonly OwnerStarterRosterResolver _resolver;
        private readonly List<string> _selectedMainCardIds = new List<string>();
        private HistoricalBakedContent _content;
        private HistoricalWorldRuntimeContent _world;
        private HistoricalYearContentDefinition _year;
        private TeamSeasonDefinition _selectedTeam;
        private PlayerSeasonDefinition _startingPlayerSeason;

        public OwnerNewGameFlow(
            IHistoricalContentProvider contentProvider,
            HistoricalWorldRuntimeBuilder worldBuilder,
            int originYear,
            ulong worldSeed,
            OwnerStarterRosterRule rule)
        {
            _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
            _worldBuilder = worldBuilder ?? throw new ArgumentNullException(nameof(worldBuilder));
            if (originYear <= 0) throw new ArgumentOutOfRangeException(nameof(originYear));
            if (worldSeed == 0UL) throw new ArgumentOutOfRangeException(nameof(worldSeed));
            _originYear = originYear;
            _worldSeed = worldSeed;
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            _resolver = new OwnerStarterRosterResolver(rule);
        }

        public OwnerNewGameStep CurrentStep { get; private set; } = OwnerNewGameStep.Team;
        public string SelectedTeamSeasonKey =>
            _startingPlayerSeason?.OriginTeamSeasonKey ?? _selectedTeam?.TeamSeasonKey ?? string.Empty;
        public string SelectedFranchiseId => _selectedTeam?.FranchiseId ?? string.Empty;
        public IReadOnlyList<string> SelectedMainCardIds => _selectedMainCardIds;
        public int StartingYear => GetStartingPlayerSeason().OriginYear;
        public string FrontManagerId { get; private set; } = FrontManagerIds.DefaultAnalysis;
        public string Nickname { get; private set; } = string.Empty;
        public string ClubName { get; private set; } = string.Empty;
        public OwnerStarterRosterResult StarterRoster { get; private set; }
        public OwnerStarterRosterRule Rule => _rule;
        public WorldIdentityRegistry Identities => EnsureWorld().IdentityRegistry;
        public WorldCardCatalog CardCatalog => EnsureWorld().WorldCardCatalog;
        /// <summary>후보 카드의 해당 연도 확정 기록을 상세 화면에 제공한다.</summary>
        public WorldHistorySnapshot WorldHistory => EnsureWorld().WorldHistory;

        public IReadOnlyList<OwnerNewGameTeamView> GetTeamCandidates()
        {
            EnsureWorld();
            var result = new OwnerNewGameTeamView[_year.TeamSeasons.Count];
            for (int index = 0; index < result.Length; index++)
            {
                TeamSeasonDefinition team = _year.TeamSeasons[index];
                result[index] = new OwnerNewGameTeamView(
                    team.TeamSeasonKey,
                    team.FranchiseId,
                    _world.IdentityRegistry.GetFranchiseDisplayName(team.FranchiseId),
                    team.OriginYear);
            }
            Array.Sort(result, (left, right) => string.CompareOrdinal(left.DisplayName, right.DisplayName));
            return result;
        }

        public void SelectTeam(string teamSeasonKey)
        {
            EnsureWorld();
            _selectedTeam = null;
            for (int index = 0; index < _year.TeamSeasons.Count; index++)
            {
                if (string.Equals(_year.TeamSeasons[index].TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal))
                {
                    _selectedTeam = _year.TeamSeasons[index];
                    break;
                }
            }
            if (_selectedTeam == null)
                throw new ArgumentException("선택 가능한 구단이 아닙니다.", nameof(teamSeasonKey));
            _selectedMainCardIds.Clear();
            _startingPlayerSeason = null;
            StarterRoster = null;
            CurrentStep = OwnerNewGameStep.MainCards;
        }

        public IReadOnlyList<OwnerNewGameCardView> GetMainCardCandidates()
        {
            return GetMainCardCandidates(default);
        }

        /// <summary>선택 가능성은 바꾸지 않고 연도·포지션·Cost·이름에 맞는 카드만 반환한다.</summary>
        public IReadOnlyList<OwnerNewGameCardView> GetMainCardCandidates(OwnerMainCardCandidateFilter filter)
        {
            EnsureSelectedTeam();
            WorldCardCatalog catalog = CardCatalog;
            var selected = new HashSet<string>(_selectedMainCardIds, StringComparer.Ordinal);
            var result = new List<OwnerNewGameCardView>();
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = catalog.Cards[index];
                if (card.Edition != PlayerCardEdition.Normal)
                    continue;
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                if (!string.Equals(season.OriginFranchiseId, _selectedTeam.FranchiseId, StringComparison.Ordinal))
                    continue;
                var view = new OwnerNewGameCardView(
                    card.CardId,
                    season.PlayerPersonId,
                    _world.IdentityRegistry.GetPlayerDisplayName(season.PlayerPersonId),
                    season.OriginYear,
                    season.Cost,
                    season.PlayerType,
                    season.Position,
                    selected.Contains(card.CardId),
                    season.PlayerType == PlayerType.Pitcher ? season.PitcherRole : null);
                if (filter.Matches(view))
                    result.Add(view);
            }
            result.Sort((left, right) =>
            {
                int cost = right.Cost.CompareTo(left.Cost);
                if (cost != 0) return cost;
                int year = right.OriginYear.CompareTo(left.OriginYear);
                return year != 0 ? year : string.CompareOrdinal(left.DisplayName, right.DisplayName);
            });
            return result;
        }

        public OwnerMainCardSelectionStatus ToggleMainCard(string cardId)
        {
            EnsureSelectedTeam();
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId가 필요합니다.", nameof(cardId));
            string normalizedCardId = cardId.Trim();
            int existing = _selectedMainCardIds.FindIndex(
                id => string.Equals(id, normalizedCardId, StringComparison.Ordinal));
            if (existing >= 0)
                _selectedMainCardIds.RemoveAt(existing);
            else
            {
                if (_selectedMainCardIds.Count >= _rule.MainCardCount)
                    throw new InvalidOperationException($"메인 카드는 {_rule.MainCardCount}장까지만 선택할 수 있습니다.");
                _selectedMainCardIds.Add(normalizedCardId);
                OwnerMainCardSelectionStatus partial = _resolver.ValidatePartialMainCards(
                    _selectedTeam.FranchiseId,
                    _selectedMainCardIds,
                    CardCatalog);
                if (!partial.IsValid)
                {
                    _selectedMainCardIds.RemoveAt(_selectedMainCardIds.Count - 1);
                    throw new InvalidOperationException(partial.Message);
                }
            }
            _startingPlayerSeason = null;
            StarterRoster = null;
            return GetMainCardSelectionStatus();
        }

        public OwnerMainCardSelectionStatus GetMainCardSelectionStatus()
        {
            EnsureSelectedTeam();
            return _resolver.ValidateMainCards(_selectedTeam.FranchiseId, _selectedMainCardIds, CardCatalog);
        }

        public void ContinueFromMainCards()
        {
            OwnerMainCardSelectionStatus status = GetMainCardSelectionStatus();
            if (!status.IsValid) throw new InvalidOperationException(status.Message);
            _startingPlayerSeason = OwnerStartingSeasonResolver.Resolve(_selectedMainCardIds, CardCatalog);
            GetStartingTeam();
            CurrentStep = OwnerNewGameStep.FrontManager;
        }

        public void SelectFrontManager(string frontManagerId)
        {
            if (!FrontManagerIds.IsSupported(frontManagerId))
                throw new ArgumentException("지원하지 않는 프런트 매니저입니다.", nameof(frontManagerId));
            FrontManagerId = frontManagerId;
            CurrentStep = OwnerNewGameStep.Nickname;
        }

        public void SetNickname(string nickname)
        {
            string validatedNickname = new OwnerProfileState(nickname, FrontManagerId).Nickname;
            OwnerStarterRosterResult starterRoster = ResolveStarterRoster(0);
            Nickname = validatedNickname;
            StarterRoster = starterRoster;
            CurrentStep = OwnerNewGameStep.StarterRosterReview;
        }

        /// <summary>새 진행에서 사용할 구단명과 구단주 이름을 함께 확정한다.</summary>
        public void SetProfile(string clubName, string nickname)
        {
            var profile = new OwnerProfileState(nickname, FrontManagerId, clubName);
            OwnerStarterRosterResult starterRoster = ResolveStarterRoster(0);
            ClubName = profile.ClubName;
            Nickname = profile.Nickname;
            StarterRoster = starterRoster;
            CurrentStep = OwnerNewGameStep.StarterRosterReview;
        }

        public OwnerStarterRosterResult RerollFiller()
        {
            if (StarterRoster == null)
                throw new InvalidOperationException("먼저 초기 보충 로스터를 생성해야 합니다.");
            if (StarterRoster.RerollIndex >= _rule.MaximumFillerRerolls)
                throw new InvalidOperationException("보충 선수 리롤 횟수를 모두 사용했습니다.");
            return GenerateStarterRoster(StarterRoster.RerollIndex + 1);
        }

        public void GoBack()
        {
            CurrentStep = CurrentStep switch
            {
                OwnerNewGameStep.MainCards => OwnerNewGameStep.Team,
                OwnerNewGameStep.FrontManager => OwnerNewGameStep.MainCards,
                OwnerNewGameStep.Nickname => OwnerNewGameStep.FrontManager,
                OwnerNewGameStep.StarterRosterReview => OwnerNewGameStep.Nickname,
                _ => OwnerNewGameStep.Team
            };
        }

        public OwnerProfileState CreateProfile() => new OwnerProfileState(
            Nickname,
            FrontManagerId,
            string.IsNullOrWhiteSpace(ClubName) ? null : ClubName);

        public OwnerNewGameReceipt CreateReceipt()
        {
            if (StarterRoster == null)
                throw new InvalidOperationException("확정할 스타터 로스터가 없습니다.");
            return new OwnerNewGameReceipt(
                StarterRoster.MainCardIds,
                StarterRoster.FillerCardIds,
                StarterRoster.RerollIndex,
                StarterRoster.ResultSeed);
        }

        public void Complete()
        {
            if (StarterRoster == null) throw new InvalidOperationException("스타터 로스터가 없습니다.");
            CurrentStep = OwnerNewGameStep.Completed;
        }

        private OwnerStarterRosterResult GenerateStarterRoster(int rerollIndex)
        {
            StarterRoster = ResolveStarterRoster(rerollIndex);
            return StarterRoster;
        }

        private OwnerStarterRosterResult ResolveStarterRoster(int rerollIndex)
        {
            EnsureSelectedTeam();
            return _resolver.Resolve(
                GetStartingTeam(),
                _selectedMainCardIds,
                CardCatalog,
                _worldSeed,
                rerollIndex);
        }

        private PlayerSeasonDefinition GetStartingPlayerSeason()
        {
            EnsureSelectedTeam();
            if (_startingPlayerSeason != null)
                return _startingPlayerSeason;
            OwnerMainCardSelectionStatus status = GetMainCardSelectionStatus();
            if (!status.IsValid)
                throw new InvalidOperationException(status.Message);
            _startingPlayerSeason = OwnerStartingSeasonResolver.Resolve(_selectedMainCardIds, CardCatalog);
            return _startingPlayerSeason;
        }

        private TeamSeasonDefinition GetStartingTeam()
        {
            PlayerSeasonDefinition startingSeason = GetStartingPlayerSeason();
            if (!string.Equals(
                    startingSeason.OriginFranchiseId,
                    _selectedTeam.FranchiseId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("첫 시즌 기준 선수와 선택 구단의 계보가 다릅니다.");
            }

            HistoricalYearContentDefinition startingYear = _content.GetYear(startingSeason.OriginYear);
            for (int index = 0; index < startingYear.TeamSeasons.Count; index++)
            {
                TeamSeasonDefinition team = startingYear.TeamSeasons[index];
                if (string.Equals(
                        team.TeamSeasonKey,
                        startingSeason.OriginTeamSeasonKey,
                        StringComparison.Ordinal))
                    return team;
            }
            throw new InvalidOperationException(
                $"{startingSeason.OriginYear}년 첫 시즌 구단을 Historical Content에서 찾을 수 없습니다.");
        }

        private HistoricalWorldRuntimeContent EnsureWorld()
        {
            if (_world != null) return _world;
            _content = _contentProvider.Load() ?? throw new InvalidOperationException("Historical Content가 없습니다.");
            _year = _content.GetYear(_originYear);
            _world = _worldBuilder.GetOrBuild(_content, WorldRecordMode.SimulatedHistory, _worldSeed);
            return _world;
        }

        private void EnsureSelectedTeam()
        {
            EnsureWorld();
            if (_selectedTeam == null)
                throw new InvalidOperationException("먼저 구단을 선택해야 합니다.");
        }
    }
}
