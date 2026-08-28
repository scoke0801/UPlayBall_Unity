using System;

namespace Baseball.Game.Career.News
{
    /// <summary>부상 시스템의 확정 진단과 복귀 단계만 뉴스 사건으로 변환한다.</summary>
    public sealed class InjuryNewsEvaluator
    {
        public NewsEvent EvaluateConfirmedInjury(
            string eventId,
            CareerDate occurredAt,
            PlayerState player,
            TeamState team,
            int expectedAbsenceGames,
            bool isSeasonEnding)
        {
            if (expectedAbsenceGames <= 1 && !isSeasonEnding)
                return null;
            int importance = isSeasonEnding
                ? 55
                : expectedAbsenceGames >= 22 ? 45 : expectedAbsenceGames >= 6 ? 30 : 20;
            var newsEvent = new NewsEvent(
                eventId,
                NewsEventType.PlayerInjuryConfirmed,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(player.PlayerId, player.Name),
                $"injury_{player.PlayerId}_{occurredAt.Cycle.SeasonId}",
                importance)
            {
                CareerImpact = isSeasonEnding ? 35 : expectedAbsenceGames >= 6 ? 20 : 5,
                Rarity = isSeasonEnding ? 20 : 0,
                IsCareerArchive = expectedAbsenceGames >= 6
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, player.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            newsEvent.FactSet.SetInteger(NewsFactKey.ExpectedAbsenceGames, expectedAbsenceGames);
            return newsEvent;
        }

        public NewsEvent EvaluateReturn(
            string eventId,
            CareerDate occurredAt,
            PlayerState player,
            TeamState team)
        {
            var newsEvent = new NewsEvent(
                eventId,
                NewsEventType.PlayerReturnedFromInjury,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(player.PlayerId, player.Name),
                $"injury_{player.PlayerId}_{occurredAt.Cycle.SeasonId}",
                baseImportance: 30)
            {
                CareerImpact = 20,
                IsCareerArchive = true
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, player.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            return newsEvent;
        }
    }

    /// <summary>감독이 확정한 장기 역할 변화만 뉴스 사건으로 변환한다.</summary>
    public sealed class PlayerRoleNewsEvaluator
    {
        public NewsEvent Evaluate(
            string eventId,
            CareerDate occurredAt,
            PlayerState player,
            TeamState team,
            string previousRole,
            string newRole)
        {
            if (string.Equals(previousRole, newRole, StringComparison.Ordinal))
                return null;
            var newsEvent = new NewsEvent(
                eventId,
                NewsEventType.PlayerRoleChanged,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(player.PlayerId, player.Name),
                $"role_{player.PlayerId}_{occurredAt.Cycle.SeasonId}",
                baseImportance: 30)
            {
                CareerImpact = 20,
                IsCareerArchive = true,
                CooldownGroup = $"role_change_{player.PlayerId}"
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, player.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            newsEvent.FactSet.SetText(NewsFactKey.PreviousRole, previousRole);
            newsEvent.FactSet.SetText(NewsFactKey.NewRole, newRole);
            return newsEvent;
        }
    }

    /// <summary>서명이 끝난 공개 계약만 기사 사건으로 만들며 제안 단계는 받지 않는다.</summary>
    public sealed class ContractNewsEvaluator
    {
        public NewsEvent EvaluateSignedContract(
            string eventId,
            CareerDate occurredAt,
            PlayerState player,
            TeamState team,
            int contractYears,
            long annualSalary)
        {
            var newsEvent = new NewsEvent(
                eventId,
                NewsEventType.ContractSigned,
                occurredAt,
                NewsReleaseGate.AfterContractConfirmation,
                NewsSubject.Player(player.PlayerId, player.Name),
                eventId,
                baseImportance: 40)
            {
                CareerImpact = 25,
                IsCareerArchive = true
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, player.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            newsEvent.FactSet.SetInteger(NewsFactKey.ContractYears, contractYears);
            newsEvent.FactSet.SetInteger(NewsFactKey.ContractSalary, annualSalary);
            return newsEvent;
        }
    }

    /// <summary>외부에 알려질 가치가 있다고 성장 시스템이 판정한 오프시즌 활동만 기사화한다.</summary>
    public sealed class OffseasonNewsEvaluator
    {
        public NewsEvent EvaluateCompletedActivity(
            string eventId,
            CareerDate occurredAt,
            PlayerState player,
            TeamState team,
            string publicActivityName,
            bool isMajorChange)
        {
            if (string.IsNullOrWhiteSpace(publicActivityName))
                return null;
            var newsEvent = new NewsEvent(
                eventId,
                NewsEventType.OffseasonActivityCompleted,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(player.PlayerId, player.Name),
                eventId,
                baseImportance: isMajorChange ? 35 : 22)
            {
                CareerImpact = isMajorChange ? 20 : 5,
                IsCareerArchive = isMajorChange
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, player.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            newsEvent.FactSet.SetText(NewsFactKey.OffseasonActivityName, publicActivityName);
            return newsEvent;
        }
    }
}
