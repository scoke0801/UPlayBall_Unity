using System;
using System.Collections.Generic;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 월드 역사 제공 시작 연도와 확정 도메인 사건을 장기 보관한다.
    /// </summary>
    public sealed class WorldRecordState
    {
        public WorldRecordState(int historyStartYear)
        {
            HistoryStartYear = historyStartYear;
        }

        public int HistoryStartYear { get; }
    }

    public readonly struct WorldDomainEvent
    {
        public WorldDomainEvent(
            string eventId,
            string eventType,
            DateTime worldDate,
            int primaryEntityId,
            int secondaryEntityId,
            int tertiaryEntityId = 0)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                throw new ArgumentException("EventId는 비어 있을 수 없습니다.", nameof(eventId));
            EventId = eventId;
            EventType = eventType ?? string.Empty;
            WorldDate = worldDate.Date;
            PrimaryEntityId = primaryEntityId;
            SecondaryEntityId = secondaryEntityId;
            TertiaryEntityId = tertiaryEntityId;
        }

        public string EventId { get; }
        public string EventType { get; }
        public DateTime WorldDate { get; }
        public int PrimaryEntityId { get; }
        public int SecondaryEntityId { get; }
        public int TertiaryEntityId { get; }
    }

    /// <summary>
    /// 월드 서비스가 확정한 사건만 저장하며 뉴스 계층은 이를 읽기만 한다.
    /// </summary>
    public sealed class DomainEventJournal
    {
        private readonly List<WorldDomainEvent> _events = new List<WorldDomainEvent>();
        private readonly HashSet<string> _eventIds = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<WorldDomainEvent> Events => _events;

        public bool Contains(string eventId) => _eventIds.Contains(eventId);

        public void Append(WorldDomainEvent domainEvent)
        {
            if (!_eventIds.Add(domainEvent.EventId))
                throw new InvalidOperationException($"EventId {domainEvent.EventId}가 중복되었습니다.");
            _events.Add(domainEvent);
        }
    }
}
