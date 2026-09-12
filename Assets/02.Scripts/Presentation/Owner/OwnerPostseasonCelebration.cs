using System;
using Baseball.Core.Historical;
using Baseball.Game.Historical;

namespace Baseball.Presentation.Owner
{
    public enum OwnerPostseasonCelebrationKind { SeriesVictory, Championship }

    /// <summary>공개가 끝난 경기에서 새로 확정된 시리즈 승리만 표현하는 불변 결과다.</summary>
    public sealed class OwnerPostseasonCelebration
    {
        private OwnerPostseasonCelebration(OwnerSeasonReviewSnapshot snapshot, OwnerPostseasonSeriesReview series)
        {
            Kind = series.Round == OwnerPostseasonRound.Championship
                ? OwnerPostseasonCelebrationKind.Championship : OwnerPostseasonCelebrationKind.SeriesVictory;
            SeasonNumber = snapshot.SeasonNumber;
            LeagueGrade = snapshot.CurrentGrade;
            TeamKey = snapshot.PlayerTeamSeasonKey;
            bool higher = series.HigherSeedTeamSeasonKey == TeamKey;
            OpponentKey = higher ? series.LowerSeedTeamSeasonKey : series.HigherSeedTeamSeasonKey;
            Wins = higher ? series.HigherSeedWins : series.LowerSeedWins;
            Losses = higher ? series.LowerSeedWins : series.HigherSeedWins;
        }

        public OwnerPostseasonCelebrationKind Kind { get; }
        public int SeasonNumber { get; }
        public LeagueGrade LeagueGrade { get; }
        public string TeamKey { get; }
        public string OpponentKey { get; }
        public int Wins { get; }
        public int Losses { get; }

        /// <summary>기존 우승·다른 구단 승리·일반 승리에는 축하 연출을 만들지 않는다.</summary>
        public static OwnerPostseasonCelebration Create(OwnerSeasonReviewSnapshot before, OwnerSeasonReviewSnapshot after)
        {
            if (before == null || after == null || before.SeasonNumber != after.SeasonNumber ||
                before.CurrentGrade != after.CurrentGrade || before.PlayerTeamSeasonKey != after.PlayerTeamSeasonKey)
                return null;
            foreach (OwnerPostseasonSeriesReview current in after.Series)
            {
                if (!current.IsCompleted) continue;
                string winner = current.HigherSeedWins == current.WinsRequired
                    ? current.HigherSeedTeamSeasonKey : current.LowerSeedTeamSeasonKey;
                if (!string.Equals(winner, after.PlayerTeamSeasonKey, StringComparison.Ordinal)) continue;
                foreach (OwnerPostseasonSeriesReview previous in before.Series)
                {
                    if (previous.SeriesId != current.SeriesId || previous.IsCompleted ||
                        previous.HigherSeedTeamSeasonKey != current.HigherSeedTeamSeasonKey ||
                        previous.LowerSeedTeamSeasonKey != current.LowerSeedTeamSeasonKey) continue;
                    if (current.HigherSeedWins + current.LowerSeedWins != previous.HigherSeedWins + previous.LowerSeedWins + 1)
                        continue;
                    return new OwnerPostseasonCelebration(after, current);
                }
            }
            return null;
        }
    }

    /// <summary>계산 완료와 관전 공개 완료를 분리하고 한 경기의 축하를 한 번만 전달한다.</summary>
    public sealed class OwnerPostseasonCelebrationGate
    {
        private OwnerSeasonReviewSnapshot _before;

        public void Begin(OwnerSeasonReviewSnapshot before) => _before = before;
        public void Clear() => _before = null;

        /// <summary>공개 전 호출은 결과를 소비하지 않으며 공개 완료 뒤에는 재호출을 무시한다.</summary>
        public OwnerPostseasonCelebration Reveal(OwnerSeasonReviewSnapshot after, bool isPlaybackComplete)
        {
            if (!isPlaybackComplete) return null;
            OwnerPostseasonCelebration result = OwnerPostseasonCelebration.Create(_before, after);
            _before = null;
            return result;
        }
    }
}
