using System;

namespace Baseball.Core.Teams
{
    /// <summary>
    /// 새 게임에서 생성되는 구단의 성향 축을 정의한다. 완전 무작위 생성 대신
    /// 이 성향에 변주를 더해 특징 있는 구단을 만든다.
    /// </summary>
    public enum TeamArchetype
    {
        Development,
        Contender,
        OffenseFocused,
        PitchingFocused,
        SmallMarket
    }

    /// <summary>
    /// 한 구단 성향이 갖는 재정·육성·선수층·스카우트 등급을 0~100으로 보관한다.
    /// </summary>
    public readonly struct TeamArchetypeProfile
    {
        /// <summary>
        /// 구단 성향과 등급을 생성한다.
        /// </summary>
        public TeamArchetypeProfile(
            TeamArchetype archetype,
            int budget,
            int development,
            int rosterDepth,
            int scouting)
        {
            ValidateRating(budget, nameof(budget));
            ValidateRating(development, nameof(development));
            ValidateRating(rosterDepth, nameof(rosterDepth));
            ValidateRating(scouting, nameof(scouting));

            Archetype = archetype;
            Budget = budget;
            Development = development;
            RosterDepth = rosterDepth;
            Scouting = scouting;
        }

        public TeamArchetype Archetype { get; }
        public int Budget { get; }
        public int Development { get; }
        public int RosterDepth { get; }
        public int Scouting { get; }

        private static void ValidateRating(int rating, string parameterName)
        {
            if (rating < 0 || rating > 100)
                throw new ArgumentOutOfRangeException(parameterName, "구단 등급은 0~100 범위여야 합니다.");
        }
    }
}
