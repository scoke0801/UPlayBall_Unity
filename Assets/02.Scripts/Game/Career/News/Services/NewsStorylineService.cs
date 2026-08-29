using System;

namespace Baseball.Game.Career.News
{
    /// <summary>확정 사건으로 스토리라인을 시작·진행·종료하고 후속 기사에 연결 ID를 붙인다.</summary>
    public sealed class NewsStorylineService
    {
        private const int MaximumActiveStorylinesPerPlayer = 5;

        public void Apply(CareerNewsState state, NewsEvent newsEvent)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (newsEvent == null) throw new ArgumentNullException(nameof(newsEvent));

            if (newsEvent.EventType == NewsEventType.PlayerFormChanged)
            {
                ApplyPlayerForm(state, newsEvent);
                return;
            }
            if (newsEvent.EventType == NewsEventType.RoleCompetitionChanged)
            {
                ApplyRoleCompetition(state, newsEvent);
                return;
            }
            if (!TryGetStorylineType(newsEvent.EventType, out NewsStorylineType type))
                return;
            string playerId = FindPlayerId(newsEvent);
            string teamId = FindTeamId(newsEvent);
            string topicKey = BuildTopicKey(type, newsEvent);
            if (type == NewsStorylineType.RecordChase &&
                newsEvent.EventType == NewsEventType.CareerMilestoneReached &&
                string.IsNullOrEmpty(topicKey))
            {
                return;
            }
            NewsStorylineState storyline = FindActive(state, type, playerId, topicKey);
            if (storyline == null && IsStartingEvent(newsEvent.EventType))
                storyline = Start(state, type, playerId, teamId, newsEvent.OccurredAt, topicKey);
            if (storyline == null)
                return;

            newsEvent.StorylineId = storyline.StorylineId;
            if (IsResolutionEvent(newsEvent.EventType, out NewsStorylineResolution resolution))
                storyline.Resolve(newsEvent.OccurredAt, resolution);
            else if (storyline.LastUpdatedAt.CompareTo(newsEvent.OccurredAt) < 0)
                storyline.Advance(newsEvent.OccurredAt, 1);
        }

        private static void ApplyPlayerForm(CareerNewsState state, NewsEvent newsEvent)
        {
            string playerId = FindPlayerId(newsEvent);
            string teamId = FindTeamId(newsEvent);
            if (newsEvent.FactSet.GetBoolean(NewsFactKey.FormRebound))
            {
                NewsStorylineState slump = FindActive(state, NewsStorylineType.Slump, playerId);
                if (slump == null)
                    return;
                newsEvent.StorylineId = slump.StorylineId;
                slump.Resolve(newsEvent.OccurredAt, NewsStorylineResolution.Recovered);
                return;
            }
            if (newsEvent.FactSet.GetBoolean(NewsFactKey.FormCooled))
            {
                NewsStorylineState hotForm = FindActive(state, NewsStorylineType.RisingForm, playerId);
                if (hotForm == null)
                    return;
                newsEvent.StorylineId = hotForm.StorylineId;
                hotForm.Resolve(newsEvent.OccurredAt, NewsStorylineResolution.Stabilized);
                return;
            }
            if (newsEvent.FactSet.GetBoolean(NewsFactKey.FormHot))
            {
                NewsStorylineState hotForm = FindActive(state, NewsStorylineType.RisingForm, playerId) ??
                                               Start(
                                                   state,
                                                   NewsStorylineType.RisingForm,
                                                   playerId,
                                                   teamId,
                                                   newsEvent.OccurredAt);
                if (hotForm == null)
                    return;
                newsEvent.StorylineId = hotForm.StorylineId;
                if (hotForm.LastUpdatedAt.CompareTo(newsEvent.OccurredAt) < 0)
                    hotForm.Advance(newsEvent.OccurredAt, 1);
                return;
            }
            if (!newsEvent.FactSet.GetBoolean(NewsFactKey.FormSlump))
                return;

            NewsStorylineState storyline = FindActive(state, NewsStorylineType.Slump, playerId) ??
                                           Start(
                                               state,
                                               NewsStorylineType.Slump,
                                               playerId,
                                               teamId,
                                               newsEvent.OccurredAt);
            if (storyline == null)
                return;
            newsEvent.StorylineId = storyline.StorylineId;
            if (storyline.LastUpdatedAt.CompareTo(newsEvent.OccurredAt) < 0)
                storyline.Advance(newsEvent.OccurredAt, 1);
        }

        private static void ApplyRoleCompetition(CareerNewsState state, NewsEvent newsEvent)
        {
            string playerId = FindPlayerId(newsEvent);
            NewsStorylineState storyline = FindActive(
                state,
                NewsStorylineType.RosterCompetition,
                playerId);
            if (newsEvent.FactSet.GetBoolean(NewsFactKey.RoleCompetitionResolved))
            {
                if (storyline == null)
                    return;
                newsEvent.StorylineId = storyline.StorylineId;
                storyline.Resolve(newsEvent.OccurredAt, NewsStorylineResolution.Stabilized);
                return;
            }
            if (!newsEvent.FactSet.GetBoolean(NewsFactKey.RoleCompetitionStarted))
                return;
            storyline ??= Start(
                state,
                NewsStorylineType.RosterCompetition,
                playerId,
                FindTeamId(newsEvent),
                newsEvent.OccurredAt);
            if (storyline == null)
                return;
            newsEvent.StorylineId = storyline.StorylineId;
        }

        private static NewsStorylineState Start(
            CareerNewsState state,
            NewsStorylineType type,
            string playerId,
            string teamId,
            CareerDate occurredAt,
            string topicKey = "")
        {
            if (string.IsNullOrEmpty(playerId))
                return null;
            if (CountActive(state, playerId) >= MaximumActiveStorylinesPerPlayer)
                return null;

            string id = $"story_{occurredAt.Cycle.SeasonId}_{playerId}_{type}_{occurredAt.Cycle.CycleIndex}";
            var storyline = new NewsStorylineState(id, type, playerId, teamId, occurredAt, topicKey);
            state.AddStoryline(storyline);
            return storyline;
        }

        private static NewsStorylineState FindActive(
            CareerNewsState state,
            NewsStorylineType type,
            string playerId,
            string topicKey = "")
        {
            for (int index = 0; index < state.ActiveStorylines.Count; index++)
            {
                NewsStorylineState storyline = state.ActiveStorylines[index];
                if (!storyline.IsResolved && storyline.Type == type &&
                    storyline.PrimaryPlayerId == playerId &&
                    (string.IsNullOrEmpty(topicKey) || storyline.TopicKey == topicKey))
                    return storyline;
            }
            return null;
        }

        private static int CountActive(CareerNewsState state, string playerId)
        {
            int count = 0;
            for (int index = 0; index < state.ActiveStorylines.Count; index++)
            {
                NewsStorylineState storyline = state.ActiveStorylines[index];
                if (!storyline.IsResolved && storyline.PrimaryPlayerId == playerId)
                    count++;
            }
            return count;
        }

        private static bool TryGetStorylineType(NewsEventType eventType, out NewsStorylineType type)
        {
            switch (eventType)
            {
                case NewsEventType.PlayerInjuryConfirmed:
                case NewsEventType.InjuryRecoveryStageReached:
                case NewsEventType.PlayerReturnedFromInjury:
                    type = NewsStorylineType.InjuryReturn;
                    return true;
                case NewsEventType.TeamRosterChanged:
                case NewsEventType.RoleCompetitionChanged:
                    type = NewsStorylineType.RosterCompetition;
                    return true;
                case NewsEventType.PlayerRoleChanged:
                    type = NewsStorylineType.RoleChange;
                    return true;
                case NewsEventType.ContractNegotiationReported:
                case NewsEventType.ContractNegotiationDeclined:
                case NewsEventType.ContractSigned:
                    type = NewsStorylineType.ContractSeason;
                    return true;
                case NewsEventType.PostseasonBerthClinched:
                case NewsEventType.PostseasonEliminated:
                case NewsEventType.ChampionshipWon:
                    type = NewsStorylineType.PostseasonRun;
                    return true;
                case NewsEventType.CareerMilestoneApproaching:
                case NewsEventType.CareerMilestoneReached:
                    type = NewsStorylineType.RecordChase;
                    return true;
                case NewsEventType.TradeInterestReported:
                case NewsEventType.TradeRumorReported:
                case NewsEventType.TradeNegotiationReported:
                case NewsEventType.PlayerTraded:
                    type = NewsStorylineType.TradeRumor;
                    return true;
                default:
                    type = default;
                    return false;
            }
        }

        private static bool IsStartingEvent(NewsEventType eventType)
        {
            return eventType is NewsEventType.PlayerInjuryConfirmed or
                NewsEventType.TeamRosterChanged or
                NewsEventType.RoleCompetitionChanged or
                NewsEventType.PlayerRoleChanged or
                NewsEventType.ContractNegotiationReported or
                NewsEventType.PostseasonBerthClinched or
                NewsEventType.CareerMilestoneApproaching or
                NewsEventType.TradeInterestReported or
                NewsEventType.TradeRumorReported or
                NewsEventType.TradeNegotiationReported or
                NewsEventType.PlayerFormChanged;
        }

        private static bool IsResolutionEvent(
            NewsEventType eventType,
            out NewsStorylineResolution resolution)
        {
            switch (eventType)
            {
                case NewsEventType.PlayerReturnedFromInjury:
                    resolution = NewsStorylineResolution.Recovered;
                    return true;
                case NewsEventType.ContractSigned:
                    resolution = NewsStorylineResolution.Succeeded;
                    return true;
                case NewsEventType.ContractNegotiationDeclined:
                    resolution = NewsStorylineResolution.Declined;
                    return true;
                case NewsEventType.CareerMilestoneReached:
                    resolution = NewsStorylineResolution.Succeeded;
                    return true;
                case NewsEventType.PlayerTraded:
                    resolution = NewsStorylineResolution.Transferred;
                    return true;
                case NewsEventType.PostseasonEliminated:
                    resolution = NewsStorylineResolution.Eliminated;
                    return true;
                case NewsEventType.ChampionshipWon:
                    resolution = NewsStorylineResolution.Champion;
                    return true;
                default:
                    resolution = NewsStorylineResolution.None;
                    return false;
            }
        }

        private static string FindPlayerId(NewsEvent newsEvent)
        {
            if (newsEvent.PrimarySubject.Type == NewsSubjectType.Player)
                return newsEvent.PrimarySubject.SubjectId;
            for (int index = 0; index < newsEvent.RelatedSubjects.Count; index++)
            {
                if (newsEvent.RelatedSubjects[index].Type == NewsSubjectType.Player)
                    return newsEvent.RelatedSubjects[index].SubjectId;
            }
            return string.Empty;
        }

        private static string FindTeamId(NewsEvent newsEvent)
        {
            if (newsEvent.PrimarySubject.Type == NewsSubjectType.Team)
                return newsEvent.PrimarySubject.SubjectId;
            for (int index = 0; index < newsEvent.RelatedSubjects.Count; index++)
            {
                if (newsEvent.RelatedSubjects[index].Type == NewsSubjectType.Team)
                    return newsEvent.RelatedSubjects[index].SubjectId;
            }
            return string.Empty;
        }

        private static string BuildTopicKey(NewsStorylineType type, NewsEvent newsEvent)
        {
            if (type != NewsStorylineType.RecordChase)
                return string.Empty;
            string name = newsEvent.FactSet.GetText(NewsFactKey.MilestoneName);
            int target = newsEvent.FactSet.GetInteger(NewsFactKey.MilestoneTarget);
            return string.IsNullOrEmpty(name) || target <= 0 ? string.Empty : $"{name}:{target}";
        }
    }
}
