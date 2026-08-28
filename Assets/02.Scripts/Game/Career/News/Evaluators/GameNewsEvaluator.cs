using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career.News
{
    /// <summary>라운드 전체가 확정된 뒤 경기 결과·개인 활약·연승·기록·리그 브리핑 사건을 만든다.</summary>
    public sealed class GameNewsEvaluator
    {
        private readonly NewsTriggerDefinition _triggers;

        public GameNewsEvaluator(NewsTriggerDefinition triggers)
        {
            _triggers = triggers ?? throw new ArgumentNullException(nameof(triggers));
        }

        public IReadOnlyList<NewsEvent> EvaluateRegularSeasonRound(
            CareerState career,
            CareerGameAdvanceResult result,
            CareerDate occurredAt)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            SeasonState season = career.League.CurrentSeason;
            TeamState team = FindTeam(career, career.MyPlayer.CurrentTeamId);
            TeamState opponent = FindTeam(career, result.OpponentTeamId);
            TeamSeasonRecordState teamRecord = season.GetTeamRecord(team.TeamId);
            int winningStreak = result.TeamRuns > result.OpponentRuns
                ? CalculateStreak(season.Schedule, team.TeamId, didWin: true)
                : 0;
            int losingStreak = result.TeamRuns < result.OpponentRuns
                ? CalculateStreak(season.Schedule, team.TeamId, didWin: false)
                : 0;
            bool isWinningMilestone = Contains(_triggers.TeamStreakMilestones, winningStreak);
            bool isLosingMilestone = Contains(_triggers.TeamStreakMilestones, losingStreak);
            bool didAppear = DidAppear(result);
            bool isPitcher = career.MyPlayer.PrimaryPosition is
                PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            bool isNotable = IsNotablePerformance(result, isPitcher);
            string mergeKey = $"season_{season.SeasonId}_game_{result.GameId}";
            NewsFactSet commonFacts = BuildGameFacts(
                career,
                result,
                team,
                opponent,
                teamRecord,
                isPitcher,
                didAppear,
                isWinningMilestone ? winningStreak : 0,
                isLosingMilestone ? losingStreak : 0);

            var events = new List<NewsEvent>(5)
            {
                CreateGameEvent(career, result, occurredAt, team, opponent, mergeKey, didAppear, commonFacts)
            };
            if (isNotable)
                events.Add(CreatePerformanceEvent(career, result, occurredAt, team, opponent, mergeKey, commonFacts));
            if (isWinningMilestone || isLosingMilestone)
            {
                events.Add(CreateStreakEvent(
                    season,
                    result,
                    occurredAt,
                    team,
                    mergeKey,
                    winningStreak,
                    losingStreak,
                    commonFacts));
            }

            NewsEvent milestone = CreateMilestoneEvent(
                career,
                result,
                occurredAt,
                team,
                mergeKey,
                didAppear,
                commonFacts);
            if (milestone != null)
                events.Add(milestone);
            events.Add(CreateLeagueBriefing(career, result.Round, occurredAt));
            return events;
        }

        private static NewsEvent CreateGameEvent(
            CareerState career,
            CareerGameAdvanceResult result,
            CareerDate occurredAt,
            TeamState team,
            TeamState opponent,
            string mergeKey,
            bool didAppear,
            NewsFactSet commonFacts)
        {
            SeasonState season = career.League.CurrentSeason;
            var newsEvent = new NewsEvent(
                $"season_{season.SeasonId}_game_{result.GameId}_completed",
                NewsEventType.GameCompleted,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Team(team.TeamId, team.Name),
                mergeKey,
                baseImportance: 35)
            {
                GameImpact = result.TeamRuns == result.OpponentRuns ? 2 : 5
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(opponent.TeamId, opponent.Name));
            newsEvent.AddRelatedSubject(NewsSubject.Game(result.GameId));
            if (didAppear)
                newsEvent.AddRelatedSubject(NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name));
            newsEvent.FactSet.MergeFrom(commonFacts);
            return newsEvent;
        }

        private static NewsEvent CreatePerformanceEvent(
            CareerState career,
            CareerGameAdvanceResult result,
            CareerDate occurredAt,
            TeamState team,
            TeamState opponent,
            string mergeKey,
            NewsFactSet commonFacts)
        {
            var newsEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_game_{result.GameId}_player_{career.MyPlayer.PlayerId}_performance",
                NewsEventType.PlayerGamePerformance,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name),
                mergeKey,
                baseImportance: 30)
            {
                GameImpact = 10,
                Rarity = GetPerformanceRarity(result)
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.AddRelatedSubject(NewsSubject.Team(opponent.TeamId, opponent.Name));
            newsEvent.AddRelatedSubject(NewsSubject.Game(result.GameId));
            newsEvent.FactSet.MergeFrom(commonFacts);
            newsEvent.FactSet.SetBoolean(NewsFactKey.HasNotablePerformance, true);
            return newsEvent;
        }

        private static NewsEvent CreateStreakEvent(
            SeasonState season,
            CareerGameAdvanceResult result,
            CareerDate occurredAt,
            TeamState team,
            string mergeKey,
            int winningStreak,
            int losingStreak,
            NewsFactSet commonFacts)
        {
            int streak = winningStreak > 0 ? winningStreak : losingStreak;
            string kind = winningStreak > 0 ? "win" : "loss";
            var newsEvent = new NewsEvent(
                $"season_{season.SeasonId}_team_{team.TeamId}_{kind}_streak_{streak}",
                NewsEventType.TeamStreakReached,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Team(team.TeamId, team.Name),
                mergeKey,
                baseImportance: 18)
            {
                GameImpact = 8,
                Rarity = streak >= 8 ? 15 : streak >= 5 ? 10 : 5
            };
            newsEvent.AddRelatedSubject(NewsSubject.Game(result.GameId));
            newsEvent.FactSet.MergeFrom(commonFacts);
            return newsEvent;
        }

        private NewsEvent CreateMilestoneEvent(
            CareerState career,
            CareerGameAdvanceResult result,
            CareerDate occurredAt,
            TeamState team,
            string mergeKey,
            bool didAppear,
            NewsFactSet commonFacts)
        {
            PlayerSeasonStatisticsState statistics = career.League.CurrentSeason.PlayerStatistics;
            var milestones = new List<string>();
            if (didAppear && statistics.GamesPlayed == 1)
                milestones.Add("1군 데뷔");
            if (result.Hits > 0 && statistics.Hits == result.Hits)
                milestones.Add("프로 첫 안타");
            if (result.HomeRuns > 0 && statistics.HomeRuns == result.HomeRuns)
                milestones.Add("프로 첫 홈런");
            if (Contains(_triggers.HomeRunMilestones, statistics.HomeRuns) && result.HomeRuns > 0)
                milestones.Add($"시즌 {statistics.HomeRuns}호 홈런");
            if (milestones.Count == 0)
                return null;

            string milestoneText = string.Join(" · ", milestones);
            var newsEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_game_{result.GameId}_player_{career.MyPlayer.PlayerId}_milestones",
                NewsEventType.CareerMilestoneReached,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name),
                mergeKey,
                baseImportance: 35)
            {
                CareerImpact = 25,
                Rarity = 10,
                IsCareerArchive = true
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.AddRelatedSubject(NewsSubject.Game(result.GameId));
            newsEvent.FactSet.MergeFrom(commonFacts);
            newsEvent.FactSet.SetText(NewsFactKey.CareerMilestone, milestoneText);
            return newsEvent;
        }

        private static NewsEvent CreateLeagueBriefing(
            CareerState career,
            int round,
            CareerDate occurredAt)
        {
            SeasonState season = career.League.CurrentSeason;
            TeamSeasonRecordState leader = FindLeader(season.TeamRecords);
            TeamState leaderTeam = FindTeam(career, leader.TeamId);
            int roundGames = 0;
            for (int index = 0; index < season.Schedule.Games.Count; index++)
            {
                if (season.Schedule.Games[index].Round == round && season.Schedule.Games[index].IsCompleted)
                    roundGames++;
            }

            var newsEvent = new NewsEvent(
                $"season_{season.SeasonId}_round_{round}_league_briefing",
                NewsEventType.LeagueBriefing,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Team(leaderTeam.TeamId, leaderTeam.Name),
                $"season_{season.SeasonId}_round_{round}_league_briefing",
                baseImportance: 25);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, leaderTeam.Name);
            newsEvent.FactSet.SetInteger(NewsFactKey.TeamRank, 1);
            newsEvent.FactSet.SetInteger(NewsFactKey.RoundGames, roundGames);
            newsEvent.FactSet.SetText(
                NewsFactKey.TeamRecordSummary,
                $"{leader.Wins}승 {leader.Losses}패 {leader.Ties}무");
            return newsEvent;
        }

        private NewsFactSet BuildGameFacts(
            CareerState career,
            CareerGameAdvanceResult result,
            TeamState team,
            TeamState opponent,
            TeamSeasonRecordState teamRecord,
            bool isPitcher,
            bool didAppear,
            int winningStreak,
            int losingStreak)
        {
            PlayerSeasonStatisticsState season = career.League.CurrentSeason.PlayerStatistics;
            var facts = new NewsFactSet();
            facts.SetText(NewsFactKey.PlayerName, career.MyPlayer.Name);
            facts.SetText(NewsFactKey.TeamName, team.Name);
            facts.SetText(NewsFactKey.OpponentName, opponent.Name);
            facts.SetInteger(NewsFactKey.GameId, result.GameId);
            facts.SetInteger(NewsFactKey.TeamRuns, result.TeamRuns);
            facts.SetInteger(NewsFactKey.OpponentRuns, result.OpponentRuns);
            facts.SetBoolean(NewsFactKey.DidWin, result.TeamRuns > result.OpponentRuns);
            facts.SetBoolean(NewsFactKey.DidLose, result.TeamRuns < result.OpponentRuns);
            facts.SetBoolean(NewsFactKey.DidTie, result.TeamRuns == result.OpponentRuns);
            facts.SetBoolean(NewsFactKey.DidAppear, didAppear);
            facts.SetBoolean(NewsFactKey.IsPitcher, isPitcher);
            facts.SetInteger(NewsFactKey.GameAtBats, result.AtBats);
            facts.SetInteger(NewsFactKey.GameHits, result.Hits);
            facts.SetInteger(NewsFactKey.GameHomeRuns, result.HomeRuns);
            facts.SetInteger(NewsFactKey.GameRbi, result.RunsBattedIn);
            facts.SetInteger(NewsFactKey.GameStrikeouts, result.Strikeouts);
            facts.SetInteger(NewsFactKey.GameInningsOuts, result.OutsRecorded);
            facts.SetInteger(NewsFactKey.GameEarnedRuns, result.EarnedRuns);
            facts.SetText(NewsFactKey.GamePerformanceSummary, BuildPerformanceSummary(result, isPitcher));
            facts.SetText(NewsFactKey.GameStatLine, BuildStatLine(result, isPitcher, didAppear));
            facts.SetDecimal(NewsFactKey.SeasonBattingAverage, season.BattingAverage);
            facts.SetInteger(NewsFactKey.SeasonHomeRuns, season.HomeRuns);
            facts.SetInteger(NewsFactKey.SeasonHits, season.Hits);
            facts.SetInteger(NewsFactKey.SeasonRbi, season.RunsBattedIn);
            facts.SetDecimal(NewsFactKey.SeasonEra, season.EarnedRunAverage);
            facts.SetInteger(NewsFactKey.SeasonStrikeouts, season.PitchingStrikeouts);
            facts.SetInteger(NewsFactKey.TeamWinningStreak, winningStreak);
            facts.SetInteger(NewsFactKey.TeamLosingStreak, losingStreak);
            facts.SetInteger(NewsFactKey.TeamRank, CalculateRank(career.League.CurrentSeason, teamRecord));
            facts.SetInteger(NewsFactKey.TeamWins, teamRecord.Wins);
            facts.SetInteger(NewsFactKey.TeamLosses, teamRecord.Losses);
            facts.SetText(
                NewsFactKey.TeamRecordSummary,
                $"{teamRecord.Wins}승 {teamRecord.Losses}패 {teamRecord.Ties}무");
            return facts;
        }

        private bool IsNotablePerformance(CareerGameAdvanceResult result, bool isPitcher)
        {
            if (isPitcher)
            {
                return result.OutsRecorded >= _triggers.ScorelessPitchingOuts && result.EarnedRuns == 0 ||
                       result.Strikeouts >= _triggers.NotableStrikeouts;
            }
            return result.Hits >= _triggers.NotableHits ||
                   result.HomeRuns >= _triggers.NotableHomeRuns ||
                   result.RunsBattedIn >= _triggers.NotableRunsBattedIn;
        }

        private static bool DidAppear(CareerGameAdvanceResult result)
        {
            return result.Role is PlayerGameRole.StartingBatter or
                PlayerGameRole.StartingPitcher or
                PlayerGameRole.ReliefPitcher ||
                result.AtBats > 0 || result.OutsRecorded > 0;
        }

        private static int GetPerformanceRarity(CareerGameAdvanceResult result)
        {
            int rarity = 5;
            if (result.HomeRuns >= 2 || result.Strikeouts >= 10) rarity += 10;
            if (result.Hits >= 4 || result.RunsBattedIn >= 5 ||
                result.OutsRecorded >= 27 && result.EarnedRuns == 0)
            {
                rarity += 10;
            }
            return rarity;
        }

        private static string BuildPerformanceSummary(CareerGameAdvanceResult result, bool isPitcher)
        {
            if (isPitcher)
            {
                if (result.OutsRecorded >= 18 && result.EarnedRuns == 0)
                    return $"{FormatInnings(result.OutsRecorded)}이닝 무실점";
                return $"{result.Strikeouts}탈삼진";
            }

            var parts = new List<string>(3);
            if (result.Hits > 0) parts.Add($"{result.Hits}안타");
            if (result.HomeRuns > 0) parts.Add($"{result.HomeRuns}홈런");
            if (result.RunsBattedIn > 0) parts.Add($"{result.RunsBattedIn}타점");
            return parts.Count == 0 ? "출전" : string.Join(" ", parts);
        }

        private static string BuildStatLine(
            CareerGameAdvanceResult result,
            bool isPitcher,
            bool didAppear)
        {
            if (!didAppear)
                return "출장 없음";
            return isPitcher
                ? $"{FormatInnings(result.OutsRecorded)}이닝 {result.EarnedRuns}자책 {result.Strikeouts}탈삼진"
                : $"{result.AtBats}타수 {result.Hits}안타 {result.HomeRuns}홈런 {result.RunsBattedIn}타점";
        }

        private static string FormatInnings(int outs) => $"{outs / 3}.{outs % 3}";

        private static int CalculateStreak(SeasonScheduleState schedule, int teamId, bool didWin)
        {
            int streak = 0;
            for (int index = schedule.Games.Count - 1; index >= 0; index--)
            {
                ScheduledGameState game = schedule.Games[index];
                if (!game.IsCompleted || !game.IncludesTeam(teamId))
                    continue;
                int teamRuns = game.HomeTeamId == teamId ? game.HomeRuns : game.AwayRuns;
                int opponentRuns = game.HomeTeamId == teamId ? game.AwayRuns : game.HomeRuns;
                bool matches = didWin ? teamRuns > opponentRuns : teamRuns < opponentRuns;
                if (!matches)
                    break;
                streak++;
            }
            return streak;
        }

        private static int CalculateRank(SeasonState season, TeamSeasonRecordState target)
        {
            int rank = 1;
            for (int index = 0; index < season.TeamRecords.Count; index++)
            {
                TeamSeasonRecordState other = season.TeamRecords[index];
                if (other.TeamId != target.TeamId && IsAhead(other, target))
                    rank++;
            }
            return rank;
        }

        private static TeamSeasonRecordState FindLeader(IReadOnlyList<TeamSeasonRecordState> records)
        {
            TeamSeasonRecordState leader = records[0];
            for (int index = 1; index < records.Count; index++)
            {
                if (IsAhead(records[index], leader))
                    leader = records[index];
            }
            return leader;
        }

        private static bool IsAhead(TeamSeasonRecordState left, TeamSeasonRecordState right)
        {
            if (left.WinningPercentage > right.WinningPercentage) return true;
            if (left.WinningPercentage < right.WinningPercentage) return false;
            if (left.Wins != right.Wins) return left.Wins > right.Wins;
            return left.FixedTiebreaker < right.FixedTiebreaker;
        }

        private static TeamState FindTeam(CareerState career, int teamId)
        {
            for (int index = 0; index < career.League.Teams.Count; index++)
            {
                if (career.League.Teams[index].TeamId == teamId)
                    return career.League.Teams[index];
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }

        private static bool Contains(int[] values, int target)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == target)
                    return true;
            }
            return false;
        }
    }
}
