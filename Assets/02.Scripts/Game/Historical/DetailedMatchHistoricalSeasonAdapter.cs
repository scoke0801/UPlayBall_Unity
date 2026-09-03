using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;

namespace Baseball.Game.Historical
{
    public enum HistoricalMatchStage
    {
        RegularSeasonFirstHalf,
        RegularSeasonSecondHalf,
        AllStarGame,
        Postseason
    }

    /// <summary>DetailedMatchEngine의 정수 PlayerId를 Baked PlayerSeason 원본에 연결한다.</summary>
    public sealed class HistoricalPlayerSeasonIdentity
    {
        public HistoricalPlayerSeasonIdentity(
            int playerId,
            string playerSeasonId,
            string teamSeasonKey,
            PlayerPosition position)
        {
            if (playerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerId));
            if (string.IsNullOrWhiteSpace(playerSeasonId))
                throw new ArgumentException("PlayerSeasonId는 비어 있을 수 없습니다.", nameof(playerSeasonId));
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            if (position == PlayerPosition.Unknown)
                throw new ArgumentException("기록 포지션이 필요합니다.", nameof(position));
            PlayerId = playerId;
            PlayerSeasonId = playerSeasonId.Trim();
            TeamSeasonKey = teamSeasonKey.Trim();
            Position = position;
        }

        public int PlayerId { get; }
        public string PlayerSeasonId { get; }
        public string TeamSeasonKey { get; }
        public PlayerPosition Position { get; }
    }

    /// <summary>기존 DetailedMatchEngine에서 끝난 한 경기와 역사 집계 구간을 연결한다.</summary>
    public sealed class HistoricalDetailedMatchRecord
    {
        public HistoricalDetailedMatchRecord(HistoricalMatchStage stage, MatchResult result)
        {
            if (!Enum.IsDefined(typeof(HistoricalMatchStage), stage))
                throw new ArgumentOutOfRangeException(nameof(stage));
            Stage = stage;
            Result = result ?? throw new ArgumentNullException(nameof(result));
            if (result.Input.RulesVersion != SimulationRulesVersion.DetailedV2)
                throw new ArgumentException("Historical Simulation은 DetailedV2 경기 결과만 소비할 수 있습니다.", nameof(result));
        }

        public HistoricalMatchStage Stage { get; }
        public MatchResult Result { get; }
    }

    /// <summary>실제 시즌 스케줄 실행기가 반환하는 Detailed 경기와 안정 PlayerSeason 매핑이다.</summary>
    public sealed class HistoricalDetailedSeasonOutput
    {
        private readonly HistoricalDetailedMatchRecord[] _matches;
        private readonly HistoricalPlayerSeasonIdentity[] _players;
        private readonly HashSet<string> _allStarGameEligiblePlayerSeasonIds;
        private readonly int? _allStarGameStatisticsTeamId;

        public HistoricalDetailedSeasonOutput(
            int seasonYear,
            IReadOnlyList<HistoricalDetailedMatchRecord> matches,
            IReadOnlyList<HistoricalPlayerSeasonIdentity> players)
            : this(
                seasonYear,
                matches,
                players,
                allStarGameEligiblePlayerSeasonIds: null,
                allStarGameStatisticsTeamId: null)
        {
        }

        /// <summary>단일 All-Star 25인 명세에서 상대 경기 참가자가 MVP 후보로 섞이지 않도록 후보를 고정한다.</summary>
        public HistoricalDetailedSeasonOutput(
            int seasonYear,
            IReadOnlyList<HistoricalDetailedMatchRecord> matches,
            IReadOnlyList<HistoricalPlayerSeasonIdentity> players,
            IReadOnlyList<string> allStarGameEligiblePlayerSeasonIds)
            : this(
                seasonYear,
                matches,
                players,
                allStarGameEligiblePlayerSeasonIds,
                allStarGameStatisticsTeamId: null)
        {
        }

        /// <summary>All-Star 후보와 후보가 출전한 팀 측을 함께 고정해 상대팀의 동명 PlayerSeason 집계를 차단한다.</summary>
        public HistoricalDetailedSeasonOutput(
            int seasonYear,
            IReadOnlyList<HistoricalDetailedMatchRecord> matches,
            IReadOnlyList<HistoricalPlayerSeasonIdentity> players,
            IReadOnlyList<string> allStarGameEligiblePlayerSeasonIds,
            int? allStarGameStatisticsTeamId)
        {
            if (seasonYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonYear));
            if (matches == null || matches.Count == 0)
                throw new ArgumentException("하나 이상의 Detailed 경기 결과가 필요합니다.", nameof(matches));
            if (players == null || players.Count == 0)
                throw new ArgumentException("PlayerSeason 매핑이 필요합니다.", nameof(players));
            SeasonYear = seasonYear;
            _matches = Copy(matches, nameof(matches));
            _players = Copy(players, nameof(players));
            ValidatePlayerIdentities(_players);
            _allStarGameEligiblePlayerSeasonIds = CopyEligibleIds(
                allStarGameEligiblePlayerSeasonIds,
                _players);
            if (allStarGameStatisticsTeamId.HasValue && allStarGameStatisticsTeamId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(allStarGameStatisticsTeamId));
            if (allStarGameStatisticsTeamId.HasValue && _allStarGameEligiblePlayerSeasonIds == null)
                throw new ArgumentException(
                    "All-Star 집계 팀을 지정하려면 후보 PlayerSeasonId가 필요합니다.",
                    nameof(allStarGameStatisticsTeamId));
            _allStarGameStatisticsTeamId = allStarGameStatisticsTeamId;
        }

        public int SeasonYear { get; }
        public IReadOnlyList<HistoricalDetailedMatchRecord> Matches => _matches;
        public IReadOnlyList<HistoricalPlayerSeasonIdentity> Players => _players;

        internal bool IsAllStarGameEligible(string playerSeasonId)
        {
            return _allStarGameEligiblePlayerSeasonIds == null ||
                   _allStarGameEligiblePlayerSeasonIds.Contains(playerSeasonId);
        }

        internal bool ShouldAccumulateTeam(HistoricalMatchStage stage, int teamId)
        {
            return stage != HistoricalMatchStage.AllStarGame ||
                   !_allStarGameStatisticsTeamId.HasValue ||
                   _allStarGameStatisticsTeamId.Value == teamId;
        }

        private static T[] Copy<T>(IReadOnlyList<T> source, string parameterName) where T : class
        {
            var result = new T[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index] ?? throw new ArgumentException("null 항목이 있습니다.", parameterName);
            return result;
        }

        private static void ValidatePlayerIdentities(IReadOnlyList<HistoricalPlayerSeasonIdentity> players)
        {
            var playerIds = new HashSet<int>();
            var playerSeasonIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < players.Count; index++)
            {
                if (!playerIds.Add(players[index].PlayerId) || !playerSeasonIds.Add(players[index].PlayerSeasonId))
                    throw new ArgumentException("PlayerId와 PlayerSeasonId 매핑은 시즌 안에서 고유해야 합니다.", nameof(players));
            }
        }

        private static HashSet<string> CopyEligibleIds(
            IReadOnlyList<string> source,
            IReadOnlyList<HistoricalPlayerSeasonIdentity> players)
        {
            if (source == null)
                return null;
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < players.Count; index++)
                knownIds.Add(players[index].PlayerSeasonId);
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                string id = source[index];
                if (string.IsNullOrWhiteSpace(id) || !knownIds.Contains(id) || !result.Add(id))
                    throw new ArgumentException("All-Star Game 후보 ID가 없거나 중복되었습니다.", nameof(source));
            }
            if (result.Count == 0)
                throw new ArgumentException("All-Star Game 후보가 비어 있습니다.", nameof(source));
            return result;
        }
    }

    /// <summary>스케줄·라인업을 구성해 기존 MatchSimulator Detailed 경로로 실제 시즌을 실행하는 공급자다.</summary>
    public interface IHistoricalDetailedSeasonSource
    {
        HistoricalDetailedSeasonOutput RunSeason(
            ulong worldHistorySeed,
            IReadOnlyList<TeamSeasonDefinition> regularFranchiseTeams);
    }

    /// <summary>DetailedMatchEngine BoxScore만으로 Award 입력 SeasonStatistics를 집계한다.</summary>
    public sealed class DetailedMatchHistoricalSeasonAdapter : IHistoricalSeasonSimulation
    {
        private readonly IHistoricalDetailedSeasonSource _source;

        public DetailedMatchHistoricalSeasonAdapter(IHistoricalDetailedSeasonSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IReadOnlyList<SeasonStatistics> Simulate(
            ulong worldHistorySeed,
            IReadOnlyList<TeamSeasonDefinition> regularFranchiseTeams)
        {
            ValidateRegularTeams(regularFranchiseTeams);
            HistoricalDetailedSeasonOutput output = _source.RunSeason(worldHistorySeed, regularFranchiseTeams)
                ?? throw new InvalidOperationException("Detailed 역사 시즌 실행 결과가 없습니다.");
            return Aggregate(output, regularFranchiseTeams);
        }

        /// <summary>전반기 종료 시점처럼 시즌 중간에도 최종 집계와 같은 규칙으로 기록을 만든다.</summary>
        internal static IReadOnlyList<SeasonStatistics> Aggregate(
            HistoricalDetailedSeasonOutput output,
            IReadOnlyList<TeamSeasonDefinition> regularTeams)
        {
            var regularKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < regularTeams.Count; index++)
                regularKeys.Add(regularTeams[index].TeamSeasonKey);

            var identities = new Dictionary<int, HistoricalPlayerSeasonIdentity>(output.Players.Count);
            for (int index = 0; index < output.Players.Count; index++)
            {
                HistoricalPlayerSeasonIdentity identity = output.Players[index];
                if (!regularKeys.Contains(identity.TeamSeasonKey))
                    throw new InvalidOperationException("특수 합성팀 선수는 Historical Simulation에 포함할 수 없습니다.");
                identities.Add(identity.PlayerId, identity);
            }

            var accumulators = new Dictionary<StatisticsKey, StatisticsAccumulator>();
            for (int matchIndex = 0; matchIndex < output.Matches.Count; matchIndex++)
            {
                HistoricalDetailedMatchRecord match = output.Matches[matchIndex];
                AccumulateMatch(match, output, identities, accumulators);
            }

            var result = new List<SeasonStatistics>(accumulators.Count);
            foreach (KeyValuePair<StatisticsKey, StatisticsAccumulator> pair in accumulators)
            {
                if (pair.Value.HasAppearance)
                    result.Add(pair.Value.Build(output.SeasonYear, pair.Key));
            }
            result.Sort(CompareStatisticsStable);
            if (result.Count == 0)
                throw new InvalidOperationException("Detailed 경기 BoxScore에서 시즌 기록을 집계하지 못했습니다.");
            return result;
        }

        private static void AccumulateMatch(
            HistoricalDetailedMatchRecord match,
            HistoricalDetailedSeasonOutput output,
            IReadOnlyDictionary<int, HistoricalPlayerSeasonIdentity> identities,
            IDictionary<StatisticsKey, StatisticsAccumulator> accumulators)
        {
            if (output.ShouldAccumulateTeam(match.Stage, match.Result.AwayBoxScore.TeamId))
                AccumulateBoxScore(match.Result.AwayBoxScore, match.Stage, output, identities, accumulators);
            if (output.ShouldAccumulateTeam(match.Stage, match.Result.HomeBoxScore.TeamId))
                AccumulateBoxScore(match.Result.HomeBoxScore, match.Stage, output, identities, accumulators);
        }

        private static void AccumulateBoxScore(
            TeamBoxScore boxScore,
            HistoricalMatchStage stage,
            HistoricalDetailedSeasonOutput output,
            IReadOnlyDictionary<int, HistoricalPlayerSeasonIdentity> identities,
            IDictionary<StatisticsKey, StatisticsAccumulator> accumulators)
        {
            for (int index = 0; index < boxScore.BattingLines.Count; index++)
            {
                PlayerBattingLine line = boxScore.BattingLines[index];
                if (!IsEligibleForStage(line.PlayerId, stage, output, identities))
                    continue;
                StatisticsAccumulator accumulator = GetAccumulator(line.PlayerId, stage, identities, accumulators);
                accumulator.AddBatting(line);
                if (stage == HistoricalMatchStage.RegularSeasonFirstHalf)
                    GetRegularAccumulator(line.PlayerId, identities, accumulators).AddBatting(line);
            }
            for (int index = 0; index < boxScore.PitchingLines.Count; index++)
            {
                PlayerPitchingLine line = boxScore.PitchingLines[index];
                if (!IsEligibleForStage(line.PlayerId, stage, output, identities))
                    continue;
                StatisticsAccumulator accumulator = GetAccumulator(line.PlayerId, stage, identities, accumulators);
                accumulator.AddPitching(line);
                if (stage == HistoricalMatchStage.RegularSeasonFirstHalf)
                    GetRegularAccumulator(line.PlayerId, identities, accumulators).AddPitching(line);
            }
            for (int index = 0; index < boxScore.FieldingLines.Count; index++)
            {
                PlayerFieldingLine line = boxScore.FieldingLines[index];
                if (!IsEligibleForStage(line.PlayerId, stage, output, identities))
                    continue;
                StatisticsAccumulator accumulator = GetAccumulator(line.PlayerId, stage, identities, accumulators);
                accumulator.AddFielding(line);
                if (stage == HistoricalMatchStage.RegularSeasonFirstHalf)
                    GetRegularAccumulator(line.PlayerId, identities, accumulators).AddFielding(line);
            }
        }

        private static bool IsEligibleForStage(
            int playerId,
            HistoricalMatchStage stage,
            HistoricalDetailedSeasonOutput output,
            IReadOnlyDictionary<int, HistoricalPlayerSeasonIdentity> identities)
        {
            if (stage != HistoricalMatchStage.AllStarGame)
                return true;
            if (!identities.TryGetValue(playerId, out HistoricalPlayerSeasonIdentity identity))
                throw new InvalidOperationException($"PlayerId {playerId}의 Baked PlayerSeason 매핑이 없습니다.");
            return output.IsAllStarGameEligible(identity.PlayerSeasonId);
        }

        private static StatisticsAccumulator GetAccumulator(
            int playerId,
            HistoricalMatchStage stage,
            IReadOnlyDictionary<int, HistoricalPlayerSeasonIdentity> identities,
            IDictionary<StatisticsKey, StatisticsAccumulator> accumulators)
        {
            if (!identities.TryGetValue(playerId, out HistoricalPlayerSeasonIdentity identity))
                throw new InvalidOperationException($"PlayerId {playerId}의 Baked PlayerSeason 매핑이 없습니다.");
            StatisticsKey key = StatisticsKey.Create(identity, stage);
            if (!accumulators.TryGetValue(key, out StatisticsAccumulator accumulator))
            {
                accumulator = new StatisticsAccumulator(identity);
                accumulators.Add(key, accumulator);
            }
            return accumulator;
        }

        private static StatisticsAccumulator GetRegularAccumulator(
            int playerId,
            IReadOnlyDictionary<int, HistoricalPlayerSeasonIdentity> identities,
            IDictionary<StatisticsKey, StatisticsAccumulator> accumulators)
        {
            return GetAccumulator(playerId, HistoricalMatchStage.RegularSeasonSecondHalf, identities, accumulators);
        }

        private static void ValidateRegularTeams(IReadOnlyList<TeamSeasonDefinition> teams)
        {
            if (teams == null || teams.Count != LeagueInstance.RequiredRegularFranchiseTeamCount)
                throw new ArgumentException("Detailed 역사 시즌에는 정규 Franchise 10구단이 필요합니다.", nameof(teams));
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < teams.Count; index++)
            {
                TeamSeasonDefinition team = teams[index]
                    ?? throw new ArgumentException("null TeamSeason이 있습니다.", nameof(teams));
                if (!keys.Add(team.TeamSeasonKey))
                    throw new ArgumentException("정규 TeamSeasonKey는 중복될 수 없습니다.", nameof(teams));
            }
        }

        private static int CompareStatisticsStable(SeasonStatistics left, SeasonStatistics right)
        {
            int comparison = left.SeasonYear.CompareTo(right.SeasonYear);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(left.PlayerSeasonId, right.PlayerSeasonId);
            if (comparison != 0) return comparison;
            comparison = left.IsPostseason.CompareTo(right.IsPostseason);
            if (comparison != 0) return comparison;
            comparison = left.IsAllStarGame.CompareTo(right.IsAllStarGame);
            if (comparison != 0) return comparison;
            return left.IsFirstHalf.CompareTo(right.IsFirstHalf);
        }

        private readonly struct StatisticsKey : IEquatable<StatisticsKey>
        {
            private StatisticsKey(string playerSeasonId, bool isFirstHalf, bool isPostseason, bool isAllStarGame)
            {
                PlayerSeasonId = playerSeasonId;
                IsFirstHalf = isFirstHalf;
                IsPostseason = isPostseason;
                IsAllStarGame = isAllStarGame;
            }

            public string PlayerSeasonId { get; }
            public bool IsFirstHalf { get; }
            public bool IsPostseason { get; }
            public bool IsAllStarGame { get; }

            public static StatisticsKey Create(HistoricalPlayerSeasonIdentity identity, HistoricalMatchStage stage)
            {
                return new StatisticsKey(
                    identity.PlayerSeasonId,
                    stage == HistoricalMatchStage.RegularSeasonFirstHalf,
                    stage == HistoricalMatchStage.Postseason,
                    stage == HistoricalMatchStage.AllStarGame);
            }

            public bool Equals(StatisticsKey other)
            {
                return IsFirstHalf == other.IsFirstHalf &&
                       IsPostseason == other.IsPostseason &&
                       IsAllStarGame == other.IsAllStarGame &&
                       string.Equals(PlayerSeasonId, other.PlayerSeasonId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is StatisticsKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = StringComparer.Ordinal.GetHashCode(PlayerSeasonId);
                    hash = hash * 397 ^ (IsFirstHalf ? 1 : 0);
                    hash = hash * 397 ^ (IsPostseason ? 1 : 0);
                    return hash * 397 ^ (IsAllStarGame ? 1 : 0);
                }
            }
        }

        private sealed class StatisticsAccumulator
        {
            private double _defensiveOutsAboveAverage;

            public StatisticsAccumulator(HistoricalPlayerSeasonIdentity identity)
            {
                Identity = identity;
            }

            public HistoricalPlayerSeasonIdentity Identity { get; }
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
            public int FieldingErrors { get; private set; }
            public bool HasAppearance => PlateAppearances > 0 || PitchingOuts > 0 || DefensiveChances > 0;

            public void AddBatting(PlayerBattingLine line)
            {
                PlateAppearances += line.PlateAppearances;
                Hits += line.Hits;
                HomeRuns += line.HomeRuns;
                Walks += line.Walks;
                Strikeouts += line.Strikeouts;
                StolenBases += line.StolenBases;
            }

            public void AddPitching(PlayerPitchingLine line)
            {
                PitchingOuts += line.OutsRecorded;
                EarnedRuns += line.EarnedRuns;
                PitchingStrikeouts += line.Strikeouts;
            }

            public void AddFielding(PlayerFieldingLine line)
            {
                DefensiveChances += line.Opportunities;
                FieldingErrors += line.Errors;
                _defensiveOutsAboveAverage += line.SuccessfulPlays - line.ExpectedOuts;
            }

            public SeasonStatistics Build(int seasonYear, StatisticsKey key)
            {
                return new SeasonStatistics(
                    Identity.PlayerSeasonId,
                    Identity.TeamSeasonKey,
                    seasonYear,
                    Identity.Position,
                    PlateAppearances,
                    Hits,
                    HomeRuns,
                    Walks,
                    Strikeouts,
                    StolenBases,
                    PitchingOuts,
                    EarnedRuns,
                    PitchingStrikeouts,
                    DefensiveChances,
                    (int)Math.Round(_defensiveOutsAboveAverage, MidpointRounding.AwayFromZero),
                    FieldingErrors,
                    key.IsFirstHalf,
                    key.IsPostseason,
                    key.IsAllStarGame);
            }
        }
    }
}
