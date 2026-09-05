using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Simulation.Career;
using Baseball.Simulation.Match;

namespace Baseball.Game.Guide
{
    /// <summary>감독 AI가 고정한 경기 역할과 Lineup을 Career Guide Fact로 변환한다.</summary>
    public sealed class CareerPreparedMatchGuideEventAdapter
    {
        public GuideFact CreateFirstEntryFact(CareerState career, GuideFactIdentity identity)
        {
            if (career == null)
                throw new ArgumentNullException(nameof(career));
            return CreateBuilder(career, "CareerModeFirstEntry", identity, matchId: 0).Build();
        }

        /// <summary>경기 기용을 다시 판단하지 않고 준비된 MatchSession에 확정된 역할만 설명한다.</summary>
        public GuideFact[] CreatePreparedMatchFacts(
            CareerState career,
            CareerMatchSession session,
            GuideFactIdentity identity)
        {
            if (career == null)
                throw new ArgumentNullException(nameof(career));
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            int matchId = session.Input.GameId;
            var facts = new List<GuideFact>(3);
            switch (session.PlayerRole)
            {
                case PlayerGameRole.StartingBatter:
                    AddStartingBatterFacts(facts, career, session, identity, matchId);
                    break;
                case PlayerGameRole.Bench:
                case PlayerGameRole.Inactive:
                case PlayerGameRole.PitcherRest:
                    AddNotStartingFact(facts, career, session, identity, matchId);
                    break;
            }
            return facts.ToArray();
        }

        private static void AddNotStartingFact(
            ICollection<GuideFact> facts,
            CareerState career,
            CareerMatchSession session,
            GuideFactIdentity identity,
            int matchId)
        {
            ManagerUsageDecision? decision = session.PlayerRoleDecision;
            if (!decision.HasValue)
                return;
            if (decision.Value.Role != session.PlayerRole)
                throw new InvalidOperationException("준비된 경기 역할과 감독 AI 판단 Snapshot이 일치하지 않습니다.");

            facts.Add(CreateBuilder(career, "CareerNotStarting", identity, matchId)
                .AddPayload("reasonSummary", CareerUsageReasonSummary.Create(decision.Value))
                .Build());
        }

        private static void AddStartingBatterFacts(
            ICollection<GuideFact> facts,
            CareerState career,
            CareerMatchSession session,
            GuideFactIdentity identity,
            int matchId)
        {
            MatchRosterSnapshot roster = session.Input.AwayRoster.TeamId == career.MyPlayer.CurrentTeamId
                ? session.Input.AwayRoster
                : session.Input.HomeRoster;
            for (int index = 0; index < roster.StartingLineup.Count; index++)
            {
                LineupSlot slot = roster.StartingLineup[index];
                if (slot.Player.PlayerId != career.MyPlayer.PlayerId)
                    continue;

                string position = FormatPosition(slot.FieldingPosition);
                facts.Add(CreateBuilder(career, "CareerStartingLineupSelected", identity, matchId)
                    .AddPayload("battingOrder", index + 1)
                    .AddPayload("position", position)
                    .Build());
                if (slot.FieldingPosition == PlayerPosition.DesignatedHitter)
                    facts.Add(CreateBuilder(career, "CareerDHStart", identity, matchId).Build());
                return;
            }

            throw new InvalidOperationException("StartingBatter 역할인데 잠금된 Lineup에서 내 선수를 찾지 못했습니다.");
        }

        private static GuideFactBuilder CreateBuilder(
            CareerState career,
            string factType,
            GuideFactIdentity identity,
            int matchId)
        {
            var builder = new GuideFactBuilder(GuideModeScope.Career, factType, identity)
                .AddContext("careerPlayerId", career.MyPlayer.PlayerId)
                .AddContext("seasonId", career.CurrentLeague.CurrentSeason.SeasonId);
            if (matchId > 0)
                builder.AddContext("matchId", matchId);
            return builder;
        }

        private static string FormatPosition(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "포수",
                PlayerPosition.FirstBase => "1루수",
                PlayerPosition.SecondBase => "2루수",
                PlayerPosition.ThirdBase => "3루수",
                PlayerPosition.Shortstop => "유격수",
                PlayerPosition.LeftField => "좌익수",
                PlayerPosition.CenterField => "중견수",
                PlayerPosition.RightField => "우익수",
                PlayerPosition.DesignatedHitter => "지명타자",
                _ => position.ToString()
            };
        }
    }

    /// <summary>감독 AI 판단 코드를 Career Guide의 플레이어 설명용 요약으로 변환한다.</summary>
    public static class CareerUsageReasonSummary
    {
        public static string Create(ManagerUsageDecision decision)
        {
            if (decision.Reason == ManagerUsageDecisionReason.RotationRest)
                return "선발 로테이션 순번이 아닌 경기입니다.";
            if (decision.Reason != ManagerUsageDecisionReason.CompetitionLoss)
                throw new ArgumentException("선발 제외 판단만 설명할 수 있습니다.", nameof(decision));

            bool hasConditionPenalty = decision.ConditionAdjustment < 0d;
            bool hasEvaluationPenalty = decision.ManagerEvaluationAdjustment < 0d;
            if (hasConditionPenalty && hasEvaluationPenalty)
                return "현재 컨디션과 감독 평가를 반영한 기용 점수가 경쟁자보다 낮았습니다.";
            if (hasConditionPenalty)
                return "현재 컨디션을 반영한 기용 점수가 경쟁자보다 낮았습니다.";
            if (hasEvaluationPenalty)
                return "현재 감독 평가를 반영한 기용 점수가 경쟁자보다 낮았습니다.";
            return "현재 종합 기용 점수가 같은 보직 경쟁자보다 낮았습니다.";
        }
    }
}
