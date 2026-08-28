using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Simulation.Match;

namespace Baseball.Game.Career
{
    /// <summary>정규 시즌과 포스트시즌 기록의 집계 범위를 구분한다.</summary>
    public enum CompetitionScope
    {
        RegularSeason,
        Postseason
    }

    /// <summary>수상과 커리어 기록에 필요한 타격 원본 및 파생 기록을 보관한다.</summary>
    public sealed class BattingStatisticsState
    {
        public int Games { get; private set; }
        public int GamesStarted { get; private set; }
        public int PlateAppearances { get; private set; }
        public int AtBats { get; private set; }
        public int Runs { get; private set; }
        public int Hits { get; private set; }
        public int Doubles { get; private set; }
        public int Triples { get; private set; }
        public int HomeRuns { get; private set; }
        public int RunsBattedIn { get; private set; }
        public int Walks { get; private set; }
        public int HitByPitches { get; private set; }
        public int Strikeouts { get; private set; }
        public int StolenBases { get; private set; }
        public int CaughtStealing { get; private set; }
        public int SacrificeFlies { get; private set; }
        public int GroundedIntoDoublePlays { get; private set; }
        public int Singles => Hits - Doubles - Triples - HomeRuns;
        public int TotalBases => Hits + Doubles + Triples * 2 + HomeRuns * 3;
        public double BattingAverage => AtBats == 0 ? 0d : Hits / (double)AtBats;
        public double OnBasePercentage => PlateAppearances == 0
            ? 0d
            : (Hits + Walks + HitByPitches) / (double)PlateAppearances;
        public double SluggingPercentage => AtBats == 0 ? 0d : TotalBases / (double)AtBats;
        public double OnBasePlusSlugging => OnBasePercentage + SluggingPercentage;
        public double WalkStrikeoutRatio => Strikeouts == 0 ? Walks : Walks / (double)Strikeouts;
        public double StolenBasePercentage => StolenBases + CaughtStealing == 0
            ? 0d
            : StolenBases / (double)(StolenBases + CaughtStealing);

        internal void Add(PlayerGameStatistics game)
        {
            if (!game.HasBattingLine) return;
            Games++;
            if (game.StartedBatting) GamesStarted++;
            PlateAppearances += game.PlateAppearances;
            AtBats += game.AtBats;
            Runs += game.Runs;
            Hits += game.Hits;
            Doubles += game.Doubles;
            Triples += game.Triples;
            HomeRuns += game.HomeRuns;
            RunsBattedIn += game.RunsBattedIn;
            Walks += game.Walks;
            HitByPitches += game.HitByPitches;
            Strikeouts += game.BattingStrikeouts;
            StolenBases += game.StolenBases;
            CaughtStealing += game.CaughtStealing;
            SacrificeFlies += game.SacrificeFlies;
            GroundedIntoDoublePlays += game.GroundedIntoDoublePlays;
        }
    }

    /// <summary>투구 이닝을 소수가 아닌 아웃 카운트 정수로 보관한다.</summary>
    public sealed class PitchingStatisticsState
    {
        public int Appearances { get; private set; }
        public int Starts { get; private set; }
        public int OutsRecorded { get; private set; }
        public int Wins { get; private set; }
        public int Losses { get; private set; }
        public int Saves { get; private set; }
        public int Holds { get; private set; }
        public int BlownSaves { get; private set; }
        public int HitsAllowed { get; private set; }
        public int HomeRunsAllowed { get; private set; }
        public int WalksAllowed { get; private set; }
        public int HitBatters { get; private set; }
        public int Strikeouts { get; private set; }
        public int RunsAllowed { get; private set; }
        public int EarnedRuns { get; private set; }
        public int BattersFaced { get; private set; }
        public int QualityStarts { get; private set; }
        public double EarnedRunAverage => OutsRecorded == 0 ? 0d : EarnedRuns * 27d / OutsRecorded;
        public double WalksHitsPerInningPitched => OutsRecorded == 0
            ? 0d
            : (WalksAllowed + HitsAllowed) * 3d / OutsRecorded;
        public double StrikeoutWalkRatio => WalksAllowed == 0 ? Strikeouts : Strikeouts / (double)WalksAllowed;
        public double HomeRunsPerNineInnings => OutsRecorded == 0 ? 0d : HomeRunsAllowed * 27d / OutsRecorded;

        internal void Add(PlayerGameStatistics game)
        {
            if (!game.HasPitchingLine) return;
            Appearances++;
            if (game.StartedPitching) Starts++;
            OutsRecorded += game.OutsRecorded;
            Wins += game.Wins;
            Losses += game.Losses;
            Saves += game.Saves;
            Holds += game.Holds;
            BlownSaves += game.BlownSaves;
            HitsAllowed += game.HitsAllowed;
            HomeRunsAllowed += game.HomeRunsAllowed;
            WalksAllowed += game.WalksAllowed;
            HitBatters += game.HitBatters;
            Strikeouts += game.PitchingStrikeouts;
            RunsAllowed += game.RunsAllowed;
            EarnedRuns += game.EarnedRuns;
            BattersFaced += game.BattersFaced;
            QualityStarts += game.QualityStarts;
        }
    }

    /// <summary>한 포지션에서 발생한 수비 기회와 기대 대비 실점 억제를 보관한다.</summary>
    public sealed class FieldingStatisticsState
    {
        public int DefensiveOuts { get; private set; }
        public int Opportunities { get; private set; }
        public int SuccessfulPlays { get; private set; }
        public int Putouts { get; private set; }
        public int Assists { get; private set; }
        public int Errors { get; private set; }
        public int DoublePlays { get; private set; }
        public int DifficultPlayAttempts { get; private set; }
        public int DifficultPlaysMade { get; private set; }
        public double ExpectedOuts { get; private set; }
        public double EstimatedRunsSaved { get; private set; }
        public double SuccessRate => Opportunities == 0 ? 0d : SuccessfulPlays / (double)Opportunities;

        internal void Add(PlayerFieldingLine line)
        {
            DefensiveOuts += line.DefensiveOuts;
            Opportunities += line.Opportunities;
            SuccessfulPlays += line.SuccessfulPlays;
            Putouts += line.Putouts;
            Assists += line.Assists;
            Errors += line.Errors;
            DoublePlays += line.DoublePlays;
            DifficultPlayAttempts += line.DifficultPlayAttempts;
            DifficultPlaysMade += line.DifficultPlaysMade;
            ExpectedOuts += line.ExpectedOuts;
            EstimatedRunsSaved += line.EstimatedRunsSaved;
        }
    }

    /// <summary>최근 구간 평가와 포스트시즌 라운드 가중치를 재현하는 한 경기 기여 기록이다.</summary>
    public readonly struct PlayerGameContributionState
    {
        public PlayerGameContributionState(
            int gameId,
            int roundIndex,
            bool isChampionship,
            bool isSeriesClinching,
            double rawScore,
            double weightedScore)
        {
            GameId = gameId;
            RoundIndex = roundIndex;
            IsChampionship = isChampionship;
            IsSeriesClinching = isSeriesClinching;
            RawScore = rawScore;
            WeightedScore = weightedScore;
        }

        public int GameId { get; }
        public int RoundIndex { get; }
        public bool IsChampionship { get; }
        public bool IsSeriesClinching { get; }
        public double RawScore { get; }
        public double WeightedScore { get; }
    }

    /// <summary>리그 전체 통계 원본에서 한 선수의 한 경쟁 범위 기록을 소유한다.</summary>
    public sealed class PlayerCompetitionStatisticsState
    {
        private readonly Dictionary<PlayerPosition, FieldingStatisticsState> _fieldingByPosition = new();
        private readonly List<PlayerGameContributionState> _gameContributions = new();

        public PlayerCompetitionStatisticsState(
            int playerId,
            string playerName,
            int teamId,
            PlayerPosition primaryPosition)
        {
            PlayerId = playerId;
            PlayerName = playerName ?? string.Empty;
            TeamId = teamId;
            PrimaryPosition = primaryPosition;
            Batting = new BattingStatisticsState();
            Pitching = new PitchingStatisticsState();
        }

        public int PlayerId { get; }
        public string PlayerName { get; }
        public int TeamId { get; private set; }
        public PlayerPosition PrimaryPosition { get; private set; }
        public int TeamGames { get; private set; }
        public BattingStatisticsState Batting { get; }
        public PitchingStatisticsState Pitching { get; }
        public IReadOnlyDictionary<PlayerPosition, FieldingStatisticsState> FieldingByPosition => _fieldingByPosition;
        public IReadOnlyList<PlayerGameContributionState> GameContributions => _gameContributions;
        public int GamesPlayed => Batting.Games + Pitching.Appearances;

        internal void UpdateIdentity(int teamId, PlayerPosition primaryPosition)
        {
            TeamId = teamId;
            if (primaryPosition != PlayerPosition.Unknown)
                PrimaryPosition = primaryPosition;
        }

        internal void RecordTeamGame() => TeamGames++;

        internal void Add(PlayerGameStatistics game)
        {
            UpdateIdentity(game.TeamId, game.PrimaryPosition);
            Batting.Add(game);
            Pitching.Add(game);
            if (game.FieldingLine != null)
            {
                PlayerPosition position = game.FieldingLine.Position;
                if (!_fieldingByPosition.TryGetValue(position, out FieldingStatisticsState fielding))
                {
                    fielding = new FieldingStatisticsState();
                    _fieldingByPosition.Add(position, fielding);
                }
                fielding.Add(game.FieldingLine);
            }
            _gameContributions.Add(game.Contribution);
        }

        public FieldingStatisticsState GetFielding(PlayerPosition position)
        {
            return _fieldingByPosition.TryGetValue(position, out FieldingStatisticsState value) ? value : null;
        }
    }

    /// <summary>한 경쟁 범위의 선수 통계를 PlayerId로 조회하고 동결 상태를 강제한다.</summary>
    public sealed class CompetitionStatisticsState
    {
        private readonly Dictionary<int, PlayerCompetitionStatisticsState> _players = new();

        public bool IsFrozen { get; private set; }
        public IReadOnlyDictionary<int, PlayerCompetitionStatisticsState> Players => _players;

        public PlayerCompetitionStatisticsState GetOrCreate(
            int playerId,
            string playerName,
            int teamId,
            PlayerPosition position)
        {
            if (_players.TryGetValue(playerId, out PlayerCompetitionStatisticsState existing))
            {
                existing.UpdateIdentity(teamId, position);
                return existing;
            }
            if (IsFrozen)
                throw new InvalidOperationException("동결된 기록에는 선수를 추가할 수 없습니다.");
            var created = new PlayerCompetitionStatisticsState(playerId, playerName, teamId, position);
            _players.Add(playerId, created);
            return created;
        }

        public PlayerCompetitionStatisticsState GetPlayer(int playerId)
        {
            return _players.TryGetValue(playerId, out PlayerCompetitionStatisticsState value) ? value : null;
        }

        public void Freeze() => IsFrozen = true;

        internal void RecordTeamGame(int teamId)
        {
            if (IsFrozen) throw new InvalidOperationException("동결된 기록은 변경할 수 없습니다.");
            foreach (PlayerCompetitionStatisticsState player in _players.Values)
            {
                if (player.TeamId == teamId) player.RecordTeamGame();
            }
        }

        internal void Add(PlayerGameStatistics game)
        {
            if (IsFrozen) throw new InvalidOperationException("동결된 기록은 변경할 수 없습니다.");
            GetOrCreate(game.PlayerId, game.PlayerName, game.TeamId, game.PrimaryPosition).Add(game);
        }
    }

    /// <summary>현재 시즌 리그 전체 선수 기록을 경쟁 범위별로 분리해 소유한다.</summary>
    public sealed class LeagueSeasonStatisticsState
    {
        public const int CurrentSchemaVersion = 2;

        public LeagueSeasonStatisticsState()
        {
            StatisticsSchemaVersion = CurrentSchemaVersion;
            RegularSeason = new CompetitionStatisticsState();
            Postseason = new CompetitionStatisticsState();
        }

        public int StatisticsSchemaVersion { get; }
        public CompetitionStatisticsState RegularSeason { get; }
        public CompetitionStatisticsState Postseason { get; }
        public CompetitionStatisticsState Get(CompetitionScope scope) =>
            scope == CompetitionScope.RegularSeason ? RegularSeason : Postseason;
        public void FreezeRegularSeasonStatistics() => RegularSeason.Freeze();
    }

    /// <summary>직접 진행과 즉시 시뮬레이션이 함께 사용하는 선수 한 경기 통계 DTO다.</summary>
    public sealed class PlayerGameStatistics
    {
        public PlayerGameStatistics(int playerId, string playerName, int teamId, PlayerPosition primaryPosition)
        {
            PlayerId = playerId;
            PlayerName = playerName ?? string.Empty;
            TeamId = teamId;
            PrimaryPosition = primaryPosition;
        }

        public int PlayerId { get; }
        public string PlayerName { get; }
        public int TeamId { get; }
        public PlayerPosition PrimaryPosition { get; }
        public bool HasBattingLine { get; internal set; }
        public bool StartedBatting { get; internal set; }
        public int PlateAppearances { get; internal set; }
        public int AtBats { get; internal set; }
        public int Runs { get; internal set; }
        public int Hits { get; internal set; }
        public int Doubles { get; internal set; }
        public int Triples { get; internal set; }
        public int HomeRuns { get; internal set; }
        public int RunsBattedIn { get; internal set; }
        public int Walks { get; internal set; }
        public int HitByPitches { get; internal set; }
        public int BattingStrikeouts { get; internal set; }
        public int StolenBases { get; internal set; }
        public int CaughtStealing { get; internal set; }
        public int SacrificeFlies { get; internal set; }
        public int GroundedIntoDoublePlays { get; internal set; }
        public bool HasPitchingLine { get; internal set; }
        public bool StartedPitching { get; internal set; }
        public int OutsRecorded { get; internal set; }
        public int Wins { get; internal set; }
        public int Losses { get; internal set; }
        public int Saves { get; internal set; }
        public int Holds { get; internal set; }
        public int BlownSaves { get; internal set; }
        public int HitsAllowed { get; internal set; }
        public int HomeRunsAllowed { get; internal set; }
        public int WalksAllowed { get; internal set; }
        public int HitBatters { get; internal set; }
        public int PitchingStrikeouts { get; internal set; }
        public int RunsAllowed { get; internal set; }
        public int EarnedRuns { get; internal set; }
        public int BattersFaced { get; internal set; }
        public int QualityStarts { get; internal set; }
        public PlayerFieldingLine FieldingLine { get; internal set; }
        public PlayerGameContributionState Contribution { get; internal set; }
    }

    /// <summary>경기 출처와 무관하게 통계 누적 서비스가 소비하는 공통 경기 결과다.</summary>
    public sealed class CareerGameResult
    {
        public CareerGameResult(
            int gameId,
            int homeTeamId,
            int awayTeamId,
            int homeScore,
            int awayScore,
            CompetitionScope scope,
            IReadOnlyList<PlayerGameStatistics> playerStatistics)
        {
            GameId = gameId;
            HomeTeamId = homeTeamId;
            AwayTeamId = awayTeamId;
            HomeScore = homeScore;
            AwayScore = awayScore;
            Scope = scope;
            PlayerStatistics = playerStatistics;
        }

        public int GameId { get; }
        public int HomeTeamId { get; }
        public int AwayTeamId { get; }
        public int HomeScore { get; }
        public int AwayScore { get; }
        public CompetitionScope Scope { get; }
        public IReadOnlyList<PlayerGameStatistics> PlayerStatistics { get; }
    }
}
