using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;

namespace Baseball.Simulation.Historical
{
    /// <summary>기록 기반 수상 점수의 조정 가능한 가중치를 한곳에 보관한다.</summary>
    public sealed class AwardScoringPolicy
    {
        public AwardScoringPolicy(
            double hitWeight,
            double homeRunWeight,
            double walkWeight,
            double stolenBaseWeight,
            double batterStrikeoutPenalty,
            double pitchingOutWeight,
            double pitchingStrikeoutWeight,
            double earnedRunPenalty,
            double defensiveChanceWeight,
            double defensiveOutAboveAverageWeight,
            double fieldingErrorPenalty)
        {
            HitWeight = RequireNonNegative(hitWeight, nameof(hitWeight));
            HomeRunWeight = RequireNonNegative(homeRunWeight, nameof(homeRunWeight));
            WalkWeight = RequireNonNegative(walkWeight, nameof(walkWeight));
            StolenBaseWeight = RequireNonNegative(stolenBaseWeight, nameof(stolenBaseWeight));
            BatterStrikeoutPenalty = RequireNonNegative(batterStrikeoutPenalty, nameof(batterStrikeoutPenalty));
            PitchingOutWeight = RequireNonNegative(pitchingOutWeight, nameof(pitchingOutWeight));
            PitchingStrikeoutWeight = RequireNonNegative(pitchingStrikeoutWeight, nameof(pitchingStrikeoutWeight));
            EarnedRunPenalty = RequireNonNegative(earnedRunPenalty, nameof(earnedRunPenalty));
            DefensiveChanceWeight = RequireNonNegative(defensiveChanceWeight, nameof(defensiveChanceWeight));
            DefensiveOutAboveAverageWeight = RequireNonNegative(
                defensiveOutAboveAverageWeight,
                nameof(defensiveOutAboveAverageWeight));
            FieldingErrorPenalty = RequireNonNegative(fieldingErrorPenalty, nameof(fieldingErrorPenalty));
        }

        public double HitWeight { get; }
        public double HomeRunWeight { get; }
        public double WalkWeight { get; }
        public double StolenBaseWeight { get; }
        public double BatterStrikeoutPenalty { get; }
        public double PitchingOutWeight { get; }
        public double PitchingStrikeoutWeight { get; }
        public double EarnedRunPenalty { get; }
        public double DefensiveChanceWeight { get; }
        public double DefensiveOutAboveAverageWeight { get; }
        public double FieldingErrorPenalty { get; }

        /// <summary>기획 수치 확정 전에도 기록의 양과 질을 함께 반영하는 최소 안전 기본값이다.</summary>
        public static AwardScoringPolicy CreateDefault()
        {
            return new AwardScoringPolicy(
                hitWeight: 1d,
                homeRunWeight: 4d,
                walkWeight: 1d,
                stolenBaseWeight: 1d,
                batterStrikeoutPenalty: 0.25d,
                pitchingOutWeight: 1d,
                pitchingStrikeoutWeight: 2d,
                earnedRunPenalty: 3d,
                defensiveChanceWeight: 0.02d,
                defensiveOutAboveAverageWeight: 4d,
                fieldingErrorPenalty: 3d);
        }

        private static double RequireNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    /// <summary>실제 전반기 기록에서 포지션 쿼터를 지켜 All-Star 25인을 선정한다.</summary>
    public sealed class AllStarSelectionResolver
    {
        private readonly AwardScoringPolicy _policy;

        public AllStarSelectionResolver(AwardScoringPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public IReadOnlyList<WorldAwardEntry> Resolve(IReadOnlyList<SeasonStatistics> statistics)
        {
            List<AwardCandidate> candidates = AwardCandidateAggregator.Aggregate(statistics, AwardStatisticsScope.FirstHalf);
            List<int> years = AwardCandidateAggregator.GetYears(candidates);
            var entries = new List<WorldAwardEntry>(years.Count * 25);
            for (int yearIndex = 0; yearIndex < years.Count; yearIndex++)
                ResolveYear(years[yearIndex], candidates, entries);
            return entries;
        }

        private void ResolveYear(int year, List<AwardCandidate> allCandidates, List<WorldAwardEntry> entries)
        {
            var selected = new HashSet<string>(StringComparer.Ordinal);
            PlayerPosition[] startingPositions = AwardPositionCatalog.StartingHitterPositions;
            for (int index = 0; index < startingPositions.Length; index++)
                SelectPosition(year, startingPositions[index], 1, allCandidates, selected, entries);

            SelectPosition(year, PlayerPosition.StartingPitcher, 5, allCandidates, selected, entries);
            SelectPosition(year, PlayerPosition.ReliefPitcher, 6, allCandidates, selected, entries);
            SelectBestHitters(year, 5, allCandidates, selected, entries);
        }

        private void SelectPosition(
            int year,
            PlayerPosition position,
            int count,
            List<AwardCandidate> allCandidates,
            HashSet<string> selected,
            List<WorldAwardEntry> entries)
        {
            List<AwardCandidate> ranked = AwardRanking.Rank(
                allCandidates,
                candidate => candidate.SeasonYear == year && candidate.Position == position && !selected.Contains(candidate.PlayerSeasonId),
                candidate => AwardRanking.GetOverallScore(candidate, _policy));
            AwardRanking.RequireCount(ranked, count, "All-Star 포지션 쿼터를 채울 기록이 부족합니다.");
            for (int index = 0; index < count; index++)
            {
                AwardCandidate candidate = ranked[index];
                selected.Add(candidate.PlayerSeasonId);
                entries.Add(new WorldAwardEntry(year, WorldAwardType.AllStar, candidate.PlayerSeasonId, candidate.Position));
            }
        }

        private void SelectBestHitters(
            int year,
            int count,
            List<AwardCandidate> allCandidates,
            HashSet<string> selected,
            List<WorldAwardEntry> entries)
        {
            List<AwardCandidate> ranked = AwardRanking.Rank(
                allCandidates,
                candidate => candidate.SeasonYear == year && AwardPositionCatalog.IsHitter(candidate.Position) &&
                    !selected.Contains(candidate.PlayerSeasonId),
                candidate => AwardRanking.GetBattingScore(candidate, _policy));
            AwardRanking.RequireCount(ranked, count, "All-Star 벤치 쿼터를 채울 기록이 부족합니다.");
            for (int index = 0; index < count; index++)
            {
                AwardCandidate candidate = ranked[index];
                selected.Add(candidate.PlayerSeasonId);
                entries.Add(new WorldAwardEntry(year, WorldAwardType.AllStar, candidate.PlayerSeasonId, candidate.Position));
            }
        }
    }

    /// <summary>정규시즌 수비 기록에서 P/C/내야/DH 각 1명과 OF 3명을 선정한다.</summary>
    public sealed class GoldenGloveAwardResolver
    {
        private readonly AwardScoringPolicy _policy;

        public GoldenGloveAwardResolver(AwardScoringPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public IReadOnlyList<WorldAwardEntry> Resolve(IReadOnlyList<SeasonStatistics> statistics)
        {
            List<AwardCandidate> candidates = AwardCandidateAggregator.Aggregate(statistics, AwardStatisticsScope.RegularSeason);
            List<int> years = AwardCandidateAggregator.GetYears(candidates);
            var entries = new List<WorldAwardEntry>(years.Count * 10);
            for (int yearIndex = 0; yearIndex < years.Count; yearIndex++)
            {
                int year = years[yearIndex];
                SelectPitcher(year, candidates, entries);
                SelectPosition(year, PlayerPosition.Catcher, candidates, entries);
                SelectPosition(year, PlayerPosition.FirstBase, candidates, entries);
                SelectPosition(year, PlayerPosition.SecondBase, candidates, entries);
                SelectPosition(year, PlayerPosition.ThirdBase, candidates, entries);
                SelectPosition(year, PlayerPosition.Shortstop, candidates, entries);
                SelectOutfielders(year, candidates, entries);
                SelectPosition(year, PlayerPosition.DesignatedHitter, candidates, entries);
            }
            return entries;
        }

        private void SelectPitcher(int year, List<AwardCandidate> candidates, List<WorldAwardEntry> entries)
        {
            List<AwardCandidate> ranked = AwardRanking.Rank(
                candidates,
                candidate => candidate.SeasonYear == year && AwardPositionCatalog.IsPitcher(candidate.Position),
                candidate => AwardRanking.GetPitchingScore(candidate, _policy) + AwardRanking.GetDefenseScore(candidate, _policy));
            AddWinner(year, ranked, entries, "Golden Glove 투수 후보가 없습니다.");
        }

        private void SelectPosition(
            int year,
            PlayerPosition position,
            List<AwardCandidate> candidates,
            List<WorldAwardEntry> entries)
        {
            List<AwardCandidate> ranked = AwardRanking.Rank(
                candidates,
                candidate => candidate.SeasonYear == year && candidate.Position == position,
                candidate => position == PlayerPosition.DesignatedHitter
                    ? AwardRanking.GetBattingScore(candidate, _policy)
                    : AwardRanking.GetDefenseScore(candidate, _policy));
            AddWinner(year, ranked, entries, "Golden Glove 포지션 후보가 없습니다.");
        }

        private void SelectOutfielders(int year, List<AwardCandidate> candidates, List<WorldAwardEntry> entries)
        {
            List<AwardCandidate> ranked = AwardRanking.Rank(
                candidates,
                candidate => candidate.SeasonYear == year && AwardPositionCatalog.IsOutfielder(candidate.Position),
                candidate => AwardRanking.GetDefenseScore(candidate, _policy));
            AwardRanking.RequireCount(ranked, 3, "Golden Glove 외야 후보가 부족합니다.");
            for (int index = 0; index < 3; index++)
            {
                AwardCandidate winner = ranked[index];
                entries.Add(new WorldAwardEntry(year, WorldAwardType.GoldenGlove, winner.PlayerSeasonId, winner.Position));
            }
        }

        private static void AddWinner(
            int year,
            List<AwardCandidate> ranked,
            List<WorldAwardEntry> entries,
            string errorMessage)
        {
            AwardRanking.RequireCount(ranked, 1, errorMessage);
            AwardCandidate winner = ranked[0];
            entries.Add(new WorldAwardEntry(year, WorldAwardType.GoldenGlove, winner.PlayerSeasonId, winner.Position));
        }
    }

    /// <summary>실제 정규시즌 기록의 종합 가치가 가장 높은 선수를 MVP로 선정한다.</summary>
    public sealed class RegularSeasonMvpResolver : SingleWinnerAwardResolver
    {
        public RegularSeasonMvpResolver(AwardScoringPolicy policy)
            : base(policy, AwardStatisticsScope.RegularSeason, WorldAwardType.RegularSeasonMvp)
        {
        }
    }

    /// <summary>실제 All-Star Game 기록의 종합 가치가 가장 높은 선수를 MVP로 선정한다.</summary>
    public sealed class AllStarGameMvpResolver : SingleWinnerAwardResolver
    {
        public AllStarGameMvpResolver(AwardScoringPolicy policy)
            : base(policy, AwardStatisticsScope.AllStarGame, WorldAwardType.AllStarGameMvp)
        {
        }
    }

    /// <summary>실제 Postseason 기록의 종합 가치가 가장 높은 선수를 MVP로 선정한다.</summary>
    public sealed class PostseasonMvpResolver : SingleWinnerAwardResolver
    {
        public PostseasonMvpResolver(AwardScoringPolicy policy)
            : base(policy, AwardStatisticsScope.Postseason, WorldAwardType.PostseasonMvp)
        {
        }
    }

    /// <summary>Statistics → Awards 순서를 고정해 완성된 WorldAwardRecord를 만든다.</summary>
    public sealed class WorldAwardResolver : ISeasonAwardResolver
    {
        private readonly AllStarSelectionResolver _allStarResolver;
        private readonly GoldenGloveAwardResolver _goldenGloveResolver;
        private readonly RegularSeasonMvpResolver _regularSeasonMvpResolver;
        private readonly AllStarGameMvpResolver _allStarGameMvpResolver;
        private readonly PostseasonMvpResolver _postseasonMvpResolver;

        public WorldAwardResolver(AwardScoringPolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));
            _allStarResolver = new AllStarSelectionResolver(policy);
            _goldenGloveResolver = new GoldenGloveAwardResolver(policy);
            _regularSeasonMvpResolver = new RegularSeasonMvpResolver(policy);
            _allStarGameMvpResolver = new AllStarGameMvpResolver(policy);
            _postseasonMvpResolver = new PostseasonMvpResolver(policy);
        }

        public WorldAwardRecord Resolve(IReadOnlyList<SeasonStatistics> statistics)
        {
            if (statistics == null)
                throw new ArgumentNullException(nameof(statistics));
            var entries = new List<WorldAwardEntry>();
            Append(entries, _allStarResolver.Resolve(statistics));
            Append(entries, _goldenGloveResolver.Resolve(statistics));
            Append(entries, _regularSeasonMvpResolver.Resolve(statistics));
            Append(entries, _allStarGameMvpResolver.Resolve(statistics));
            Append(entries, _postseasonMvpResolver.Resolve(statistics));
            return new WorldAwardRecord(entries);
        }

        private static void Append(List<WorldAwardEntry> target, IReadOnlyList<WorldAwardEntry> source)
        {
            for (int index = 0; index < source.Count; index++)
                target.Add(source[index]);
        }
    }

    /// <summary>한 경기 범주의 연도별 단일 기록 MVP 공통 구현이다.</summary>
    public abstract class SingleWinnerAwardResolver
    {
        private readonly AwardScoringPolicy _policy;
        private readonly AwardStatisticsScope _scope;
        private readonly WorldAwardType _awardType;

        protected SingleWinnerAwardResolver(
            AwardScoringPolicy policy,
            AwardStatisticsScope scope,
            WorldAwardType awardType)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _scope = scope;
            _awardType = awardType;
        }

        public IReadOnlyList<WorldAwardEntry> Resolve(IReadOnlyList<SeasonStatistics> statistics)
        {
            List<AwardCandidate> candidates = AwardCandidateAggregator.Aggregate(statistics, _scope);
            List<int> years = AwardCandidateAggregator.GetYears(candidates);
            var entries = new List<WorldAwardEntry>(years.Count);
            for (int yearIndex = 0; yearIndex < years.Count; yearIndex++)
            {
                int year = years[yearIndex];
                List<AwardCandidate> ranked = AwardRanking.Rank(
                    candidates,
                    candidate => candidate.SeasonYear == year,
                    candidate => AwardRanking.GetOverallScore(candidate, _policy));
                AwardRanking.RequireCount(ranked, 1, _awardType + " 후보 기록이 없습니다.");
                AwardCandidate winner = ranked[0];
                entries.Add(new WorldAwardEntry(year, _awardType, winner.PlayerSeasonId, winner.Position));
            }
            return entries;
        }
    }

    /// <summary>수상 Resolver가 사용할 실제 경기 기록 구간을 구분한다.</summary>
    public enum AwardStatisticsScope
    {
        FirstHalf,
        RegularSeason,
        AllStarGame,
        Postseason
    }

    internal sealed class AwardCandidate
    {
        public AwardCandidate(SeasonStatistics statistics)
        {
            PlayerSeasonId = statistics.PlayerSeasonId;
            SeasonYear = statistics.SeasonYear;
            Position = statistics.Position;
        }

        public string PlayerSeasonId { get; }
        public int SeasonYear { get; }
        public PlayerPosition Position { get; }
        public int PlateAppearances { get; private set; }
        public int Hits { get; private set; }
        public int HomeRuns { get; private set; }
        public int Walks { get; private set; }
        public int Strikeouts { get; private set; }
        public int StolenBases { get; private set; }
        public int PitchingOuts { get; private set; }
        public int EarnedRuns { get; private set; }
        public int PitchingStrikeouts { get; private set; }
        public int DefensiveChances { get; private set; }
        public int DefensiveOutsAboveAverage { get; private set; }
        public int FieldingErrors { get; private set; }

        public void Add(SeasonStatistics statistics)
        {
            if (statistics.SeasonYear != SeasonYear ||
                statistics.Position != Position ||
                !string.Equals(statistics.PlayerSeasonId, PlayerSeasonId, StringComparison.Ordinal))
                throw new InvalidOperationException("서로 다른 선수 시즌 기록을 합칠 수 없습니다.");

            PlateAppearances += statistics.PlateAppearances;
            Hits += statistics.Hits;
            HomeRuns += statistics.HomeRuns;
            Walks += statistics.Walks;
            Strikeouts += statistics.Strikeouts;
            StolenBases += statistics.StolenBases;
            PitchingOuts += statistics.PitchingOuts;
            EarnedRuns += statistics.EarnedRuns;
            PitchingStrikeouts += statistics.PitchingStrikeouts;
            DefensiveChances += statistics.DefensiveChances;
            DefensiveOutsAboveAverage += statistics.DefensiveOutsAboveAverage;
            FieldingErrors += statistics.FieldingErrors;
        }
    }

    internal static class AwardCandidateAggregator
    {
        public static List<AwardCandidate> Aggregate(
            IReadOnlyList<SeasonStatistics> statistics,
            AwardStatisticsScope scope)
        {
            if (statistics == null)
                throw new ArgumentNullException(nameof(statistics));

            var byPlayer = new Dictionary<string, AwardCandidate>(StringComparer.Ordinal);
            for (int index = 0; index < statistics.Count; index++)
            {
                SeasonStatistics row = statistics[index]
                    ?? throw new ArgumentException("null 시즌 기록이 있습니다.", nameof(statistics));
                if (!MatchesScope(row, scope))
                    continue;

                string key = row.SeasonYear + "\u001f" + row.PlayerSeasonId + "\u001f" + (int)row.Position;
                if (!byPlayer.TryGetValue(key, out AwardCandidate candidate))
                {
                    candidate = new AwardCandidate(row);
                    byPlayer.Add(key, candidate);
                }
                candidate.Add(row);
            }

            var result = new List<AwardCandidate>(byPlayer.Count);
            foreach (KeyValuePair<string, AwardCandidate> pair in byPlayer)
                result.Add(pair.Value);
            result.Sort(AwardRanking.CompareStableIdentity);
            return result;
        }

        public static List<int> GetYears(List<AwardCandidate> candidates)
        {
            var unique = new HashSet<int>();
            var years = new List<int>();
            for (int index = 0; index < candidates.Count; index++)
            {
                if (unique.Add(candidates[index].SeasonYear))
                    years.Add(candidates[index].SeasonYear);
            }
            years.Sort();
            return years;
        }

        private static bool MatchesScope(SeasonStatistics statistics, AwardStatisticsScope scope)
        {
            switch (scope)
            {
                case AwardStatisticsScope.FirstHalf:
                    return statistics.IsFirstHalf && !statistics.IsPostseason && !statistics.IsAllStarGame;
                case AwardStatisticsScope.RegularSeason:
                    return !statistics.IsFirstHalf && !statistics.IsPostseason && !statistics.IsAllStarGame;
                case AwardStatisticsScope.AllStarGame:
                    return statistics.IsAllStarGame;
                case AwardStatisticsScope.Postseason:
                    return statistics.IsPostseason;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scope));
            }
        }
    }

    internal static class AwardPositionCatalog
    {
        public static readonly PlayerPosition[] StartingHitterPositions =
        {
            PlayerPosition.Catcher,
            PlayerPosition.FirstBase,
            PlayerPosition.SecondBase,
            PlayerPosition.ThirdBase,
            PlayerPosition.Shortstop,
            PlayerPosition.LeftField,
            PlayerPosition.CenterField,
            PlayerPosition.RightField,
            PlayerPosition.DesignatedHitter
        };

        public static bool IsHitter(PlayerPosition position)
        {
            return position >= PlayerPosition.Catcher && position <= PlayerPosition.DesignatedHitter;
        }

        public static bool IsPitcher(PlayerPosition position)
        {
            return position == PlayerPosition.StartingPitcher || position == PlayerPosition.ReliefPitcher;
        }

        public static bool IsOutfielder(PlayerPosition position)
        {
            return position == PlayerPosition.LeftField ||
                position == PlayerPosition.CenterField ||
                position == PlayerPosition.RightField;
        }
    }

    internal static class AwardRanking
    {
        public static List<AwardCandidate> Rank(
            List<AwardCandidate> source,
            Predicate<AwardCandidate> predicate,
            Func<AwardCandidate, double> score)
        {
            var result = new List<AwardCandidate>();
            for (int index = 0; index < source.Count; index++)
                if (predicate(source[index])) result.Add(source[index]);
            result.Sort((left, right) => CompareScore(left, right, score));
            return result;
        }

        public static double GetOverallScore(AwardCandidate candidate, AwardScoringPolicy policy)
        {
            if (AwardPositionCatalog.IsPitcher(candidate.Position))
                return GetPitchingScore(candidate, policy) + GetDefenseScore(candidate, policy);
            return GetBattingScore(candidate, policy) + GetDefenseScore(candidate, policy);
        }

        public static double GetBattingScore(AwardCandidate candidate, AwardScoringPolicy policy)
        {
            return candidate.Hits * policy.HitWeight +
                candidate.HomeRuns * policy.HomeRunWeight +
                candidate.Walks * policy.WalkWeight +
                candidate.StolenBases * policy.StolenBaseWeight -
                candidate.Strikeouts * policy.BatterStrikeoutPenalty;
        }

        public static double GetPitchingScore(AwardCandidate candidate, AwardScoringPolicy policy)
        {
            return candidate.PitchingOuts * policy.PitchingOutWeight +
                candidate.PitchingStrikeouts * policy.PitchingStrikeoutWeight -
                candidate.EarnedRuns * policy.EarnedRunPenalty;
        }

        public static double GetDefenseScore(AwardCandidate candidate, AwardScoringPolicy policy)
        {
            return candidate.DefensiveChances * policy.DefensiveChanceWeight +
                candidate.DefensiveOutsAboveAverage * policy.DefensiveOutAboveAverageWeight -
                candidate.FieldingErrors * policy.FieldingErrorPenalty;
        }

        public static int CompareStableIdentity(AwardCandidate left, AwardCandidate right)
        {
            int year = left.SeasonYear.CompareTo(right.SeasonYear);
            if (year != 0) return year;
            int player = string.CompareOrdinal(left.PlayerSeasonId, right.PlayerSeasonId);
            if (player != 0) return player;
            return left.Position.CompareTo(right.Position);
        }

        public static void RequireCount(List<AwardCandidate> candidates, int count, string message)
        {
            if (candidates.Count < count)
                throw new InvalidOperationException(message);
        }

        private static int CompareScore(
            AwardCandidate left,
            AwardCandidate right,
            Func<AwardCandidate, double> score)
        {
            int scoreOrder = score(right).CompareTo(score(left));
            return scoreOrder != 0 ? scoreOrder : CompareStableIdentity(left, right);
        }
    }
}
