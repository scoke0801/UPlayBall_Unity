using System;
using System.Collections.Generic;
using Baseball.Simulation.Career;
using Baseball.Simulation.Match;

namespace Baseball.Game.Career.News
{
    /// <summary>포스트시즌 경기·시리즈·우승·수상의 확정 결과를 공개 관문별 사건으로 만든다.</summary>
    public sealed class PostseasonNewsEvaluator
    {
        public IReadOnlyList<NewsEvent> Evaluate(
            CareerState career,
            CareerPostseasonGameResult result,
            int gameId,
            MatchResult matchResult,
            CareerDate occurredAt)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (matchResult == null) throw new ArgumentNullException(nameof(matchResult));

            var events = new List<NewsEvent>();
            int playerTeamId = career.MyPlayer.CurrentTeamId;
            int focusTeamId = result.PlayerResult.HasValue ? playerTeamId : result.WinnerTeamId;
            int opponentTeamId = focusTeamId == result.HigherSeedTeamId
                ? result.LowerSeedTeamId
                : result.HigherSeedTeamId;
            TeamState focusTeam = FindTeam(career, focusTeamId);
            TeamState opponent = FindTeam(career, opponentTeamId);
            int focusRuns = matchResult.HomeBoxScore.TeamId == focusTeamId
                ? matchResult.HomeBoxScore.Runs
                : matchResult.AwayBoxScore.Runs;
            int opponentRuns = matchResult.HomeBoxScore.TeamId == opponentTeamId
                ? matchResult.HomeBoxScore.Runs
                : matchResult.AwayBoxScore.Runs;
            string mergeKey = $"season_{occurredAt.Cycle.SeasonId}_post_game_{gameId}";
            NewsFactSet facts = BuildFacts(
                career,
                result,
                gameId,
                focusTeam,
                opponent,
                focusRuns,
                opponentRuns);

            var gameEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_post_game_{gameId}_completed",
                NewsEventType.PostseasonGameCompleted,
                occurredAt,
                NewsReleaseGate.AfterGameResult,
                NewsSubject.Team(focusTeam.TeamId, focusTeam.Name),
                mergeKey,
                baseImportance: 40)
            {
                GameImpact = 15
            };
            gameEvent.AddRelatedSubject(NewsSubject.Team(opponent.TeamId, opponent.Name));
            gameEvent.AddRelatedSubject(NewsSubject.Game(gameId));
            if (result.PlayerResult.HasValue)
                gameEvent.AddRelatedSubject(NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name));
            gameEvent.FactSet.MergeFrom(facts);
            events.Add(gameEvent);

            if (result.IsSeriesCompleted)
                events.Add(CreateSeriesEvent(career, result, occurredAt, gameId, mergeKey, facts));
            if (result.IsPostseasonCompleted)
            {
                events.Add(CreateChampionshipEvent(career, result, occurredAt, gameId, mergeKey, facts));
                AddAwardEvents(career, occurredAt, events);
            }
            return events;
        }

        private static NewsEvent CreateSeriesEvent(
            CareerState career,
            CareerPostseasonGameResult result,
            CareerDate occurredAt,
            int gameId,
            string mergeKey,
            NewsFactSet facts)
        {
            int winnerTeamId = result.HigherSeedWins > result.LowerSeedWins
                ? result.HigherSeedTeamId
                : result.LowerSeedTeamId;
            int loserTeamId = winnerTeamId == result.HigherSeedTeamId
                ? result.LowerSeedTeamId
                : result.HigherSeedTeamId;
            bool isPlayerEliminated = loserTeamId == career.MyPlayer.CurrentTeamId;
            TeamState primary = FindTeam(career, isPlayerEliminated ? loserTeamId : winnerTeamId);
            TeamState related = FindTeam(career, isPlayerEliminated ? winnerTeamId : loserTeamId);
            var newsEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_post_game_{gameId}_series_completed",
                isPlayerEliminated ? NewsEventType.PostseasonEliminated : NewsEventType.PostseasonSeriesCompleted,
                occurredAt,
                NewsReleaseGate.AfterSeriesResult,
                NewsSubject.Team(primary.TeamId, primary.Name),
                mergeKey,
                baseImportance: isPlayerEliminated ? 50 : 40)
            {
                CareerImpact = isPlayerEliminated ? 25 : 15,
                GameImpact = 20,
                IsCareerArchive = isPlayerEliminated
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(related.TeamId, related.Name));
            if (isPlayerEliminated)
                newsEvent.AddRelatedSubject(NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name));
            newsEvent.FactSet.MergeFrom(facts);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, primary.Name);
            newsEvent.FactSet.SetText(NewsFactKey.OpponentName, related.Name);
            SetSeriesScore(newsEvent.FactSet, result, winnerTeamId);
            return newsEvent;
        }

        private static NewsEvent CreateChampionshipEvent(
            CareerState career,
            CareerPostseasonGameResult result,
            CareerDate occurredAt,
            int gameId,
            string mergeKey,
            NewsFactSet facts)
        {
            TeamState champion = FindTeam(career, result.ChampionTeamId);
            int runnerUpId = result.ChampionTeamId == result.HigherSeedTeamId
                ? result.LowerSeedTeamId
                : result.HigherSeedTeamId;
            TeamState runnerUp = FindTeam(career, runnerUpId);
            var newsEvent = new NewsEvent(
                $"season_{occurredAt.Cycle.SeasonId}_champion_{result.ChampionTeamId}",
                NewsEventType.ChampionshipWon,
                occurredAt,
                NewsReleaseGate.AfterPostseasonReveal,
                NewsSubject.Team(champion.TeamId, champion.Name),
                mergeKey,
                baseImportance: 50)
            {
                CareerImpact = 35,
                GameImpact = 25,
                Rarity = 20,
                IsCareerArchive = champion.TeamId == career.MyPlayer.CurrentTeamId
            };
            newsEvent.AddRelatedSubject(NewsSubject.Team(runnerUp.TeamId, runnerUp.Name));
            if (champion.TeamId == career.MyPlayer.CurrentTeamId)
                newsEvent.AddRelatedSubject(NewsSubject.Player(career.MyPlayer.PlayerId, career.MyPlayer.Name));
            newsEvent.FactSet.MergeFrom(facts);
            newsEvent.FactSet.SetText(NewsFactKey.TeamName, champion.Name);
            newsEvent.FactSet.SetText(NewsFactKey.OpponentName, runnerUp.Name);
            SetSeriesScore(newsEvent.FactSet, result, champion.TeamId);
            return newsEvent;
        }

        private static void AddAwardEvents(
            CareerState career,
            CareerDate occurredAt,
            List<NewsEvent> events)
        {
            SeasonAwardsState awards = career.CurrentLeague.CurrentSeason.Awards;
            if (awards == null)
                return;
            for (int index = 0; index < awards.Results.Count; index++)
            {
                SeasonAwardResultState award = awards.Results[index];
                bool isHeadlineAward = award.Category is
                    AwardCategory.RegularSeasonMvp or
                    AwardCategory.PostseasonMvp or
                    AwardCategory.RookieOfYear;
                bool isMyAward = award.IncludesWinner(career.MyPlayer.PlayerId);
                if (!isHeadlineAward && !isMyAward)
                    continue;
                AwardCandidateResult winner = FindWinner(award);
                if (winner == null)
                    continue;
                TeamState winnerTeam = FindTeam(career, winner.TeamId);
                var newsEvent = new NewsEvent(
                    $"season_{occurredAt.Cycle.SeasonId}_award_{award.AwardId}",
                    NewsEventType.SeasonAwardGranted,
                    occurredAt,
                    NewsReleaseGate.AfterAwardReveal,
                    NewsSubject.Player(winner.PlayerId, winner.PlayerName),
                    $"season_{occurredAt.Cycle.SeasonId}_award_{award.AwardId}",
                    baseImportance: isHeadlineAward ? 40 : 25)
                {
                    CareerImpact = isMyAward ? 30 : 20,
                    Rarity = isHeadlineAward ? 15 : 5,
                    IsCareerArchive = isMyAward
                };
                newsEvent.AddRelatedSubject(NewsSubject.Team(winnerTeam.TeamId, winnerTeam.Name));
                newsEvent.FactSet.SetText(NewsFactKey.PlayerName, winner.PlayerName);
                newsEvent.FactSet.SetText(NewsFactKey.TeamName, winnerTeam.Name);
                newsEvent.FactSet.SetText(NewsFactKey.AwardName, GetAwardLabel(award));
                events.Add(newsEvent);
            }
        }

        private static NewsFactSet BuildFacts(
            CareerState career,
            CareerPostseasonGameResult result,
            int gameId,
            TeamState team,
            TeamState opponent,
            int teamRuns,
            int opponentRuns)
        {
            var facts = new NewsFactSet();
            facts.SetText(NewsFactKey.PlayerName, career.MyPlayer.Name);
            facts.SetText(NewsFactKey.TeamName, team.Name);
            facts.SetText(NewsFactKey.OpponentName, opponent.Name);
            facts.SetInteger(NewsFactKey.GameId, gameId);
            facts.SetInteger(NewsFactKey.TeamRuns, teamRuns);
            facts.SetInteger(NewsFactKey.OpponentRuns, opponentRuns);
            facts.SetBoolean(NewsFactKey.DidWin, teamRuns > opponentRuns);
            facts.SetBoolean(NewsFactKey.DidLose, teamRuns < opponentRuns);
            facts.SetBoolean(NewsFactKey.DidTie, teamRuns == opponentRuns);
            facts.SetText(NewsFactKey.PostseasonRound, GetRoundLabel(result.Round));
            SetSeriesScore(facts, result, team.TeamId);
            if (result.PlayerResult.HasValue)
            {
                CareerGameAdvanceResult player = result.PlayerResult.Value;
                bool isPitcher = career.MyPlayer.PrimaryPosition is
                    Baseball.Core.Players.PlayerPosition.StartingPitcher or
                    Baseball.Core.Players.PlayerPosition.ReliefPitcher;
                facts.SetText(
                    NewsFactKey.GameStatLine,
                    isPitcher
                        ? $"{player.OutsRecorded / 3}.{player.OutsRecorded % 3}이닝 {player.EarnedRuns}자책 {player.Strikeouts}탈삼진"
                        : $"{player.AtBats}타수 {player.Hits}안타 {player.HomeRuns}홈런 {player.RunsBattedIn}타점");
            }
            return facts;
        }

        private static void SetSeriesScore(
            NewsFactSet facts,
            CareerPostseasonGameResult result,
            int perspectiveTeamId)
        {
            int wins = perspectiveTeamId == result.HigherSeedTeamId
                ? result.HigherSeedWins
                : result.LowerSeedWins;
            int losses = perspectiveTeamId == result.HigherSeedTeamId
                ? result.LowerSeedWins
                : result.HigherSeedWins;
            facts.SetText(NewsFactKey.PostseasonSeriesScore, $"{wins}승 {losses}패");
        }

        private static AwardCandidateResult FindWinner(SeasonAwardResultState award)
        {
            for (int index = 0; index < award.TopCandidates.Count; index++)
            {
                if (award.TopCandidates[index].PlayerId == award.WinnerPlayerId)
                    return award.TopCandidates[index];
            }
            return null;
        }

        private static string GetAwardLabel(SeasonAwardResultState award)
        {
            if (award.Category == AwardCategory.GoldGlove)
                return $"{award.Position} 골든글러브";
            return award.Category switch
            {
                AwardCategory.RegularSeasonMvp => "정규시즌 MVP",
                AwardCategory.PostseasonMvp => "포스트시즌 MVP",
                AwardCategory.RookieOfYear => "신인왕",
                AwardCategory.BattingAverage => "타격왕",
                AwardCategory.HomeRun => "홈런왕",
                AwardCategory.RunsBattedIn => "타점왕",
                AwardCategory.StolenBase => "도루왕",
                AwardCategory.EarnedRunAverage => "평균자책점 1위",
                AwardCategory.Win => "다승왕",
                AwardCategory.Strikeout => "탈삼진왕",
                AwardCategory.Save => "세이브왕",
                _ => award.AwardId
            };
        }

        private static string GetRoundLabel(PostseasonRound round)
        {
            return round == PostseasonRound.ChampionshipSeries ? "결승" : "준결승";
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
    }
}
