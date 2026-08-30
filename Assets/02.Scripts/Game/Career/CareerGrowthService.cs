using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 실제 시즌 출장량과 포지션 평가 가중치로 자연 성장용 활용 요약을 만든다.
    /// </summary>
    public sealed class CareerSeasonUsageSummaryBuilder
    {
        private readonly PlayerEvaluationBalance _playerEvaluation;
        private readonly int _startingRotationSize;

        public CareerSeasonUsageSummaryBuilder(
            PlayerEvaluationBalance playerEvaluation,
            int startingRotationSize)
        {
            if (startingRotationSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(startingRotationSize));
            _playerEvaluation = playerEvaluation;
            _startingRotationSize = startingRotationSize;
        }

        /// <summary>
        /// 타자는 출장 경기, 선발은 로테이션 기회, 구원은 등판 경기를 기준으로 활용량을 정규화한다.
        /// </summary>
        public SeasonUsageSummary Build(
            PlayerPosition position,
            PlayerSeasonStatisticsState statistics,
            bool isStarter = true,
            double competitorGap = 0d)
        {
            if (statistics == null)
                throw new ArgumentNullException(nameof(statistics));

            double usageRatio = CalculateUsageRatio(position, statistics);
            AbilityWeight[] weights = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher
                ? BuildPitcherWeights(position)
                : BuildBatterWeights(position);
            return new SeasonUsageSummary(usageRatio, weights, isStarter, competitorGap);
        }

        /// <summary>리그 전역 원본 기록에서 AI 선수의 실제 출장량과 성장 가중치를 계산한다.</summary>
        public SeasonUsageSummary Build(
            PlayerPosition position,
            PlayerCompetitionStatisticsState statistics,
            bool isStarter = true,
            double competitorGap = 0d)
        {
            if (statistics == null)
                throw new ArgumentNullException(nameof(statistics));

            double usageRatio = 0d;
            if (statistics.TeamGames > 0)
            {
                usageRatio = position switch
                {
                    PlayerPosition.StartingPitcher =>
                        statistics.Pitching.Starts * _startingRotationSize / (double)statistics.TeamGames,
                    PlayerPosition.ReliefPitcher =>
                        statistics.Pitching.Appearances / (double)statistics.TeamGames,
                    _ => statistics.Batting.Games / (double)statistics.TeamGames
                };
            }
            AbilityWeight[] weights = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher
                ? BuildPitcherWeights(position)
                : BuildBatterWeights(position);
            return new SeasonUsageSummary(usageRatio, weights, isStarter, competitorGap);
        }

        private double CalculateUsageRatio(
            PlayerPosition position,
            PlayerSeasonStatisticsState statistics)
        {
            if (statistics.TeamGames <= 0)
                return 0d;
            return position switch
            {
                PlayerPosition.StartingPitcher =>
                    statistics.PitchingStarts * _startingRotationSize / (double)statistics.TeamGames,
                PlayerPosition.ReliefPitcher =>
                    statistics.PitchingAppearances / (double)statistics.TeamGames,
                _ => statistics.GamesPlayed / (double)statistics.TeamGames
            };
        }

        private AbilityWeight[] BuildBatterWeights(PlayerPosition position)
        {
            double contact = _playerEvaluation.GeneralAttributeWeight;
            double power = _playerEvaluation.GeneralAttributeWeight;
            double speed = _playerEvaluation.GeneralAttributeWeight;
            double arm = _playerEvaluation.GeneralAttributeWeight;
            double defense = _playerEvaluation.GeneralAttributeWeight;
            double mental = _playerEvaluation.GeneralAttributeWeight;
            switch (position)
            {
                case PlayerPosition.Catcher:
                    contact = _playerEvaluation.SupportingAttributeWeight;
                    arm = _playerEvaluation.KeyAttributeWeight;
                    defense = _playerEvaluation.KeyAttributeWeight;
                    mental = _playerEvaluation.KeyAttributeWeight;
                    break;
                case PlayerPosition.FirstBase:
                case PlayerPosition.DesignatedHitter:
                    contact = _playerEvaluation.SupportingAttributeWeight;
                    power = _playerEvaluation.KeyAttributeWeight;
                    mental = _playerEvaluation.SupportingAttributeWeight;
                    break;
                case PlayerPosition.SecondBase:
                    contact = _playerEvaluation.SupportingAttributeWeight;
                    speed = _playerEvaluation.SupportingAttributeWeight;
                    defense = _playerEvaluation.KeyAttributeWeight;
                    mental = _playerEvaluation.SupportingAttributeWeight;
                    break;
                case PlayerPosition.ThirdBase:
                    contact = _playerEvaluation.SupportingAttributeWeight;
                    power = _playerEvaluation.KeyAttributeWeight;
                    arm = _playerEvaluation.KeyAttributeWeight;
                    defense = _playerEvaluation.SupportingAttributeWeight;
                    break;
                case PlayerPosition.Shortstop:
                    contact = _playerEvaluation.SupportingAttributeWeight;
                    speed = _playerEvaluation.SupportingAttributeWeight;
                    arm = _playerEvaluation.KeyAttributeWeight;
                    defense = _playerEvaluation.KeyAttributeWeight;
                    mental = _playerEvaluation.SupportingAttributeWeight;
                    break;
                case PlayerPosition.LeftField:
                case PlayerPosition.RightField:
                    contact = _playerEvaluation.SupportingAttributeWeight;
                    power = _playerEvaluation.KeyAttributeWeight;
                    arm = _playerEvaluation.KeyAttributeWeight;
                    defense = _playerEvaluation.SupportingAttributeWeight;
                    break;
                case PlayerPosition.CenterField:
                    contact = _playerEvaluation.SupportingAttributeWeight;
                    speed = _playerEvaluation.KeyAttributeWeight;
                    arm = _playerEvaluation.SupportingAttributeWeight;
                    defense = _playerEvaluation.KeyAttributeWeight;
                    break;
            }

            double total = contact + power + speed + arm + defense + mental;
            return new[]
            {
                new AbilityWeight(PlayerAbility.Contact, contact / total),
                new AbilityWeight(PlayerAbility.Power, power / total),
                new AbilityWeight(PlayerAbility.Speed, speed / total),
                new AbilityWeight(PlayerAbility.Arm, arm / total),
                new AbilityWeight(PlayerAbility.Defense, defense / total),
                new AbilityWeight(PlayerAbility.BatterMental, mental / total)
            };
        }

        private AbilityWeight[] BuildPitcherWeights(PlayerPosition position)
        {
            double stamina = position == PlayerPosition.StartingPitcher
                ? _playerEvaluation.KeyAttributeWeight
                : _playerEvaluation.GeneralAttributeWeight;
            double velocity = position == PlayerPosition.ReliefPitcher
                ? _playerEvaluation.KeyAttributeWeight
                : _playerEvaluation.SupportingAttributeWeight;
            double stuff = position == PlayerPosition.ReliefPitcher
                ? _playerEvaluation.KeyAttributeWeight
                : _playerEvaluation.SupportingAttributeWeight;
            double breaking = _playerEvaluation.SupportingAttributeWeight;
            double control = position == PlayerPosition.StartingPitcher
                ? _playerEvaluation.KeyAttributeWeight
                : _playerEvaluation.SupportingAttributeWeight;
            double mental = _playerEvaluation.SupportingAttributeWeight;
            double total = stamina + velocity + stuff + breaking + control + mental;
            return new[]
            {
                new AbilityWeight(PlayerAbility.Stamina, stamina / total),
                new AbilityWeight(PlayerAbility.Velocity, velocity / total),
                new AbilityWeight(PlayerAbility.Stuff, stuff / total),
                new AbilityWeight(PlayerAbility.Breaking, breaking / total),
                new AbilityWeight(PlayerAbility.Control, control / total),
                new AbilityWeight(PlayerAbility.PitcherMental, mental / total)
            };
        }
    }

    /// <summary>
    /// 시즌 결산에서 분리해 보여 줄 자연 성장·노쇠·수입 결과를 묶는다.
    /// </summary>
    public readonly struct SeasonGrowthSettlementResult
    {
        public SeasonGrowthSettlementResult(
            GrowthResultRecord naturalDevelopment,
            GrowthResultRecord aging,
            long salaryIncome,
            long bonusIncome,
            OffseasonState offseason)
        {
            NaturalDevelopment = naturalDevelopment;
            Aging = aging;
            SalaryIncome = salaryIncome;
            BonusIncome = bonusIncome;
            Offseason = offseason;
        }

        public GrowthResultRecord NaturalDevelopment { get; }
        public GrowthResultRecord Aging { get; }
        public long SalaryIncome { get; }
        public long BonusIncome { get; }
        public OffseasonState Offseason { get; }
    }

    /// <summary>
    /// 커리어 시즌 결산과 결정론적 오프시즌 활동을 Game 상태에 연결한다.
    /// </summary>
    public sealed class CareerGrowthService
    {
        private const ulong NaturalDevelopmentStream = 0x4E41545552414CUL;
        private const ulong AgingStream = 0x4147494E47UL;
        private const ulong OffseasonActivityStream = 0x4F4646534541534FUL;

        private readonly CareerState _career;
        private readonly BalanceTable _balance;
        private readonly NaturalDevelopmentResolver _naturalDevelopmentResolver;
        private readonly AgingResolver _agingResolver;
        private readonly OffseasonScheduler _offseasonScheduler;

        public CareerGrowthService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            if (_career.MyPlayer.GrowthState == null)
                throw new InvalidOperationException("커리어 선수의 성장 상태가 필요합니다.");

            SimulationVersionStamp stamp = _career.CurrentLeague.CurrentSeason.VersionStamp;
            _naturalDevelopmentResolver = new NaturalDevelopmentResolver(balance.Growth, stamp);
            _agingResolver = new AgingResolver(balance.Growth, stamp);
            _offseasonScheduler = new OffseasonScheduler(balance.Growth, stamp);
        }

        /// <summary>
        /// 완료된 시즌을 한 번 결산하고 12주 오프시즌 상태를 생성한다.
        /// </summary>
        public SeasonGrowthSettlementResult SettleSeasonAndBeginOffseason(
            SeasonUsageSummary usage,
            long bonusIncome = 0L,
            int mandatoryRehabWeeks = 0)
        {
            if (usage == null)
                throw new ArgumentNullException(nameof(usage));
            if (bonusIncome < 0L)
                throw new ArgumentOutOfRangeException(nameof(bonusIncome));

            SeasonState season = _career.CurrentLeague.CurrentSeason ??
                                 throw new InvalidOperationException("현재 시즌이 없습니다.");
            if (season.Phase != SeasonPhase.SeasonReview)
                throw new InvalidOperationException("시즌 결산 단계에서만 성장 결산할 수 있습니다.");
            if (_career.CurrentOffseason != null)
                throw new InvalidOperationException("이미 진행 중인 오프시즌이 있습니다.");

            PlayerGrowthState growth = _career.MyPlayer.GrowthState;
            mandatoryRehabWeeks = Math.Max(
                mandatoryRehabWeeks,
                CalculateMandatoryRehabilitationWeeks(growth, season.Year));
            int[] abilitiesBefore = growth.BaseAbilities.ToArray();
            ulong seasonStream = ((ulong)(uint)season.SeasonId << 32) | (uint)growth.PlayerId;
            ulong naturalSeed = DeterministicSeed.Derive(
                _career.CurrentLeague.RandomSeed,
                seasonStream ^ NaturalDevelopmentStream);
            ulong agingSeed = DeterministicSeed.Derive(
                _career.CurrentLeague.RandomSeed,
                seasonStream ^ AgingStream);

            GrowthResultRecord naturalDevelopment = _naturalDevelopmentResolver.Resolve(
                growth,
                usage,
                season.Year,
                naturalSeed,
                new Pcg32Random(naturalSeed));
            GrowthResultRecord aging = _agingResolver.Resolve(
                growth,
                season.Year,
                agingSeed,
                new Pcg32Random(agingSeed));
            new WorldAiPlayerDevelopmentService(_career, _balance)
                .SettleCompletedSeasonPlayers();

            SeasonSettlementState settlement = new SeasonSettlementService(
                    _career,
                    _balance.SeasonSettlement,
                    _balance.ContractBonus)
                .ApplyOnce(bonusIncome);
            season.ReviewSnapshot?.CompleteSettlement(
                settlement,
                abilitiesBefore,
                growth.BaseAbilities.ToArray());
            long salaryIncome = settlement.SalaryIncome;

            var offseason = new OffseasonState(
                season.Year,
                _balance.Growth.OffseasonWeeks,
                growth.Condition,
                mandatoryRehabWeeks,
                CareerTrainingAccess.GetAccessTier(
                    _career.Reputation.HighestReachedTier,
                    _balance.Growth.Progression),
                CareerTrainingAccess.GetAccessTier(
                    season.LeagueLevel,
                    _balance.Growth.Progression),
                _career.GrowthMilestones.AdditionalProgramCandidates,
                _career.GrowthMilestones.HasSeasonalRepetitionWaiver
                    ? _balance.Growth.Progression.RepetitionPenaltyWaivers
                    : 0,
                _career.GrowthMilestones.CanRedirectTrainingGrowth
                    ? FindDefaultMasterFocusAbility(growth)
                    : null,
                _career.GrowthMilestones.IsLegacyTraitConversionUnlocked);
            PlanMandatoryRehabilitation(offseason, growth, mandatoryRehabWeeks);
            _career.MyPlayer.SkillBoardState.UnlockForOffseason();
            if (season.Review?.Step == SeasonReviewStep.SeasonSummary)
                season.Review.MarkIncomeSettlementReady();
            else
                season.Review?.Complete();
            _career.BeginOffseason(offseason);
            _career.MyPlayer.SynchronizeFromGrowthState();
            season.BeginOffseason();
            new WorldSeasonLifecycleService(_career, _balance)
                .BeginBackgroundOffseasons(_career.CurrentLeague.LeagueId);
            return new SeasonGrowthSettlementResult(
                naturalDevelopment,
                aging,
                salaryIncome,
                settlement.BonusIncome,
                offseason);
        }

        public PlannedOffseasonActivity PlanActivity(
            string programId,
            int startWeek,
            TrainingIntensity intensity = TrainingIntensity.Standard)
        {
            OffseasonState offseason = RequireOffseason();
            TrainingProgramDefinition program = _balance.Growth.FindProgram(programId) ??
                                                throw new ArgumentException(
                                                    "존재하지 않는 성장 프로그램입니다.",
                                                    nameof(programId));
            if (!CareerTrainingAccess.CanAccess(
                    program,
                    _career.CurrentLeague.CurrentSeason.LeagueLevel,
                    _career.Reputation.HighestReachedTier,
                    _balance.Growth.Progression))
            {
                throw new InvalidOperationException("현재 리그에서 해금되지 않은 성장 프로그램입니다.");
            }
            return _offseasonScheduler.PlanActivity(
                offseason,
                _career.Economy,
                _career.MyPlayer.GrowthState,
                programId,
                startWeek,
                intensity);
        }

        public void CancelActivity(int activityId)
        {
            _offseasonScheduler.CancelActivity(RequireOffseason(), activityId);
        }

        /// <summary>
        /// 활동 시작 시 외부에서 Seed를 선택하지 못하게 커리어 Seed에서 결과 Seed를 파생한다.
        /// </summary>
        public void StartActivity(int activityId)
        {
            OffseasonState offseason = RequireOffseason();
            ulong stream = OffseasonActivityStream ^
                           ((ulong)(uint)_career.CurrentLeague.CurrentSeason.SeasonId << 32) ^
                           (uint)activityId;
            ulong activitySeed = DeterministicSeed.Derive(_career.CurrentLeague.RandomSeed, stream);
            _offseasonScheduler.StartActivity(
                offseason,
                _career.Economy,
                _career.MyPlayer.GrowthState,
                _career.MyPlayer.StudyState,
                activityId,
                activitySeed);
        }

        public GrowthResultRecord CompleteActivity(int activityId)
        {
            OffseasonState offseason = RequireOffseason();
            PlannedOffseasonActivity activity = FindActivity(offseason, activityId);
            if (activity.Status != OffseasonActivityStatus.InProgress)
                throw new InvalidOperationException("진행 중인 활동만 완료할 수 있습니다.");

            GrowthResultRecord result = _offseasonScheduler.CompleteActivity(
                offseason,
                _career.MyPlayer.GrowthState,
                _career.MyPlayer.StudyState,
                activityId,
                new Pcg32Random(activity.RandomSeed));
            UnlockLegacyTrait(activity.ProgramId, offseason, _career.MyPlayer.GrowthState);
            _career.MyPlayer.SynchronizeFromGrowthState();
            new RetirementRecapService(_balance).RecordGrowthResult(_career, result);
            return result;
        }

        public void SetMasterFocusAbility(PlayerAbility ability)
        {
            OffseasonState offseason = RequireOffseason();
            if (!_career.GrowthMilestones.CanRedirectTrainingGrowth)
                throw new InvalidOperationException("Master 리그 최초 진출 후 사용할 수 있습니다.");
            bool matchesPlayerType = _career.MyPlayer.GrowthState.PlayerType == PlayerType.Batter
                ? PlayerAbilityCatalog.IsBatterAbility(ability)
                : PlayerAbilityCatalog.IsPitcherAbility(ability);
            if (!matchesPlayerType)
                throw new InvalidOperationException("현재 선수 유형에 맞지 않는 집중 능력입니다.");
            offseason.SetMasterFocusAbility(ability);
        }

        /// <summary>
        /// 현재 주에 선택한 활동을 계획·시작·완료해 활동 기간 전체를 한 번에 진행한다.
        /// </summary>
        public GrowthResultRecord ExecuteActivity(
            string programId,
            TrainingIntensity intensity = TrainingIntensity.Standard)
        {
            OffseasonState offseason = RequireOffseason();
            PlannedOffseasonActivity activity = PlanActivity(
                programId,
                offseason.CurrentWeek,
                intensity);
            try
            {
                StartActivity(activity.ActivityId);
                return CompleteActivity(activity.ActivityId);
            }
            catch
            {
                if (activity.Status == OffseasonActivityStatus.Planned)
                    CancelActivity(activity.ActivityId);
                throw;
            }
        }

        /// <summary>
        /// 계획된 활동을 주차·활동 ID 순서로 시작하고 완료해 하나의 오프시즌 계획으로 실행한다.
        /// </summary>
        public GrowthResultRecord[] ExecutePlannedActivities()
        {
            OffseasonState offseason = RequireOffseason();
            var results = new List<GrowthResultRecord>();
            PlannedOffseasonActivity activity = FindNextPlannedActivity(offseason);
            while (activity != null)
            {
                StartActivity(activity.ActivityId);
                results.Add(CompleteActivity(activity.ActivityId));
                activity = FindNextPlannedActivity(offseason);
            }
            return results.ToArray();
        }

        private OffseasonState RequireOffseason()
        {
            if (_career.CurrentLeague.CurrentSeason?.Phase != SeasonPhase.Offseason ||
                _career.CurrentOffseason == null)
            {
                throw new InvalidOperationException("진행 중인 오프시즌이 없습니다.");
            }
            return _career.CurrentOffseason;
        }

        private void PlanMandatoryRehabilitation(
            OffseasonState offseason,
            PlayerGrowthState growth,
            int mandatoryWeeks)
        {
            for (int week = 1; week <= mandatoryWeeks; week++)
            {
                _offseasonScheduler.PlanActivity(
                    offseason,
                    _career.Economy,
                    growth,
                    "mandatory_rehab",
                    week,
                    TrainingIntensity.Standard);
            }
        }

        private static int CalculateMandatoryRehabilitationWeeks(
            PlayerGrowthState growth,
            int seasonYear)
        {
            int maximumAbsenceDays = 0;
            for (int index = growth.InjuryHistory.Count - 1; index >= 0; index--)
            {
                InjuryRecord injury = growth.InjuryHistory[index];
                if (injury.SeasonYear < seasonYear)
                    break;
                if (injury.SeasonYear == seasonYear)
                    maximumAbsenceDays = Math.Max(maximumAbsenceDays, injury.MaximumAbsenceDays);
            }
            if (maximumAbsenceDays < 21)
                return 0;
            return Math.Min(12, Math.Max(1, (int)Math.Ceiling(maximumAbsenceDays / 30d)));
        }

        private static PlayerAbility FindDefaultMasterFocusAbility(PlayerGrowthState growth)
        {
            PlayerAbility selected = growth.PlayerType == PlayerType.Batter
                ? PlayerAbility.Contact
                : PlayerAbility.Control;
            int selectedGap = -1;
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
            {
                PlayerAbility ability = (PlayerAbility)index;
                bool valid = growth.PlayerType == PlayerType.Batter
                    ? PlayerAbilityCatalog.IsBatterAbility(ability)
                    : PlayerAbilityCatalog.IsPitcherAbility(ability);
                if (!valid)
                    continue;
                int gap = growth.PotentialByAbility.Get(ability) - growth.BaseAbilities.Get(ability);
                if (gap <= selectedGap)
                    continue;
                selected = ability;
                selectedGap = gap;
            }
            return selected;
        }

        private static void UnlockLegacyTrait(
            string programId,
            OffseasonState offseason,
            PlayerGrowthState growth)
        {
            if (!offseason.IsLegacyTraitConversionUnlocked)
                return;
            if (string.Equals(programId, "legacy_batter_mastery", StringComparison.Ordinal))
                growth.UnlockLegacyTrait(SkillTraitIds.ScoringPositionFocus);
            else if (string.Equals(programId, "legacy_pitcher_mastery", StringComparison.Ordinal))
                growth.UnlockLegacyTrait(SkillTraitIds.CrisisManagement);
        }

        private static PlannedOffseasonActivity FindActivity(OffseasonState offseason, int activityId)
        {
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                if (offseason.Activities[index].ActivityId == activityId)
                    return offseason.Activities[index];
            }
            throw new ArgumentException("존재하지 않는 오프시즌 활동입니다.", nameof(activityId));
        }

        private static PlannedOffseasonActivity FindNextPlannedActivity(OffseasonState offseason)
        {
            PlannedOffseasonActivity result = null;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                PlannedOffseasonActivity candidate = offseason.Activities[index];
                if (candidate.Status != OffseasonActivityStatus.Planned)
                    continue;
                if (result == null ||
                    candidate.StartWeek < result.StartWeek ||
                    candidate.StartWeek == result.StartWeek && candidate.ActivityId < result.ActivityId)
                {
                    result = candidate;
                }
            }
            return result;
        }
    }
}
