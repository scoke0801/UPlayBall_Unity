using System;

namespace Baseball.Game.Career.News
{
    /// <summary>플레이어 관련도와 확정 사건의 커리어 영향을 합쳐 뉴스 가치를 계산한다.</summary>
    internal sealed class NewsPriorityService
    {
        private readonly NewsPriorityDefinition _definition;

        public NewsPriorityService(NewsPriorityDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public void Evaluate(
            NewsCandidate candidate,
            NewsPublicationContext context,
            CareerNewsState state)
        {
            int score = candidate.BaseImportance + candidate.CareerImpact + candidate.GameImpact + candidate.Rarity;
            string playerId = context.MyPlayerId.ToString();
            string teamId = context.MyTeamId.ToString();
            bool isPlayerRelated = candidate.IncludesSubject(NewsSubjectType.Player, playerId);
            bool isTeamRelated = candidate.IncludesSubject(NewsSubjectType.Team, teamId);
            if (isPlayerRelated)
                score += _definition.PlayerRelevance;
            if (isTeamRelated)
                score += _definition.PlayerTeamRelevance;
            if (!isPlayerRelated && !isTeamRelated)
                score += _definition.LeagueRelevance;
            if (!string.IsNullOrEmpty(candidate.StorylineId))
                score += _definition.StorylineContinuation;
            if (state.IsTopicOnCooldown(
                    candidate.CooldownGroup,
                    context.Cycle.ToOrdinal(),
                    _definition.DefaultCooldownCycles))
            {
                score -= _definition.RepeatPenalty;
            }

            candidate.Score = score;
            candidate.Importance = GetImportance(score);
        }

        private NewsImportance GetImportance(int score)
        {
            if (score >= _definition.SThreshold) return NewsImportance.S;
            if (score >= _definition.AThreshold) return NewsImportance.A;
            if (score >= _definition.BThreshold) return NewsImportance.B;
            if (score >= _definition.CThreshold) return NewsImportance.C;
            return NewsImportance.D;
        }
    }
}
