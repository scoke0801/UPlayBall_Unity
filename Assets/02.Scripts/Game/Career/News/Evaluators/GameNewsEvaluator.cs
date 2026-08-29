using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career.Narrative;

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
            CareerDate occurredAt,
            MatchNarrativeSnapshot narrative = null)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            SeasonState season = career.CurrentLeague.CurrentSeason;
            int resultTeamId = narrative?.TeamId ?? career.MyPlayer.CurrentTeamId;
            TeamState team = FindTeam(career, resultTeamId);
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
                isLosingMilestone ? losingStreak : 0,
                narrative);

            var events = new List<NewsEvent>(10)
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
            NewsEvent form = CreateFormEvent(career, result, occurredAt, team, narrative, commonFacts);
            if (form != null)
                events.Add(form);
            NewsEvent roleCompetition = CreateRoleCompetitionEvent(
                career,
                result,
                occurredAt,
                team,
                narrative,
                commonFacts);
            if (roleCompetition != null)
                events.Add(roleCompetition);
            NewsEvent approachingMilestone = CreateApproachingMilestoneEvent(
                career,
                result,
                occurredAt,
                team,
                commonFacts);
            if (approachingMilestone != null)
                events.Add(approachingMilestone);
            NewsEvent periodicReport = CreatePeriodicReport(career, result, occurredAt, team);
            if (periodicReport != null)
                events.Add(periodicReport);
            events.Add(CreateLeagueBriefing(career, result, occurredAt, narrative));
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
            SeasonState season = career.CurrentLeague.CurrentSeason;
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
            PlayerSeasonStatisticsState statistics = career.CurrentLeague.CurrentSeason.PlayerStatistics;
            int careerGamesBeforeSeason = SumCareer(career, CareerStat.GamesPlayed);
            int careerHitsBeforeSeason = SumCareer(career, CareerStat.Hits);
            int careerHomeRunsBeforeSeason = SumCareer(career, CareerStat.HomeRuns);
            int careerHits = careerHitsBeforeSeason + statistics.Hits;
            int careerHomeRuns = careerHomeRunsBeforeSeason + statistics.HomeRuns;
            var milestones = new List<string>();
            int achievedTarget = 0;
            string achievedName = string.Empty;
            if (didAppear && careerGamesBeforeSeason == 0 && statistics.GamesPlayed == 1)
                milestones.Add("1군 데뷔");
            if (result.Hits > 0 && careerHitsBeforeSeason == 0 && statistics.Hits == result.Hits)
                milestones.Add("프로 첫 안타");
            if (result.HomeRuns > 0 && careerHomeRunsBeforeSeason == 0 && statistics.HomeRuns == result.HomeRuns)
                milestones.Add("프로 첫 홈런");
            if (Contains(_triggers.HomeRunMilestones, statistics.HomeRuns) && result.HomeRuns > 0)
                milestones.Add($"시즌 {statistics.HomeRuns}호 홈런");
            if (Contains(_triggers.CareerHitMilestones, careerHits) && result.Hits > 0)
            {
                milestones.Add($"통산 {careerHits}안타");
                achievedTarget = careerHits;
                achievedName = "통산 안타";
            }
            if (Contains(_triggers.CareerHomeRunMilestones, careerHomeRuns) && result.HomeRuns > 0)
            {
                milestones.Add($"통산 {careerHomeRuns}홈런");
                achievedTarget = careerHomeRuns;
                achievedName = "통산 홈런";
            }
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
            if (achievedTarget > 0)
            {
                newsEvent.FactSet.SetText(NewsFactKey.MilestoneName, achievedName);
                newsEvent.FactSet.SetInteger(NewsFactKey.MilestoneTarget, achievedTarget);
            }
            return newsEvent;
        }

        private NewsEvent CreateFormEvent(
            CareerState career,
            CareerGameAdvanceResult result,
            CareerDate occurredAt,
            TeamState team,
            MatchNarrativeSnapshot narrative,
            NewsFactSet commonFacts)
        {
            if (narrative == null)
                return null;
            bool startsSlump = narrative.HitlessStreak == 3;
            bool endsSlump = narrative.HasTag(NarrativeTag.SlumpEnded);
            bool startsHot = narrative.HitStreak == _triggers.HotStreakStart ||
                             narrative.HitStreak > _triggers.HotStreakStart &&
                             Contains(_triggers.HittingStreakMilestones, narrative.HitStreak);
            bool coolsHot = result.Hits == 0 &&
                            HasActiveStoryline(
                                career.News,
                                NewsStorylineType.RisingForm,
                                career.MyPlayer.PlayerId);
            if (!startsSlump && !endsSlump && !startsHot && !coolsHot)
                return null;

            string form = startsSlump ? "slump" : endsSlump ? "rebound" : startsHot ? "hot" : "cooled";
            var newsEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_form_{form}_{result.GameId}",
                NewsEventType.PlayerFormChanged,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name),
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_form_{form}_{result.GameId}",
                baseImportance: 35)
            {
                CareerImpact = 8,
                GameImpact = endsSlump ? 8 : 4,
                Rarity = endsSlump ? Math.Min(15, narrative.PreviousHitlessStreak * 2) : startsHot ? 8 : 5
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.AddRelatedSubject(NewsSubject.Game(result.GameId));
            newsEvent.FactSet.MergeFrom(commonFacts);
            newsEvent.FactSet.SetBoolean(NewsFactKey.FormSlump, startsSlump);
            newsEvent.FactSet.SetBoolean(NewsFactKey.FormRebound, endsSlump);
            newsEvent.FactSet.SetBoolean(NewsFactKey.FormHot, startsHot);
            newsEvent.FactSet.SetBoolean(NewsFactKey.FormCooled, coolsHot);
            return newsEvent;
        }

        private NewsEvent CreateRoleCompetitionEvent(
            CareerState career,
            CareerGameAdvanceResult result,
            CareerDate occurredAt,
            TeamState team,
            MatchNarrativeSnapshot narrative,
            NewsFactSet commonFacts)
        {
            if (narrative == null || career.CurrentExpectedRole != ExpectedRole.StartingCompetition)
                return null;
            bool hasActiveCompetition = HasActiveStoryline(
                career.News,
                NewsStorylineType.RosterCompetition,
                career.MyPlayer.PlayerId);
            bool starts = result.ManagerEvaluationAfter <= _triggers.RoleCompetitionStartTrust &&
                          !hasActiveCompetition;
            bool resolves = result.ManagerEvaluationAfter >= _triggers.RoleCompetitionResolveTrust &&
                            hasActiveCompetition;
            if (!starts && !resolves)
                return null;

            var newsEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_role_competition_{(starts ? "start" : "resolve")}",
                NewsEventType.RoleCompetitionChanged,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name),
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_role_competition",
                baseImportance: starts ? 32 : 38)
            {
                CareerImpact = 15,
                GameImpact = 5
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.AddRelatedSubject(NewsSubject.Game(result.GameId));
            newsEvent.FactSet.MergeFrom(commonFacts);
            newsEvent.FactSet.SetBoolean(NewsFactKey.RoleCompetitionStarted, starts);
            newsEvent.FactSet.SetBoolean(NewsFactKey.RoleCompetitionResolved, resolves);
            return newsEvent;
        }

        private NewsEvent CreateApproachingMilestoneEvent(
            CareerState career,
            CareerGameAdvanceResult result,
            CareerDate occurredAt,
            TeamState team,
            NewsFactSet commonFacts)
        {
            PlayerSeasonStatisticsState statistics = career.CurrentLeague.CurrentSeason.PlayerStatistics;
            int careerHits = SumCareer(career, CareerStat.Hits) + statistics.Hits;
            int careerHomeRuns = SumCareer(career, CareerStat.HomeRuns) + statistics.HomeRuns;
            int hitTarget = FindApproachingTarget(
                _triggers.CareerHitMilestones,
                careerHits,
                _triggers.MilestoneApproachRange);
            int homeRunTarget = FindApproachingTarget(
                _triggers.CareerHomeRunMilestones,
                careerHomeRuns,
                _triggers.MilestoneApproachRange);
            if (hitTarget == 0 && homeRunTarget == 0)
                return null;

            bool useHomeRuns = hitTarget == 0 ||
                               homeRunTarget > 0 && homeRunTarget - careerHomeRuns < hitTarget - careerHits;
            int target = useHomeRuns ? homeRunTarget : hitTarget;
            string name = useHomeRuns ? "통산 홈런" : "통산 안타";
            var newsEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_{(useHomeRuns ? "hr" : "hit")}_chase_{target}",
                NewsEventType.CareerMilestoneApproaching,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name),
                $"career_player_{career.MyPlayer.PlayerId}_{(useHomeRuns ? "hr" : "hit")}_chase_{target}",
                baseImportance: 28)
            {
                CareerImpact = 12,
                Rarity = target >= 100 ? 10 : 5
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.AddRelatedSubject(NewsSubject.Game(result.GameId));
            newsEvent.FactSet.MergeFrom(commonFacts);
            newsEvent.FactSet.SetText(NewsFactKey.MilestoneName, name);
            newsEvent.FactSet.SetInteger(NewsFactKey.MilestoneTarget, target);
            return newsEvent;
        }

        private NewsEvent CreatePeriodicReport(
            CareerState career,
            CareerGameAdvanceResult result,
            CareerDate occurredAt,
            TeamState team)
        {
            int interval;
            NewsEventType type;
            string label;
            if (result.Round % _triggers.MonthlyReportInterval == 0)
            {
                interval = _triggers.MonthlyReportInterval;
                type = NewsEventType.MonthlyReport;
                label = "월간 리포트";
            }
            else if (result.Round % _triggers.WeeklyReportInterval == 0)
            {
                interval = _triggers.WeeklyReportInterval;
                type = NewsEventType.WeeklyReport;
                label = "주간 리포트";
            }
            else
            {
                return null;
            }

            int games = 0;
            int atBats = 0;
            int hits = 0;
            int homeRuns = 0;
            int rbi = 0;
            int wins = 0;
            IReadOnlyList<MatchNarrativeSnapshot> snapshots =
                career.CurrentLeague.CurrentSeason.MatchNarrativeSnapshots;
            int minimumRound = result.Round - interval;
            for (int index = 0; index < snapshots.Count; index++)
            {
                MatchNarrativeSnapshot snapshot = snapshots[index];
                if (snapshot.CompetitionScope != CompetitionScope.RegularSeason ||
                    snapshot.PlayerLine.Round <= minimumRound ||
                    snapshot.PlayerLine.Round > result.Round)
                {
                    continue;
                }
                games++;
                atBats += snapshot.PlayerLine.AtBats;
                hits += snapshot.PlayerLine.Hits;
                homeRuns += snapshot.PlayerLine.HomeRuns;
                rbi += snapshot.PlayerLine.RunsBattedIn;
                if (snapshot.PlayerLine.TeamRuns > snapshot.PlayerLine.OpponentRuns)
                    wins++;
            }
            string trend = atBats == 0
                ? "출전 기회가 제한된 구간이었다"
                : hits / (double)atBats >= 0.3d
                    ? "타격 흐름이 상승했다"
                    : hits / (double)atBats <= 0.2d
                        ? "결과 회복이 필요한 구간이었다"
                        : "기복 없이 흐름을 유지했다";
            var newsEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_{type}_{result.Round}",
                type,
                occurredAt,
                NewsReleaseGate.EndOfScheduleDate,
                NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name),
                $"season_{occurredAt.Cycle.SeasonId}_player_{career.MyPlayer.PlayerId}_{type}_{result.Round}",
                baseImportance: type == NewsEventType.MonthlyReport ? 32 : 22)
            {
                CareerImpact = type == NewsEventType.MonthlyReport ? 8 : 3
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(team.TeamId, team.Name));
            newsEvent.FactSet.SetText(NewsFactKey.PlayerName, career.MyPlayer.Name);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, team.Name);
            newsEvent.FactSet.SetText(NewsFactKey.ReportLabel, label);
            newsEvent.FactSet.SetInteger(NewsFactKey.ReportGames, games);
            newsEvent.FactSet.SetInteger(NewsFactKey.ReportAtBats, atBats);
            newsEvent.FactSet.SetInteger(NewsFactKey.ReportHits, hits);
            newsEvent.FactSet.SetInteger(NewsFactKey.ReportHomeRuns, homeRuns);
            newsEvent.FactSet.SetInteger(NewsFactKey.ReportRbi, rbi);
            newsEvent.FactSet.SetInteger(NewsFactKey.ReportTeamWins, wins);
            newsEvent.FactSet.SetInteger(NewsFactKey.ReportTeamLosses, games - wins);
            newsEvent.FactSet.SetText(NewsFactKey.ReportTrend, trend);
            return newsEvent;
        }

        private static NewsEvent CreateLeagueBriefing(
            CareerState career,
            CareerGameAdvanceResult playerResult,
            CareerDate occurredAt,
            MatchNarrativeSnapshot narrative)
        {
            int round = playerResult.Round;
            SeasonState season = career.CurrentLeague.CurrentSeason;
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
            newsEvent.FactSet.SetText(
                NewsFactKey.RoundScoreSummary,
                BuildRoundScoreSummary(career, season, round));
            BuildStandingChangeFacts(career, season, round, leader.TeamId, newsEvent.FactSet);
            newsEvent.FactSet.SetText(
                NewsFactKey.PlayerGameSummary,
                BuildPlayerGameSummary(career, playerResult, narrative));
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
            int losingStreak,
            MatchNarrativeSnapshot narrative)
        {
            PlayerSeasonStatisticsState season = career.CurrentLeague.CurrentSeason.PlayerStatistics;
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
            facts.SetBoolean(NewsFactKey.IsOneRunGame, Math.Abs(result.TeamRuns - result.OpponentRuns) == 1);
            facts.SetInteger(NewsFactKey.ScoreMargin, Math.Abs(result.TeamRuns - result.OpponentRuns));
            facts.SetInteger(NewsFactKey.GamePlateAppearances, result.PlateAppearances);
            facts.SetInteger(NewsFactKey.GameAtBats, result.AtBats);
            facts.SetInteger(NewsFactKey.GameRuns, result.Runs);
            facts.SetInteger(NewsFactKey.GameHits, result.Hits);
            facts.SetInteger(NewsFactKey.GameDoubles, result.Doubles);
            facts.SetInteger(NewsFactKey.GameTriples, result.Triples);
            facts.SetInteger(NewsFactKey.GameHomeRuns, result.HomeRuns);
            facts.SetInteger(NewsFactKey.GameRbi, result.RunsBattedIn);
            facts.SetInteger(NewsFactKey.GameWalks, result.Walks);
            facts.SetInteger(NewsFactKey.GameHitByPitches, result.HitByPitches);
            facts.SetInteger(NewsFactKey.GameStrikeouts, result.Strikeouts);
            facts.SetInteger(NewsFactKey.GameInningsOuts, result.OutsRecorded);
            facts.SetInteger(NewsFactKey.GameEarnedRuns, result.EarnedRuns);
            facts.SetText(NewsFactKey.GamePerformanceSummary, BuildPerformanceSummary(result, isPitcher));
            facts.SetText(NewsFactKey.GameStatLine, BuildStatLine(result, isPitcher, didAppear));
            facts.SetInteger(NewsFactKey.ManagerTrustBefore, result.ManagerEvaluationBefore);
            facts.SetInteger(NewsFactKey.ManagerTrustAfter, result.ManagerEvaluationAfter);
            facts.SetInteger(
                NewsFactKey.ManagerTrustChange,
                result.ManagerEvaluationAfter - result.ManagerEvaluationBefore);
            facts.SetText(NewsFactKey.PlayerRole, result.Role.ToString());
            if (narrative != null)
            {
                facts.SetInteger(NewsFactKey.HitStreak, narrative.HitStreak);
                facts.SetInteger(NewsFactKey.HitlessStreak, narrative.HitlessStreak);
                facts.SetInteger(NewsFactKey.PreviousHitlessStreak, narrative.PreviousHitlessStreak);
                facts.SetInteger(NewsFactKey.GamesSinceLastHit, narrative.PreviousHitlessStreak + 1);
                facts.SetInteger(NewsFactKey.RecentFiveGames, narrative.RecentGameCount);
                facts.SetInteger(NewsFactKey.RecentFiveAtBats, narrative.RecentAtBats);
                facts.SetInteger(NewsFactKey.RecentFiveHits, narrative.RecentHits);
                facts.SetText(NewsFactKey.ManagerComment, narrative.ManagerComment);
                facts.SetText(NewsFactKey.ManagerStyle, GetManagerStyleLabel(narrative.ManagerStyle));
            }
            facts.SetDecimal(NewsFactKey.SeasonBattingAverage, season.BattingAverage);
            facts.SetInteger(NewsFactKey.SeasonHomeRuns, season.HomeRuns);
            facts.SetInteger(NewsFactKey.SeasonHits, season.Hits);
            facts.SetInteger(NewsFactKey.SeasonRbi, season.RunsBattedIn);
            facts.SetDecimal(NewsFactKey.SeasonEra, season.EarnedRunAverage);
            facts.SetInteger(NewsFactKey.SeasonStrikeouts, season.PitchingStrikeouts);
            facts.SetInteger(NewsFactKey.TeamWinningStreak, winningStreak);
            facts.SetInteger(NewsFactKey.TeamLosingStreak, losingStreak);
            facts.SetInteger(NewsFactKey.TeamRank, CalculateRank(career.CurrentLeague.CurrentSeason, teamRecord));
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
            if (parts.Count > 0)
                return string.Join(" ", parts);
            int freePasses = result.Walks + result.HitByPitches;
            if (freePasses > 0)
                return $"무안타 {freePasses}출루";
            return "무안타";
        }

        private static string BuildStatLine(
            CareerGameAdvanceResult result,
            bool isPitcher,
            bool didAppear)
        {
            if (!didAppear)
                return "출장 없음";
            if (isPitcher)
                return $"{FormatInnings(result.OutsRecorded)}이닝 {result.EarnedRuns}자책 {result.Strikeouts}탈삼진";

            string line = $"{result.AtBats}타수 {result.Hits}안타";
            if (result.HomeRuns > 0) line += $" {result.HomeRuns}홈런";
            if (result.RunsBattedIn > 0) line += $" {result.RunsBattedIn}타점";
            if (result.Walks > 0) line += $" {result.Walks}볼넷";
            if (result.HitByPitches > 0) line += $" {result.HitByPitches}사구";
            line += $" {result.Strikeouts}삼진";
            return line;
        }

        private static string BuildRoundScoreSummary(
            CareerState career,
            SeasonState season,
            int round)
        {
            var builder = new StringBuilder(160);
            for (int index = 0; index < season.Schedule.Games.Count; index++)
            {
                ScheduledGameState game = season.Schedule.Games[index];
                if (game.Round != round || !game.IsCompleted)
                    continue;
                if (builder.Length > 0)
                    builder.Append('\n');
                builder.Append(FindTeam(career, game.AwayTeamId).Name)
                    .Append(' ')
                    .Append(game.AwayRuns)
                    .Append(" : ")
                    .Append(game.HomeRuns)
                    .Append(' ')
                    .Append(FindTeam(career, game.HomeTeamId).Name);
            }
            return builder.ToString();
        }

        private static string BuildPlayerGameSummary(
            CareerState career,
            CareerGameAdvanceResult result,
            MatchNarrativeSnapshot narrative)
        {
            bool isPitcher = career.MyPlayer.PrimaryPosition is
                PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            string line = BuildStatLine(result, isPitcher, DidAppear(result));
            return narrative == null
                ? $"{career.MyPlayer.Name}: {line}."
                : $"{career.MyPlayer.Name}: {line}. {narrative.PerformanceEvaluation}";
        }

        private static void BuildStandingChangeFacts(
            CareerState career,
            SeasonState season,
            int round,
            int currentLeaderTeamId,
            NewsFactSet facts)
        {
            var before = new StandingRecord[season.TeamRecords.Count];
            for (int index = 0; index < season.TeamRecords.Count; index++)
            {
                TeamSeasonRecordState record = season.TeamRecords[index];
                int wins = record.Wins;
                int losses = record.Losses;
                int ties = record.Ties;
                RemoveRoundResult(season.Schedule, round, record.TeamId, ref wins, ref losses, ref ties);
                before[index] = new StandingRecord(record.TeamId, wins, losses, ties, record.FixedTiebreaker);
            }

            int previousLeaderTeamId = FindLeader(before).TeamId;
            int currentLeaderPreviousRank = CalculateRank(before, currentLeaderTeamId);
            facts.SetBoolean(NewsFactKey.LeaderChanged, previousLeaderTeamId != currentLeaderTeamId);
            facts.SetInteger(NewsFactKey.PreviousTeamRank, currentLeaderPreviousRank);

            var changes = new List<StandingChange>(season.TeamRecords.Count);
            for (int index = 0; index < season.TeamRecords.Count; index++)
            {
                TeamSeasonRecordState record = season.TeamRecords[index];
                changes.Add(new StandingChange(
                    record.TeamId,
                    FindTeam(career, record.TeamId).Name,
                    CalculateRank(before, record.TeamId),
                    CalculateRank(season, record)));
            }
            changes.Sort((left, right) =>
            {
                int rank = left.CurrentRank.CompareTo(right.CurrentRank);
                return rank != 0 ? rank : left.TeamId.CompareTo(right.TeamId);
            });

            var builder = new StringBuilder(180);
            for (int index = 0; index < changes.Count; index++)
            {
                StandingChange change = changes[index];
                if (change.PreviousRank == change.CurrentRank &&
                    change.TeamId != currentLeaderTeamId &&
                    change.TeamId != career.MyPlayer.CurrentTeamId)
                {
                    continue;
                }
                if (builder.Length > 0)
                    builder.Append(" · ");
                builder.Append(change.TeamName).Append(' ');
                if (change.PreviousRank == change.CurrentRank)
                    builder.Append(change.CurrentRank).Append("위 유지");
                else
                    builder.Append(change.PreviousRank).Append("위 → ").Append(change.CurrentRank).Append('위');
            }
            if (builder.Length == 0)
                builder.Append("상위권 순위 변화 없음");
            facts.SetText(NewsFactKey.StandingChangeSummary, builder.ToString());
        }

        private static void RemoveRoundResult(
            SeasonScheduleState schedule,
            int round,
            int teamId,
            ref int wins,
            ref int losses,
            ref int ties)
        {
            for (int index = 0; index < schedule.Games.Count; index++)
            {
                ScheduledGameState game = schedule.Games[index];
                if (game.Round != round || !game.IsCompleted || !game.IncludesTeam(teamId))
                    continue;
                int teamRuns = game.HomeTeamId == teamId ? game.HomeRuns : game.AwayRuns;
                int opponentRuns = game.HomeTeamId == teamId ? game.AwayRuns : game.HomeRuns;
                if (teamRuns > opponentRuns) wins--;
                else if (teamRuns < opponentRuns) losses--;
                else ties--;
            }
        }

        private static StandingRecord FindLeader(IReadOnlyList<StandingRecord> records)
        {
            StandingRecord leader = records[0];
            for (int index = 1; index < records.Count; index++)
            {
                if (IsAhead(records[index], leader))
                    leader = records[index];
            }
            return leader;
        }

        private static int CalculateRank(IReadOnlyList<StandingRecord> records, int teamId)
        {
            StandingRecord target = default;
            for (int index = 0; index < records.Count; index++)
            {
                if (records[index].TeamId == teamId)
                {
                    target = records[index];
                    break;
                }
            }
            int rank = 1;
            for (int index = 0; index < records.Count; index++)
            {
                if (records[index].TeamId != teamId && IsAhead(records[index], target))
                    rank++;
            }
            return rank;
        }

        private static bool IsAhead(StandingRecord left, StandingRecord right)
        {
            if (left.WinningPercentage > right.WinningPercentage) return true;
            if (left.WinningPercentage < right.WinningPercentage) return false;
            if (left.Wins != right.Wins) return left.Wins > right.Wins;
            return left.FixedTiebreaker < right.FixedTiebreaker;
        }

        private readonly struct StandingRecord
        {
            public StandingRecord(int teamId, int wins, int losses, int ties, ulong fixedTiebreaker)
            {
                TeamId = teamId;
                Wins = wins;
                Losses = losses;
                Ties = ties;
                FixedTiebreaker = fixedTiebreaker;
            }

            public int TeamId { get; }
            public int Wins { get; }
            public int Losses { get; }
            public int Ties { get; }
            public ulong FixedTiebreaker { get; }
            public double WinningPercentage => Wins + Losses == 0 ? 0d : Wins / (double)(Wins + Losses);
        }

        private readonly struct StandingChange
        {
            public StandingChange(int teamId, string teamName, int previousRank, int currentRank)
            {
                TeamId = teamId;
                TeamName = teamName;
                PreviousRank = previousRank;
                CurrentRank = currentRank;
            }

            public int TeamId { get; }
            public string TeamName { get; }
            public int PreviousRank { get; }
            public int CurrentRank { get; }
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
            for (int index = 0; index < career.CurrentLeague.Teams.Count; index++)
            {
                if (career.CurrentLeague.Teams[index].TeamId == teamId)
                    return career.CurrentLeague.Teams[index];
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

        private static int FindApproachingTarget(int[] targets, int current, int range)
        {
            for (int index = 0; index < targets.Length; index++)
            {
                int remaining = targets[index] - current;
                if (remaining > 0 && remaining <= range)
                    return targets[index];
            }
            return 0;
        }

        private static bool HasActiveStoryline(
            CareerNewsState state,
            NewsStorylineType type,
            int playerId)
        {
            string id = playerId.ToString();
            for (int index = 0; index < state.ActiveStorylines.Count; index++)
            {
                NewsStorylineState storyline = state.ActiveStorylines[index];
                if (!storyline.IsResolved && storyline.Type == type && storyline.PrimaryPlayerId == id)
                    return true;
            }
            return false;
        }

        private static int SumCareer(CareerState career, CareerStat stat)
        {
            int total = 0;
            for (int index = 0; index < career.SeasonHistory.Count; index++)
            {
                PlayerSeasonStatisticsState statistics = career.SeasonHistory[index].Statistics;
                if (statistics == null)
                    continue;
                total += stat switch
                {
                    CareerStat.GamesPlayed => statistics.GamesPlayed,
                    CareerStat.HomeRuns => statistics.HomeRuns,
                    _ => statistics.Hits
                };
            }
            return total;
        }

        private static string GetManagerStyleLabel(ManagerNarrativeStyle style)
        {
            return style switch
            {
                ManagerNarrativeStyle.Results => "성과 중시형",
                ManagerNarrativeStyle.Development => "육성형",
                ManagerNarrativeStyle.Conservative => "보수형",
                _ => "분석형"
            };
        }

        private enum CareerStat
        {
            GamesPlayed,
            Hits,
            HomeRuns
        }
    }
}
