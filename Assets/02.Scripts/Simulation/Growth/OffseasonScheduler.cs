using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Growth
{
    /// <summary>
    /// 12주 시간 예산의 겹침·유학·파트너·비용 제약을 검증하고 활동을 진행한다.
    /// </summary>
    public sealed class OffseasonScheduler
    {
        private readonly GrowthBalanceTable _balance;
        private readonly GrowthResolver _growthResolver;

        public OffseasonScheduler(GrowthBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _growthResolver = new GrowthResolver(balance);
        }

        public PlannedOffseasonActivity PlanActivity(
            OffseasonState offseason,
            CareerEconomyState economy,
            PlayerGrowthState player,
            string programId,
            int startWeek,
            TrainingIntensity intensity = TrainingIntensity.Standard)
        {
            if (offseason == null) throw new ArgumentNullException(nameof(offseason));
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            if (player == null) throw new ArgumentNullException(nameof(player));
            TrainingProgramDefinition program = _balance.GetProgram(programId, intensity);
            if (!program.CanUse(player.PlayerType))
                throw new InvalidOperationException("선수 유형에 맞지 않는 프로그램입니다.");
            if (startWeek < offseason.CurrentWeek || startWeek + program.DurationWeeks - 1 > offseason.TotalWeeks)
                throw new InvalidOperationException("남은 오프시즌 주 수에 배치할 수 없습니다.");
            ValidateNoOverlap(offseason, startWeek, program.DurationWeeks);
            ValidateUniqueActivity(offseason, program);
            int projectedCondition = GetProjectedConditionBefore(
                offseason,
                player.Condition,
                startWeek);
            if (projectedCondition < program.MinimumCondition)
            {
                throw new InvalidOperationException(
                    $"계획 순서상 시작 컨디션이 {projectedCondition}으로 예상됩니다. " +
                    $"컨디션 {program.MinimumCondition} 이상이 되도록 회복 활동을 먼저 배치해 주세요.");
            }
            if (GetPlannedCost(offseason) + program.MoneyCost > economy.Money)
                throw new InvalidOperationException("계획한 활동을 모두 실행할 Money가 부족합니다.");

            int activityId = GetNextActivityId(offseason);
            var activity = new PlannedOffseasonActivity(
                activityId,
                program.ProgramId,
                startWeek,
                program.DurationWeeks,
                intensity);
            offseason.AddActivity(activity);
            return activity;
        }

        public void CancelActivity(OffseasonState offseason, int activityId)
        {
            PlannedOffseasonActivity activity = FindActivity(offseason, activityId);
            activity.Cancel();
            CompactPlannedActivities(offseason);
        }

        private static void CompactPlannedActivities(OffseasonState offseason)
        {
            int count = 0;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                if (offseason.Activities[index].Status == OffseasonActivityStatus.Planned)
                    count++;
            }
            var planned = new PlannedOffseasonActivity[count];
            int writeIndex = 0;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                PlannedOffseasonActivity activity = offseason.Activities[index];
                if (activity.Status == OffseasonActivityStatus.Planned)
                    planned[writeIndex++] = activity;
            }
            Array.Sort(planned, (left, right) =>
            {
                int weekComparison = left.StartWeek.CompareTo(right.StartWeek);
                return weekComparison != 0
                    ? weekComparison
                    : left.ActivityId.CompareTo(right.ActivityId);
            });
            int startWeek = offseason.CurrentWeek;
            for (int index = 0; index < planned.Length; index++)
            {
                planned[index].Reschedule(startWeek);
                startWeek = planned[index].EndWeek + 1;
            }
        }

        /// <summary>
        /// 활동 시작 시 비용과 결과 Seed를 확정해 재로드 재뽑기를 차단한다.
        /// </summary>
        public void StartActivity(
            OffseasonState offseason,
            CareerEconomyState economy,
            PlayerGrowthState player,
            PlayerStudyState studyState,
            int activityId,
            ulong randomSeed)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            PlannedOffseasonActivity activity = FindActivity(offseason, activityId);
            TrainingProgramDefinition program = _balance.GetProgram(
                activity.ProgramId,
                activity.Intensity);
            if (activity.Status != OffseasonActivityStatus.Planned)
                throw new InvalidOperationException("계획된 활동만 시작할 수 있습니다.");
            if (activity.StartWeek < offseason.CurrentWeek)
                throw new InvalidOperationException("시작 시점을 지난 활동입니다.");
            if (!program.CanUse(player.PlayerType))
                throw new InvalidOperationException("선수 유형에 맞지 않는 프로그램입니다.");
            if (player.Condition < program.MinimumCondition)
                throw new InvalidOperationException("현재 컨디션으로 시작할 수 없는 프로그램입니다.");
            if (program.IsStudy && studyState == null)
                throw new InvalidOperationException("유학 시작에는 PlayerStudyState가 필요합니다.");
            if (program.IsStudy && offseason.StudyUsed)
                throw new InvalidOperationException("유학은 오프시즌당 한 번만 가능합니다.");
            if (program.IsStudy && studyState.StudyUsedThisOffseason)
                throw new InvalidOperationException("유학은 오프시즌당 한 번만 가능합니다.");
            if (economy.Money < program.MoneyCost)
                throw new InvalidOperationException("Money가 부족합니다.");

            economy.Spend(
                offseason.SeasonYear,
                GetExpenseType(program.ActivityType),
                program.ProgramId,
                program.MoneyCost);
            if (program.IsStudy)
                offseason.MarkStudyUsed();
            offseason.AdvanceToWeek(activity.StartWeek);
            activity.Start(randomSeed);
        }

        public GrowthResultRecord CompleteActivity(
            OffseasonState offseason,
            PlayerGrowthState player,
            PlayerStudyState studyState,
            int activityId,
            IRandomSource random)
        {
            PlannedOffseasonActivity activity = FindActivity(offseason, activityId);
            if (activity.Status != OffseasonActivityStatus.InProgress)
                throw new InvalidOperationException("진행 중인 활동만 완료할 수 있습니다.");
            TrainingProgramDefinition program = _balance.GetProgram(
                activity.ProgramId,
                activity.Intensity);
            if (program.IsStudy && studyState == null)
                throw new InvalidOperationException("유학 완료에는 PlayerStudyState가 필요합니다.");
            if (program.IsStudy && studyState.StudyUsedThisOffseason)
                throw new InvalidOperationException("이미 완료한 유학을 다시 반영할 수 없습니다.");
            int priorSelections = program.IsStudy && studyState != null
                ? studyState.GetConsecutiveVisits(program.ProgramId)
                : CountCompletedCategory(offseason, program.Category);
            TrainingFitGrade fit = player.GetTrainingFit(program.Category);
            GrowthResultRecord result = _growthResolver.Resolve(
                player,
                program,
                offseason.SeasonYear,
                priorSelections,
                fit,
                activity.RandomSeed,
                random);

            if (program.IsStudy)
            {
                studyState.RecordVisit(program.ProgramId, offseason.SeasonYear);
            }
            activity.Complete();
            offseason.SetCurrentCondition(player.Condition);
            offseason.AdvanceToWeek(activity.EndWeek + 1);
            return result;
        }

        private long GetPlannedCost(OffseasonState offseason)
        {
            long total = 0L;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                PlannedOffseasonActivity activity = offseason.Activities[index];
                if (activity.Status != OffseasonActivityStatus.Planned)
                    continue;
                total = checked(total + _balance.GetProgram(
                    activity.ProgramId,
                    activity.Intensity).MoneyCost);
            }
            return total;
        }

        private int GetProjectedConditionBefore(
            OffseasonState offseason,
            int currentCondition,
            int startWeek)
        {
            int condition = currentCondition;
            for (int week = offseason.CurrentWeek; week < startWeek; week++)
            {
                for (int index = 0; index < offseason.Activities.Count; index++)
                {
                    PlannedOffseasonActivity activity = offseason.Activities[index];
                    if (activity.Status != OffseasonActivityStatus.Planned ||
                        activity.StartWeek != week)
                    {
                        continue;
                    }
                    TrainingProgramDefinition planned = _balance.GetProgram(
                        activity.ProgramId,
                        activity.Intensity);
                    condition = Math.Max(0, Math.Min(100, condition + planned.ConditionChange));
                    break;
                }
            }
            return condition;
        }

        private static int GetNextActivityId(OffseasonState offseason)
        {
            int maximum = 0;
            for (int index = 0; index < offseason.Activities.Count; index++)
                maximum = Math.Max(maximum, offseason.Activities[index].ActivityId);
            return maximum + 1;
        }

        private static void ValidateNoOverlap(OffseasonState offseason, int startWeek, int durationWeeks)
        {
            int endWeek = startWeek + durationWeeks - 1;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                PlannedOffseasonActivity existing = offseason.Activities[index];
                if (existing.Status == OffseasonActivityStatus.Cancelled)
                    continue;
                if (startWeek <= existing.EndWeek && endWeek >= existing.StartWeek)
                    throw new InvalidOperationException("오프시즌 활동은 서로 겹칠 수 없습니다.");
            }
        }

        private void ValidateUniqueActivity(OffseasonState offseason, TrainingProgramDefinition program)
        {
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                PlannedOffseasonActivity existing = offseason.Activities[index];
                if (existing.Status == OffseasonActivityStatus.Cancelled)
                    continue;
                TrainingProgramDefinition existingProgram = _balance.GetProgram(
                    existing.ProgramId,
                    existing.Intensity);
                if (program.IsStudy && existingProgram.IsStudy)
                    throw new InvalidOperationException("유학은 오프시즌당 한 번만 계획할 수 있습니다.");
                if (program.ActivityType == OffseasonActivityType.TrainingPartner &&
                    existingProgram.ActivityType == OffseasonActivityType.TrainingPartner &&
                    string.Equals(program.PartnerId, existingProgram.PartnerId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("같은 훈련 파트너는 오프시즌당 한 번만 선택할 수 있습니다.");
                }
            }
        }

        private int CountCompletedCategory(OffseasonState offseason, TrainingCategory category)
        {
            int count = 0;
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                PlannedOffseasonActivity activity = offseason.Activities[index];
                if (activity.Status == OffseasonActivityStatus.Completed &&
                    _balance.GetProgram(activity.ProgramId, activity.Intensity).Category == category)
                    count++;
            }
            return count;
        }

        private static PlannedOffseasonActivity FindActivity(OffseasonState offseason, int activityId)
        {
            if (offseason == null) throw new ArgumentNullException(nameof(offseason));
            for (int index = 0; index < offseason.Activities.Count; index++)
            {
                if (offseason.Activities[index].ActivityId == activityId)
                    return offseason.Activities[index];
            }
            throw new ArgumentException("존재하지 않는 오프시즌 활동입니다.", nameof(activityId));
        }

        private static MoneyTransactionType GetExpenseType(OffseasonActivityType type)
        {
            return type == OffseasonActivityType.Rehabilitation
                ? MoneyTransactionType.TreatmentExpense
                : MoneyTransactionType.TrainingExpense;
        }
    }
}
