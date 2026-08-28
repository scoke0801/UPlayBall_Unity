using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
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

            _naturalDevelopmentResolver = new NaturalDevelopmentResolver(balance.Growth);
            _agingResolver = new AgingResolver(balance.Growth);
            _offseasonScheduler = new OffseasonScheduler(balance.Growth);
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

            SeasonState season = _career.League.CurrentSeason ??
                                 throw new InvalidOperationException("현재 시즌이 없습니다.");
            if (season.Phase != SeasonPhase.SeasonReview)
                throw new InvalidOperationException("시즌 결산 단계에서만 성장 결산할 수 있습니다.");
            if (_career.CurrentOffseason != null)
                throw new InvalidOperationException("이미 진행 중인 오프시즌이 있습니다.");

            PlayerGrowthState growth = _career.MyPlayer.GrowthState;
            ulong seasonStream = ((ulong)(uint)season.SeasonId << 32) | (uint)growth.PlayerId;
            ulong naturalSeed = DeterministicSeed.Derive(
                _career.League.RandomSeed,
                seasonStream ^ NaturalDevelopmentStream);
            ulong agingSeed = DeterministicSeed.Derive(
                _career.League.RandomSeed,
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

            long salaryIncome = _career.CurrentContract.AnnualSalary;
            if (salaryIncome > 0L)
            {
                _career.Economy.Earn(
                    season.Year,
                    MoneyTransactionType.SalaryIncome,
                    "current_contract",
                    salaryIncome);
            }
            if (bonusIncome > 0L)
            {
                _career.Economy.Earn(
                    season.Year,
                    MoneyTransactionType.BonusIncome,
                    "season_bonus",
                    bonusIncome);
            }

            var offseason = new OffseasonState(
                season.Year,
                _balance.Growth.OffseasonWeeks,
                growth.Condition,
                mandatoryRehabWeeks);
            _career.BeginOffseason(offseason);
            _career.MyPlayer.SynchronizeFromGrowthState();
            season.BeginOffseason();
            return new SeasonGrowthSettlementResult(
                naturalDevelopment,
                aging,
                salaryIncome,
                bonusIncome,
                offseason);
        }

        public PlannedOffseasonActivity PlanActivity(string programId, int startWeek)
        {
            OffseasonState offseason = RequireOffseason();
            return _offseasonScheduler.PlanActivity(
                offseason,
                _career.Economy,
                _career.MyPlayer.GrowthState,
                programId,
                startWeek);
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
                           ((ulong)(uint)_career.League.CurrentSeason.SeasonId << 32) ^
                           (uint)activityId;
            ulong activitySeed = DeterministicSeed.Derive(_career.League.RandomSeed, stream);
            _offseasonScheduler.StartActivity(
                offseason,
                _career.Economy,
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
            _career.MyPlayer.SynchronizeFromGrowthState();
            return result;
        }

        private OffseasonState RequireOffseason()
        {
            if (_career.League.CurrentSeason?.Phase != SeasonPhase.Offseason ||
                _career.CurrentOffseason == null)
            {
                throw new InvalidOperationException("진행 중인 오프시즌이 없습니다.");
            }
            return _career.CurrentOffseason;
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
    }
}
