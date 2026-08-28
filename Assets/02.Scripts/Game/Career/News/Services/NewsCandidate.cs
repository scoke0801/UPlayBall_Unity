using System.Collections.Generic;

namespace Baseball.Game.Career.News
{
    /// <summary>하나 이상의 관련 사건을 자연스러운 기사 한 건으로 묶은 내부 후보 상태다.</summary>
    internal sealed class NewsCandidate
    {
        private readonly List<NewsSubject> _relatedSubjects = new();
        private readonly List<string> _sourceEventIds = new();

        public NewsCandidate(NewsEvent source)
        {
            DominantEventType = source.EventType;
            DominantEventValue = GetDominance(source);
            PrimarySubject = source.PrimarySubject;
            Facts = source.FactSet.Clone();
            StorylineId = source.StorylineId ?? string.Empty;
            CooldownGroup = source.CooldownGroup ?? string.Empty;
            IsCareerArchive = source.IsCareerArchive;
            BaseImportance = source.BaseImportance;
            CareerImpact = source.CareerImpact;
            GameImpact = source.GameImpact;
            Rarity = source.Rarity;
            AddSource(source);
        }

        public NewsEventType DominantEventType { get; private set; }
        public int DominantEventValue { get; private set; }
        public NewsSubject PrimarySubject { get; private set; }
        public IReadOnlyList<NewsSubject> RelatedSubjects => _relatedSubjects;
        public IReadOnlyList<string> SourceEventIds => _sourceEventIds;
        public NewsFactSet Facts { get; }
        public string StorylineId { get; private set; }
        public string CooldownGroup { get; private set; }
        public bool IsCareerArchive { get; private set; }
        public int BaseImportance { get; private set; }
        public int CareerImpact { get; private set; }
        public int GameImpact { get; private set; }
        public int Rarity { get; private set; }
        public int Score { get; set; }
        public NewsImportance Importance { get; set; }

        public bool IsLeagueBriefing => DominantEventType == NewsEventType.LeagueBriefing;

        public void Merge(NewsEvent source)
        {
            int dominance = GetDominance(source);
            if (dominance > DominantEventValue ||
                dominance == DominantEventValue && (int)source.EventType > (int)DominantEventType)
            {
                DominantEventValue = dominance;
                DominantEventType = source.EventType;
                PrimarySubject = source.PrimarySubject;
            }

            if (source.BaseImportance > BaseImportance) BaseImportance = source.BaseImportance;
            if (source.CareerImpact > CareerImpact) CareerImpact = source.CareerImpact;
            if (source.GameImpact > GameImpact) GameImpact = source.GameImpact;
            if (source.Rarity > Rarity) Rarity = source.Rarity;
            if (!string.IsNullOrEmpty(source.StorylineId)) StorylineId = source.StorylineId;
            if (!string.IsNullOrEmpty(source.CooldownGroup)) CooldownGroup = source.CooldownGroup;
            IsCareerArchive |= source.IsCareerArchive;
            Facts.MergeFrom(source.FactSet);
            AddSource(source);
        }

        public bool IncludesSubject(NewsSubjectType type, string subjectId)
        {
            if (PrimarySubject.Type == type && PrimarySubject.SubjectId == subjectId)
                return true;
            for (int index = 0; index < _relatedSubjects.Count; index++)
            {
                if (_relatedSubjects[index].Type == type && _relatedSubjects[index].SubjectId == subjectId)
                    return true;
            }
            return false;
        }

        private void AddSource(NewsEvent source)
        {
            _sourceEventIds.Add(source.EventId);
            AddRelated(source.PrimarySubject);
            for (int index = 0; index < source.RelatedSubjects.Count; index++)
                AddRelated(source.RelatedSubjects[index]);
            _sourceEventIds.Sort(System.StringComparer.Ordinal);
        }

        private void AddRelated(NewsSubject subject)
        {
            if (subject.Equals(PrimarySubject))
                return;
            for (int index = 0; index < _relatedSubjects.Count; index++)
            {
                if (_relatedSubjects[index].Equals(subject))
                    return;
            }
            _relatedSubjects.Add(subject);
        }

        private static int GetDominance(NewsEvent source)
        {
            int eventPriority = source.EventType switch
            {
                NewsEventType.ChampionshipWon => 80,
                NewsEventType.SeasonAwardGranted => 75,
                NewsEventType.CareerMilestoneReached => 70,
                NewsEventType.PlayerInjuryConfirmed => 65,
                NewsEventType.ContractSigned => 65,
                NewsEventType.PlayerRoleChanged => 60,
                NewsEventType.PlayerGamePerformance => 55,
                NewsEventType.PostseasonEliminated => 70,
                NewsEventType.PostseasonSeriesCompleted => 50,
                NewsEventType.PostseasonGameCompleted => 45,
                NewsEventType.GameCompleted => 40,
                NewsEventType.TeamStreakReached => 30,
                NewsEventType.LeagueBriefing => 10,
                _ => 20
            };
            return eventPriority + source.BaseImportance + source.CareerImpact + source.GameImpact + source.Rarity;
        }
    }
}
