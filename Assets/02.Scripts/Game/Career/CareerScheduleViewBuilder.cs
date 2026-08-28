using System;
using System.Collections.Generic;
using Baseball.Core.Balance;

namespace Baseball.Game.Career
{
    /// <summary>세이브 상태의 시즌 일정을 달력 화면 전용 읽기 모델로 변환한다.</summary>
    public sealed class CareerScheduleViewBuilder
    {
        private const int RecentGameCount = 4;
        private const int UpcomingGameCount = 5;

        private readonly CareerState _career;
        private readonly CareerSeasonBalance _balance;

        public CareerScheduleViewBuilder(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));
            _balance = balance.CareerSeason;
        }

        public CareerScheduleView Build()
        {
            SeasonState season = _career.CurrentLeague.CurrentSeason ??
                                 throw new InvalidOperationException("현재 시즌이 없습니다.");
            IReadOnlyList<ScheduledGameState> sourceGames = season.Schedule?.Games ??
                                                            throw new InvalidOperationException("시즌 일정이 없습니다.");
            int playerTeamId = _career.MyPlayer.CurrentTeamId;
            TeamState playerTeam = GetTeam(playerTeamId);
            var games = new CareerScheduleGameView[sourceGames.Count];
            for (int index = 0; index < sourceGames.Count; index++)
                games[index] = BuildGame(sourceGames[index], season.Year, playerTeamId);

            DateTime seasonStart = games[0].Date;
            DateTime seasonEnd = games[0].Date;
            DateTime currentDate = games[0].Date;
            bool foundNextPlayerGame = false;
            DateTime lastPlayerGameDate = games[0].Date;
            for (int index = 0; index < games.Length; index++)
            {
                CareerScheduleGameView game = games[index];
                if (game.Date < seasonStart) seasonStart = game.Date;
                if (game.Date > seasonEnd) seasonEnd = game.Date;
                if (!game.IsPlayerGame)
                    continue;
                lastPlayerGameDate = game.Date;
                if (!foundNextPlayerGame && !game.IsCompleted)
                {
                    currentDate = game.Date;
                    foundNextPlayerGame = true;
                }
            }
            if (!foundNextPlayerGame)
                currentDate = lastPlayerGameDate;

            TeamSeasonRecordState record = season.GetTeamRecord(playerTeamId);
            return new CareerScheduleView(
                season.Year,
                season.LeagueLevel,
                season.Phase,
                _career.AvailableMoney,
                playerTeamId,
                playerTeam.Name,
                playerTeam.PrimaryColor,
                CalculateRank(record, season.TeamRecords),
                record?.Wins ?? 0,
                record?.Losses ?? 0,
                record?.Ties ?? 0,
                seasonStart,
                seasonEnd,
                currentDate,
                games,
                BuildRecentGames(games),
                BuildUpcomingGames(games));
        }

        private CareerScheduleGameView BuildGame(ScheduledGameState game, int year, int playerTeamId)
        {
            TeamState away = GetTeam(game.AwayTeamId);
            TeamState home = GetTeam(game.HomeTeamId);
            return new CareerScheduleGameView(
                game.GameId,
                game.Round,
                SeasonDateCalculator.GetGameDate(year, game.Round, _balance),
                away.TeamId,
                away.Name,
                away.PrimaryColor,
                home.TeamId,
                home.Name,
                home.PrimaryColor,
                game.IsCompleted,
                game.AwayRuns,
                game.HomeRuns,
                playerTeamId);
        }

        private TeamState GetTeam(int teamId)
        {
            IReadOnlyList<TeamState> teams = _career.CurrentLeague.Teams;
            for (int index = 0; index < teams.Count; index++)
            {
                if (teams[index].TeamId == teamId)
                    return teams[index];
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }

        private static int CalculateRank(
            TeamSeasonRecordState playerRecord,
            IReadOnlyList<TeamSeasonRecordState> records)
        {
            if (playerRecord == null || records == null)
                return 1;

            int rank = 1;
            for (int index = 0; index < records.Count; index++)
            {
                TeamSeasonRecordState other = records[index];
                if (other.TeamId == playerRecord.TeamId)
                    continue;
                if (other.WinningPercentage > playerRecord.WinningPercentage ||
                    Math.Abs(other.WinningPercentage - playerRecord.WinningPercentage) < 0.000001d &&
                    other.Wins > playerRecord.Wins)
                {
                    rank++;
                }
            }
            return rank;
        }

        private static CareerScheduleGameView[] BuildRecentGames(CareerScheduleGameView[] games)
        {
            int count = 0;
            for (int index = games.Length - 1; index >= 0 && count < RecentGameCount; index--)
            {
                if (games[index].IsPlayerGame && games[index].IsCompleted)
                    count++;
            }

            var result = new CareerScheduleGameView[count];
            int resultIndex = 0;
            for (int index = games.Length - 1; index >= 0 && resultIndex < count; index--)
            {
                if (games[index].IsPlayerGame && games[index].IsCompleted)
                    result[resultIndex++] = games[index];
            }
            return result;
        }

        private static CareerScheduleGameView[] BuildUpcomingGames(CareerScheduleGameView[] games)
        {
            int count = 0;
            for (int index = 0; index < games.Length && count < UpcomingGameCount; index++)
            {
                if (games[index].IsPlayerGame && !games[index].IsCompleted)
                    count++;
            }

            var result = new CareerScheduleGameView[count];
            int resultIndex = 0;
            for (int index = 0; index < games.Length && resultIndex < count; index++)
            {
                if (games[index].IsPlayerGame && !games[index].IsCompleted)
                    result[resultIndex++] = games[index];
            }
            return result;
        }
    }
}
