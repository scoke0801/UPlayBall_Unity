using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Simulation.Career;

namespace Baseball.Presentation.Career
{
    /// <summary>저장된 시즌·성장 결과를 재계산하지 않고 챕터 컷 표시 요청으로 변환한다.</summary>
    public static class CareerPresentationRequestFactory
    {
        public static bool TryCreateSeasonReview(
            CareerDashboardView view,
            Action completed,
            out CareerPresentationRequest request)
        {
            request = null;
            SeasonReviewSnapshot snapshot = view?.SeasonReview;
            if (snapshot == null)
                return false;

            if (view.SeasonReviewStep == SeasonReviewStep.RegularSeasonResult &&
                snapshot.PlayerTeamRank == 1)
            {
                SeasonStandingSnapshot standing = FindPlayerStanding(snapshot);
                request = new CareerPresentationRequest(
                    $"season:{snapshot.SeasonId}:regular-season-first",
                    CareerPresentationType.RegularSeasonFirst,
                    CareerPresentationGrade.Major,
                    snapshot.Year,
                    $"{snapshot.Year} REGULAR SEASON",
                    "정규 시즌 1위 확정",
                    snapshot.PlayerTeamName,
                    "한 시즌의 꾸준함이 가장 높은 자리로 이어졌습니다.\n포스트시즌에서 마지막 승부를 준비합니다.",
                    new[]
                    {
                        new PresentationStat("정규시즌", $"{standing.Wins}승 {standing.Losses}패", true),
                        new PresentationStat("승률", standing.WinningPercentage.ToString(".000")),
                        new PresentationStat("최종 순위", "1위 · 포스트시즌 진출", true)
                    },
                    completed: completed);
                return true;
            }

            if (view.SeasonReviewStep == SeasonReviewStep.PostseasonResult &&
                snapshot.PlayerTeamPostseasonResult == PlayerTeamPostseasonResult.Champion)
            {
                GetChampionshipSeriesRecord(snapshot, out int wins, out int losses);
                request = new CareerPresentationRequest(
                    $"season:{snapshot.SeasonId}:postseason-champion",
                    CareerPresentationType.PostseasonChampion,
                    CareerPresentationGrade.Major,
                    snapshot.Year,
                    $"{snapshot.Year} POSTSEASON",
                    "포스트시즌 우승",
                    snapshot.PlayerTeamName,
                    "마지막 아웃카운트가 올라갔습니다.\n올 시즌의 마지막 승자는 우리 팀입니다.",
                    new[]
                    {
                        new PresentationStat("결승 시리즈", $"{wins}승 {losses}패", true),
                        new PresentationStat("정규시즌", $"{snapshot.PlayerTeamRank}위"),
                        new PresentationStat("시즌 결과", snapshot.IsIntegratedChampion ? "통합 우승" : "포스트시즌 우승", true)
                    },
                    completed: completed);
                return true;
            }

            if (view.SeasonReviewStep != SeasonReviewStep.Awards || view.RevealedAwardCount <= 0 ||
                view.RevealedAwardCount > snapshot.PlayerAwards.Count)
            {
                return false;
            }

            SeasonAwardReviewSnapshot award = snapshot.PlayerAwards[view.RevealedAwardCount - 1];
            CareerPresentationType type;
            string category;
            string title;
            string description;
            PresentationStat[] stats;
            switch (award.Category)
            {
                case AwardCategory.PostseasonMvp:
                    type = CareerPresentationType.PostseasonMvp;
                    category = "POSTSEASON AWARD";
                    title = "포스트시즌 MVP";
                    description = "가장 큰 무대에서 남긴 활약이\n이번 포스트시즌 최고의 선수로 이어졌습니다.";
                    stats = BuildPostseasonStats(snapshot);
                    break;
                case AwardCategory.GoldGlove:
                    type = CareerPresentationType.GoldenGlove;
                    category = $"{snapshot.Year} SEASON AWARD";
                    title = "골든글러브 수상";
                    description = "한 시즌 동안 가장 믿을 수 있는 수비를 보여 주며\n포지션 최고의 수비수로 선정되었습니다.";
                    stats = new[]
                    {
                        new PresentationStat("수상 부문", $"{GetPositionCode(snapshot.PlayerPosition)}", true),
                        new PresentationStat("시즌 출장", $"{snapshot.PlayerStatistics.GamesPlayed}경기"),
                        new PresentationStat("실책", $"{view.Statistics.FieldingErrors}")
                    };
                    break;
                case AwardCategory.RegularSeasonMvp:
                    type = CareerPresentationType.RegularSeasonMvp;
                    category = $"{snapshot.Year} SEASON MVP";
                    title = "정규 시즌 MVP";
                    description = "긴 정규시즌 동안 쌓은 결과가\n리그에서 가장 가치 있는 한 시즌으로 인정받았습니다.";
                    stats = BuildRegularSeasonStats(snapshot.PlayerStatistics);
                    break;
                default:
                    return false;
            }

            request = new CareerPresentationRequest(
                $"season:{snapshot.SeasonId}:award:{award.AwardId}",
                type,
                CareerPresentationGrade.Major,
                snapshot.Year,
                category,
                title,
                snapshot.PlayerName,
                description,
                stats,
                completed: completed);
            return true;
        }

        public static bool TryCreateGrowthActivity(
            GrowthResultRecord result,
            string playerName,
            int startWeek,
            bool isRepeat,
            out CareerPresentationRequest request)
        {
            request = null;
            if (result == null)
                return false;

            CareerPresentationType type;
            string category;
            string description;
            if (result.SourceType == GrowthSourceType.Study)
            {
                type = CareerPresentationType.OverseasTraining;
                category = "OFFSEASON STUDY";
                description = $"{result.WeeksSpent}주간의 유학을 마쳤습니다.\n낯선 환경에서 얻은 경험이 성장으로 남았습니다.";
            }
            else if (string.Equals(result.SourceId, "rest", StringComparison.Ordinal))
            {
                type = CareerPresentationType.Rest;
                category = "OFFSEASON RECOVERY";
                description = "충분한 시간을 보내며 몸과 마음을 회복했습니다.";
            }
            else if (result.SourceType is GrowthSourceType.PersonalTraining or GrowthSourceType.TrainingPartner)
            {
                type = CareerPresentationType.Training;
                category = "OFFSEASON TRAINING";
                description = $"{result.WeeksSpent}주간의 훈련을 마쳤습니다.\n선택한 방향이 실제 능력 변화로 이어졌습니다.";
            }
            else
            {
                return false;
            }

            int endWeek = Math.Max(startWeek, startWeek + Math.Max(1, result.WeeksSpent) - 1);
            request = new CareerPresentationRequest(
                $"growth:{result.SeasonYear}:{result.RandomSeed}:{result.SourceId}",
                type,
                isRepeat ? CareerPresentationGrade.Compact : CareerPresentationGrade.Activity,
                result.SeasonYear,
                category,
                GrowthProgramNameFormatter.GetLabel(result.SourceId),
                playerName,
                description,
                BuildGrowthStats(result),
                startWeek,
                endWeek);
            return true;
        }

        /// <summary>영속된 수상 이력을 보상 없는 커리어 앨범 재감상 요청으로 변환한다.</summary>
        public static bool TryCreateAwardReplay(
            CareerAwardRecordView award,
            string playerName,
            out CareerPresentationRequest request)
        {
            request = null;
            CareerPresentationType type;
            string category;
            string title;
            switch (award.Category)
            {
                case AwardCategory.PostseasonMvp:
                    type = CareerPresentationType.PostseasonMvp;
                    category = "POSTSEASON AWARD";
                    title = "포스트시즌 MVP";
                    break;
                case AwardCategory.GoldGlove:
                    type = CareerPresentationType.GoldenGlove;
                    category = $"{award.Year} SEASON AWARD";
                    title = "골든글러브 수상";
                    break;
                case AwardCategory.RegularSeasonMvp:
                    type = CareerPresentationType.RegularSeasonMvp;
                    category = $"{award.Year} SEASON MVP";
                    title = "정규 시즌 MVP";
                    break;
                default:
                    return false;
            }

            request = new CareerPresentationRequest(
                $"career-record:{award.Year}:{award.Category}:{award.Position}",
                type,
                CareerPresentationGrade.Major,
                award.Year,
                category,
                title,
                playerName,
                "커리어 기록에 보존된 수상 장면입니다.\n한 시즌을 대표했던 순간을 다시 확인합니다.",
                new[]
                {
                    new PresentationStat("수상 연도", award.Year.ToString(), true),
                    new PresentationStat("리그", award.LeagueLevel.ToString()),
                    new PresentationStat("포지션", GetPositionCode(award.Position), true)
                });
            return true;
        }

        private static PresentationStat[] BuildRegularSeasonStats(PlayerSeasonReviewStatistics stats)
        {
            return stats.IsPitcher
                ? new[]
                {
                    new PresentationStat("승패", $"{stats.Wins}승 {stats.Losses}패", true),
                    new PresentationStat("평균자책점", stats.EarnedRunAverage.ToString("0.00"), true),
                    new PresentationStat("탈삼진", stats.PitchingStrikeouts.ToString())
                }
                : new[]
                {
                    new PresentationStat("타율", stats.BattingAverage.ToString(".000"), true),
                    new PresentationStat("홈런", stats.HomeRuns.ToString(), true),
                    new PresentationStat("타점", stats.RunsBattedIn.ToString()),
                    new PresentationStat("OPS", stats.OnBasePlusSlugging.ToString(".000"))
                };
        }

        private static PresentationStat[] BuildPostseasonStats(SeasonReviewSnapshot snapshot)
        {
            int atBats = 0;
            int hits = 0;
            int homeRuns = 0;
            int runsBattedIn = 0;
            int outs = 0;
            int earnedRuns = 0;
            int strikeouts = 0;
            for (int index = 0; index < snapshot.PlayerTeamPostseasonGames.Count; index++)
            {
                PostseasonGameReviewSnapshot game = snapshot.PlayerTeamPostseasonGames[index];
                if (!game.HasPlayerLine)
                    continue;
                PlayerGameLogState line = game.PlayerLine;
                atBats += line.AtBats;
                hits += line.Hits;
                homeRuns += line.HomeRuns;
                runsBattedIn += line.RunsBattedIn;
                outs += line.OutsRecorded;
                earnedRuns += line.EarnedRuns;
                strikeouts += line.Strikeouts;
            }

            if (snapshot.PlayerStatistics.IsPitcher)
            {
                double earnedRunAverage = outs == 0 ? 0d : earnedRuns * 27d / outs;
                return new[]
                {
                    new PresentationStat("투구 이닝", $"{outs / 3}.{outs % 3}", true),
                    new PresentationStat("평균자책점", earnedRunAverage.ToString("0.00"), true),
                    new PresentationStat("탈삼진", strikeouts.ToString())
                };
            }

            double battingAverage = atBats == 0 ? 0d : hits / (double)atBats;
            return new[]
            {
                new PresentationStat("타율", battingAverage.ToString(".000"), true),
                new PresentationStat("홈런", homeRuns.ToString(), true),
                new PresentationStat("타점", runsBattedIn.ToString())
            };
        }

        private static PresentationStat[] BuildGrowthStats(GrowthResultRecord result)
        {
            var stats = new List<PresentationStat>(6);
            for (int index = 0; index < result.AbilityChanges.Length && stats.Count < 4; index++)
            {
                AbilityChange change = result.AbilityChanges[index];
                if (change.Amount == 0)
                    continue;
                stats.Add(new PresentationStat(
                    GetAbilityLabel(change.Ability),
                    FormatSigned(change.Amount),
                    change.Amount > 0));
            }
            for (int index = 0; index < result.PotentialChanges.Length && stats.Count < 4; index++)
            {
                AbilityChange change = result.PotentialChanges[index];
                if (change.Amount == 0)
                    continue;
                stats.Add(new PresentationStat(
                    GetAbilityLabel(change.Ability) + " Potential",
                    FormatSigned(change.Amount),
                    true));
            }
            if (result.ConditionChange != 0)
                stats.Add(new PresentationStat("컨디션", FormatSigned(result.ConditionChange), result.ConditionChange > 0));
            if (result.MoneySpent > 0L && stats.Count < 6)
                stats.Add(new PresentationStat("비용", $"-{result.MoneySpent / 10_000d:N0}만원"));
            if (stats.Count == 0)
                stats.Add(new PresentationStat("활동 결과", "변화 없음"));
            return stats.ToArray();
        }

        private static SeasonStandingSnapshot FindPlayerStanding(SeasonReviewSnapshot snapshot)
        {
            for (int index = 0; index < snapshot.Standings.Count; index++)
            {
                if (snapshot.Standings[index].TeamId == snapshot.PlayerTeamId)
                    return snapshot.Standings[index];
            }
            return default;
        }

        private static void GetChampionshipSeriesRecord(
            SeasonReviewSnapshot snapshot,
            out int wins,
            out int losses)
        {
            wins = 0;
            losses = 0;
            for (int index = 0; index < snapshot.PostseasonSeries.Count; index++)
            {
                PostseasonSeriesReviewSnapshot series = snapshot.PostseasonSeries[index];
                if (series.Round != PostseasonRound.ChampionshipSeries)
                    continue;
                bool isHigherSeed = series.HigherSeedTeamId == snapshot.PlayerTeamId;
                wins = isHigherSeed ? series.HigherSeedWins : series.LowerSeedWins;
                losses = isHigherSeed ? series.LowerSeedWins : series.HigherSeedWins;
                return;
            }
        }

        private static string GetPositionCode(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "C",
                PlayerPosition.FirstBase => "1B",
                PlayerPosition.SecondBase => "2B",
                PlayerPosition.ThirdBase => "3B",
                PlayerPosition.Shortstop => "SS",
                PlayerPosition.LeftField => "LF",
                PlayerPosition.CenterField => "CF",
                PlayerPosition.RightField => "RF",
                PlayerPosition.DesignatedHitter => "DH",
                PlayerPosition.StartingPitcher => "SP",
                PlayerPosition.ReliefPitcher => "RP",
                _ => "-"
            };
        }

        private static string GetAbilityLabel(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Contact => "컨택트",
                PlayerAbility.Power => "파워",
                PlayerAbility.Speed => "주력",
                PlayerAbility.Arm => "송구",
                PlayerAbility.Defense => "수비",
                PlayerAbility.BatterMental => "타자 멘탈",
                PlayerAbility.Velocity => "구속",
                PlayerAbility.Stuff => "구위",
                PlayerAbility.Breaking => "변화구",
                PlayerAbility.Control => "제구",
                PlayerAbility.Stamina => "체력",
                PlayerAbility.PitcherMental => "투수 멘탈",
                _ => ability.ToString()
            };
        }

        private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString();
    }
}
