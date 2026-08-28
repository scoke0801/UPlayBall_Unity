using Baseball.Core.Players;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 시뮬레이션 계수를 한 버전의 순수 C# 데이터로 묶어 제공한다.
    /// </summary>
    public sealed class BalanceTable
    {
        /// <summary>
        /// Game 레이어에서 변환한 경기·새 게임 밸런스 데이터를 묶는다.
        /// </summary>
        public BalanceTable(
            int version,
            PlateDisciplineBalance plateDiscipline,
            BattedBallBalance battedBall,
            BaseRunningBalance baseRunning)
            : this(
                version,
                plateDiscipline,
                battedBall,
                baseRunning,
                CharacterCreationBalance.CreateDefault(),
                ContractOfferBalance.CreateDefault(),
                TeamGenerationBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault(),
                CareerSeasonBalance.CreateDefault())
        {
        }

        /// <summary>
        /// 경기와 새 게임 생성에 쓰이는 모든 조정 가능 계수를 한 버전으로 묶는다.
        /// </summary>
        public BalanceTable(
            int version,
            PlateDisciplineBalance plateDiscipline,
            BattedBallBalance battedBall,
            BaseRunningBalance baseRunning,
            CharacterCreationBalance characterCreation,
            ContractOfferBalance contractOffer,
            TeamGenerationBalance teamGeneration,
            PlayerEvaluationBalance playerEvaluation)
            : this(
                version,
                plateDiscipline,
                battedBall,
                baseRunning,
                characterCreation,
                contractOffer,
                teamGeneration,
                playerEvaluation,
                CareerSeasonBalance.CreateDefault())
        {
        }

        /// <summary>
        /// 경기·새 게임·커리어 시즌의 모든 조정 가능 계수를 한 버전으로 묶는다.
        /// </summary>
        public BalanceTable(
            int version,
            PlateDisciplineBalance plateDiscipline,
            BattedBallBalance battedBall,
            BaseRunningBalance baseRunning,
            CharacterCreationBalance characterCreation,
            ContractOfferBalance contractOffer,
            TeamGenerationBalance teamGeneration,
            PlayerEvaluationBalance playerEvaluation,
            CareerSeasonBalance careerSeason,
            GrowthBalanceTable growth = null,
            InjuryBalanceTable? injury = null,
            ManagerEvaluationWeightTable? managerRoleEvaluation = null,
            ContractMarketBalanceTable? contractMarket = null,
            RosterTurnoverBalance? rosterTurnover = null,
            PostseasonBalance? postseason = null,
            BattingApproachBalance? battingApproach = null,
            ContractBonusBalance? contractBonus = null)
        {
            Version = version;
            PlateDiscipline = plateDiscipline;
            BattedBall = battedBall;
            BaseRunning = baseRunning;
            CharacterCreation = characterCreation;
            ContractOffer = contractOffer;
            TeamGeneration = teamGeneration;
            PlayerEvaluation = playerEvaluation;
            CareerSeason = careerSeason;
            Growth = growth ?? GrowthBalanceTable.CreateDefault();
            Injury = injury ?? InjuryBalanceTable.CreateDefault();
            ManagerRoleEvaluation = managerRoleEvaluation ?? ManagerEvaluationWeightTable.CreateDefault();
            ContractMarket = contractMarket ?? ContractMarketBalanceTable.CreateDefault();
            RosterTurnover = rosterTurnover ?? RosterTurnoverBalance.CreateDefault();
            Postseason = postseason ?? PostseasonBalance.CreateDefault();
            BattingApproach = battingApproach ?? BattingApproachBalance.CreateDefault();
            ContractBonus = contractBonus ?? ContractBonusBalance.CreateDefault();
            SeasonAwards = SeasonAwardBalance.CreateDefault();
            SeasonSettlement = SeasonSettlementBalance.CreateDefault();
        }

        public int Version { get; }
        public PlateDisciplineBalance PlateDiscipline { get; }
        public BattedBallBalance BattedBall { get; }
        public BaseRunningBalance BaseRunning { get; }
        public CharacterCreationBalance CharacterCreation { get; }
        public ContractOfferBalance ContractOffer { get; }
        public TeamGenerationBalance TeamGeneration { get; }
        public PlayerEvaluationBalance PlayerEvaluation { get; }
        public CareerSeasonBalance CareerSeason { get; }
        public GrowthBalanceTable Growth { get; }
        public InjuryBalanceTable Injury { get; }
        public ManagerEvaluationWeightTable ManagerRoleEvaluation { get; }
        public ContractMarketBalanceTable ContractMarket { get; }
        public RosterTurnoverBalance RosterTurnover { get; }
        public PostseasonBalance Postseason { get; }
        public BattingApproachBalance BattingApproach { get; }
        public ContractBonusBalance ContractBonus { get; }
        public SeasonAwardBalance SeasonAwards { get; }
        public SeasonSettlementBalance SeasonSettlement { get; }

        /// <summary>
        /// 현대 프로야구의 평균 타격 지표를 초기 가설로 삼은 프로토타입 값을 만든다.
        /// </summary>
        public static BalanceTable CreateDefault()
        {
            // 평균 BB 8~10%, SO 17~22%, BABIP 약 .300을 목표로 한 최초 검증용 계수다.
            // Mental은 기존 Eye가 담당하던 스윙/추격 판단을 흡수했다 (2026-08-26 능력치 개편).
            // Velocity는 Stuff와 대칭인 헛스윙 유발 축으로 신설했고, 계수는 Stuff의 절반으로 시작해
            // 급격한 밸런스 이동 없이 "구속만 높고 구위가 낮으면 삼진은 늘어도 맞으면 크게 맞는다"를 재현한다.
            var plateDiscipline = new PlateDisciplineBalance(
                strikeZoneProbability: 0.50d,
                controlStrikeZoneWeight: 0.0020d,
                strikeSwingProbability: 0.67d,
                mentalStrikeSwingWeight: 0.0010d,
                chaseProbability: 0.25d,
                mentalChaseWeight: 0.0020d,
                stuffChaseWeight: 0.0007d,
                velocityChaseWeight: 0.00035d,
                strikeContactProbability: 0.76d,
                chaseContactProbability: 0.58d,
                contactMatchupWeight: 0.0030d,
                velocityContactWeight: 0.0015d,
                fairContactProbability: 0.72d,
                sameHandedContactPenalty: 2.0d,
                oppositeHandedContactBonus: 1.0d);

            // HR/PA 약 3%, 2B/Hit 약 20%, BABIP 약 .300을 초기 목표로 둔다.
            // Movement는 Breaking으로 개명만 되었을 뿐 역할은 그대로다 (인플레이 타구 품질 억제).
            var battedBall = new BattedBallBalance(
                homeRunProbability: 0.045d,
                powerHomeRunWeight: 0.0012d,
                breakingHomeRunWeight: 0.0008d,
                nonHomeRunHitProbability: 0.295d,
                contactHitWeight: 0.0010d,
                breakingHitWeight: 0.0004d,
                defenseHitWeight: 0.0008d,
                doubleShare: 0.205d,
                powerDoubleWeight: 0.0008d,
                breakingDoubleWeight: 0.0003d,
                tripleShare: 0.020d,
                speedTripleWeight: 0.00035d,
                groundOutShare: 0.55d,
                breakingGroundOutWeight: 0.0010d,
                powerGroundOutWeight: 0.0005d);

            // Arm이 별도 능력치에서 사라지고 Defense로 흡수되었으므로(2026-08-26) 송구 억제도 Defense로 계산한다.
            var baseRunning = new BaseRunningBalance(
                singleFromSecondScoreProbability: 0.58d,
                singleFromFirstToThirdProbability: 0.28d,
                doubleFromFirstScoreProbability: 0.48d,
                sacrificeFlyProbability: 0.58d,
                groundOutFromThirdScoreProbability: 0.32d,
                groundOutAdvanceProbability: 0.45d,
                doublePlayProbability: 0.34d,
                runnerSpeedWeight: 0.0040d,
                defenseWeight: 0.0030d,
                doublePlayRunnerSpeedWeight: 0.0030d,
                doublePlayDefenseWeight: 0.0020d);

            return new BalanceTable(
                1,
                plateDiscipline,
                battedBall,
                baseRunning,
                CharacterCreationBalance.CreateDefault(),
                ContractOfferBalance.CreateDefault(),
                TeamGenerationBalance.CreateDefault(),
                PlayerEvaluationBalance.CreateDefault(),
                CareerSeasonBalance.CreateDefault());
        }
    }
}
