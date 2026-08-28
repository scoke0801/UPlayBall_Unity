using System;

namespace Baseball.Game.Career.News
{
    /// <summary>한 뉴스 주기에 공개 가능한 관문과 플레이어 관련 대상을 고정한다.</summary>
    public sealed class NewsPublicationContext
    {
        private readonly NewsReleaseGate[] _releasedGates;

        public NewsPublicationContext(
            CareerDate publishedAt,
            int myPlayerId,
            int myTeamId,
            params NewsReleaseGate[] releasedGates)
        {
            PublishedAt = publishedAt;
            MyPlayerId = myPlayerId;
            MyTeamId = myTeamId;
            _releasedGates = releasedGates ?? Array.Empty<NewsReleaseGate>();
        }

        public CareerDate PublishedAt { get; }
        public NewsCycleKey Cycle => PublishedAt.Cycle;
        public int MyPlayerId { get; }
        public int MyTeamId { get; }

        public bool IsReleased(NewsReleaseGate gate)
        {
            for (int index = 0; index < _releasedGates.Length; index++)
            {
                if (_releasedGates[index] == gate)
                    return true;
            }
            return false;
        }
    }
}
