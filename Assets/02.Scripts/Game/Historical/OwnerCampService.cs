using System;
using Baseball.Core.Historical;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>전지훈련의 등록·중도 귀환·경험치·인접 칸 개방을 관리한다.</summary>
    public static class OwnerCampService
    {
        public static void Start(ManagerHistoricalRuntimeState runtime, string cardId, OwnerCampDefinition camp,
            int experienceRequired, bool automaticReturn = true)
        {
            OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.TrainingCamp, 1).RequireAllowed();
            if (!runtime.TryGetOwnedCard(cardId, out var owned)) throw new InvalidOperationException("보유 선수를 선택하세요.");
            foreach (var entry in runtime.GetRoster(runtime.PlayerTeamSeasonKey).Entries)
                if (entry.CardId == cardId) throw new InvalidOperationException("전지훈련에는 2군 선수만 등록할 수 있습니다.");
            foreach (var study in runtime.PlayerGrowth.StudyProjects)
                if (study.CardId == cardId) throw new InvalidOperationException("유학 중인 선수는 등록할 수 없습니다.");
            int count = 0;
            foreach (var project in runtime.PlayerGrowth.Camps)
            {
                if (project.CardId == cardId) throw new InvalidOperationException("이미 전지훈련 중입니다.");
                if (project.FacilityId == camp.id) count++;
            }
            if (count >= camp.capacity) throw new InvalidOperationException("이 캠프의 정원은 " + camp.capacity + "명입니다.");
            if (owned.SkillBoard.UnlockedMask == OwnedCardSkillBoardState.CompleteUnlockedMask) throw new InvalidOperationException("성장판 16칸이 모두 열렸습니다.");
            var progress = OwnerCardStudyUnlockEvaluator.Evaluate(runtime, camp.requiredChampionshipLeague);
            if (progress.HighestLeagueGrade < camp.requiredLeague || progress.PostseasonChampionships < camp.requiredChampionships)
                throw new InvalidOperationException("캠프 해금에 필요한 리그·우승 조건을 달성하지 못했습니다.");
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out var card)) throw new InvalidOperationException("선수 원본이 없습니다.");
            long fee = checked(runtime.WorldCardCatalog.GetPlayerSeason(card).Cost * camp.costPerCardCost);
            var projectState = new OwnerCampProject(cardId, camp.id, camp.weeklyExperience, experienceRequired, automaticReturn);
            if (!runtime.Economy.TrySpendMoney(fee)) throw new InvalidOperationException("캠프 등록에 필요한 PT가 부족합니다.");
            runtime.PlayerGrowth.Camps.Add(projectState);
        }
        public static int ResolveWeeklyExperience(ManagerHistoricalRuntimeState runtime, string cardId, OwnerCampDefinition camp, BalanceTable balance)
            => ResolveWeeklyExperience(runtime, cardId, camp.weeklyExperience, balance);
        public static int ResolveWeeklyExperience(ManagerHistoricalRuntimeState runtime, string cardId, int baseExperience, BalanceTable balance)
        {
            if (balance == null) return baseExperience;
            runtime.WorldCardCatalog.TryGetCard(cardId, out var card);
            var discipline = runtime.WorldCardCatalog.GetPlayerSeason(card).PlayerType == PlayerType.Batter
                ? StaffTrainingDiscipline.Hitting : StaffTrainingDiscipline.Pitching;
            var mode = runtime.ManagerMode;
            var profile = new TeamStaffEffectResolver().Resolve(mode.StaffCatalog, mode.StaffContracts, mode.StaffAssignment, balance.Staff);
            var efficiency = StaffTrainingEfficiencyResolver.Resolve(profile, new StaffTrainingEfficiencyContext(discipline, true));
            return checked((int)Math.Floor(baseExperience * efficiency.EfficiencyMultiplier));
        }
        public static void Return(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.TrainingCamp).RequireAllowed();
            for (int i = 0; i < runtime.PlayerGrowth.Camps.Count; i++)
                if (runtime.PlayerGrowth.Camps[i].CardId == cardId) { runtime.PlayerGrowth.Camps.RemoveAt(i); return; }
            throw new InvalidOperationException("전지훈련 중인 선수가 아닙니다.");
        }
        public static void AdvanceWeek(ManagerHistoricalRuntimeState runtime, BalanceTable balance = null)
        {
            for (int i = runtime.PlayerGrowth.Camps.Count - 1; i >= 0; i--)
            {
                var project = runtime.PlayerGrowth.Camps[i];
                if (!runtime.TryGetOwnedCard(project.CardId, out var owned)) throw new InvalidOperationException("캠프 선수가 없습니다.");
                owned.SkillBoard.AddSlotExperience(ResolveWeeklyExperience(runtime, project.CardId, project.WeeklyExperience, balance));
                if (runtime.PlayerGrowth.Offseason.RemainingWeeks == 0
                    || project.AutomaticReturn && owned.SkillBoard.SlotExperience >= project.RequiredExperience)
                    runtime.PlayerGrowth.Camps.RemoveAt(i);
            }
        }
        public static void Unlock(ManagerHistoricalRuntimeState runtime, string cardId, int x, int y, int requiredExperience)
        {
            OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.SkillBlock).RequireAllowed();
            if (!runtime.TryGetOwnedCard(cardId, out var owned)) throw new InvalidOperationException("보유 선수를 선택하세요.");
            owned.SkillBoard.UnlockCell(x, y, requiredExperience);
        }
    }
}
