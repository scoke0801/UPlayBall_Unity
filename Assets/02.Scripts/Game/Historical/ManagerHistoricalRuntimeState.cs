using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Game.Guide;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    /// <summary>구단주 모드 한 세이브가 소유하는 역사·리그·로스터·플레이어 구단 경제 상태다.</summary>
    public sealed partial class ManagerHistoricalRuntimeState
    {
        private readonly CurrentRosterState[] _rosters;
        private readonly List<OwnedPlayerCardState> _ownedCards;
        private readonly Dictionary<string, CurrentRosterState> _rostersByTeamSeasonKey;
        private readonly Dictionary<string, OwnedPlayerCardState> _ownedCardsById;

        public ManagerHistoricalRuntimeState(
            string playerTeamSeasonKey,
            HistoricalContentReference contentReference,
            WorldIdentityRegistry identityRegistry,
            WorldHistorySnapshot worldHistory,
            WorldCardCatalog worldCardCatalog,
            LeagueInstance league,
            IReadOnlyList<CurrentRosterState> rosters,
            IReadOnlyList<OwnedPlayerCardState> ownedCards,
            ManagerEconomyState economy,
            ManagerModeRuntimeState managerMode = null,
            TacticCollectionState tacticCollection = null,
            ShopPurchaseHistoryState shopPurchaseHistory = null,
            GuideRepeatStateData guideRepeatState = null,
            OwnerProfileState ownerProfile = null,
            OwnerNewGameReceipt newGameReceipt = null,
            OwnerOnboardingState onboarding = null,
            OwnerPlayerGrowthState playerGrowth = null)
        {
            PlayerTeamSeasonKey = RequireId(playerTeamSeasonKey, nameof(playerTeamSeasonKey));
            ContentReference = contentReference ?? throw new ArgumentNullException(nameof(contentReference));
            IdentityRegistry = identityRegistry ?? throw new ArgumentNullException(nameof(identityRegistry));
            WorldHistory = worldHistory ?? throw new ArgumentNullException(nameof(worldHistory));
            WorldCardCatalog = worldCardCatalog ?? throw new ArgumentNullException(nameof(worldCardCatalog));
            League = league ?? throw new ArgumentNullException(nameof(league));
            Economy = economy ?? throw new ArgumentNullException(nameof(economy));
            ManagerMode = managerMode;
            TacticCollection = tacticCollection ?? new TacticCollectionState();
            ShopPurchaseHistory = shopPurchaseHistory ?? new ShopPurchaseHistoryState();
            GuideRepeatState = guideRepeatState ?? new GuideRepeatStateData();
            OwnerProfile = ownerProfile ?? OwnerProfileState.CreateLegacyDefault();
            NewGameReceipt = newGameReceipt;
            Onboarding = onboarding ?? new OwnerOnboardingState(0, false);
            PlayerGrowth = playerGrowth ?? new OwnerPlayerGrowthState();

            if (!Contains(league.RegularTeamSeasonKeys, PlayerTeamSeasonKey))
                throw new ArgumentException("플레이어 구단은 해당 연도 정규 Franchise 중 하나여야 합니다.", nameof(playerTeamSeasonKey));

            _rosters = CopyAndValidateRosters(rosters, worldCardCatalog, league);
            _rostersByTeamSeasonKey = IndexRosters(_rosters);
            _ownedCards = CopyAndValidateOwnedCards(ownedCards, worldCardCatalog);
            _ownedCardsById = IndexOwnedCards(_ownedCards);
            ValidateSpecialEditionActivation();
            ValidatePlayerRosterOwnership();
            ValidatePlayerGrowth();
            ValidateSpecialCompositeOverlap();
            if (ManagerMode != null &&
                !string.Equals(ManagerMode.ClubOperation.TeamSeasonKey, PlayerTeamSeasonKey, StringComparison.Ordinal))
                throw new ArgumentException("ManagerMode 상태가 플레이어 구단과 일치하지 않습니다.", nameof(managerMode));
        }

        public string PlayerTeamSeasonKey { get; }
        public HistoricalContentReference ContentReference { get; }
        public WorldIdentityRegistry IdentityRegistry { get; }
        public WorldHistorySnapshot WorldHistory { get; }
        public WorldAwardRecord WorldAwardRecord => WorldHistory.Awards;
        public WorldCardCatalog WorldCardCatalog { get; }
        public LeagueInstance League { get; private set; }
        public IReadOnlyList<CurrentRosterState> Rosters => _rosters;
        public IReadOnlyList<OwnedPlayerCardState> OwnedCards => _ownedCards;
        public ManagerEconomyState Economy { get; }
        public ManagerModeRuntimeState ManagerMode { get; }
        public TacticCollectionState TacticCollection { get; }
        public ShopPurchaseHistoryState ShopPurchaseHistory { get; }
        public GuideRepeatStateData GuideRepeatState { get; private set; }
        public OwnerProfileState OwnerProfile { get; }
        public OwnerNewGameReceipt NewGameReceipt { get; }
        public OwnerOnboardingState Onboarding { get; }
        public OwnerPlayerGrowthState PlayerGrowth { get; }
        public bool HasManagerMode => ManagerMode != null;

        /// <summary>GuideManager가 확정한 Save 범위 반복 상태를 저장 Aggregate에 동기화한다.</summary>
        public void SetGuideRepeatState(GuideRepeatStateData state)
        {
            GuideRepeatState = state ?? new GuideRepeatStateData();
        }

        /// <summary>참가 구단과 로스터를 그대로 유지한 채 다음 시즌 리그 등급만 교체한다.</summary>
        public void MoveLeagueTo(LeagueGrade nextGrade)
        {
            if (!Enum.IsDefined(typeof(LeagueGrade), nextGrade))
                throw new ArgumentOutOfRangeException(nameof(nextGrade));
            if (League.Grade == nextGrade)
                return;
            League = new LeagueInstance(
                League.LeagueInstanceId,
                nextGrade,
                League.RegularTeamSeasonKeys,
                League.SpecialCompositeTeams);
        }

        public CurrentRosterState GetRoster(string teamSeasonKey)
        {
            string key = RequireId(teamSeasonKey, nameof(teamSeasonKey));
            if (!_rostersByTeamSeasonKey.TryGetValue(key, out CurrentRosterState roster))
                throw new KeyNotFoundException($"TeamSeasonKey {key}의 CurrentRosterState가 없습니다.");
            return roster;
        }

        public bool TryGetOwnedCard(string cardId, out OwnedPlayerCardState ownedCard)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                ownedCard = null;
                return false;
            }
            return _ownedCardsById.TryGetValue(cardId.Trim(), out ownedCard);
        }

        /// <summary>
        /// 스카우트로 카드를 획득한다. 이미 보유한 카드면 중복 수를 올려 강화 재료로 남긴다.
        /// </summary>
        /// <returns>처음 획득한 카드면 true다.</returns>
        public bool AcquireCard(string cardId)
        {
            string id = RequireId(cardId, nameof(cardId));
            if (!WorldCardCatalog.TryGetCard(id, out _))
                throw new ArgumentException("WorldCardCatalog에 없는 카드는 획득할 수 없습니다.", nameof(cardId));
            if (_ownedCardsById.TryGetValue(id, out OwnedPlayerCardState owned))
            {
                owned.AddDuplicate();
                return false;
            }
            var acquired = new OwnedPlayerCardState(id);
            _ownedCards.Add(acquired);
            _ownedCardsById.Add(id, acquired);
            return true;
        }

        /// <summary>AI 구단은 카드 소유 경제를 갖지 않으므로 플레이어 구단 여부만 명시적으로 반환한다.</summary>
        public bool HasOwnedEconomy(string teamSeasonKey)
        {
            return string.Equals(
                RequireId(teamSeasonKey, nameof(teamSeasonKey)),
                PlayerTeamSeasonKey,
                StringComparison.Ordinal);
        }

        private void ValidatePlayerGrowth()
        {
            var equipped = new HashSet<int>();
            for (int cardIndex = 0; cardIndex < _ownedCards.Count; cardIndex++)
            {
                OwnedPlayerCardState card = _ownedCards[cardIndex];
                for (int blockIndex = 0; blockIndex < card.SkillBoard.Placements.Count; blockIndex++)
                {
                    int instanceId = card.SkillBoard.Placements[blockIndex].Instance.InstanceId;
                    if (!PlayerGrowth.Inventory.Contains(instanceId))
                        throw new ArgumentException("카드 성장판이 인벤토리에 없는 블록을 참조합니다.", nameof(PlayerGrowth));
                    if (!string.Equals(
                            PlayerGrowth.Inventory.GetRequired(instanceId).DefinitionId,
                            card.SkillBoard.Placements[blockIndex].Instance.DefinitionId,
                            StringComparison.Ordinal))
                        throw new ArgumentException("성장판과 인벤토리의 블록 정의가 일치하지 않습니다.", nameof(PlayerGrowth));
                    if (!equipped.Add(instanceId))
                        throw new ArgumentException("하나의 스킬 블록을 여러 카드에 장착할 수 없습니다.", nameof(PlayerGrowth));
                }
            }

            var studyingCards = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < PlayerGrowth.StudyProjects.Count; index++)
            {
                string cardId = PlayerGrowth.StudyProjects[index].CardId;
                if (!_ownedCardsById.ContainsKey(cardId) || !studyingCards.Add(cardId))
                    throw new ArgumentException("유학 중인 카드 상태가 보유 카드와 일치하지 않습니다.", nameof(PlayerGrowth));
            }
        }

        private void ValidatePlayerRosterOwnership()
        {
            CurrentRosterState playerRoster = GetRoster(PlayerTeamSeasonKey);
            for (int index = 0; index < playerRoster.Entries.Count; index++)
            {
                string cardId = playerRoster.Entries[index].CardId;
                if (!_ownedCardsById.ContainsKey(cardId))
                    throw new ArgumentException("플레이어 구단의 1군 카드는 OwnedPlayerCardState에 존재해야 합니다.", nameof(_ownedCards));
            }
        }

        private void ValidateSpecialEditionActivation()
        {
            for (int index = 0; index < WorldCardCatalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = WorldCardCatalog.Cards[index];
                bool isActivated;
                switch (card.Edition)
                {
                    case PlayerCardEdition.Normal:
                        continue;
                    case PlayerCardEdition.AllStar:
                        isActivated = WorldAwardRecord.HasAward(card.PlayerSeasonId, WorldAwardType.AllStar);
                        break;
                    case PlayerCardEdition.GoldenGlove:
                        isActivated = WorldAwardRecord.HasAward(card.PlayerSeasonId, WorldAwardType.GoldenGlove);
                        break;
                    case PlayerCardEdition.Mvp:
                        isActivated = WorldAwardRecord.HasAward(card.PlayerSeasonId, WorldAwardType.RegularSeasonMvp) ||
                                      WorldAwardRecord.HasAward(card.PlayerSeasonId, WorldAwardType.AllStarGameMvp) ||
                                      WorldAwardRecord.HasAward(card.PlayerSeasonId, WorldAwardType.PostseasonMvp);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(card.Edition));
                }
                if (!isActivated)
                    throw new ArgumentException("특수 Edition은 해당 WorldAwardRecord가 존재할 때만 활성화할 수 있습니다.", nameof(WorldCardCatalog));
            }
        }

        private void ValidateSpecialCompositeOverlap()
        {
            if (League.SpecialCompositeTeams.Count == 0)
                return;

            var assignedPlayerSeasons = new HashSet<string>(StringComparer.Ordinal);
            for (int teamIndex = 0; teamIndex < League.SpecialCompositeTeams.Count; teamIndex++)
            {
                CurrentRosterState roster = GetRoster(League.SpecialCompositeTeams[teamIndex].TeamSeasonKey);
                for (int rosterIndex = 0; rosterIndex < roster.Entries.Count; rosterIndex++)
                {
                    if (!assignedPlayerSeasons.Add(roster.Entries[rosterIndex].PlayerSeasonId))
                        throw new ArgumentException("특수 합성팀의 최종 로스터는 PlayerSeasonId가 겹칠 수 없습니다.", nameof(_rosters));
                }
            }
        }

        private static CurrentRosterState[] CopyAndValidateRosters(
            IReadOnlyList<CurrentRosterState> source,
            WorldCardCatalog catalog,
            LeagueInstance league)
        {
            int expectedCount = league.ParticipantTeamCount;
            if (source == null || source.Count != expectedCount)
                throw new ArgumentException("모든 리그 참가팀의 CurrentRosterState가 필요합니다.", nameof(source));

            var result = new CurrentRosterState[source.Count];
            var teamKeys = new HashSet<string>(StringComparer.Ordinal);
            var validator = new ActiveRosterValidator();
            for (int index = 0; index < source.Count; index++)
            {
                CurrentRosterState roster = source[index]
                    ?? throw new ArgumentException("null CurrentRosterState가 있습니다.", nameof(source));
                if (!IsParticipant(league, roster.TeamSeasonKey))
                    throw new ArgumentException("리그 참가팀이 아닌 로스터가 포함되었습니다.", nameof(source));
                if (!teamKeys.Add(roster.TeamSeasonKey))
                    throw new ArgumentException("TeamSeasonKey별 CurrentRosterState는 하나만 존재해야 합니다.", nameof(source));

                RosterValidationResult validation = validator.Validate(roster);
                if (!validation.IsValid)
                    throw new ArgumentException(
                        $"{roster.TeamSeasonKey} 로스터가 ActiveRoster 계약을 위반했습니다: {validation.Issues[0].Code}",
                        nameof(source));
                ValidateRosterCards(roster, catalog);
                result[index] = roster;
            }
            return result;
        }

        private static void ValidateRosterCards(CurrentRosterState roster, WorldCardCatalog catalog)
        {
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (!catalog.TryGetCard(entry.CardId, out PlayerCardDefinition card))
                    throw new ArgumentException("CurrentRosterState가 WorldCardCatalog에 없는 카드를 참조합니다.", nameof(roster));
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                if (!string.Equals(card.PlayerSeasonId, entry.PlayerSeasonId, StringComparison.Ordinal) ||
                    !string.Equals(season.PlayerPersonId, entry.PlayerPersonId, StringComparison.Ordinal) ||
                    season.RegistrationType != entry.RegistrationType)
                {
                    throw new ArgumentException("로스터 항목과 WorldCardCatalog 원본의 선수 식별 정보가 일치하지 않습니다.", nameof(roster));
                }
            }
        }

        private static List<OwnedPlayerCardState> CopyAndValidateOwnedCards(
            IReadOnlyList<OwnedPlayerCardState> source,
            WorldCardCatalog catalog)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var result = new List<OwnedPlayerCardState>(source.Count);
            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                OwnedPlayerCardState owned = source[index]
                    ?? throw new ArgumentException("null OwnedPlayerCardState가 있습니다.", nameof(source));
                if (!catalog.TryGetCard(owned.CardId, out _))
                    throw new ArgumentException("OwnedPlayerCardState가 WorldCardCatalog에 없는 카드를 참조합니다.", nameof(source));
                if (!cardIds.Add(owned.CardId))
                    throw new ArgumentException("카드 소유 상태는 CardId별 하나만 존재해야 합니다.", nameof(source));
                result.Add(owned);
            }
            return result;
        }

        private static Dictionary<string, CurrentRosterState> IndexRosters(IReadOnlyList<CurrentRosterState> rosters)
        {
            var result = new Dictionary<string, CurrentRosterState>(rosters.Count, StringComparer.Ordinal);
            for (int index = 0; index < rosters.Count; index++)
                result.Add(rosters[index].TeamSeasonKey, rosters[index]);
            return result;
        }

        private static Dictionary<string, OwnedPlayerCardState> IndexOwnedCards(IReadOnlyList<OwnedPlayerCardState> cards)
        {
            var result = new Dictionary<string, OwnedPlayerCardState>(cards.Count, StringComparer.Ordinal);
            for (int index = 0; index < cards.Count; index++)
                result.Add(cards[index].CardId, cards[index]);
            return result;
        }

        private static bool IsParticipant(LeagueInstance league, string teamSeasonKey)
        {
            if (Contains(league.RegularTeamSeasonKeys, teamSeasonKey))
                return true;
            for (int index = 0; index < league.SpecialCompositeTeams.Count; index++)
                if (string.Equals(league.SpecialCompositeTeams[index].TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(values[index], value, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>구단주 모드 한 월드의 기록 방식, 대상 연도, 플레이어 구단을 명시한다.</summary>
    public readonly struct ManagerHistoricalNewGameRequest
    {
        public ManagerHistoricalNewGameRequest(
            WorldRecordMode recordMode,
            ulong worldHistorySeed,
            int originYear,
            string leagueInstanceId,
            string playerTeamSeasonKey,
            ManagerEconomyState initialEconomy,
            CurrentRosterState starterRoster = null,
            OwnerProfileState ownerProfile = null,
            OwnerNewGameReceipt newGameReceipt = null)
        {
            if (originYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));
            if (string.IsNullOrWhiteSpace(leagueInstanceId))
                throw new ArgumentException("LeagueInstanceId가 필요합니다.", nameof(leagueInstanceId));
            if (string.IsNullOrWhiteSpace(playerTeamSeasonKey))
                throw new ArgumentException("플레이어 TeamSeasonKey가 필요합니다.", nameof(playerTeamSeasonKey));

            RecordMode = recordMode;
            WorldHistorySeed = worldHistorySeed;
            OriginYear = originYear;
            LeagueInstanceId = leagueInstanceId.Trim();
            PlayerTeamSeasonKey = playerTeamSeasonKey.Trim();
            InitialEconomy = initialEconomy ?? throw new ArgumentNullException(nameof(initialEconomy));
            if (starterRoster != null && !string.Equals(starterRoster.TeamSeasonKey, PlayerTeamSeasonKey, StringComparison.Ordinal))
                throw new ArgumentException("StarterRoster는 플레이어 구단과 일치해야 합니다.", nameof(starterRoster));
            StarterRoster = starterRoster;
            OwnerProfile = ownerProfile;
            NewGameReceipt = newGameReceipt;
        }

        public WorldRecordMode RecordMode { get; }
        public ulong WorldHistorySeed { get; }
        public int OriginYear { get; }
        public string LeagueInstanceId { get; }
        public string PlayerTeamSeasonKey { get; }
        public ManagerEconomyState InitialEconomy { get; }
        public CurrentRosterState StarterRoster { get; }
        public OwnerProfileState OwnerProfile { get; }
        public OwnerNewGameReceipt NewGameReceipt { get; }
    }

    /// <summary>Baked Content부터 World Record, 합성팀, 저장 가능한 구단주 모드 상태까지 한 번에 조립한다.</summary>
    public sealed class ManagerHistoricalNewGameService
    {
        private const ulong LeagueFillerSelectionStream = 0x4F574E5246494C4CUL;

        private readonly IHistoricalContentProvider _contentProvider;
        private readonly HistoricalWorldRuntimeBuilder _worldBuilder;
        private readonly BalanceTable _balance;

        public ManagerHistoricalNewGameService(
            IHistoricalContentProvider contentProvider,
            HistoricalWorldRuntimeBuilder worldBuilder,
            BalanceTable balance = null)
        {
            _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
            _worldBuilder = worldBuilder ?? throw new ArgumentNullException(nameof(worldBuilder));
            _balance = balance ?? BalanceTable.CreateDefault();
        }

        public ManagerHistoricalRuntimeState Create(ManagerHistoricalNewGameRequest request)
        {
            if (request.RecordMode != WorldRecordMode.SimulatedHistory)
            {
                throw new InvalidOperationException(
                    "Production 구단주 모드 새 게임은 SimulatedHistory만 지원합니다. OriginalHistory는 Legacy 검증 전용입니다.");
            }
            HistoricalBakedContent bakedContent = _contentProvider.Load()
                ?? throw new InvalidOperationException("Runtime Historical Content Provider가 null을 반환했습니다.");
            HistoricalYearContentDefinition year = bakedContent.GetYear(request.OriginYear);
            HistoricalWorldRuntimeContent world = _worldBuilder.GetOrBuild(
                bakedContent,
                request.RecordMode,
                request.WorldHistorySeed);
            SpecialCompositeTeamDefinition[] selectedComposites = SelectCompositeTeams(
                year,
                world,
                request.WorldHistorySeed);
            LeagueInstance league = CreateLeague(request, year, selectedComposites);
            CurrentRosterState[] rosters = CreateRosters(
                year,
                selectedComposites,
                world.WorldCardCatalog,
                request.PlayerTeamSeasonKey,
                request.StarterRoster);
            OwnedPlayerCardState[] ownedCards = CreateInitialOwnedCards(
                request.PlayerTeamSeasonKey,
                rosters);
            StaffCatalog staffCatalog = CreateStaffCatalog(world.IdentityRegistry, request.WorldHistorySeed);
            ManagerModeRuntimeState managerMode = ManagerModeRuntimeFactory.CreateInitial(
                request.PlayerTeamSeasonKey,
                request.OriginYear,
                request.WorldHistorySeed,
                league,
                rosters,
                staffCatalog,
                _balance);

            return new ManagerHistoricalRuntimeState(
                request.PlayerTeamSeasonKey,
                world.ContentReference,
                world.IdentityRegistry,
                world.WorldHistory,
                world.WorldCardCatalog,
                league,
                rosters,
                ownedCards,
                request.InitialEconomy,
                managerMode,
                ownerProfile: request.OwnerProfile,
                newGameReceipt: request.NewGameReceipt,
                onboarding: new OwnerOnboardingState(0, false));
        }

        private StaffCatalog CreateStaffCatalog(WorldIdentityRegistry identities, ulong worldSeed)
        {
            int countPerRole = _balance.Staff.Market.OffseasonOfferCount;
            int requiredNameCount = checked(Enum.GetValues(typeof(StaffRole)).Length * countPerRole);
            if (identities.PlayerIdentities.Count < requiredNameCount)
                throw new InvalidOperationException("가상 스태프 생성에 필요한 이름 후보가 부족합니다.");
            var sorted = new WorldPlayerIdentity[identities.PlayerIdentities.Count];
            for (int index = 0; index < sorted.Length; index++) sorted[index] = identities.PlayerIdentities[index];
            Array.Sort(sorted, (left, right) => string.CompareOrdinal(left.PlayerPersonId, right.PlayerPersonId));
            var names = new string[requiredNameCount];
            for (int index = 0; index < names.Length; index++) names[index] = sorted[index].DisplayName;
            return new StaffCatalogGenerator().Generate(
                new StaffNameCatalog(names),
                countPerRole,
                worldSeed,
                _balance.Staff);
        }

        private static LeagueInstance CreateLeague(
            ManagerHistoricalNewGameRequest request,
            HistoricalYearContentDefinition year,
            IReadOnlyList<SpecialCompositeTeamDefinition> composites)
        {
            var regularKeys = new string[year.TeamSeasons.Count];
            for (int index = 0; index < regularKeys.Length; index++)
                regularKeys[index] = year.TeamSeasons[index].TeamSeasonKey;

            var registrations = new SpecialCompositeTeamRegistration[composites.Count];
            for (int index = 0; index < registrations.Length; index++)
            {
                SpecialCompositeTeamDefinition team = composites[index];
                registrations[index] = new SpecialCompositeTeamRegistration(
                    team.TeamSeasonKey,
                    team.OriginYear,
                    team.TeamType);
            }

            // 모든 Baked TeamSeason은 새 World에서 Rookie부터 시작한다.
            return new LeagueInstance(
                request.LeagueInstanceId,
                LeagueGrade.Rookie,
                regularKeys,
                registrations);
        }

        /// <summary>실제 구단을 우선 배치하고 빈 슬롯만 World Seed 기반 특수팀으로 채운다.</summary>
        private static SpecialCompositeTeamDefinition[] SelectCompositeTeams(
            HistoricalYearContentDefinition year,
            HistoricalWorldRuntimeContent world,
            ulong worldSeed)
        {
            if (!LeagueInstance.IsSupportedRegularFranchiseTeamCount(year.TeamSeasons.Count))
            {
                throw new InvalidOperationException(
                    $"{year.Year} 정규 Franchise 구단 수 {year.TeamSeasons.Count}개는 지원 범위가 아닙니다.");
            }

            int requiredCount = LeagueInstance.MaximumRegularFranchiseTeamCount - year.TeamSeasons.Count;
            if (requiredCount == 0)
                return Array.Empty<SpecialCompositeTeamDefinition>();

            SpecialCompositeTeamSet set = world.GetSpecialCompositeTeamSet(year.Year);
            if (set.Teams.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"{year.Year} 리그의 빈 슬롯 {requiredCount}개를 채울 특수 합성팀이 부족합니다.");
            }

            var candidates = new SpecialCompositeTeamDefinition[set.Teams.Count];
            for (int index = 0; index < candidates.Length; index++)
                candidates[index] = set.Teams[index];
            Array.Sort(candidates, (left, right) => left.TeamType.CompareTo(right.TeamType));

            ulong selectionSeed = DeterministicSeed.Derive(
                DeterministicSeed.Derive(worldSeed, LeagueFillerSelectionStream),
                unchecked((ulong)year.Year));
            var random = new Pcg32Random(selectionSeed);
            for (int index = candidates.Length - 1; index > 0; index--)
            {
                int selectedIndex = (int)(random.NextDouble() * (index + 1));
                SpecialCompositeTeamDefinition selected = candidates[index];
                candidates[index] = candidates[selectedIndex];
                candidates[selectedIndex] = selected;
            }

            var result = new SpecialCompositeTeamDefinition[requiredCount];
            Array.Copy(candidates, result, requiredCount);
            Array.Sort(result, (left, right) => left.TeamType.CompareTo(right.TeamType));
            return result;
        }

        private static CurrentRosterState[] CreateRosters(
            HistoricalYearContentDefinition year,
            IReadOnlyList<SpecialCompositeTeamDefinition> composites,
            WorldCardCatalog catalog,
            string playerTeamSeasonKey,
            CurrentRosterState starterRoster)
        {
            var result = new CurrentRosterState[
                year.TeamSeasons.Count + composites.Count];
            int outputIndex = 0;
            for (int index = 0; index < year.TeamSeasons.Count; index++)
            {
                TeamSeasonDefinition team = year.TeamSeasons[index];
                result[outputIndex++] = starterRoster != null &&
                    string.Equals(team.TeamSeasonKey, playerTeamSeasonKey, StringComparison.Ordinal)
                        ? starterRoster
                        : CreateRegularRoster(team, catalog);
            }
            for (int index = 0; index < composites.Count; index++)
                result[outputIndex++] = CreateCompositeRoster(composites[index], catalog);
            return result;
        }

        private static CurrentRosterState CreateRegularRoster(
            TeamSeasonDefinition team,
            WorldCardCatalog catalog)
        {
            if (team.Core25CardIds.Count != ActiveRosterCompositionRule.ActiveRosterSize)
                throw new InvalidOperationException($"{team.TeamSeasonKey} Core25가 정확히 25명이 아닙니다.");

            var entries = new ActiveRosterEntry[team.Core25CardIds.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                string cardId = team.Core25CardIds[index];
                if (!catalog.TryGetCard(cardId, out PlayerCardDefinition card) ||
                    card.Edition != PlayerCardEdition.Normal)
                {
                    throw new InvalidOperationException(
                        $"{team.TeamSeasonKey} Core25가 WorldCardCatalog의 Normal 카드를 참조하지 않습니다.");
                }
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                entries[index] = new ActiveRosterEntry(
                    card.CardId,
                    season.PlayerSeasonId,
                    season.PlayerPersonId,
                    season.RegistrationType,
                    GetCore25Role(index));
            }
            return new CurrentRosterState(team.TeamSeasonKey, entries);
        }

        private static CurrentRosterState CreateCompositeRoster(
            SpecialCompositeTeamDefinition team,
            WorldCardCatalog catalog)
        {
            var entries = new ActiveRosterEntry[team.Roster.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                SpecialCompositeRosterEntry source = team.Roster[index];
                if (!catalog.TryGetCard(source.CardId, out PlayerCardDefinition card) ||
                    !string.Equals(card.PlayerSeasonId, source.PlayerSeasonId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"{team.TeamSeasonKey} 합성 로스터 카드가 WorldCardCatalog와 다릅니다.");
                }
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                entries[index] = new ActiveRosterEntry(
                    card.CardId,
                    season.PlayerSeasonId,
                    season.PlayerPersonId,
                    season.RegistrationType,
                    source.Role);
            }
            return new CurrentRosterState(team.TeamSeasonKey, entries);
        }

        private static OwnedPlayerCardState[] CreateInitialOwnedCards(
            string playerTeamSeasonKey,
            IReadOnlyList<CurrentRosterState> rosters)
        {
            CurrentRosterState playerRoster = null;
            for (int index = 0; index < rosters.Count; index++)
            {
                if (string.Equals(
                        rosters[index].TeamSeasonKey,
                        playerTeamSeasonKey,
                        StringComparison.Ordinal))
                {
                    playerRoster = rosters[index];
                    break;
                }
            }
            if (playerRoster == null)
                throw new ArgumentException("플레이어 구단이 해당 연도의 정규 Franchise에 없습니다.", nameof(playerTeamSeasonKey));

            var result = new OwnedPlayerCardState[playerRoster.Entries.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = new OwnedPlayerCardState(playerRoster.Entries[index].CardId);
            return result;
        }

        private static ActiveRosterRole GetCore25Role(int index)
        {
            if (index < 0 || index >= ActiveRosterCompositionRule.ActiveRosterSize)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (index < ActiveRosterCompositionRule.StartingHitterCount)
                return (ActiveRosterRole)index;
            if (index < ActiveRosterCompositionRule.HitterCount)
                return ActiveRosterRole.BenchHitter;
            return (ActiveRosterRole)(
                (int)ActiveRosterRole.StartingPitcher1 +
                index - ActiveRosterCompositionRule.HitterCount);
        }
    }
}
