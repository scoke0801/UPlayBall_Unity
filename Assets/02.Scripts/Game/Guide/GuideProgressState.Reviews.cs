using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Game.Career;

namespace Baseball.Game.Guide
{
    public sealed partial class GuideProgressState
    {
        /// <summary>플레이어 구단의 확정 정규시즌 기록을 한 번만 집계한다. 다른 경기 종류는 제외한다.</summary>
        public bool RecordOfficialGame(int season, int ordinal, int gamesPerWeek, bool isLastGame,
            CompetitionScope scope, int runsFor, int runsAgainst, IReadOnlyList<ManagerNewsPlayerLine> players)
        {
            if (scope != CompetitionScope.RegularSeason) return false;
            if (season <= 0 || ordinal <= 0 || gamesPerWeek <= 0 || runsFor < 0 || runsAgainst < 0 || players == null)
                throw new ArgumentException("공식 경기 보고 입력이 잘못되었습니다.");
            var cards = new HashSet<string>(StringComparer.Ordinal);
            foreach (var player in players)
                if (player == null || string.IsNullOrWhiteSpace(player.CardId) || !cards.Add(player.CardId) ||
                    player.Outs < 0 || player.Walks < 0 || player.EarnedRuns < 0 || player.PlateAppearances < 0 ||
                    player.Hits < 0 || player.HomeRuns < 0 || player.Strikeouts < 0 ||
                    player.SeasonHomeRuns < player.HomeRuns || player.SeasonStrikeouts < player.Strikeouts)
                    throw new ArgumentException("공식 선수 기록이 잘못되었습니다.");
            if (season < _news.matchSeason || (season == _news.matchSeason && ordinal <= _news.lastMatchOrdinal)) return false;
            int week = (ordinal - 1) / gamesPerWeek;
            bool newSeason = season != _news.matchSeason;
            bool gap = !newSeason && ordinal != _news.lastMatchOrdinal + 1;
            if (newSeason || week != _news.weeklyIndex || gap)
            {
                if (newSeason || week != _news.weeklyIndex + 1 || gap) _news.previousWeek = new ManagerNewsEvidence();
                _news.weekly = new ManagerNewsEvidence { kind = ManagerNewsKind.WeeklyReview };
                _news.weeklyIndex = week;
            }
            _news.matchSeason = season; _news.lastMatchOrdinal = ordinal;
            AdvanceNewsClock(season, week);
            var summary = _news.weekly;
            summary.games++; summary.runsFor += runsFor; summary.runsAgainst += runsAgainst;
            if (runsFor > runsAgainst) summary.wins++;
            else if (runsFor < runsAgainst) summary.losses++;
            else summary.draws++;
            foreach (var player in players)
            {
                if (player.HasPitchingLine && !player.StartedPitching)
                { summary.bullpenOuts += player.Outs; summary.bullpenWalks += player.Walks; }
                PublishMilestone(season, week, player.CardId, player.SeasonHomeRuns, player.HomeRuns,
                    _reportPolicy.homeRunMilestones, ManagerNewsKind.HomeRunMilestone);
                PublishMilestone(season, week, player.CardId, player.SeasonStrikeouts, player.Strikeouts,
                    _reportPolicy.strikeoutMilestones, ManagerNewsKind.StrikeoutMilestone);
            }
            ObserveDecisions(season, ordinal, week, players);
            if (ordinal % gamesPerWeek == 0 || isLastGame)
            {
                // 도중 도입·누락으로 한 주의 일부만 받은 경우 완전한 주간 회고로 꾸미지 않는다.
                int expectedGames = ordinal - week * gamesPerWeek;
                if (summary.games == expectedGames)
                {
                    CompleteWeeklyReview(season, week);
                    _news.previousWeek = summary.Copy();
                }
                else _news.previousWeek = new ManagerNewsEvidence();
            }
            return true;
        }

        private void PublishMilestone(int season, int week, string cardId, int total, int gained, int[] thresholds, ManagerNewsKind kind)
        {
            int reached = 0;
            foreach (int threshold in thresholds)
                if (total >= threshold && total - gained < threshold) reached = threshold;
            if (reached == 0) return;
            PublishNews("milestone:" + season + ":" + kind + ":" + cardId + ":" + reached,
                cardId, season, week, new ManagerNewsEvidence { kind = kind, milestone = reached });
        }

        private void CompleteWeeklyReview(int season, int week)
        {
            var summary = _news.weekly.Copy();
            var previous = _news.previousWeek;
            summary.previousGames = previous.games;
            summary.previousOuts = previous.bullpenOuts; summary.previousWalks = previous.bullpenWalks;
            if (summary.games >= _reportPolicy.minimumReviewGames && previous.games >= _reportPolicy.minimumReviewGames &&
                summary.bullpenOuts >= _reportPolicy.minimumBullpenOuts && previous.bullpenOuts >= _reportPolicy.minimumBullpenOuts)
            {
                double difference = Math.Abs(summary.bullpenWalks * 27d / summary.bullpenOuts -
                    previous.bullpenWalks * 27d / previous.bullpenOuts);
                summary.hasComparison = difference >= _reportPolicy.minimumWalksPerNineChange;
            }
            PublishNews("weekly:" + season + ":" + week, string.Empty, season, week, summary);
        }

        private void StartDecisionObservation(GuideGoal goal)
        {
            if (string.IsNullOrEmpty(goal.CardId) || _decisions.Exists(item => item.key == goal.Key)) return;
            // 같은 선수의 한 선택만 관찰한다. 슬롯 이동으로 중복 관찰을 만들지 않는다.
            _decisions.RemoveAll(item => item.cardId == goal.CardId);
            _decisions.Add(new ManagerDecisionObservation
            {
                key = goal.Key, cardId = goal.CardId, sequence = ++_news.decisionSequence,
                season = _currentSeasonNumber, startGame = _news.lastMatchOrdinal,
                expectsStarter = goal.Group == LineupPresetAssignmentGroup.StarterRotation,
                hasPitchingLine = goal.Evidence == "PitcherRoleMismatch"
            });
        }

        private void ObserveDecisions(int season, int ordinal, int week, IReadOnlyList<ManagerNewsPlayerLine> players)
        {
            for (int i = _decisions.Count - 1; i >= 0; i--)
            {
                var observation = _decisions[i];
                if (observation.season != season) { _decisions.RemoveAt(i); continue; }
                observation.games++;
                foreach (var player in players)
                {
                    if (player.CardId != observation.cardId) continue;
                    if (player.HasPitchingLine && observation.expectsStarter != player.StartedPitching)
                    {
                        observation.expectsStarter = player.StartedPitching; observation.roleChanged = true;
                        observation.appearances = observation.outs = observation.earnedRuns = observation.walks = observation.strikeouts = 0;
                    }
                    if (player.HasPitchingLine)
                    {
                        observation.appearances++; observation.outs += player.Outs;
                        observation.earnedRuns += player.EarnedRuns; observation.walks += player.Walks; observation.strikeouts += player.Strikeouts;
                    }
                    observation.plateAppearances += player.PlateAppearances; observation.hits += player.Hits;
                }
                bool enough = observation.hasPitchingLine
                    ? observation.appearances >= _reportPolicy.minimumDecisionAppearances && observation.outs >= _reportPolicy.minimumDecisionOuts
                    : observation.plateAppearances >= _reportPolicy.minimumDecisionPlateAppearances;
                if (enough)
                {
                    PublishNews("decision:" + observation.sequence, observation.cardId, season, week,
                        new ManagerNewsEvidence { kind = ManagerNewsKind.DecisionFollowup, roleChanged = observation.roleChanged,
                            games = observation.games, appearances = observation.appearances, plateAppearances = observation.plateAppearances,
                            hits = observation.hits, outs = observation.outs, earnedRuns = observation.earnedRuns,
                            walks = observation.walks, strikeouts = observation.strikeouts });
                }
                if (enough || observation.games >= _reportPolicy.maximumDecisionGames) _decisions.RemoveAt(i);
            }
        }
    }
}
