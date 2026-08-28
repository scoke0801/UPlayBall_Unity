using System;
using System.Collections.Generic;
using Baseball.Core.Players;

namespace Baseball.Game.Career
{
    public enum AwardScope
    {
        RegularSeason,
        Postseason
    }

    public enum AwardCategory
    {
        RegularSeasonMvp,
        PostseasonMvp,
        RookieOfYear,
        BattingAverage,
        HomeRun,
        RunsBattedIn,
        StolenBase,
        EarnedRunAverage,
        Win,
        Strikeout,
        Save,
        GoldGlove
    }

    /// <summary>수상 점수의 한 평가 지표와 가중 결과를 설명한다.</summary>
    public readonly struct AwardScoreBreakdown
    {
        public AwardScoreBreakdown(string metricId, double percentileScore, double weight)
        {
            MetricId = metricId ?? string.Empty;
            PercentileScore = percentileScore;
            Weight = weight;
        }

        public string MetricId { get; }
        public double PercentileScore { get; }
        public double Weight { get; }
        public double WeightedScore => PercentileScore * Weight;
    }

    /// <summary>화면에 표시할 후보의 최종 점수와 동점 처리 근거를 보관한다.</summary>
    public sealed class AwardCandidateResult
    {
        public AwardCandidateResult(
            int playerId,
            string playerName,
            int teamId,
            double finalScore,
            double individualScore,
            double participationScore,
            double recentScore,
            IReadOnlyList<AwardScoreBreakdown> breakdown)
        {
            PlayerId = playerId;
            PlayerName = playerName ?? string.Empty;
            TeamId = teamId;
            FinalScore = finalScore;
            IndividualScore = individualScore;
            ParticipationScore = participationScore;
            RecentScore = recentScore;
            ScoreBreakdown = breakdown ?? Array.Empty<AwardScoreBreakdown>();
        }

        public int PlayerId { get; }
        public string PlayerName { get; }
        public int TeamId { get; }
        public double FinalScore { get; }
        public double IndividualScore { get; }
        public double ParticipationScore { get; }
        public double RecentScore { get; }
        public IReadOnlyList<AwardScoreBreakdown> ScoreBreakdown { get; }
    }

    /// <summary>한 수상의 승자·공동 수상자·상위 후보와 근거를 세이브한다.</summary>
    public sealed class SeasonAwardResultState
    {
        public SeasonAwardResultState(
            string awardId,
            AwardScope scope,
            AwardCategory category,
            PlayerPosition position,
            int winnerPlayerId,
            int[] coWinnerPlayerIds,
            double finalScore,
            IReadOnlyList<AwardCandidateResult> topCandidates)
        {
            AwardId = awardId ?? throw new ArgumentNullException(nameof(awardId));
            Scope = scope;
            Category = category;
            Position = position;
            WinnerPlayerId = winnerPlayerId;
            CoWinnerPlayerIds = coWinnerPlayerIds ?? Array.Empty<int>();
            FinalScore = finalScore;
            TopCandidates = topCandidates ?? Array.Empty<AwardCandidateResult>();
        }

        public string AwardId { get; }
        public AwardScope Scope { get; }
        public AwardCategory Category { get; }
        public PlayerPosition Position { get; }
        public int WinnerPlayerId { get; }
        public IReadOnlyList<int> CoWinnerPlayerIds { get; }
        public double FinalScore { get; }
        public IReadOnlyList<AwardCandidateResult> TopCandidates { get; }

        public bool IncludesWinner(int playerId)
        {
            if (WinnerPlayerId == playerId) return true;
            for (int index = 0; index < CoWinnerPlayerIds.Count; index++)
            {
                if (CoWinnerPlayerIds[index] == playerId) return true;
            }
            return false;
        }
    }

    /// <summary>한 시즌에서 확정된 모든 수상 결과를 보관한다.</summary>
    public sealed class SeasonAwardsState
    {
        private readonly List<SeasonAwardResultState> _results = new();

        public IReadOnlyList<SeasonAwardResultState> Results => _results;

        public void Add(SeasonAwardResultState result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            for (int index = 0; index < _results.Count; index++)
            {
                if (_results[index].AwardId == result.AwardId)
                    throw new InvalidOperationException("같은 수상 결과를 두 번 저장할 수 없습니다.");
            }
            _results.Add(result);
        }

        public SeasonAwardResultState Find(AwardCategory category, PlayerPosition position = PlayerPosition.Unknown)
        {
            for (int index = 0; index < _results.Count; index++)
            {
                SeasonAwardResultState result = _results[index];
                if (result.Category == category && result.Position == position) return result;
            }
            return null;
        }
    }
}
