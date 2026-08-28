using System;

namespace Baseball.Game.Career.News
{
    /// <summary>확정 사건으로 스토리라인을 시작·진행·종료하고 후속 기사에 연결 ID를 붙인다.</summary>
    public sealed class NewsStorylineService
    {
        private const int MaximumActiveStorylinesPerPlayer = 3;

        public void Apply(CareerNewsState state, NewsEvent newsEvent)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (newsEvent == null) throw new ArgumentNullException(nameof(newsEvent));

            if (!TryGetStorylineType(newsEvent.EventType, out NewsStorylineType type))
                return;
            string playerId = FindPlayerId(newsEvent);
            string teamId = FindTeamId(newsEvent);
            NewsStorylineState storyline = FindActive(state, type, playerId);
            if (storyline == null && IsStartingEvent(newsEvent.EventType))
                storyline = Start(state, type, playerId, teamId, newsEvent.OccurredAt);
            if (storyline == null)
                return;

            newsEvent.StorylineId = storyline.StorylineId;
            if (IsResolutionEvent(newsEvent.EventType, out NewsStorylineResolution resolution))
                storyline.Resolve(newsEvent.OccurredAt, resolution);
            else if (storyline.LastUpdatedAt.CompareTo(newsEvent.OccurredAt) < 0)
                storyline.Advance(newsEvent.OccurredAt, 1);
        }

        private static NewsStorylineState Start(
            CareerNewsState state,
            NewsStorylineType type,
            string playerId,
            string teamId,
            CareerDate occurredAt)
        {
            if (string.IsNullOrEmpty(playerId))
                return null;
            if (CountActive(state, playerId) >= MaximumActiveStorylinesPerPlayer)
                return null;

            string id = $"story_{occurredAt.Cycle.SeasonId}_{playerId}_{type}_{occurredAt.Cycle.CycleIndex}";
            var storyline = new NewsStorylineState(id, type, playerId, teamId, occurredAt);
            state.AddStoryline(storyline);
            return storyline;
        }

        private static NewsStorylineState FindActive(
            CareerNewsState state,
            NewsStorylineType type,
            string playerId)
        {
            for (int index = 0; index < state.ActiveStorylines.Count; index++)
            {
                NewsStorylineState storyline = state.ActiveStorylines[index];
                if (!storyline.IsResolved && storyline.Type == type && storyline.PrimaryPlayerId == playerId)
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
                    type = NewsStorylineType.RosterCompetition;
                    return true;
                case NewsEventType.PlayerRoleChanged:
                    type = NewsStorylineType.RoleChange;
                    return true;
                case NewsEventType.ContractSigned:
                    type = NewsStorylineType.ContractSeason;
                    return true;
                case NewsEventType.PostseasonBerthClinched:
                case NewsEventType.PostseasonEliminated:
                case NewsEventType.ChampionshipWon:
                    type = NewsStorylineType.PostseasonRun;
                    return true;
                case NewsEventType.CareerMilestoneReached:
                    type = NewsStorylineType.RecordChase;
                    return true;
                case NewsEventType.PlayerFormChanged:
                    type = NewsStorylineType.RisingForm;
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
                NewsEventType.PlayerRoleChanged or
                NewsEventType.PostseasonBerthClinched or
                NewsEventType.CareerMilestoneReached or
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
    }
}
