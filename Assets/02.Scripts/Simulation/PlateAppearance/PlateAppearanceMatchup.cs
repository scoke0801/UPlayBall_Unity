using System;
using Baseball.Core.Players;

namespace Baseball.Simulation.PlateAppearance
{
    /// <summary>
    /// 한 타석의 타자·투수·수비 입력을 불변 값으로 묶는다.
    /// </summary>
    public readonly struct PlateAppearanceMatchup
    {
        /// <summary>
        /// 타석 확률 계산에 필요한 입력을 생성한다.
        /// </summary>
        public PlateAppearanceMatchup(
            Player batter,
            Player pitcher,
            double defenseRating,
            bool hasRunnerInScoringPosition)
        {
            Batter = batter ?? throw new ArgumentNullException(nameof(batter));
            Pitcher = pitcher ?? throw new ArgumentNullException(nameof(pitcher));
            DefenseRating = defenseRating;
            HasRunnerInScoringPosition = hasRunnerInScoringPosition;
        }

        public Player Batter { get; }
        public Player Pitcher { get; }
        public double DefenseRating { get; }
        public bool HasRunnerInScoringPosition { get; }
    }
}
