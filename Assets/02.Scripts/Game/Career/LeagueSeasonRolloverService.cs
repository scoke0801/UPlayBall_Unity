using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Rules;
using Baseball.Simulation.Career;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>모든 리그가 같은 로스터 회귀·일정 생성 규칙으로 다음 정규시즌을 만들게 한다.</summary>
    public sealed class LeagueSeasonRolloverService
    {
        private const ulong ScheduleStream = 0x5343484544554C45UL;

        private readonly BalanceTable _balance;
        private readonly PlayerValueEvaluator _playerValueEvaluator;
        private readonly SkillBoardService _skillBoardService;

        public LeagueSeasonRolloverService(BalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _playerValueEvaluator = new PlayerValueEvaluator(balance.PlayerEvaluation);
            _skillBoardService = new SkillBoardService(balance.Growth.SkillBoard, balance.Growth.SkillBlocks);
        }

        /// <summary>글로벌 선수 성장 상태를 다음 시즌 구단 로스터 스냅샷으로 투영한다.</summary>
        public TeamState[] AdvanceRosters(
            LeagueState league,
            WorldState world,
            int nextSeasonId)
        {
            if (league == null) throw new ArgumentNullException(nameof(league));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (nextSeasonId != league.CurrentSeason.SeasonId + 1)
                throw new ArgumentOutOfRangeException(nameof(nextSeasonId));
            IReadOnlyList<TeamState> teams = league.Teams;
            var result = new TeamState[teams.Count];
            for (int index = 0; index < teams.Count; index++)
            {
                TeamState team = teams[index];
                var nextRoster = new RosterCompetitorState[team.RosterCompetitors.Count];
                for (int rosterIndex = 0; rosterIndex < nextRoster.Length; rosterIndex++)
                {
                    RosterCompetitorState competitor = team.RosterCompetitors[rosterIndex];
                    PlayerState player = world.GetPlayer(competitor.PlayerId);
                    nextRoster[rosterIndex] = new RosterCompetitorState(
                        player.PlayerId,
                        player.Name,
                        player.PrimaryPosition,
                        _playerValueEvaluator.CalculatePositionValue(player.ToRosterPlayer(_skillBoardService)),
                        player.CareerPlateAppearances,
                        player.CareerPitchingOuts,
                        player.RegisteredSeasons);
                }
                result[index] = team.WithRoster(nextRoster);
            }
            return result;
        }

        public SeasonState BuildNextRegularSeason(
            LeagueState league,
            IReadOnlyList<TeamState> teams,
            int nextSeasonId,
            int nextYear,
            PlayerState myPlayer = null,
            int careerPlateAppearances = 0,
            int careerPitchingOuts = 0,
            int registeredSeasons = 0)
        {
            if (league == null) throw new ArgumentNullException(nameof(league));
            if (teams == null) throw new ArgumentNullException(nameof(teams));
            var season = new SeasonState(
                NewGameFlow.CurrentSaveVersion,
                nextSeasonId,
                nextYear,
                league.LeagueLevel,
                SimulationVersionStamp.CreateCurrent(_balance.Version, _balance.ContentHash));
            var teamIds = new int[teams.Count];
            var teamRecords = new TeamSeasonRecordState[teams.Count];
            for (int index = 0; index < teams.Count; index++)
            {
                teamIds[index] = teams[index].TeamId;
                teamRecords[index] = new TeamSeasonRecordState(
                    teams[index].TeamId,
                    DeterministicSeed.Derive(
                        league.RandomSeed,
                        0x544945425245414BUL ^
                        ((ulong)(uint)nextSeasonId << 32) ^
                        (uint)teams[index].TeamId));
            }

            ulong scheduleSeed = DeterministicSeed.Derive(
                league.RandomSeed,
                ScheduleStream ^ (uint)nextSeasonId);
            ScheduledGameDefinition[] definitions = new SeasonScheduleGenerator(
                    new Pcg32Random(scheduleSeed))
                .Generate(teamIds, _balance.CareerSeason.RegularSeasonGamesPerTeam);
            var games = new ScheduledGameState[definitions.Length];
            for (int index = 0; index < definitions.Length; index++)
            {
                ScheduledGameDefinition definition = definitions[index];
                ulong streamId = ((ulong)nextSeasonId << 32) | (uint)definition.GameId;
                games[index] = new ScheduledGameState(
                    definition.GameId,
                    definition.Round,
                    DeterministicSeed.Derive(league.RandomSeed, streamId),
                    definition.AwayTeamId,
                    definition.HomeTeamId);
            }

            season.StartRegularSeason(
                new SeasonScheduleState(games),
                teamRecords,
                new PlayerSeasonStatisticsState(),
                myPlayer,
                teams);
            if (myPlayer == null)
            {
                season.SnapshotRookieEligibility(teams, _balance.SeasonAwards);
            }
            else
            {
                season.SnapshotRookieEligibility(
                    teams,
                    myPlayer,
                    _balance.SeasonAwards,
                    careerPlateAppearances,
                    careerPitchingOuts,
                    registeredSeasons);
            }
            return season;
        }

    }
}
