using System;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>유학 취소는 진행 전 전액·진행 후 절반을 환급하고 성장 없이 귀환시킨다.</summary>
    public static class OwnerStudyCancellationService
    {
        public static long GetMoneyRefund(CardStudyProjectState project) =>
            project.RemainingWeeks == project.DurationWeeks ? project.PaidMoney : project.PaidMoney / 2;
        public static int GetPointRefund(CardStudyProjectState project) =>
            project.RemainingWeeks == project.DurationWeeks ? project.PaidDevelopmentPoints : project.PaidDevelopmentPoints / 2;
        public static void Cancel(ManagerHistoricalRuntimeState runtime, string cardId)
        {
            OwnerScheduleGateService.Evaluate(runtime, OwnerGrowthAction.Correction).RequireAllowed();
            for (int i = 0; i < runtime.PlayerGrowth.StudyProjects.Count; i++)
            {
                var project = runtime.PlayerGrowth.StudyProjects[i];
                if (project.CardId != cardId) continue;
                if (!runtime.TryGetOwnedCard(cardId, out var owned)) throw new InvalidOperationException("유학 선수가 없습니다.");
                long money = GetMoneyRefund(project); int points = GetPointRefund(project);
                _ = checked(runtime.Economy.Money + money); _ = checked(runtime.Economy.DevelopmentPoints + points);
                runtime.Economy.AddMoney(money); runtime.Economy.AddDevelopmentPoints(points);
                runtime.PlayerGrowth.RemoveStudyAt(i); owned.CancelStudyParticipation(); return;
            }
            throw new InvalidOperationException("유학 중인 선수가 아닙니다.");
        }
    }
}
