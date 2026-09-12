using System;
using System.Collections.Generic;
using Baseball.Core.Growth;

namespace Baseball.Core.Historical
{
    /// <summary>한 World에서 실제로 활성화된 공통 선수 카드와 원본 시즌을 제공한다.</summary>
    public sealed class WorldCardCatalog
    {
        private readonly PlayerCardDefinition[] _cards;
        private readonly Dictionary<string, PlayerCardDefinition> _cardsById;
        private readonly Dictionary<string, PlayerSeasonDefinition> _seasonsById;
        private readonly Dictionary<string, PlayerPersonDefinition> _personsById;
        private readonly HashSet<string> _activeRosterSeasonIds;

        /// <param name="activeRosterPlayerSeasonIds">
        /// 원 소속 구단 연도의 1군 25인에 든 선수 시즌이다. null이면 1군 여부를 모르는 카탈로그로 보고
        /// 1군 한정 Scout가 전체 선수를 후보로 쓴다.
        /// </param>
        public WorldCardCatalog(
            IReadOnlyList<PlayerSeasonDefinition> playerSeasons,
            IReadOnlyList<PlayerCardDefinition> cards,
            IReadOnlyList<PlayerPersonDefinition> playerPersons = null,
            TeamColorLineageMap teamColorLineages = null,
            IReadOnlyList<SpecialRecruitRecipe> specialRecruitRecipes = null,
            IReadOnlyCollection<string> activeRosterPlayerSeasonIds = null)
        {
            if (playerSeasons == null)
                throw new ArgumentNullException(nameof(playerSeasons));
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));
            TeamColorLineages = teamColorLineages;

            _personsById = new Dictionary<string, PlayerPersonDefinition>(StringComparer.Ordinal);
            if (playerPersons != null)
            {
                for (int index = 0; index < playerPersons.Count; index++)
                {
                    PlayerPersonDefinition person = playerPersons[index]
                        ?? throw new ArgumentException("null 선수 인물이 있습니다.", nameof(playerPersons));
                    if (!_personsById.TryAdd(person.PlayerPersonId, person))
                        throw new ArgumentException("PlayerPersonId는 중복될 수 없습니다.", nameof(playerPersons));
                }
            }

            _seasonsById = new Dictionary<string, PlayerSeasonDefinition>(StringComparer.Ordinal);
            for (int index = 0; index < playerSeasons.Count; index++)
            {
                PlayerSeasonDefinition season = playerSeasons[index]
                    ?? throw new ArgumentException("null 선수 시즌이 있습니다.", nameof(playerSeasons));
                if (_personsById.Count > 0 && !_personsById.ContainsKey(season.PlayerPersonId))
                    throw new ArgumentException("PlayerSeason이 존재하지 않는 PlayerPerson을 참조합니다.", nameof(playerSeasons));
                if (!_seasonsById.TryAdd(season.PlayerSeasonId, season))
                    throw new ArgumentException("PlayerSeasonId는 중복될 수 없습니다.", nameof(playerSeasons));
            }

            _cards = new PlayerCardDefinition[cards.Count];
            _cardsById = new Dictionary<string, PlayerCardDefinition>(StringComparer.Ordinal);
            var normalSeasonIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < cards.Count; index++)
            {
                PlayerCardDefinition card = cards[index]
                    ?? throw new ArgumentException("null 카드가 있습니다.", nameof(cards));
                if (!_seasonsById.ContainsKey(card.PlayerSeasonId))
                    throw new ArgumentException("카드가 존재하지 않는 PlayerSeason을 참조합니다.", nameof(cards));
                PlayerSeasonDefinition sourceSeason = _seasonsById[card.PlayerSeasonId];
                if (card.IsFranchiseWildcard && (teamColorLineages == null || specialRecruitRecipes == null ||
                    teamColorLineages.GetRequired(sourceSeason.OriginFranchiseId) != card.TeamColorLineageId))
                    throw new ArgumentException("특수 영입 카드에는 일치하는 사전 Bake 계보와 레시피가 필요합니다.", nameof(cards));
                if ((card.Edition == PlayerCardEdition.Ex && sourceSeason.Cost != 10) ||
                    (card.Edition == PlayerCardEdition.Rare && sourceSeason.Cost != 4 && sourceSeason.Cost != 5) ||
                    (card.IsFranchiseWildcard && sourceSeason.Cost < 9))
                    throw new ArgumentException("특수 카드의 원본 Cost가 발급 조건을 충족하지 않습니다.", nameof(cards));
                string stableCardId = PlayerCardDefinition.CreateStableCardId(card.PlayerSeasonId, card.Edition, card.TeamColorLineageId);
                if (!string.Equals(card.CardId, stableCardId, StringComparison.Ordinal))
                    throw new ArgumentException("CardId가 Stable CardId 규칙과 일치하지 않습니다.", nameof(cards));
                if (!_cardsById.TryAdd(card.CardId, card))
                    throw new ArgumentException("CardId는 중복될 수 없습니다.", nameof(cards));
                if (card.Edition == PlayerCardEdition.Normal && !normalSeasonIds.Add(card.PlayerSeasonId))
                    throw new ArgumentException("한 PlayerSeason에는 Normal 카드가 하나만 있어야 합니다.", nameof(cards));
                _cards[index] = card;
            }

            if (normalSeasonIds.Count != _seasonsById.Count)
                throw new ArgumentException("모든 PlayerSeason에는 Normal 카드가 있어야 합니다.", nameof(cards));
            if (activeRosterPlayerSeasonIds != null)
            {
                _activeRosterSeasonIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string seasonId in activeRosterPlayerSeasonIds)
                {
                    if (seasonId == null || !_seasonsById.ContainsKey(seasonId))
                        throw new ArgumentException("1군 명단이 존재하지 않는 PlayerSeason을 참조합니다.",
                            nameof(activeRosterPlayerSeasonIds));
                    _activeRosterSeasonIds.Add(seasonId);
                }
            }
            if (specialRecruitRecipes != null)
                SpecialCards = new SpecialCardCatalog(this, teamColorLineages, specialRecruitRecipes);
        }

        public IReadOnlyList<PlayerCardDefinition> Cards => _cards;
        public TeamColorLineageMap TeamColorLineages { get; }
        public SpecialCardCatalog SpecialCards { get; }

        /// <summary>1군 명단 정보가 없는 카탈로그는 모든 선수 시즌을 1군으로 취급한다.</summary>
        public bool IsActiveRosterSeason(string playerSeasonId)
        {
            if (string.IsNullOrWhiteSpace(playerSeasonId))
                return false;
            return _activeRosterSeasonIds == null
                ? _seasonsById.ContainsKey(playerSeasonId)
                : _activeRosterSeasonIds.Contains(playerSeasonId);
        }

        public bool TryGetCard(string cardId, out PlayerCardDefinition card)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                card = null;
                return false;
            }
            return _cardsById.TryGetValue(cardId, out card);
        }

        /// <summary>명시적인 카드 ID 참조가 존재하지 않으면 즉시 실패한다.</summary>
        public PlayerCardDefinition GetRequiredCard(string cardId)
        {
            if (!TryGetCard(cardId, out var card)) throw new ArgumentException("등록되지 않은 카드입니다.", nameof(cardId));
            return card;
        }

        public PlayerSeasonDefinition GetPlayerSeason(PlayerCardDefinition card)
        {
            if (card == null)
                throw new ArgumentNullException(nameof(card));
            if (!_seasonsById.TryGetValue(card.PlayerSeasonId, out PlayerSeasonDefinition season))
                throw new ArgumentException("카탈로그에 속하지 않은 카드입니다.", nameof(card));
            return season;
        }

        public bool TryGetPlayerPerson(string playerPersonId, out PlayerPersonDefinition person)
        {
            if (string.IsNullOrWhiteSpace(playerPersonId))
            {
                person = null;
                return false;
            }
            return _personsById.TryGetValue(playerPersonId, out person);
        }
    }

    /// <summary>Edition 능력치 수치를 Resolver에 주입하는 초기 밸런스다.</summary>
    public sealed class CardEditionBalanceTable
    {
        private readonly int[] _allStarBonusByCost;
        private readonly int[] _mvpAllBonusByCost;

        public CardEditionBalanceTable(
            IReadOnlyList<int> allStarBonusByCost,
            int goldenGloveBonus,
            IReadOnlyList<int> mvpAllBonusByCost)
        {
            _allStarBonusByCost = CopyCostValues(allStarBonusByCost, nameof(allStarBonusByCost));
            _mvpAllBonusByCost = CopyCostValues(mvpAllBonusByCost, nameof(mvpAllBonusByCost));
            if (goldenGloveBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(goldenGloveBonus));
            GoldenGloveBonus = goldenGloveBonus;
        }

        public int GoldenGloveBonus { get; }
        public int GetAllStarBonus(int cost) => GetCostValue(_allStarBonusByCost, cost);
        public int GetMvpAllBonus(int cost) => GetCostValue(_mvpAllBonusByCost, cost);

        public static CardEditionBalanceTable CreateInitial()
        {
            return new CardEditionBalanceTable(
                new[] { 0, 5, 5, 5, 5, 4, 4, 3, 3, 2, 2 },
                2,
                new[] { 0, 5, 5, 5, 5, 5, 4, 4, 4, 3, 3 });
        }

        private static int[] CopyCostValues(IReadOnlyList<int> values, string parameterName)
        {
            if (values == null || values.Count != 11)
                throw new ArgumentException("Cost 1~10과 미사용 0 인덱스 값이 필요합니다.", parameterName);
            var copy = new int[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] < 0)
                    throw new ArgumentOutOfRangeException(parameterName);
                copy[index] = values[index];
            }
            return copy;
        }

        private static int GetCostValue(int[] values, int cost)
        {
            if (cost < 1 || cost > 10)
                throw new ArgumentOutOfRangeException(nameof(cost));
            return values[cost];
        }
    }

    public enum ScoutType
    {
        General,
        Franchise,
        Year,
        YearFranchise,
        Award
    }

    /// <summary>Scout 후보를 원 소속 구단의 어떤 선수까지 넓힐지 정한다.</summary>
    public enum ScoutRosterScope
    {
        /// <summary>그 해 기록이 있는 모든 선수 시즌이다.</summary>
        AllPlayers,

        /// <summary>원 소속 구단 연도의 1군 25인(Core25)만이다.</summary>
        ActiveRoster
    }

    /// <summary>Joint Bucket 스카우트의 필터, 가중치와 SP 가격을 보관한다.</summary>
    public sealed class ScoutPoolDefinition
    {
        private readonly double[] _costWeights;
        private readonly double[] _editionWeights;

        public ScoutPoolDefinition(
            string scoutPoolId,
            ScoutType scoutType,
            IReadOnlyList<double> costWeights,
            IReadOnlyList<double> editionWeights,
            int priceSp,
            string franchiseFilter = null,
            int? yearFilter = null,
            PlayerCardEdition? editionFilter = null,
            ScoutRosterScope rosterScope = ScoutRosterScope.AllPlayers)
        {
            if (string.IsNullOrWhiteSpace(scoutPoolId))
                throw new ArgumentException("ScoutPoolId는 비어 있을 수 없습니다.", nameof(scoutPoolId));
            if (priceSp < 0)
                throw new ArgumentOutOfRangeException(nameof(priceSp));
            if (yearFilter.HasValue && yearFilter.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(yearFilter));
            if (scoutType == ScoutType.Franchise && string.IsNullOrWhiteSpace(franchiseFilter))
                throw new ArgumentException("Franchise Scout에는 구단 필터가 필요합니다.", nameof(franchiseFilter));
            if (scoutType == ScoutType.Year && !yearFilter.HasValue)
                throw new ArgumentException("Year Scout에는 연도 필터가 필요합니다.", nameof(yearFilter));
            if (scoutType == ScoutType.YearFranchise &&
                (string.IsNullOrWhiteSpace(franchiseFilter) || !yearFilter.HasValue))
                throw new ArgumentException("YearFranchise Scout에는 구단과 연도 필터가 필요합니다.");
            if (scoutType == ScoutType.Award &&
                (!editionFilter.HasValue || editionFilter.Value == PlayerCardEdition.Normal))
                throw new ArgumentException("Award Scout에는 특수 Edition 필터가 필요합니다.", nameof(editionFilter));

            ScoutPoolId = scoutPoolId.Trim();
            ScoutType = scoutType;
            PriceSp = priceSp;
            FranchiseFilter = string.IsNullOrWhiteSpace(franchiseFilter) ? null : franchiseFilter.Trim();
            YearFilter = yearFilter;
            EditionFilter = editionFilter;
            RosterScope = rosterScope;
            _costWeights = CopyWeights(costWeights, 11, nameof(costWeights));
            if (editionWeights == null || (editionWeights.Count != 4 && editionWeights.Count != 8))
                throw new ArgumentException("Edition 가중치는 기존 4종 또는 전체 8종이어야 합니다.", nameof(editionWeights));
            _editionWeights = CopyWeights(editionWeights, editionWeights.Count, nameof(editionWeights));
        }

        public string ScoutPoolId { get; }
        public ScoutType ScoutType { get; }
        public int PriceSp { get; }
        public string FranchiseFilter { get; }
        public int? YearFilter { get; }
        public PlayerCardEdition? EditionFilter { get; }
        public ScoutRosterScope RosterScope { get; }

        public double GetCostWeight(int cost)
        {
            if (cost < 1 || cost > 10)
                throw new ArgumentOutOfRangeException(nameof(cost));
            return _costWeights[cost];
        }

        public double GetEditionWeight(PlayerCardEdition edition)
        {
            int index = (int)edition;
            return index >= 0 && index < _editionWeights.Length ? _editionWeights[index] : 0d;
        }

        public static double[] CreateInitialCostWeights()
        {
            return new[] { 0d, 12d, 13d, 15d, 15d, 14d, 12d, 8d, 6d, 3.5d, 1.5d };
        }

        public static double[] CreateNormalOnlyEditionWeights()
        {
            return new[] { 100d, 0d, 0d, 0d };
        }

        public static double[] CreateStandardEditionWeights()
        {
            // 레어는 중저코스트 특화 슬롯을 제공하며 EX·특수 영입은 스카우트에서 제외한다.
            return new[] { 96d, 2d, 0.7d, 0.3d, 1d, 0d, 0d, 0d };
        }

        private static double[] CopyWeights(IReadOnlyList<double> weights, int count, string parameterName)
        {
            if (weights == null || weights.Count != count)
                throw new ArgumentException("가중치 개수가 올바르지 않습니다.", parameterName);
            var copy = new double[count];
            double sum = 0d;
            for (int index = 0; index < count; index++)
            {
                double value = weights[index];
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                    throw new ArgumentOutOfRangeException(parameterName);
                copy[index] = value;
                sum += value;
            }
            if (sum <= 0d)
                throw new ArgumentException("하나 이상의 양수 가중치가 필요합니다.", parameterName);
            return copy;
        }
    }

    /// <summary>Phase별 Scout Edition 활성 경계를 명시한다.</summary>
    public sealed class ScoutFeaturePolicy
    {
        public ScoutFeaturePolicy(bool isAwardScoutEnabled, bool areSpecialEditionsEnabled)
        {
            if (isAwardScoutEnabled && !areSpecialEditionsEnabled)
                throw new ArgumentException("특수 Edition 없이 Award Scout를 활성화할 수 없습니다.");
            IsAwardScoutEnabled = isAwardScoutEnabled;
            AreSpecialEditionsEnabled = areSpecialEditionsEnabled;
        }

        public bool IsAwardScoutEnabled { get; }
        public bool AreSpecialEditionsEnabled { get; }

        public bool IsEditionEnabled(PlayerCardEdition edition)
        {
            return edition == PlayerCardEdition.Normal || AreSpecialEditionsEnabled;
        }

        public static ScoutFeaturePolicy Phase4NormalOnly => new ScoutFeaturePolicy(false, false);
        public static ScoutFeaturePolicy FullWorldAwards => new ScoutFeaturePolicy(true, true);
    }

    /// <summary>
    /// Scout에 쓴 SP만큼 차는 Pity 게이지와 보장 영입 조건을 정의한다.
    /// 게이지를 뽑기 횟수가 아니라 SP로 채우는 이유는, 싼 일반 Scout를 반복해 게이지만 채운 뒤
    /// 정밀 보장 영입으로 원하는 선수를 확정하는 우회로가 정밀 Scout보다 싸지지 않게 하기 위해서다.
    /// </summary>
    public sealed class ScoutPityBalanceTable
    {
        public ScoutPityBalanceTable(int thresholdScoutingPoints, int guaranteedMinimumCost)
        {
            if (thresholdScoutingPoints <= 0)
                throw new ArgumentOutOfRangeException(nameof(thresholdScoutingPoints));
            if (guaranteedMinimumCost < 1 || guaranteedMinimumCost > 10)
                throw new ArgumentOutOfRangeException(nameof(guaranteedMinimumCost));
            Threshold = thresholdScoutingPoints;
            GuaranteedMinimumCost = guaranteedMinimumCost;
        }

        /// <summary>보장 영입 1회에 필요한 게이지다. 게이지 1은 Scout에 쓴 SP 1이다.</summary>
        public int Threshold { get; }
        public int GuaranteedMinimumCost { get; }

        /// <summary>Scout 1회가 게이지에 더하는 양이다. 지불한 SP와 같다.</summary>
        public int GetGaugeGain(ScoutPoolDefinition pool)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            return pool.PriceSp;
        }

        /// <summary>정밀 Scout 10회(2,400 SP)마다 보장 영입 1회가 열리는 기본값이다.</summary>
        public static ScoutPityBalanceTable CreateInitial() => new ScoutPityBalanceTable(2400, 7);
    }

    /// <summary>
    /// 구단주 모드 SP 수입과 Pity 규칙을 묶는다. SP는 선수 Scout에만 쓰이는 재화라서
    /// 이 값들이 곧 "원하는 연도 구단의 1군 25인을 모으는 기간"을 정한다.
    /// </summary>
    public sealed class ScoutEconomyBalance
    {
        public ScoutEconomyBalance(
            ScoutPityBalanceTable pity,
            int scoutingPointsPerCompletedGame,
            int scoutingPointsPerWin)
        {
            if (scoutingPointsPerCompletedGame < 0)
                throw new ArgumentOutOfRangeException(nameof(scoutingPointsPerCompletedGame));
            if (scoutingPointsPerWin < 0)
                throw new ArgumentOutOfRangeException(nameof(scoutingPointsPerWin));
            Pity = pity ?? throw new ArgumentNullException(nameof(pity));
            ScoutingPointsPerCompletedGame = scoutingPointsPerCompletedGame;
            ScoutingPointsPerWin = scoutingPointsPerWin;
        }

        public ScoutPityBalanceTable Pity { get; }

        /// <summary>플레이어 구단이 정규시즌·포스트시즌 경기를 하나 마칠 때마다 받는 SP다.</summary>
        public int ScoutingPointsPerCompletedGame { get; }

        /// <summary>승리한 경기에 추가로 받는 SP다. 무승부와 패배에는 주지 않는다.</summary>
        public int ScoutingPointsPerWin { get; }

        public int GetMatchReward(bool isPlayerWin)
        {
            return isPlayerWin
                ? checked(ScoutingPointsPerCompletedGame + ScoutingPointsPerWin)
                : ScoutingPointsPerCompletedGame;
        }

        /// <summary>
        /// 144경기·승률 5할이면 시즌당 약 2,900 SP다. 시작 SP 3,000과 합쳐 정밀 Scout에 모두 쓰면
        /// 한 연도 구단의 1군 25인이 중앙값 3~4시즌에 모이도록 1982~2025 전 구단으로 맞춘 값이다.
        /// </summary>
        public static ScoutEconomyBalance CreateDefault()
        {
            return new ScoutEconomyBalance(ScoutPityBalanceTable.CreateInitial(), 15, 10);
        }
    }

    /// <summary>구단주 모드 카드 한 장에만 귀속되는 DP 훈련 누적치다.</summary>
    public sealed class CardTrainingState
    {
        public OwnerGrowthLedger Ledger { get; }

        public CardTrainingState()
        {
            Ledger = new OwnerGrowthLedger();
        }

        public CardTrainingState(
            IReadOnlyList<int> bonuses,
            IReadOnlyList<int> studyBonuses = null,
            OwnerGrowthLedger ledger = null)
        {
            if (bonuses == null || bonuses.Count != PlayerAbilityCatalog.AbilityCount)
                throw new ArgumentException("모든 능력치의 훈련 누적치가 필요합니다.", nameof(bonuses));
            if (studyBonuses != null && studyBonuses.Count != PlayerAbilityCatalog.AbilityCount)
                throw new ArgumentException("모든 능력치의 유학 누적치가 필요합니다.", nameof(studyBonuses));
            Ledger = ledger ?? new OwnerGrowthLedger();
            var direct = new int[bonuses.Count];
            var study = new int[bonuses.Count];
            for (int index = 0; index < bonuses.Count; index++)
            {
                if (bonuses[index] < 0)
                    throw new ArgumentOutOfRangeException(nameof(bonuses));
                int studyBonus = studyBonuses == null ? 0 : studyBonuses[index];
                if (studyBonus < 0 || studyBonus > bonuses[index])
                    throw new ArgumentOutOfRangeException(nameof(studyBonuses));
                direct[index] = bonuses[index] - studyBonus;
                study[index] = studyBonus;
                if (ledger != null && (ledger.Get(OwnerGrowthSource.Training, (PlayerAbility)index) != direct[index]
                    || ledger.Get(OwnerGrowthSource.OverseasTraining, (PlayerAbility)index) != study[index]))
                    throw new ArgumentException("성장 원장과 저장 합계가 일치하지 않습니다.", nameof(ledger));
            }
            if (ledger == null)
            {
                Ledger.Add(new OwnerGrowthModifier("initial_training", OwnerGrowthSource.Training, "기존 카드 훈련", direct));
                Ledger.Add(new OwnerGrowthModifier("initial_study", OwnerGrowthSource.OverseasTraining, "기존 해외 훈련", study));
            }
        }

        public int GetBonus(PlayerAbility ability) => checked(GetDirectTrainingBonus(ability) + GetStudyBonus(ability));
        public int GetStudyBonus(PlayerAbility ability) => Ledger.Get(OwnerGrowthSource.OverseasTraining, ability);
        public int GetDirectTrainingBonus(PlayerAbility ability) =>
            Ledger.Get(OwnerGrowthSource.Training, ability);

        public void AddBonus(PlayerAbility ability, int amount)
        {
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            AddEntry(OwnerGrowthSource.Training, ability, amount, "카드 훈련");
        }

        /// <summary>일반 훈련을 보존하고 유학으로 누적된 능력치만 제거한다.</summary>
        public void ResetStudyBonuses()
        {
            Ledger.Expire(OwnerGrowthSource.OverseasTraining);
        }

        public void AddStudyBonus(PlayerAbility ability, int amount, string programName = "해외 훈련")
        {
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            AddEntry(OwnerGrowthSource.OverseasTraining, ability, amount, programName);
        }

        private void AddEntry(OwnerGrowthSource source, PlayerAbility ability, int amount, string name)
        {
            if (amount == 0) return;
            var values = new int[PlayerAbilityCatalog.AbilityCount];
            values[(int)ability] = amount;
            Ledger.Add(new OwnerGrowthModifier("growth_" + Ledger.Count, source, name, values));
        }
    }

    /// <summary>구단주 모드 플레이어 구단에만 저장되는 카드 소유 상태다.</summary>
    public sealed class OwnedPlayerCardState
    {
        public const int MaximumEnhancementLevel = 5;

        public OwnedPlayerCardState(
            string cardId,
            int enhancementLevel = 0,
            int duplicateCount = 0,
            bool isLocked = false,
            bool isFavorite = false,
            CardTrainingState training = null,
            OwnedCardSkillBoardState skillBoard = null,
            int lastStudySeason = -1)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            if (enhancementLevel < 0 || enhancementLevel > MaximumEnhancementLevel)
                throw new ArgumentOutOfRangeException(nameof(enhancementLevel));
            if (duplicateCount < 0)
                throw new ArgumentOutOfRangeException(nameof(duplicateCount));
            CardId = cardId.Trim();
            EnhancementLevel = enhancementLevel;
            DuplicateCount = duplicateCount;
            IsLocked = isLocked;
            IsFavorite = isFavorite;
            Training = training ?? new CardTrainingState();
            SkillBoard = skillBoard ?? new OwnedCardSkillBoardState();
            LastStudySeason = lastStudySeason;
        }

        public string CardId { get; }
        public int EnhancementLevel { get; private set; }
        public int DuplicateCount { get; private set; }
        public bool IsLocked { get; set; }
        public bool IsFavorite { get; set; }
        public CardTrainingState Training { get; }
        public OwnedCardSkillBoardState SkillBoard { get; }
        public int LastStudySeason { get; private set; }
        public PlayerTraitProgress Trait { get; set; } = new PlayerTraitProgress();

        /// <summary>유학 누적 효과와 참가 시즌을 초기화한다.</summary>
        public void ResetStudy()
        {
            Training.ResetStudyBonuses();
            LastStudySeason = -1;
        }

        public void RecordStudySeason(int season)
        {
            if (season < 0) throw new ArgumentOutOfRangeException(nameof(season));
            LastStudySeason = season;
        }
        public void CancelStudyParticipation() => LastStudySeason = -1;

        public void AddDuplicate(int count = 1)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            checked { DuplicateCount += count; }
        }

        public bool TryConsumeDuplicate()
        {
            if (DuplicateCount <= 0)
                return false;
            DuplicateCount--;
            return true;
        }

        public void IncreaseEnhancement()
        {
            if (EnhancementLevel >= MaximumEnhancementLevel)
                throw new InvalidOperationException("강화는 +5를 넘을 수 없습니다.");
            EnhancementLevel++;
        }
    }

    /// <summary>한 Save에서 정상 획득한 적이 있는 CardId를 현재 보유 상태와 분리해 보존한다.</summary>
    public sealed class CardCollectionHistoryState
    {
        private readonly List<string> _everAcquiredCardIds;
        private readonly HashSet<string> _everAcquiredCardIdSet;

        public CardCollectionHistoryState(IReadOnlyList<string> everAcquiredCardIds = null)
        {
            _everAcquiredCardIds = new List<string>();
            _everAcquiredCardIdSet = new HashSet<string>(StringComparer.Ordinal);
            if (everAcquiredCardIds == null)
                return;

            for (int index = 0; index < everAcquiredCardIds.Count; index++)
                MarkAcquired(everAcquiredCardIds[index]);
        }

        public IReadOnlyList<string> EverAcquiredCardIds => _everAcquiredCardIds;
        public int Count => _everAcquiredCardIds.Count;

        public bool MarkAcquired(string cardId)
        {
            string id = RequireCardId(cardId);
            if (!_everAcquiredCardIdSet.Add(id))
                return false;
            _everAcquiredCardIds.Add(id);
            return true;
        }

        public bool WasEverAcquired(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                return false;
            return _everAcquiredCardIdSet.Contains(cardId.Trim());
        }

        private static string RequireCardId(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            return cardId.Trim();
        }
    }

    /// <summary>한 위시 카드의 Stable ID와 Save 내부 등록 순번을 보존한다.</summary>
    public sealed class WishlistEntry
    {
        public WishlistEntry(string cardId, long addedSequence)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            if (addedSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(addedSequence));
            CardId = cardId.Trim();
            AddedSequence = addedSequence;
        }

        public string CardId { get; }
        public long AddedSequence { get; }
    }

    /// <summary>Scout 확률과 독립적으로 정확한 CardId별 영입 목표와 등록 순서를 보존한다.</summary>
    public sealed class WishlistState
    {
        private readonly List<WishlistEntry> _entries;
        private readonly Dictionary<string, WishlistEntry> _entriesByCardId;
        private long _nextAddedSequence;

        public WishlistState(
            IReadOnlyList<WishlistEntry> entries = null,
            long nextAddedSequence = 0)
        {
            if (nextAddedSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(nextAddedSequence));

            _entries = new List<WishlistEntry>();
            _entriesByCardId = new Dictionary<string, WishlistEntry>(StringComparer.Ordinal);
            long maximumSequence = -1;
            if (entries != null)
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    WishlistEntry entry = entries[index]
                        ?? throw new ArgumentException("WishlistEntry는 null일 수 없습니다.", nameof(entries));
                    if (_entriesByCardId.ContainsKey(entry.CardId))
                        throw new ArgumentException("Wishlist에 같은 CardId를 중복 저장할 수 없습니다.", nameof(entries));
                    _entries.Add(entry);
                    _entriesByCardId.Add(entry.CardId, entry);
                    if (entry.AddedSequence > maximumSequence)
                        maximumSequence = entry.AddedSequence;
                }
            }

            if (maximumSequence >= nextAddedSequence)
                throw new ArgumentOutOfRangeException(
                    nameof(nextAddedSequence),
                    "다음 등록 순번은 저장된 모든 WishlistEntry보다 커야 합니다.");
            _nextAddedSequence = nextAddedSequence;
        }

        public IReadOnlyList<WishlistEntry> Entries => _entries;
        public int Count => _entries.Count;
        public long NextAddedSequence => _nextAddedSequence;

        public bool Add(string cardId)
        {
            string id = RequireCardId(cardId);
            if (_entriesByCardId.ContainsKey(id))
                return false;

            var entry = new WishlistEntry(id, _nextAddedSequence);
            _nextAddedSequence = checked(_nextAddedSequence + 1);
            _entries.Add(entry);
            _entriesByCardId.Add(id, entry);
            return true;
        }

        public bool Remove(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                return false;
            string id = cardId.Trim();
            if (!_entriesByCardId.TryGetValue(id, out WishlistEntry entry))
                return false;
            _entriesByCardId.Remove(id);
            _entries.Remove(entry);
            return true;
        }

        public bool Contains(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                return false;
            return _entriesByCardId.ContainsKey(cardId.Trim());
        }

        public WishlistEntry[] GetOldestFirst()
        {
            WishlistEntry[] result = _entries.ToArray();
            Array.Sort(result, CompareOldestFirst);
            return result;
        }

        public WishlistEntry[] GetMostRecentFirst()
        {
            WishlistEntry[] result = _entries.ToArray();
            Array.Sort(result, CompareMostRecentFirst);
            return result;
        }

        private static int CompareOldestFirst(WishlistEntry left, WishlistEntry right)
        {
            int comparison = left.AddedSequence.CompareTo(right.AddedSequence);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.CardId, right.CardId);
        }

        private static int CompareMostRecentFirst(WishlistEntry left, WishlistEntry right)
        {
            int comparison = right.AddedSequence.CompareTo(left.AddedSequence);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.CardId, right.CardId);
        }

        private static string RequireCardId(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            return cardId.Trim();
        }
    }

    /// <summary>현금 결제와 필수 계약 비용의 미지급 이월을 구분한다.</summary>
    public enum ContractPaymentMode
    {
        RequireCash,
        AllowArrears
    }

    /// <summary>구단주 모드 플레이어 구단 전용 Money/SP/DP와 Pity 진행 상태다.</summary>
    public sealed class ManagerEconomyState
    {
        public ManagerEconomyState(long money = 0, int scoutingPoints = 0, int developmentPoints = 0, int pityGauge = 0,
            long contractArrears = 0)
        {
            if (money < 0 || scoutingPoints < 0 || developmentPoints < 0 || pityGauge < 0 || contractArrears < 0)
                throw new ArgumentOutOfRangeException(nameof(money));
            if (money > 0 && contractArrears > 0)
                throw new ArgumentException("가용 현금과 미지급 계약 비용은 동시에 남을 수 없습니다.");
            Money = money;
            ContractArrears = contractArrears;
            ScoutingPoints = scoutingPoints;
            DevelopmentPoints = developmentPoints;
            PityGauge = pityGauge;
        }

        public long Money { get; private set; }
        public long ContractArrears { get; private set; }

        /// <summary>필수 급여·갱신 비용을 현금으로 지급하고 부족액만 이월한다. 일반 구매에는 사용하지 않는다.</summary>
        public void SettleContractPayment(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            long paid = Math.Min(Money, amount);
            long nextArrears = checked(ContractArrears + amount - paid);
            Money -= paid;
            ContractArrears = nextArrears;
        }
        public int ScoutingPoints { get; private set; }
        public int DevelopmentPoints { get; private set; }
        public int PityGauge { get; private set; }

        public bool TrySpendMoney(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (Money < amount)
                return false;
            Money -= amount;
            return true;
        }

        public bool TrySpendScoutingPoints(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (ScoutingPoints < amount)
                return false;
            ScoutingPoints -= amount;
            return true;
        }

        public bool TrySpendDevelopmentPoints(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (DevelopmentPoints < amount)
                return false;
            DevelopmentPoints -= amount;
            return true;
        }

        public void AddScoutingPoints(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            checked { ScoutingPoints += amount; }
        }

        public void AddMoney(long amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            // 모든 현금 수입이 같은 경계를 거쳐야 보상·경기 수입으로 상환을 우회할 수 없다.
            long repaid = Math.Min(ContractArrears, amount);
            long nextMoney = checked(Money + amount - repaid);
            ContractArrears -= repaid;
            Money = nextMoney;
        }

        public void AddDevelopmentPoints(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            checked { DevelopmentPoints += amount; }
        }

        public void AddPityGauge(int amount, int threshold)
        {
            if (amount < 0 || threshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            PityGauge = Math.Min(threshold, checked(PityGauge + amount));
        }

        public bool TryConsumePity(int threshold)
        {
            if (threshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(threshold));
            if (PityGauge < threshold)
                return false;
            PityGauge -= threshold;
            return true;
        }
    }

    public enum DuplicateAutoSalePolicy
    {
        Disabled,
        MaxDuplicateOnly
    }

    /// <summary>Cost와 Edition에 따른 중복 카드 SP 판매가를 데이터화한다.</summary>
    public sealed class CardSaleBalanceTable
    {
        private readonly int[] _baseSaleSpByCost;
        private readonly double[] _editionMultipliers;

        public CardSaleBalanceTable(IReadOnlyList<int> baseSaleSpByCost, IReadOnlyList<double> editionMultipliers)
        {
            if (baseSaleSpByCost == null || baseSaleSpByCost.Count != 11)
                throw new ArgumentException("Cost 1~10 판매가가 필요합니다.", nameof(baseSaleSpByCost));
            if (editionMultipliers == null || (editionMultipliers.Count != 4 && editionMultipliers.Count != 8))
                throw new ArgumentException("기존 4종 또는 전체 8종의 판매 배율이 필요합니다.", nameof(editionMultipliers));
            _baseSaleSpByCost = new int[11];
            _editionMultipliers = new double[8];
            for (int index = 0; index < 11; index++)
            {
                if (baseSaleSpByCost[index] < 0)
                    throw new ArgumentOutOfRangeException(nameof(baseSaleSpByCost));
                _baseSaleSpByCost[index] = baseSaleSpByCost[index];
            }
            for (int index = 0; index < editionMultipliers.Count; index++)
            {
                if (editionMultipliers[index] < 0d || double.IsNaN(editionMultipliers[index]))
                    throw new ArgumentOutOfRangeException(nameof(editionMultipliers));
                _editionMultipliers[index] = editionMultipliers[index];
            }
            // 이전 4종 데이터에서도 새 등급은 동일 Cost의 일반 카드 가격으로 평가한다.
            for (int index = editionMultipliers.Count; index < _editionMultipliers.Length; index++)
                _editionMultipliers[index] = editionMultipliers[0];
        }

        public int GetBaseSaleSp(int cost)
        {
            if (cost < 1 || cost > 10)
                throw new ArgumentOutOfRangeException(nameof(cost));
            return _baseSaleSpByCost[cost];
        }

        public double GetEditionMultiplier(PlayerCardEdition edition) => _editionMultipliers[(int)edition];

        public static CardSaleBalanceTable CreateInitial()
        {
            return new CardSaleBalanceTable(
                new[] { 0, 3, 4, 6, 8, 10, 14, 20, 28, 40, 55 },
                new[] { 1d, 1.2d, 1.4d, 1.8d, 1d, 1d, 0d, 0d });
        }
    }

    /// <summary>한 능력치에 DP를 사용하는 카드 훈련 프로그램이다.</summary>
    public sealed class CardTrainingProgramDefinition
    {
        public CardTrainingProgramDefinition(string programId, PlayerAbility ability, int dpCostPerPoint, int maximumPointsPerSession)
        {
            if (string.IsNullOrWhiteSpace(programId))
                throw new ArgumentException("ProgramId는 비어 있을 수 없습니다.", nameof(programId));
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
            if (dpCostPerPoint <= 0 || maximumPointsPerSession <= 0)
                throw new ArgumentOutOfRangeException(nameof(dpCostPerPoint));
            ProgramId = programId.Trim();
            Ability = ability;
            DpCostPerPoint = dpCostPerPoint;
            MaximumPointsPerSession = maximumPointsPerSession;
        }

        public string ProgramId { get; }
        public PlayerAbility Ability { get; }
        public int DpCostPerPoint { get; }
        public int MaximumPointsPerSession { get; }
    }
}
