using System;

namespace Baseball.Game.Historical
{
    public enum OwnerSeasonPhase
    {
        RegularSeason,
        Postseason,
        Offseason
    }

    public enum OwnerGrowthAction
    {
        Support,
        Staff,
        Slogan,
        SkillBlock,
        OverseasTraining,
        Correction,
        TrainingPartner,
        TraitTraining
    }

    /// <summary>UI의 잠금 안내와 실제 성장 명령이 공유하는 일정 판정이다.</summary>
    public readonly struct OwnerSchedulePermission
    {
        public OwnerSchedulePermission(bool isAllowed, string reason)
        {
            IsAllowed = isAllowed;
            Reason = reason ?? string.Empty;
        }

        public bool IsAllowed { get; }
        public string Reason { get; }

        public void RequireAllowed()
        {
            if (!IsAllowed) throw new InvalidOperationException(Reason);
        }
    }

    /// <summary>정규시즌과 모든 조의 포스트시즌 종료를 기준으로 성장 명령의 실행 시기를 검증한다.</summary>
    public static class OwnerScheduleGateService
    {
        public static OwnerSeasonPhase GetPhase(ManagerHistoricalRuntimeState runtime)
        {
            if (runtime?.ManagerMode == null)
                throw new ArgumentException("구단주 시즌 진행 상태가 필요합니다.", nameof(runtime));
            if (!runtime.ManagerMode.LiveSeason.IsCompleted ||
                runtime.LeagueWorld != null && !runtime.LeagueWorld.IsRegularSeasonCompleted)
                return OwnerSeasonPhase.RegularSeason;
            return runtime.LeagueWorld != null && !runtime.LeagueWorld.IsPostseasonCompleted
                ? OwnerSeasonPhase.Postseason
                : OwnerSeasonPhase.Offseason;
        }

        /// <summary>비시즌에는 경기 기용이 없으므로 1군 등록을 유지한 채 유학에 참가한다.</summary>
        public static bool CanStudyWhileRegistered(ManagerHistoricalRuntimeState runtime) =>
            GetPhase(runtime) == OwnerSeasonPhase.Offseason;

        /// <summary>시즌 중 유학 선수의 재등록으로 경기 불참 조건을 우회하지 못하게 한다.</summary>
        public static OwnerSchedulePermission EvaluateStudyRosterRegistration(
            ManagerHistoricalRuntimeState runtime, string cardId)
        {
            if (!CanStudyWhileRegistered(runtime))
                foreach (var project in runtime.PlayerGrowth.StudyProjects)
                    if (string.Equals(project.CardId, cardId, StringComparison.Ordinal))
                        return new OwnerSchedulePermission(false, "유학 중인 선수는 귀환 후 1군에 등록할 수 있습니다.");
            return new OwnerSchedulePermission(true, string.Empty);
        }

        public static OwnerSchedulePermission Evaluate(
            ManagerHistoricalRuntimeState runtime, OwnerGrowthAction action, int durationWeeks = 0)
        {
            if (!Enum.IsDefined(typeof(OwnerGrowthAction), action))
                throw new ArgumentOutOfRangeException(nameof(action));
            if (durationWeeks < 0) throw new ArgumentOutOfRangeException(nameof(durationWeeks));
            OwnerSeasonPhase phase = GetPhase(runtime);
            if (action == OwnerGrowthAction.Support || action == OwnerGrowthAction.Staff ||
                action == OwnerGrowthAction.SkillBlock ||
                action == OwnerGrowthAction.OverseasTraining && phase != OwnerSeasonPhase.Offseason)
                return new OwnerSchedulePermission(true, string.Empty);
            if (phase != OwnerSeasonPhase.Offseason)
                return new OwnerSchedulePermission(false, "시즌 중에는 성장 효과를 조회할 수 있습니다. 포스트시즌 종료 후 이용해 주세요.");
            int remaining = runtime.PlayerGrowth.Offseason.RemainingWeeks;
            if (remaining == 0 && action == OwnerGrowthAction.OverseasTraining)
                return new OwnerSchedulePermission(false, "이번 오프시즌 훈련 일정이 끝났습니다. 다음 시즌을 시작해 주세요.");
            if (durationWeeks > remaining)
                return new OwnerSchedulePermission(false, $"남은 오프시즌은 {remaining}주입니다. {durationWeeks}주 과정은 시작할 수 없습니다.");
            return new OwnerSchedulePermission(true, string.Empty);
        }
    }
}
