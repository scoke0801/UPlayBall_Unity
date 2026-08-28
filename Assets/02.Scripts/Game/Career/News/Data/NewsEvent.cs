using System;
using System.Collections.Generic;

namespace Baseball.Game.Career.News
{
    /// <summary>기사 안에서 선수·구단·경기 같은 대상을 ID와 표시명으로 고정한다.</summary>
    public readonly struct NewsSubject : IEquatable<NewsSubject>
    {
        public NewsSubject(NewsSubjectType type, string subjectId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
                throw new ArgumentException("뉴스 대상 ID가 비어 있습니다.", nameof(subjectId));
            Type = type;
            SubjectId = subjectId;
            DisplayName = displayName ?? string.Empty;
        }

        public NewsSubjectType Type { get; }
        public string SubjectId { get; }
        public string DisplayName { get; }

        public static NewsSubject Player(int playerId, string name) =>
            new(NewsSubjectType.Player, playerId.ToString(), name);

        public static NewsSubject Team(int teamId, string name) =>
            new(NewsSubjectType.Team, teamId.ToString(), name);

        public static NewsSubject Game(int gameId) =>
            new(NewsSubjectType.Game, gameId.ToString(), string.Empty);

        public bool Equals(NewsSubject other) => Type == other.Type && SubjectId == other.SubjectId;
        public override bool Equals(object obj) => obj is NewsSubject other && Equals(other);
        public override int GetHashCode() => ((int)Type * 397) ^ SubjectId.GetHashCode();
    }

    /// <summary>다른 시스템이 확정한 사실만 담아 발행 주기까지 보류하는 뉴스 입력 사건이다.</summary>
    public sealed class NewsEvent
    {
        private readonly List<NewsSubject> _relatedSubjects = new();

        public NewsEvent(
            string eventId,
            NewsEventType eventType,
            CareerDate occurredAt,
            NewsReleaseGate releaseGate,
            NewsSubject primarySubject,
            string mergeKey,
            int baseImportance)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                throw new ArgumentException("EventId가 비어 있습니다.", nameof(eventId));
            EventId = eventId;
            EventType = eventType;
            OccurredAt = occurredAt;
            ReleaseGate = releaseGate;
            PrimarySubject = primarySubject;
            MergeKey = mergeKey ?? string.Empty;
            BaseImportance = baseImportance;
            FactSet = new NewsFactSet();
        }

        public string EventId { get; }
        public NewsEventType EventType { get; }
        public CareerDate OccurredAt { get; }
        public NewsReleaseGate ReleaseGate { get; }
        public NewsSubject PrimarySubject { get; }
        public IReadOnlyList<NewsSubject> RelatedSubjects => _relatedSubjects;
        public NewsFactSet FactSet { get; }
        public string StorylineId { get; set; }
        public string MergeKey { get; }
        public string CooldownGroup { get; set; }
        public int BaseImportance { get; }
        public int CareerImpact { get; set; }
        public int GameImpact { get; set; }
        public int Rarity { get; set; }
        public bool IsCareerArchive { get; set; }

        public void AddRelatedSubject(NewsSubject subject)
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
    }
}
