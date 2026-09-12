using System;
using Baseball.Core.Historical;
using Baseball.Game.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>유학 일정표에 표시할 선수·과정·귀환 시점을 복사한다.</summary>
    public readonly struct OwnerOffseasonTrainingRow
    {
        public OwnerOffseasonTrainingRow(string playerName, string programName, int remainingWeeks)
        {
            PlayerName = playerName;
            ProgramName = programName;
            RemainingWeeks = remainingWeeks;
        }
        public string PlayerName { get; }
        public string ProgramName { get; }
        public int RemainingWeeks { get; }
    }

    /// <summary>일정 팝업은 게임 상태를 직접 읽거나 변경하지 않는다.</summary>
    public sealed class OwnerOffseasonPresentationModel
    {
        public OwnerOffseasonPresentationModel(OwnerSeasonPhase phase, int completedWeeks,
            OwnerOffseasonTrainingRow[] training, string pendingActions = "")
        {
            Phase = phase;
            CompletedWeeks = completedWeeks;
            PendingActions = pendingActions;
            Training = (OwnerOffseasonTrainingRow[])(training ?? Array.Empty<OwnerOffseasonTrainingRow>()).Clone();
        }
        public OwnerSeasonPhase Phase { get; }
        public int CompletedWeeks { get; }
        public OwnerOffseasonTrainingRow[] Training { get; }
        public string PendingActions { get; }
        public bool CanAdvance => Phase == OwnerSeasonPhase.Offseason && CompletedWeeks < OwnerOffseasonState.DurationWeeks;

        public static OwnerOffseasonPresentationModel Build(OwnerModeManager manager)
        {
            var runtime = manager.Runtime;
            var projects = runtime.PlayerGrowth.StudyProjects;
            var rows = new OwnerOffseasonTrainingRow[projects.Count + runtime.PlayerGrowth.Camps.Count];
            for (int index = 0; index < projects.Count; index++)
            {
                var project = projects[index];
                runtime.WorldCardCatalog.TryGetCard(project.CardId, out var card);
                var season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                rows[index] = new OwnerOffseasonTrainingRow(
                    runtime.IdentityRegistry.GetPlayerDisplayName(season.PlayerPersonId),
                    manager.Balance.OwnerCardGrowth.GetStudyProgram(project.ProgramId).DisplayName,
                    project.RemainingWeeks);
            }
            for (int i = 0; i < runtime.PlayerGrowth.Camps.Count; i++)
            {
                var camp = runtime.PlayerGrowth.Camps[i];
                runtime.WorldCardCatalog.TryGetCard(camp.CardId, out var card);
                var season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                runtime.TryGetOwnedCard(camp.CardId, out var owned);
                int experience = OwnerCampService.ResolveWeeklyExperience(runtime,camp.CardId,camp.WeeklyExperience,manager.Balance);
                int weeks = camp.AutomaticReturn ? Math.Max(1, (int)Math.Ceiling((double)(camp.RequiredExperience - owned.SkillBoard.SlotExperience) / experience))
                    : runtime.PlayerGrowth.Offseason.RemainingWeeks;
                rows[projects.Count + i] = new OwnerOffseasonTrainingRow(runtime.IdentityRegistry.GetPlayerDisplayName(season.PlayerPersonId),
                    "전지훈련 · 성장판 경험치", Math.Min(weeks, runtime.PlayerGrowth.Offseason.RemainingWeeks));
            }
            int unlocks = 0; int required = manager.GetDevelopmentBalance().slotExperienceRequired;
            foreach (var owned in runtime.OwnedCards)
                if (owned.SkillBoard.SlotExperience >= required && owned.SkillBoard.UnlockedMask != OwnedCardSkillBoardState.CompleteUnlockedMask) unlocks++;
            string pending = "S 선택 상자 " + runtime.PlayerGrowth.Inventory.SelectionBoxes + "개 · 성장판 개방 가능 " + unlocks + "명"
                + (runtime.PlayerGrowth.Slogan == null ? " · 슬로건 미선택" : "");
            return new OwnerOffseasonPresentationModel(OwnerScheduleGateService.GetPhase(runtime),
                runtime.PlayerGrowth.Offseason.CompletedWeeks, rows, pending);
        }
    }
}
