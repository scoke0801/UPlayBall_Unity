using System;
using System.Collections.Generic;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>완료된 시즌의 최종 순위 한 행을 변경 불가능한 값으로 보관한다.</summary>
    public readonly struct TeamSeasonSummaryState
    {
        public TeamSeasonSummaryState(
            int rank,
            int teamId,
            int wins,
            int losses,
            int runsScored,
            int runsAllowed)
        {
            Rank = rank;
            TeamId = teamId;
            Wins = wins;
            Losses = losses;
            RunsScored = runsScored;
            RunsAllowed = runsAllowed;
        }

        public int Rank { get; }
        public int TeamId { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int RunsScored { get; }
        public int RunsAllowed { get; }
    }

    /// <summary>리그가 시즌 교체 뒤에도 보존할 우승·순위·수상 요약이다.</summary>
    public sealed class LeagueSeasonSummaryState
    {
        public LeagueSeasonSummaryState(
            LeagueId leagueId,
            int seasonId,
            int year,
            int championTeamId,
            int runnerUpTeamId,
            IReadOnlyList<TeamSeasonSummaryState> standings,
            SeasonAwardsState awards)
        {
            LeagueId = leagueId;
            SeasonId = seasonId;
            Year = year;
            ChampionTeamId = championTeamId;
            RunnerUpTeamId = runnerUpTeamId;
            Standings = standings ?? throw new ArgumentNullException(nameof(standings));
            Awards = awards ?? new SeasonAwardsState();
        }

        public LeagueId LeagueId { get; }
        public int SeasonId { get; }
        public int Year { get; }
        public int ChampionTeamId { get; }
        public int RunnerUpTeamId { get; }
        public IReadOnlyList<TeamSeasonSummaryState> Standings { get; }
        public SeasonAwardsState Awards { get; }

        public static LeagueSeasonSummaryState Create(LeagueState league)
        {
            if (league == null) throw new ArgumentNullException(nameof(league));
            SeasonState season = league.CurrentSeason ??
                                 throw new InvalidOperationException("보관할 현재 시즌이 없습니다.");
            if (season.Phase != SeasonPhase.Completed)
                throw new InvalidOperationException("완료된 시즌만 리그 역사에 보관할 수 있습니다.");

            TeamStandingEntry[] entries = BuildStandingEntries(season);
            int[] orderedTeamIds = PostseasonBracket.SelectSeeds(entries, entries.Length);
            var standings = new TeamSeasonSummaryState[orderedTeamIds.Length];
            for (int rank = 0; rank < orderedTeamIds.Length; rank++)
            {
                TeamSeasonRecordState record = season.GetTeamRecord(orderedTeamIds[rank]);
                standings[rank] = new TeamSeasonSummaryState(
                    rank + 1,
                    record.TeamId,
                    record.Wins,
                    record.Losses,
                    record.RunsScored,
                    record.RunsAllowed);
            }

            return new LeagueSeasonSummaryState(
                league.LeagueId,
                season.SeasonId,
                season.Year,
                season.Postseason?.ChampionTeamId ?? 0,
                season.Postseason?.RunnerUpTeamId ?? 0,
                standings,
                season.Awards);
        }

        private static TeamStandingEntry[] BuildStandingEntries(SeasonState season)
        {
            var result = new TeamStandingEntry[season.TeamRecords.Count];
            for (int index = 0; index < result.Length; index++)
            {
                TeamSeasonRecordState record = season.TeamRecords[index];
                result[index] = new TeamStandingEntry(
                    record.TeamId,
                    record.Wins,
                    record.Losses,
                    record.RunsScored,
                    record.RunsAllowed,
                    record.FixedTiebreaker,
                    record.GetHeadToHeadEntries());
            }
            return result;
        }
    }
}
