using Baseball.Core.Players;
using Baseball.Core.Growth;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Match;

namespace Baseball.Game.Career
{
    /// <summary>월드 선수의 현재 계약 시장 상태를 구분한다.</summary>
    public enum PlayerCareerStatus
    {
        ActiveRoster,
        FreeAgent,
        Retired
    }

    /// <summary>
    /// 세이브 대상이 되는 선수의 커리어 런타임 상태다. 구단은 ID로만 참조한다.
    /// </summary>
    public sealed class PlayerState
    {
        /// <summary>
        /// 새 게임에서 계약이 확정된 직후의 선수 상태를 생성한다.
        /// </summary>
        public PlayerState(
            int saveVersion,
            int playerId,
            string name,
            PlayerPosition primaryPosition,
            Handedness battingHand,
            Handedness throwingHand,
            BatterAttributes batterAttributes,
            PitcherAttributes pitcherAttributes,
            int currentTeamId)
            : this(
                saveVersion,
                playerId,
                name,
                nationality: string.Empty,
                age: 18,
                primaryPosition,
                battingHand,
                throwingHand,
                batterAttributes,
                pitcherAttributes,
                currentTeamId,
                LeagueId.Unassigned)
        {
        }

        /// <summary>
        /// 국적과 시작 나이를 포함한 계약 직후 선수 상태를 생성한다.
        /// </summary>
        public PlayerState(
            int saveVersion,
            int playerId,
            string name,
            string nationality,
            int age,
            PlayerPosition primaryPosition,
            Handedness battingHand,
            Handedness throwingHand,
            BatterAttributes batterAttributes,
            PitcherAttributes pitcherAttributes,
            int currentTeamId)
            : this(
                saveVersion,
                playerId,
                name,
                nationality,
                age,
                primaryPosition,
                battingHand,
                throwingHand,
                batterAttributes,
                pitcherAttributes,
                currentTeamId,
                LeagueId.Unassigned)
        {
        }

        /// <summary>
        /// 월드 소속 리그까지 확정된 선수 상태를 생성한다.
        /// </summary>
        public PlayerState(
            int saveVersion,
            int playerId,
            string name,
            string nationality,
            int age,
            PlayerPosition primaryPosition,
            Handedness battingHand,
            Handedness throwingHand,
            BatterAttributes batterAttributes,
            PitcherAttributes pitcherAttributes,
            int currentTeamId,
            LeagueId currentLeagueId)
        {
            SaveVersion = saveVersion;
            PlayerId = playerId;
            Name = name;
            Nationality = nationality;
            Age = age;
            PrimaryPosition = primaryPosition;
            BattingHand = battingHand;
            ThrowingHand = throwingHand;
            BatterAttributes = batterAttributes;
            PitcherAttributes = pitcherAttributes;
            CurrentTeamId = currentTeamId;
            CurrentLeagueId = currentLeagueId;
            CareerStatus = currentTeamId > 0 ? PlayerCareerStatus.ActiveRoster : PlayerCareerStatus.FreeAgent;
            StudyState = new PlayerStudyState();
            SkillBoardState = new SkillBoardState("standard_4x4");
        }

        public int SaveVersion { get; }
        public int PlayerId { get; }
        public RetirementPersonality RetirementPersonality =>
            (RetirementPersonality)((PlayerId - 1) % 4);
        public string Name { get; }
        public string Nationality { get; }
        public int Age { get; private set; }
        public PlayerPosition PrimaryPosition { get; }
        public Handedness BattingHand { get; }
        public Handedness ThrowingHand { get; }
        public BatterAttributes BatterAttributes { get; }
        public PitcherAttributes PitcherAttributes { get; }
        public int CurrentTeamId { get; private set; }
        public LeagueId CurrentLeagueId { get; private set; }
        public int ActiveContractId { get; private set; }
        public PlayerCareerStatus CareerStatus { get; private set; }
        public int Condition { get; private set; }
        public int ManagerEvaluation { get; private set; }
        public int CareerPlateAppearances { get; private set; }
        public int CareerPitchingOuts { get; private set; }
        public int RegisteredSeasons { get; private set; }
        public int LastCareerStatisticsYear { get; private set; }
        public System.DateTime LatestPitchingAppearanceDate { get; private set; }
        public int LatestPitchCount { get; private set; }
        public System.DateTime PreviousPitchingAppearanceDate { get; private set; }
        public int PreviousPitchCount { get; private set; }
        public System.DateTime ThirdPitchingAppearanceDate { get; private set; }
        public int ThirdPitchCount { get; private set; }
        public PlayerGrowthState GrowthState { get; private set; }
        public PlayerStudyState StudyState { get; }
        public SkillBoardState SkillBoardState { get; }

        /// <summary>
        /// 계약 직후 생성한 Base Ability와 Potential 상태를 커리어 세이브에 연결한다.
        /// </summary>
        public void AttachGrowthState(PlayerGrowthState growthState)
        {
            if (growthState == null)
                throw new System.ArgumentNullException(nameof(growthState));
            if (growthState.PlayerId != PlayerId)
                throw new System.InvalidOperationException("다른 선수의 성장 상태를 연결할 수 없습니다.");
            if (GrowthState != null)
                throw new System.InvalidOperationException("성장 상태는 한 번만 연결할 수 있습니다.");
            GrowthState = growthState;
        }

        /// <summary>마이그레이션·월드 생성 시 AI 선수의 기존 커리어 출전 이력을 한 번 복원한다.</summary>
        public void InitializeAiCareerHistory(
            int careerPlateAppearances,
            int careerPitchingOuts,
            int registeredSeasons)
        {
            if (careerPlateAppearances < 0)
                throw new System.ArgumentOutOfRangeException(nameof(careerPlateAppearances));
            if (careerPitchingOuts < 0)
                throw new System.ArgumentOutOfRangeException(nameof(careerPitchingOuts));
            if (registeredSeasons < 0)
                throw new System.ArgumentOutOfRangeException(nameof(registeredSeasons));
            if (CareerPlateAppearances != 0 || CareerPitchingOuts != 0 || RegisteredSeasons != 0)
                throw new System.InvalidOperationException("AI 선수의 커리어 이력은 한 번만 초기화할 수 있습니다.");

            CareerPlateAppearances = careerPlateAppearances;
            CareerPitchingOuts = careerPitchingOuts;
            RegisteredSeasons = registeredSeasons;
        }

        /// <summary>확정된 한 시즌 기록을 AI 선수의 영구 커리어 합계에 중복 없이 반영한다.</summary>
        public void RecordAiSeasonStatistics(
            int seasonYear,
            int plateAppearances,
            int pitchingOuts)
        {
            if (seasonYear <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(seasonYear));
            if (plateAppearances < 0)
                throw new System.ArgumentOutOfRangeException(nameof(plateAppearances));
            if (pitchingOuts < 0)
                throw new System.ArgumentOutOfRangeException(nameof(pitchingOuts));
            if (LastCareerStatisticsYear >= seasonYear)
                throw new System.InvalidOperationException("같은 시즌의 AI 커리어 기록을 두 번 반영할 수 없습니다.");

            CareerPlateAppearances += plateAppearances;
            CareerPitchingOuts += pitchingOuts;
            RegisteredSeasons++;
            LastCareerStatisticsYear = seasonYear;
        }

        /// <summary>
        /// 첫 시즌을 시작할 때 컨디션과 감독 평가의 초기값을 확정한다.
        /// </summary>
        public void InitializeSeasonStatus(int condition, int managerEvaluation)
        {
            Condition = ClampRating(condition);
            ManagerEvaluation = ClampRating(managerEvaluation);
            SynchronizeGrowthCondition();
        }

        /// <summary>
        /// 한 경기의 출장 여부와 수행 결과를 현재 상태에 반영한다.
        /// </summary>
        public void ApplyGameFeedback(int conditionDelta, int managerEvaluationDelta, int minimumCondition)
        {
            Condition = Clamp(Condition + conditionDelta, minimumCondition, 100);
            ManagerEvaluation = ClampRating(ManagerEvaluation + managerEvaluationDelta);
            SynchronizeGrowthCondition();
        }

        /// <summary>경기 날짜를 기준으로 직전 3일의 투구 부하 스냅샷을 만든다.</summary>
        public RecentPitchingWorkload GetRecentPitchingWorkload(System.DateTime gameDate)
        {
            int previousDay = 0;
            int twoDaysAgo = 0;
            int threeDaysAgo = 0;
            AddWorkload(gameDate, LatestPitchingAppearanceDate, LatestPitchCount,
                ref previousDay, ref twoDaysAgo, ref threeDaysAgo);
            AddWorkload(gameDate, PreviousPitchingAppearanceDate, PreviousPitchCount,
                ref previousDay, ref twoDaysAgo, ref threeDaysAgo);
            AddWorkload(gameDate, ThirdPitchingAppearanceDate, ThirdPitchCount,
                ref previousDay, ref twoDaysAgo, ref threeDaysAgo);
            return new RecentPitchingWorkload(previousDay, twoDaysAgo, threeDaysAgo);
        }

        /// <summary>확정된 경기의 투구 수를 최근 등판 부하에 한 번 기록한다.</summary>
        public void RecordPitchingUsage(System.DateTime gameDate, int pitchCount)
        {
            if (pitchCount <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(pitchCount));
            gameDate = gameDate.Date;
            if (LatestPitchingAppearanceDate.Date == gameDate)
            {
                LatestPitchCount += pitchCount;
                return;
            }
            if (LatestPitchingAppearanceDate != default && gameDate < LatestPitchingAppearanceDate.Date)
                throw new System.InvalidOperationException("투수 부하는 경기 날짜 순서대로 기록해야 합니다.");

            ThirdPitchingAppearanceDate = PreviousPitchingAppearanceDate;
            ThirdPitchCount = PreviousPitchCount;
            PreviousPitchingAppearanceDate = LatestPitchingAppearanceDate;
            PreviousPitchCount = LatestPitchCount;
            LatestPitchingAppearanceDate = gameDate;
            LatestPitchCount = pitchCount;
        }

        private static void AddWorkload(
            System.DateTime gameDate,
            System.DateTime appearanceDate,
            int pitchCount,
            ref int previousDay,
            ref int twoDaysAgo,
            ref int threeDaysAgo)
        {
            if (appearanceDate == default || pitchCount <= 0)
                return;
            int daysAgo = (gameDate.Date - appearanceDate.Date).Days;
            if (daysAgo == 1) previousDay += pitchCount;
            else if (daysAgo == 2) twoDaysAgo += pitchCount;
            else if (daysAgo == 3) threeDaysAgo += pitchCount;
        }

        /// <summary>
        /// FA·이적으로 소속 구단이 바뀔 때 ID만 갱신한다.
        /// </summary>
        public void TransferTo(int teamId)
        {
            if (teamId <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(teamId));
            CurrentTeamId = teamId;
            CareerStatus = PlayerCareerStatus.ActiveRoster;
        }

        /// <summary>
        /// 오프시즌 계약으로 구단과 리그가 함께 바뀔 때 두 참조를 한 번에 갱신한다.
        /// </summary>
        public void TransferTo(int teamId, LeagueId leagueId)
        {
            if (teamId <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(teamId));
            if (!leagueId.IsAssigned)
                throw new System.ArgumentException("선수 이동에는 유효한 LeagueId가 필요합니다.", nameof(leagueId));
            CurrentTeamId = teamId;
            CurrentLeagueId = leagueId;
            CareerStatus = PlayerCareerStatus.ActiveRoster;
        }

        /// <summary>
        /// 월드 생성·마이그레이션에서 구단으로부터 현재 리그를 확정한다.
        /// </summary>
        public void AssignLeague(LeagueId leagueId)
        {
            if (!leagueId.IsAssigned)
                throw new System.ArgumentException("선수에게 유효한 LeagueId가 필요합니다.", nameof(leagueId));
            if (CurrentLeagueId.IsAssigned && CurrentLeagueId != leagueId)
                throw new System.InvalidOperationException("이미 다른 리그에 소속된 선수입니다.");
            CurrentLeagueId = leagueId;
        }

        public void AttachContract(int contractId, LeagueId leagueId)
        {
            if (contractId <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(contractId));
            if (ActiveContractId > 0 && ActiveContractId != contractId)
                throw new System.InvalidOperationException("선수는 활성 계약을 하나만 가질 수 있습니다.");
            AssignLeague(leagueId);
            ActiveContractId = contractId;
        }

        public void ReplaceActiveContract(int contractId, LeagueId leagueId)
        {
            if (contractId <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(contractId));
            AssignLeague(leagueId);
            ActiveContractId = contractId;
        }

        /// <summary>기존 계약을 해제하고 선수를 공개 시장 대기 상태로 전환한다.</summary>
        public void ReleaseToFreeAgency()
        {
            CurrentTeamId = 0;
            CurrentLeagueId = LeagueId.Unassigned;
            ActiveContractId = 0;
            CareerStatus = PlayerCareerStatus.FreeAgent;
        }

        /// <summary>선수의 현역 소속과 활성 계약을 종료하되 커리어 기록은 월드에 보존한다.</summary>
        public void Retire()
        {
            CurrentTeamId = 0;
            CurrentLeagueId = LeagueId.Unassigned;
            ActiveContractId = 0;
            CareerStatus = PlayerCareerStatus.Retired;
        }

        /// <summary>월드 계약 레지스트리가 계약을 종료할 때 선수 측 활성 계약 참조도 지운다.</summary>
        internal void ClearActiveContract()
        {
            ActiveContractId = 0;
        }

        /// <summary>
        /// 새 구단 감독이 기존 평가를 그대로 승계하지 않도록 이적 직후 신뢰 기준을 다시 잡는다.
        /// </summary>
        public void ResetManagerEvaluation(int managerEvaluation)
        {
            ManagerEvaluation = ClampRating(managerEvaluation);
        }

        /// <summary>
        /// 시즌이 끝나 선수 나이가 한 살 증가할 때 호출한다.
        /// </summary>
        public void AdvanceAge()
        {
            Age++;
            GrowthState?.AdvanceAge();
        }

        /// <summary>
        /// 오프시즌 성장 활동이 바꾼 컨디션을 기존 커리어 상태에 반영한다.
        /// </summary>
        public void SynchronizeFromGrowthState()
        {
            if (GrowthState == null)
                throw new System.InvalidOperationException("연결된 성장 상태가 없습니다.");
            Age = GrowthState.Age;
            Condition = GrowthState.Condition;
        }

        /// <summary>
        /// 현재 저장 상태를 경기 시뮬레이터가 소비하는 불변 Player 입력으로 변환한다.
        /// </summary>
        public Player ToPlayer()
        {
            BatterAttributes currentBatterAttributes = GrowthState == null
                ? BatterAttributes
                : GrowthState.BaseAbilities.ToBatterAttributes();
            PitcherAttributes currentPitcherAttributes = GrowthState == null
                ? PitcherAttributes
                : GrowthState.BaseAbilities.ToPitcherAttributes();
            string[] traitIds = MergeTraitIds(
                System.Array.Empty<string>(),
                GrowthState?.LegacyTraitIds);
            return new Player(
                PlayerId,
                Name,
                PrimaryPosition,
                BattingHand,
                ThrowingHand,
                currentBatterAttributes,
                currentPitcherAttributes,
                nationality: Nationality,
                traitIds: traitIds);
        }

        /// <summary>
        /// 장착 성장판의 안정 능력치 보너스까지 합쳐 경기 시뮬레이터 입력을 만든다.
        /// </summary>
        public Player ToPlayer(SkillBoardService skillBoardService)
        {
            return ToResolvedPlayer(skillBoardService, includePeak: true);
        }

        /// <summary>계약·시장 평가용으로 Peak를 제외한 안정 전력 선수 입력을 만든다.</summary>
        public Player ToRosterPlayer(SkillBoardService skillBoardService)
        {
            return ToResolvedPlayer(skillBoardService, includePeak: false);
        }

        private Player ToResolvedPlayer(SkillBoardService skillBoardService, bool includePeak)
        {
            if (skillBoardService == null)
                throw new System.ArgumentNullException(nameof(skillBoardService));
            if (GrowthState == null)
                return ToPlayer();

            int[] values = GrowthState.BaseAbilities.ToArray();
            var resolver = new EffectiveAbilityResolver(skillBoardService);
            for (int index = 0; index < values.Length; index++)
            {
                AbilityBreakdown breakdown = resolver.Resolve(
                    GrowthState,
                    SkillBoardState,
                    (PlayerAbility)index,
                    EffectiveAbilityContext.Neutral);
                values[index] = includePeak
                    ? breakdown.CurrentAbility
                    : breakdown.RosterAbility;
            }
            var currentAbilities = new AbilityRatings(values);
            string[] traitIds = MergeTraitIds(
                skillBoardService.GetActiveTraitIds(SkillBoardState),
                GrowthState.LegacyTraitIds);
            return new Player(
                PlayerId,
                Name,
                PrimaryPosition,
                BattingHand,
                ThrowingHand,
                currentAbilities.ToBatterAttributes(),
                currentAbilities.ToPitcherAttributes(),
                nationality: Nationality,
                traitIds: traitIds);
        }

        private static string[] MergeTraitIds(
            string[] boardTraits,
            System.Collections.Generic.IReadOnlyList<string> legacyTraits)
        {
            int boardCount = boardTraits?.Length ?? 0;
            int legacyCount = legacyTraits?.Count ?? 0;
            var result = new System.Collections.Generic.List<string>(boardCount + legacyCount);
            for (int index = 0; index < boardCount; index++)
                if (!result.Contains(boardTraits[index])) result.Add(boardTraits[index]);
            for (int index = 0; index < legacyCount; index++)
                if (!result.Contains(legacyTraits[index])) result.Add(legacyTraits[index]);
            result.Sort(System.StringComparer.Ordinal);
            return result.ToArray();
        }

        private static int ClampRating(int value) => Clamp(value, 0, 100);

        private void SynchronizeGrowthCondition()
        {
            if (GrowthState != null)
                GrowthState.ChangeCondition(Condition - GrowthState.Condition);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
