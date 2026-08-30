using System;
using System.Collections.Generic;
using Baseball.Core.Players;

namespace Baseball.Core.Growth
{
    public enum OffseasonActivityStatus
    {
        Planned,
        InProgress,
        Completed,
        Cancelled
    }

    /// <summary>
    /// 12주 플래너에 배치된 한 활동과 진행 상태다.
    /// </summary>
    public sealed class PlannedOffseasonActivity
    {
        public PlannedOffseasonActivity(
            int activityId,
            string programId,
            int startWeek,
            int durationWeeks,
            TrainingIntensity intensity = TrainingIntensity.Standard)
        {
            if (activityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(activityId));
            if (string.IsNullOrWhiteSpace(programId))
                throw new ArgumentException("ProgramId는 비어 있을 수 없습니다.", nameof(programId));
            if (startWeek <= 0 || durationWeeks <= 0)
                throw new ArgumentOutOfRangeException(nameof(startWeek));
            if (intensity < TrainingIntensity.Safe || intensity > TrainingIntensity.Intensive)
                throw new ArgumentOutOfRangeException(nameof(intensity));
            ActivityId = activityId;
            ProgramId = programId;
            StartWeek = startWeek;
            DurationWeeks = durationWeeks;
            Intensity = intensity;
            Status = OffseasonActivityStatus.Planned;
        }

        public int ActivityId { get; }
        public string ProgramId { get; }
        public int StartWeek { get; private set; }
        public int DurationWeeks { get; }
        public TrainingIntensity Intensity { get; }
        public int EndWeek => StartWeek + DurationWeeks - 1;
        public OffseasonActivityStatus Status { get; private set; }
        public ulong RandomSeed { get; private set; }

        public void Start(ulong randomSeed)
        {
            if (Status != OffseasonActivityStatus.Planned)
                throw new InvalidOperationException("계획된 활동만 시작할 수 있습니다.");
            RandomSeed = randomSeed;
            Status = OffseasonActivityStatus.InProgress;
        }

        public void Complete()
        {
            if (Status != OffseasonActivityStatus.InProgress)
                throw new InvalidOperationException("진행 중인 활동만 완료할 수 있습니다.");
            Status = OffseasonActivityStatus.Completed;
        }

        public void Cancel()
        {
            if (Status != OffseasonActivityStatus.Planned)
                throw new InvalidOperationException("시작하지 않은 활동만 취소할 수 있습니다.");
            Status = OffseasonActivityStatus.Cancelled;
        }

        /// <summary>
        /// 아직 시작하지 않은 계획을 앞 활동 삭제 후 빈 주차가 없도록 다시 배치한다.
        /// </summary>
        public void Reschedule(int startWeek)
        {
            if (Status != OffseasonActivityStatus.Planned)
                throw new InvalidOperationException("계획된 활동만 다시 배치할 수 있습니다.");
            if (startWeek <= 0)
                throw new ArgumentOutOfRangeException(nameof(startWeek));
            StartWeek = startWeek;
        }
    }

    /// <summary>
    /// 한 시즌의 고정된 오프시즌 시간 예산과 활동 계획을 소유한다.
    /// </summary>
    public sealed class OffseasonState
    {
        private readonly List<PlannedOffseasonActivity> _activities;

        public OffseasonState(
            int seasonYear,
            int totalWeeks,
            int currentCondition,
            int mandatoryRehabWeeks = 0,
            TrainingAccessTier knowledgeTier = TrainingAccessTier.Legacy,
            TrainingAccessTier facilityTier = TrainingAccessTier.Legacy,
            int additionalProgramCandidates = 0,
            int repetitionPenaltyWaivers = 0,
            PlayerAbility? masterFocusAbility = null,
            bool isLegacyTraitConversionUnlocked = false)
        {
            if (totalWeeks <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalWeeks));
            if (currentCondition < 0 || currentCondition > 100)
                throw new ArgumentOutOfRangeException(nameof(currentCondition));
            if (mandatoryRehabWeeks < 0 || mandatoryRehabWeeks > totalWeeks)
                throw new ArgumentOutOfRangeException(nameof(mandatoryRehabWeeks));
            SeasonYear = seasonYear;
            TotalWeeks = totalWeeks;
            CurrentWeek = 1;
            CurrentCondition = currentCondition;
            MandatoryRehabWeeks = mandatoryRehabWeeks;
            KnowledgeTier = knowledgeTier;
            FacilityTier = facilityTier;
            AdditionalProgramCandidates = Math.Max(0, additionalProgramCandidates);
            RepetitionPenaltyWaivers = Math.Max(0, repetitionPenaltyWaivers);
            MasterFocusAbility = masterFocusAbility;
            IsLegacyTraitConversionUnlocked = isLegacyTraitConversionUnlocked;
            _activities = new List<PlannedOffseasonActivity>();
        }

        public int SeasonYear { get; }
        public int TotalWeeks { get; }
        public int CurrentWeek { get; private set; }
        public int CurrentCondition { get; private set; }
        public int MandatoryRehabWeeks { get; }
        public TrainingAccessTier KnowledgeTier { get; }
        public TrainingAccessTier FacilityTier { get; }
        public int AdditionalProgramCandidates { get; }
        public int RepetitionPenaltyWaivers { get; private set; }
        public PlayerAbility? MasterFocusAbility { get; private set; }
        public bool IsLegacyTraitConversionUnlocked { get; }
        public int CompletedRestWeeks { get; private set; }
        public int CompletedRehabilitationWeeks { get; private set; }
        public double NextSeasonInjuryRiskReduction => Math.Min(
            0.30d,
            (CompletedRestWeeks >= 3 ? 0.08d : 0d) + CompletedRehabilitationWeeks * 0.04d);
        public int PhysicalDeclineProtectionPoints => CompletedRehabilitationWeeks >= 2 ? 1 : 0;
        public bool StudyUsed { get; private set; }
        public bool BoardRedesignUsed { get; private set; }
        public bool IsCompleted => CurrentWeek > TotalWeeks;
        public IReadOnlyList<PlannedOffseasonActivity> Activities => _activities;

        public void AddActivity(PlannedOffseasonActivity activity)
        {
            _activities.Add(activity ?? throw new ArgumentNullException(nameof(activity)));
        }

        public void MarkStudyUsed()
        {
            if (StudyUsed)
                throw new InvalidOperationException("유학은 오프시즌당 한 번만 가능합니다.");
            StudyUsed = true;
        }

        public void MarkBoardRedesignUsed()
        {
            if (BoardRedesignUsed)
                throw new InvalidOperationException("전문 재설계는 오프시즌당 한 번만 가능합니다.");
            BoardRedesignUsed = true;
        }

        public bool TryUseRepetitionPenaltyWaiver()
        {
            if (RepetitionPenaltyWaivers <= 0)
                return false;
            RepetitionPenaltyWaivers--;
            return true;
        }

        public void SetMasterFocusAbility(PlayerAbility ability)
        {
            if (MasterFocusAbility == null)
                throw new InvalidOperationException("Master 성장 집중 권한이 없습니다.");
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
            MasterFocusAbility = ability;
        }

        public void RecordCompletedRecovery(OffseasonActivityType activityType, int durationWeeks)
        {
            if (durationWeeks <= 0)
                return;
            if (activityType == OffseasonActivityType.Rest)
                CompletedRestWeeks += durationWeeks;
            else if (activityType == OffseasonActivityType.Rehabilitation)
                CompletedRehabilitationWeeks += durationWeeks;
        }

        public void AdvanceToWeek(int week)
        {
            if (week < CurrentWeek || week > TotalWeeks + 1)
                throw new ArgumentOutOfRangeException(nameof(week));
            CurrentWeek = week;
        }

        public void SetCurrentCondition(int value)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(nameof(value));
            CurrentCondition = value;
        }

        /// <summary>
        /// 남은 주를 활동으로 채우지 않고 오프시즌을 마감할 때, 진행 중인 활동이 없는지 확인한 뒤
        /// 마지막 주 다음으로 넘겨 IsCompleted를 참으로 만든다.
        /// </summary>
        public void CompleteRemainingWeeks()
        {
            for (int index = 0; index < _activities.Count; index++)
            {
                if (_activities[index].Status == OffseasonActivityStatus.InProgress)
                    throw new InvalidOperationException("진행 중인 활동이 있으면 오프시즌을 마감할 수 없습니다.");
            }
            if (CompletedRehabilitationWeeks < MandatoryRehabWeeks)
            {
                throw new InvalidOperationException(
                    $"필수 재활 {MandatoryRehabWeeks - CompletedRehabilitationWeeks}주를 먼저 완료해야 합니다.");
            }
            AdvanceToWeek(TotalWeeks + 1);
        }
    }
}
