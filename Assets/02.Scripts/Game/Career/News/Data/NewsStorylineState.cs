using System;

namespace Baseball.Game.Career.News
{
    /// <summary>여러 기사에 걸쳐 진행되는 한 선수의 커리어 이야기 상태다.</summary>
    public sealed class NewsStorylineState
    {
        public NewsStorylineState(
            string storylineId,
            NewsStorylineType type,
            string primaryPlayerId,
            string relatedTeamId,
            CareerDate startedAt)
        {
            if (string.IsNullOrWhiteSpace(storylineId))
                throw new ArgumentException("StorylineId가 비어 있습니다.", nameof(storylineId));
            StorylineId = storylineId;
            Type = type;
            PrimaryPlayerId = primaryPlayerId ?? string.Empty;
            RelatedTeamId = relatedTeamId ?? string.Empty;
            StartedAt = startedAt;
            LastUpdatedAt = startedAt;
            Stage = 1;
        }

        public string StorylineId { get; }
        public NewsStorylineType Type { get; }
        public string PrimaryPlayerId { get; }
        public string RelatedTeamId { get; }
        public int Stage { get; private set; }
        public CareerDate StartedAt { get; }
        public CareerDate LastUpdatedAt { get; private set; }
        public int ProgressValue { get; private set; }
        public bool IsResolved { get; private set; }
        public NewsStorylineResolution Resolution { get; private set; }

        public void Advance(CareerDate occurredAt, int progressDelta)
        {
            if (IsResolved)
                throw new InvalidOperationException("종료된 스토리라인은 진행할 수 없습니다.");
            Stage++;
            ProgressValue += progressDelta;
            LastUpdatedAt = occurredAt;
        }

        public void Resolve(CareerDate occurredAt, NewsStorylineResolution resolution)
        {
            if (resolution == NewsStorylineResolution.None)
                throw new ArgumentException("종료 사유가 필요합니다.", nameof(resolution));
            IsResolved = true;
            Resolution = resolution;
            LastUpdatedAt = occurredAt;
        }
    }
}
