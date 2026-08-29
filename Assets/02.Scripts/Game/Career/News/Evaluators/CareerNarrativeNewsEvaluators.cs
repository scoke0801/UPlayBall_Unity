using System;
using System.Collections.Generic;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career.News
{
    /// <summary>실제 연장 제안·거절 사실을 계약 스토리 사건으로 변환한다.</summary>
    public sealed class ContractNarrativeNewsEvaluator
    {
        public NewsEvent EvaluateOffer(
            CareerState career,
            CareerDate occurredAt,
            ContractOffer offer)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            TeamState team = FindTeam(career, offer.Team.TeamId);
            var newsEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_extension_offer",
                NewsEventType.ContractNegotiationReported,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name),
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_contract",
                baseImportance: 32)
            {
                CareerImpact = 18
            };
            AddContractFacts(newsEvent, career, team, offer.ContractYears, offer.AnnualSalary);
            return newsEvent;
        }

        public NewsEvent EvaluateDeclined(CareerState career, CareerDate occurredAt, TeamState team)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (team == null) throw new ArgumentNullException(nameof(team));
            var newsEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_extension_declined",
                NewsEventType.ContractNegotiationDeclined,
                occurredAt,
                NewsReleaseGate.AfterContractConfirmation,
                NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name),
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_contract",
                baseImportance: 35)
            {
                CareerImpact = 20,
                IsCareerArchive = true
            };
            AddContractFacts(newsEvent, career, team, 0, 0L);
            return newsEvent;
        }

        private static void AddContractFacts(
            NewsEvent newsEvent,
            CareerState career,
            TeamState team,
            int years,
            long salary)
        {
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, career.MyPlayer.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            if (years > 0)
                newsEvent.FactSet.SetInteger(NewsFactKey.ContractYears, years);
            if (salary > 0L)
                newsEvent.FactSet.SetInteger(NewsFactKey.ContractSalary, salary);
        }

        private static TeamState FindTeam(CareerState career, int teamId)
        {
            for (int index = 0; index < career.CurrentLeague.Teams.Count; index++)
            {
                TeamState team = career.CurrentLeague.Teams[index];
                if (team.TeamId == teamId)
                    return team;
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }
    }

    /// <summary>트레이드 시장의 실제 단계 변화와 확정 이동만 기사 사건으로 만든다.</summary>
    public sealed class TradeNarrativeNewsEvaluator
    {
        public IReadOnlyList<NewsEvent> Evaluate(
            CareerState career,
            CareerDate occurredAt,
            IReadOnlyList<TradeInterestRecord> previousInterests,
            TradeExecutionResult? execution)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            previousInterests ??= Array.Empty<TradeInterestRecord>();
            var events = new List<NewsEvent>();
            if (execution.HasValue)
            {
                events.Add(CreateCompleted(career, occurredAt, execution.Value));
                return events;
            }

            IReadOnlyList<TradeInterestRecord> current = career.TradeState.Interests;
            for (int index = 0; index < current.Count; index++)
            {
                TradeInterestRecord interest = current[index];
                TradeInterestRecord? previous = Find(previousInterests, interest.InterestedTeamId);
                if (previous.HasValue && previous.Value.Stage == interest.Stage)
                    continue;
                if (interest.Stage is TradeInterestStage.Completed or TradeInterestStage.Failed)
                    continue;
                events.Add(CreateStage(career, occurredAt, interest));
            }
            return events;
        }

        private static NewsEvent CreateStage(
            CareerState career,
            CareerDate occurredAt,
            TradeInterestRecord interest)
        {
            TeamState interestedTeam = FindTeam(career, interest.InterestedTeamId);
            NewsEventType type = interest.Stage switch
            {
                TradeInterestStage.Rumor => NewsEventType.TradeRumorReported,
                TradeInterestStage.Negotiating => NewsEventType.TradeNegotiationReported,
                _ => NewsEventType.TradeInterestReported
            };
            var newsEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_trade_{interest.InterestedTeamId}_{interest.Stage}",
                type,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name),
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_trade",
                baseImportance: interest.Stage == TradeInterestStage.Interest ? 20 : 35)
            {
                CareerImpact = interest.Stage == TradeInterestStage.Negotiating ? 22 : 12,
                Rarity = interest.Stage == TradeInterestStage.Negotiating ? 8 : 0
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(interestedTeam.TeamId, interestedTeam.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, career.MyPlayer.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, FindTeam(career, career.MyPlayer.CurrentTeamId).Name);
            newsEvent.FactSet.SetText(NewsFactKey.InterestedTeamName, interestedTeam.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TradeStage, GetStageLabel(interest.Stage));
            newsEvent.FactSet.SetText(NewsFactKey.ProjectedRole, GetRoleLabel(interest.ProjectedRole));
            return newsEvent;
        }

        private static NewsEvent CreateCompleted(
            CareerState career,
            CareerDate occurredAt,
            TradeExecutionResult execution)
        {
            TeamState previousTeam = FindTeam(career, execution.PreviousTeamId);
            TeamState newTeam = FindTeam(career, execution.NewTeamId);
            var newsEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_traded_{execution.NewTeamId}",
                NewsEventType.PlayerTraded,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name),
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_trade",
                baseImportance: 52)
            {
                CareerImpact = 40,
                Rarity = 18,
                IsCareerArchive = true
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(previousTeam.TeamId, previousTeam.Name));
            newsEvent.AddRelatedSubject(NewsSubject.Team(newTeam.TeamId, newTeam.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, career.MyPlayer.Name);
            newsEvent.FactSet.SetText(NewsFactKey.PreviousTeamName, previousTeam.Name);
            newsEvent.FactSet.SetText(NewsFactKey.NewTeamName, newTeam.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, newTeam.Name);
            newsEvent.FactSet.SetText(NewsFactKey.ProjectedRole, GetRoleLabel(execution.ProjectedRole));
            newsEvent.FactSet.SetText(NewsFactKey.TradeStage, "이적 확정");
            return newsEvent;
        }

        private static TradeInterestRecord? Find(IReadOnlyList<TradeInterestRecord> source, int teamId)
        {
            for (int index = 0; index < source.Count; index++)
            {
                if (source[index].InterestedTeamId == teamId)
                    return source[index];
            }
            return null;
        }

        private static TeamState FindTeam(CareerState career, int teamId)
        {
            for (int index = 0; index < career.CurrentLeague.Teams.Count; index++)
            {
                TeamState team = career.CurrentLeague.Teams[index];
                if (team.TeamId == teamId)
                    return team;
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }

        private static string GetStageLabel(TradeInterestStage stage) => stage switch
        {
            TradeInterestStage.Rumor => "이적설",
            TradeInterestStage.Negotiating => "협상 중",
            _ => "관심"
        };

        private static string GetRoleLabel(ExpectedRole role) => role switch
        {
            ExpectedRole.StartingCompetition => "주전 경쟁",
            ExpectedRole.BenchCompetition => "벤치 경쟁",
            _ => "로스터 경쟁"
        };
    }
}
