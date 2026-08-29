using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 최근 경기 표시와 재현 가능한 커리어 기록에 필요한 내 선수 한 경기 로그다.
    /// </summary>
    public readonly struct PlayerGameLogState
    {
        public PlayerGameLogState(
            int gameId,
            int opponentTeamId,
            bool isHome,
            bool didWin,
            int teamRuns,
            int opponentRuns,
            PlayerGameRole role,
            int atBats,
            int hits,
            int homeRuns,
            int runsBattedIn,
            int walks,
            int hitByPitches,
            int outsRecorded,
            int earnedRuns,
            int strikeouts,
            int walksAllowed,
            int hitBatters)
        {
            GameId = gameId;
            OpponentTeamId = opponentTeamId;
            IsHome = isHome;
            DidWin = didWin;
            TeamRuns = teamRuns;
            OpponentRuns = opponentRuns;
            Role = role;
            AtBats = atBats;
            Hits = hits;
            HomeRuns = homeRuns;
            RunsBattedIn = runsBattedIn;
            Walks = walks;
            HitByPitches = hitByPitches;
            OutsRecorded = outsRecorded;
            EarnedRuns = earnedRuns;
            Strikeouts = strikeouts;
            WalksAllowed = walksAllowed;
            HitBatters = hitBatters;
        }

        public int GameId { get; }
        public int OpponentTeamId { get; }
        public bool IsHome { get; }
        public bool DidWin { get; }
        public int TeamRuns { get; }
        public int OpponentRuns { get; }
        public PlayerGameRole Role { get; }
        public int AtBats { get; }
        public int Hits { get; }
        public int HomeRuns { get; }
        public int RunsBattedIn { get; }
        /// <summary>타자로서 얻은 볼넷이다.</summary>
        public int Walks { get; }
        /// <summary>타자로서 맞은 사구다.</summary>
        public int HitByPitches { get; }
        public int OutsRecorded { get; }
        public int EarnedRuns { get; }
        public int Strikeouts { get; }
        /// <summary>투수로서 허용한 볼넷이다.</summary>
        public int WalksAllowed { get; }
        /// <summary>투수로서 맞힌 사구다.</summary>
        public int HitBatters { get; }
    }

    /// <summary>
    /// 화면 재계산 없이 읽을 수 있도록 내 선수의 시즌 타격·투구 기록을 누적한다.
    /// </summary>
    public sealed class PlayerSeasonStatisticsState
    {
        private const int RecentGameCapacity = 5;
        private readonly List<PlayerGameLogState> _recentGames = new(RecentGameCapacity);
        private PlayerCompetitionStatisticsState _statistics;

        public PlayerSeasonStatisticsState()
        {
            _statistics = new PlayerCompetitionStatisticsState(
                playerId: 0,
                playerName: string.Empty,
                teamId: 0,
                Baseball.Core.Players.PlayerPosition.Unknown);
        }

        public int TeamGames => _statistics.TeamGames;
        public int GamesPlayed => _statistics.GamesPlayed;
        public int GamesStarted => _statistics.Batting.GamesStarted + _statistics.Pitching.Starts;
        public int PlateAppearances => _statistics.Batting.PlateAppearances;
        public int AtBats => _statistics.Batting.AtBats;
        public int Runs => _statistics.Batting.Runs;
        public int Hits => _statistics.Batting.Hits;
        public int Singles => _statistics.Batting.Singles;
        public int Doubles => _statistics.Batting.Doubles;
        public int Triples => _statistics.Batting.Triples;
        public int HomeRuns => _statistics.Batting.HomeRuns;
        public int RunsBattedIn => _statistics.Batting.RunsBattedIn;
        public int Walks => _statistics.Batting.Walks;
        public int HitByPitches => _statistics.Batting.HitByPitches;
        public int BattingStrikeouts => _statistics.Batting.Strikeouts;
        public int SacrificeFlies => _statistics.Batting.SacrificeFlies;
        public int SacrificeBunts => _statistics.Batting.SacrificeBunts;
        public int IntentionalWalks => _statistics.Batting.IntentionalWalks;
        public int ReachedOnErrors => _statistics.Batting.ReachedOnErrors;
        public int GroundedIntoDoublePlays => _statistics.Batting.GroundedIntoDoublePlays;
        public int TotalBases => _statistics.Batting.TotalBases;
        public int StolenBases => _statistics.Batting.StolenBases;
        public int CaughtStealing => _statistics.Batting.CaughtStealing;
        public int FieldingErrors
        {
            get
            {
                int errors = 0;
                foreach (FieldingStatisticsState fielding in _statistics.FieldingByPosition.Values)
                    errors += fielding.Errors;
                return errors;
            }
        }
        public int PitchingAppearances => _statistics.Pitching.Appearances;
        public int PitchingStarts => _statistics.Pitching.Starts;
        public int OutsRecorded => _statistics.Pitching.OutsRecorded;
        public int PitchesThrown => _statistics.Pitching.PitchesThrown;
        public int Wins => _statistics.Pitching.Wins;
        public int Losses => _statistics.Pitching.Losses;
        public int Saves => _statistics.Pitching.Saves;
        public int Holds => _statistics.Pitching.Holds;
        public int BlownSaves => _statistics.Pitching.BlownSaves;
        public int HitsAllowed => _statistics.Pitching.HitsAllowed;
        public int HomeRunsAllowed => _statistics.Pitching.HomeRunsAllowed;
        public int RunsAllowed => _statistics.Pitching.RunsAllowed;
        public int EarnedRuns => _statistics.Pitching.EarnedRuns;
        public int WalksAllowed => _statistics.Pitching.WalksAllowed;
        public int HitBatters => _statistics.Pitching.HitBatters;
        public int PitchingStrikeouts => _statistics.Pitching.Strikeouts;
        public int BattersFaced => _statistics.Pitching.BattersFaced;
        public int InheritedRunners => _statistics.Pitching.InheritedRunners;
        public int InheritedRunnersScored => _statistics.Pitching.InheritedRunnersScored;
        public int QualityStarts => _statistics.Pitching.QualityStarts;
        public IReadOnlyList<PlayerGameLogState> RecentGames => _recentGames;
        public IReadOnlyDictionary<int, PlayerTeamStatisticsSplitState> TeamSplits => _statistics.TeamSplits;

        public double BattingAverage => _statistics.Batting.BattingAverage;
        public double OnBasePercentage => _statistics.Batting.OnBasePercentage;
        public double SluggingPercentage => _statistics.Batting.SluggingPercentage;
        public double OnBasePlusSlugging => _statistics.Batting.OnBasePlusSlugging;
        public double WalkStrikeoutRatio => _statistics.Batting.WalkStrikeoutRatio;
        public double EarnedRunAverage => _statistics.Pitching.EarnedRunAverage;
        public double WalksHitsPerInningPitched => _statistics.Pitching.WalksHitsPerInningPitched;
        public double StrikeoutWalkRatio => _statistics.Pitching.StrikeoutWalkRatio;
        public double HomeRunsPerNineInnings => _statistics.Pitching.HomeRunsPerNineInnings;
        public double StolenBasePercentage => _statistics.Batting.StolenBasePercentage;

        /// <summary>시즌 이력에서도 포지션별 수비 원본을 동일하게 조회한다.</summary>
        public FieldingStatisticsState GetFielding(PlayerPosition position) => _statistics.GetFielding(position);

        /// <summary>트레이드 전후 특정 구단에서 누적한 시즌 분할 기록을 조회한다.</summary>
        public PlayerTeamStatisticsSplitState GetTeamSplit(int teamId) => _statistics.GetTeamSplit(teamId);

        /// <summary>리그 전체 통계의 내 선수 원본을 조회 대상으로 연결한다.</summary>
        public void BindTo(PlayerCompetitionStatisticsState statistics)
        {
            if (statistics == null)
                throw new ArgumentNullException(nameof(statistics));
            if (HasStatistics())
                throw new InvalidOperationException("기록이 생긴 파사드는 다른 원본에 다시 연결할 수 없습니다.");
            _statistics = statistics;
        }

        public void RecordTeamGame() => _statistics.RecordTeamGame();

        public void RecordBatting(
            bool started,
            int plateAppearances,
            int atBats,
            int runs,
            int hits,
            int doubles,
            int triples,
            int homeRuns,
            int runsBattedIn,
            int walks,
            int strikeouts)
        {
            _statistics.Add(new PlayerGameStatistics(0, string.Empty, 0,
                Baseball.Core.Players.PlayerPosition.Unknown)
            {
                HasBattingLine = true,
                StartedBatting = started,
                PlateAppearances = plateAppearances,
                AtBats = atBats,
                Runs = runs,
                Hits = hits,
                Doubles = doubles,
                Triples = triples,
                HomeRuns = homeRuns,
                RunsBattedIn = runsBattedIn,
                Walks = walks,
                BattingStrikeouts = strikeouts
            });
        }

        public void RecordPitching(
            bool started,
            int outsRecorded,
            int hitsAllowed,
            int earnedRuns,
            int walksAllowed,
            int strikeouts)
        {
            _statistics.Add(new PlayerGameStatistics(0, string.Empty, 0,
                Baseball.Core.Players.PlayerPosition.Unknown)
            {
                HasPitchingLine = true,
                StartedPitching = started,
                OutsRecorded = outsRecorded,
                HitsAllowed = hitsAllowed,
                EarnedRuns = earnedRuns,
                WalksAllowed = walksAllowed,
                PitchingStrikeouts = strikeouts
            });
        }

        public void AddGameLog(PlayerGameLogState gameLog)
        {
            if (_recentGames.Count == RecentGameCapacity)
                _recentGames.RemoveAt(0);
            _recentGames.Add(gameLog);
        }

        private bool HasStatistics()
        {
            return TeamGames > 0 || GamesPlayed > 0 || _recentGames.Count > 0;
        }
    }
}
