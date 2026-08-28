using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// 한 타자의 경기 기록을 누적한다.
    /// </summary>
    public sealed class PlayerBattingLine
    {
        internal PlayerBattingLine(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }
        public int PlateAppearances { get; internal set; }
        public int AtBats { get; internal set; }
        public int Runs { get; internal set; }
        public int Hits { get; internal set; }
        public int Doubles { get; internal set; }
        public int Triples { get; internal set; }
        public int HomeRuns { get; internal set; }
        public int RunsBattedIn { get; internal set; }
        public int Walks { get; internal set; }
        public int Strikeouts { get; internal set; }
        public int SacrificeFlies { get; internal set; }
        public int GroundedIntoDoublePlays { get; internal set; }
    }

    /// <summary>
    /// 한 투수의 경기 기록을 누적한다.
    /// </summary>
    public sealed class PlayerPitchingLine
    {
        internal PlayerPitchingLine(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }
        public int BattersFaced { get; internal set; }
        public int OutsRecorded { get; internal set; }
        public int PitchesThrown { get; internal set; }
        public int HitsAllowed { get; internal set; }
        public int RunsAllowed { get; internal set; }
        public int EarnedRuns { get; internal set; }
        public int WalksAllowed { get; internal set; }
        public int Strikeouts { get; internal set; }
        public int HomeRunsAllowed { get; internal set; }
    }

    /// <summary>
    /// 한 경기에서 야수가 실제로 관여한 수비 기회와 기대 대비 결과를 누적한다.
    /// </summary>
    public sealed class PlayerFieldingLine
    {
        internal PlayerFieldingLine(int playerId, PlayerPosition position)
        {
            PlayerId = playerId;
            Position = position;
        }

        public int PlayerId { get; }
        public PlayerPosition Position { get; }
        public int DefensiveOuts { get; internal set; }
        public int Opportunities { get; internal set; }
        public int SuccessfulPlays { get; internal set; }
        public int Putouts { get; internal set; }
        public int Assists { get; internal set; }
        public int Errors { get; internal set; }
        public int DoublePlays { get; internal set; }
        public int DifficultPlayAttempts { get; internal set; }
        public int DifficultPlaysMade { get; internal set; }
        public double ExpectedOuts { get; internal set; }
        public double EstimatedRunsSaved { get; internal set; }
    }

    /// <summary>
    /// 한 팀의 이닝별 득점과 선수별 경기 기록을 묶는다.
    /// </summary>
    public sealed class TeamBoxScore
    {
        internal TeamBoxScore(
            int teamId,
            int runs,
            int hits,
            int[] runsByInning,
            PlayerBattingLine[] battingLines,
            PlayerPitchingLine[] pitchingLines,
            PlayerFieldingLine[] fieldingLines)
        {
            TeamId = teamId;
            Runs = runs;
            Hits = hits;
            RunsByInning = runsByInning;
            BattingLines = battingLines;
            PitchingLines = pitchingLines;
            FieldingLines = fieldingLines;
            PitchingLine = pitchingLines[0];
        }

        public int TeamId { get; }
        public int Runs { get; }
        public int Hits { get; }
        public IReadOnlyList<int> RunsByInning { get; }
        public IReadOnlyList<PlayerBattingLine> BattingLines { get; }
        public IReadOnlyList<PlayerPitchingLine> PitchingLines { get; }
        public IReadOnlyList<PlayerFieldingLine> FieldingLines { get; }
        /// <summary>기존 호출부 호환을 위해 선발투수 기록을 반환한다.</summary>
        public PlayerPitchingLine PitchingLine { get; }
    }

    internal sealed class TeamBoxScoreBuilder
    {
        private readonly int[] _runsByInning;

        public TeamBoxScoreBuilder(Team team, int maximumInnings)
        {
            Team = team;
            _runsByInning = new int[maximumInnings];
            int substituteCount = team.PositionPlayerSubstitution == null ? 0 : 1;
            BattingLines = new PlayerBattingLine[team.Lineup.Count + substituteCount];
            for (int index = 0; index < team.Lineup.Count; index++)
                BattingLines[index] = new PlayerBattingLine(team.Lineup[index].Player.PlayerId);
            if (team.PositionPlayerSubstitution != null)
            {
                BattingLines[team.Lineup.Count] = new PlayerBattingLine(
                    team.PositionPlayerSubstitution.Player.PlayerId);
            }

            PitchingLines = team.ReliefPitcher == null
                ? new[] { new PlayerPitchingLine(team.StartingPitcher.PlayerId) }
                : new[]
                {
                    new PlayerPitchingLine(team.StartingPitcher.PlayerId),
                    new PlayerPitchingLine(team.ReliefPitcher.PlayerId)
                };

            bool hasSubstituteFielder = team.PositionPlayerSubstitution != null &&
                                        team.PositionPlayerSubstitution.Player.PrimaryPosition !=
                                        PlayerPosition.DesignatedHitter;
            FieldingLines = new PlayerFieldingLine[
                team.Lineup.Count - 1 + PitchingLines.Length + (hasSubstituteFielder ? 1 : 0)];
            int fieldingIndex = 0;
            for (int index = 0; index < team.Lineup.Count; index++)
            {
                LineupSlot slot = team.Lineup[index];
                if (slot.FieldingPosition == PlayerPosition.DesignatedHitter)
                    continue;
                FieldingLines[fieldingIndex++] = new PlayerFieldingLine(
                    slot.Player.PlayerId,
                    slot.FieldingPosition);
            }
            FieldingLines[fieldingIndex++] = new PlayerFieldingLine(
                team.StartingPitcher.PlayerId,
                PlayerPosition.StartingPitcher);
            if (team.ReliefPitcher != null)
            {
                FieldingLines[fieldingIndex++] = new PlayerFieldingLine(
                    team.ReliefPitcher.PlayerId,
                    PlayerPosition.ReliefPitcher);
            }
            if (hasSubstituteFielder)
            {
                FieldingLines[fieldingIndex] = new PlayerFieldingLine(
                    team.PositionPlayerSubstitution.Player.PlayerId,
                    team.PositionPlayerSubstitution.Player.PrimaryPosition);
            }
        }

        public Team Team { get; }
        public PlayerBattingLine[] BattingLines { get; }
        public PlayerPitchingLine PitchingLine => PitchingLines[0];
        public PlayerPitchingLine[] PitchingLines { get; }
        public PlayerFieldingLine[] FieldingLines { get; }
        public int Runs { get; private set; }
        public int Hits { get; set; }

        public void AddRun(int inning)
        {
            Runs++;
            _runsByInning[inning - 1]++;
        }

        public TeamBoxScore Build(int inningsPlayed)
        {
            var finalRunsByInning = new int[inningsPlayed];
            Array.Copy(_runsByInning, finalRunsByInning, inningsPlayed);
            return new TeamBoxScore(
                Team.TeamId,
                Runs,
                Hits,
                finalRunsByInning,
                BattingLines,
                PitchingLines,
                FieldingLines);
        }

        public PlayerFieldingLine GetFieldingLine(PlayerPosition position)
        {
            for (int index = 0; index < FieldingLines.Length; index++)
            {
                if (FieldingLines[index].Position == position)
                    return FieldingLines[index];
            }

            throw new InvalidOperationException($"{position} 수비수를 찾을 수 없습니다.");
        }

        public PlayerFieldingLine GetFieldingLineByPlayer(int playerId)
        {
            for (int index = 0; index < FieldingLines.Length; index++)
            {
                if (FieldingLines[index].PlayerId == playerId)
                    return FieldingLines[index];
            }
            throw new InvalidOperationException($"PlayerId {playerId} 수비수를 찾을 수 없습니다.");
        }

        /// <summary>
        /// 현재 등판 중인 투수의 누적 기록을 반환한다.
        /// </summary>
        public PlayerPitchingLine GetPitchingLine(int playerId)
        {
            for (int index = 0; index < PitchingLines.Length; index++)
            {
                if (PitchingLines[index].PlayerId == playerId)
                    return PitchingLines[index];
            }

            throw new InvalidOperationException("등록되지 않은 투수의 기록을 요청했습니다.");
        }
    }
}
