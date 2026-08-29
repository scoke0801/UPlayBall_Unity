using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>정규시즌 최종 순위표의 변경 불가능한 한 행이다.</summary>
    public readonly struct SeasonStandingSnapshot
    {
        public SeasonStandingSnapshot(
            int rank,
            int teamId,
            string teamName,
            int wins,
            int losses,
            int ties)
        {
            Rank = rank;
            TeamId = teamId;
            TeamName = teamName ?? string.Empty;
            Wins = wins;
            Losses = losses;
            Ties = ties;
        }

        public int Rank { get; }
        public int TeamId { get; }
        public string TeamName { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int Ties { get; }
        public double WinningPercentage => Wins + Losses == 0 ? 0d : Wins / (double)(Wins + Losses);
    }

    /// <summary>결산 연출이 다시 계산하지 않고 표시할 포스트시즌 한 경기 결과다.</summary>
    public readonly struct PostseasonGameReviewSnapshot
    {
        public PostseasonGameReviewSnapshot(
            PostseasonRound round,
            int gameNumber,
            int awayTeamId,
            string awayTeamName,
            int awayRuns,
            int homeTeamId,
            string homeTeamName,
            int homeRuns,
            bool hasPlayerLine,
            PlayerGameLogState playerLine)
        {
            Round = round;
            GameNumber = gameNumber;
            AwayTeamId = awayTeamId;
            AwayTeamName = awayTeamName ?? string.Empty;
            AwayRuns = awayRuns;
            HomeTeamId = homeTeamId;
            HomeTeamName = homeTeamName ?? string.Empty;
            HomeRuns = homeRuns;
            HasPlayerLine = hasPlayerLine;
            PlayerLine = playerLine;
        }

        public PostseasonRound Round { get; }
        public int GameNumber { get; }
        public int AwayTeamId { get; }
        public string AwayTeamName { get; }
        public int AwayRuns { get; }
        public int HomeTeamId { get; }
        public string HomeTeamName { get; }
        public int HomeRuns { get; }
        public bool HasPlayerLine { get; }
        public PlayerGameLogState PlayerLine { get; }
    }

    /// <summary>결산 대진표가 사용할 포스트시즌 한 시리즈의 확정 결과다.</summary>
    public readonly struct PostseasonSeriesReviewSnapshot
    {
        public PostseasonSeriesReviewSnapshot(
            PostseasonRound round,
            int higherSeedTeamId,
            string higherSeedTeamName,
            int higherSeedWins,
            int lowerSeedTeamId,
            string lowerSeedTeamName,
            int lowerSeedWins,
            int winnerTeamId)
        {
            Round = round;
            HigherSeedTeamId = higherSeedTeamId;
            HigherSeedTeamName = higherSeedTeamName ?? string.Empty;
            HigherSeedWins = higherSeedWins;
            LowerSeedTeamId = lowerSeedTeamId;
            LowerSeedTeamName = lowerSeedTeamName ?? string.Empty;
            LowerSeedWins = lowerSeedWins;
            WinnerTeamId = winnerTeamId;
        }

        public PostseasonRound Round { get; }
        public int HigherSeedTeamId { get; }
        public string HigherSeedTeamName { get; }
        public int HigherSeedWins { get; }
        public int LowerSeedTeamId { get; }
        public string LowerSeedTeamName { get; }
        public int LowerSeedWins { get; }
        public int WinnerTeamId { get; }
    }

    /// <summary>시상식과 뉴스가 공유할 확정 수상 한 건이다.</summary>
    public readonly struct SeasonAwardReviewSnapshot
    {
        public SeasonAwardReviewSnapshot(
            string awardId,
            string awardName,
            AwardCategory category,
            int winnerPlayerId,
            string winnerPlayerName,
            int winnerTeamId,
            string winnerTeamName,
            bool isPlayerWinner)
        {
            AwardId = awardId ?? string.Empty;
            AwardName = awardName ?? string.Empty;
            Category = category;
            WinnerPlayerId = winnerPlayerId;
            WinnerPlayerName = winnerPlayerName ?? string.Empty;
            WinnerTeamId = winnerTeamId;
            WinnerTeamName = winnerTeamName ?? string.Empty;
            IsPlayerWinner = isPlayerWinner;
        }

        public string AwardId { get; }
        public string AwardName { get; }
        public AwardCategory Category { get; }
        public int WinnerPlayerId { get; }
        public string WinnerPlayerName { get; }
        public int WinnerTeamId { get; }
        public string WinnerTeamName { get; }
        public bool IsPlayerWinner { get; }
    }

    /// <summary>개인 성과 화면이 사용할 정규시즌 기록을 동결한다.</summary>
    public readonly struct PlayerSeasonReviewStatistics
    {
        public PlayerSeasonReviewStatistics(PlayerSeasonStatisticsState statistics, bool isPitcher)
        {
            IsPitcher = isPitcher;
            GamesPlayed = statistics?.GamesPlayed ?? 0;
            AtBats = statistics?.AtBats ?? 0;
            Hits = statistics?.Hits ?? 0;
            HomeRuns = statistics?.HomeRuns ?? 0;
            RunsBattedIn = statistics?.RunsBattedIn ?? 0;
            BattingAverage = statistics?.BattingAverage ?? 0d;
            OnBasePlusSlugging = statistics?.OnBasePlusSlugging ?? 0d;
            PitchingAppearances = statistics?.PitchingAppearances ?? 0;
            OutsRecorded = statistics?.OutsRecorded ?? 0;
            Wins = statistics?.Wins ?? 0;
            Losses = statistics?.Losses ?? 0;
            EarnedRunAverage = statistics?.EarnedRunAverage ?? 0d;
            PitchingStrikeouts = statistics?.PitchingStrikeouts ?? 0;
        }

        public bool IsPitcher { get; }
        public int GamesPlayed { get; }
        public int AtBats { get; }
        public int Hits { get; }
        public int HomeRuns { get; }
        public int RunsBattedIn { get; }
        public double BattingAverage { get; }
        public double OnBasePlusSlugging { get; }
        public int PitchingAppearances { get; }
        public int OutsRecorded { get; }
        public int Wins { get; }
        public int Losses { get; }
        public double EarnedRunAverage { get; }
        public int PitchingStrikeouts { get; }
    }

    /// <summary>시즌 성장 화면이 표시할 능력치 전후 값이다.</summary>
    public readonly struct SeasonAbilityChangeSnapshot
    {
        public SeasonAbilityChangeSnapshot(PlayerAbility ability, int before, int after)
        {
            Ability = ability;
            Before = before;
            After = after;
        }

        public PlayerAbility Ability { get; }
        public int Before { get; }
        public int After { get; }
        public int Change => After - Before;
    }

    /// <summary>
    /// 정규시즌 순위부터 포스트시즌·수상·정산까지 모든 소비자가 공유하는 시즌 결과 스냅샷이다.
    /// </summary>
    public sealed class SeasonReviewSnapshot
    {
        private SeasonReviewSnapshot(
            int seasonId,
            int year,
            LeagueLevel leagueLevel,
            int playerId,
            string playerName,
            PlayerPosition playerPosition,
            int playerTeamId,
            string playerTeamName,
            int playerTeamRank,
            int postseasonSeed,
            SeasonStandingSnapshot[] standings,
            PlayerSeasonReviewStatistics playerStatistics)
        {
            SeasonId = seasonId;
            Year = year;
            LeagueLevel = leagueLevel;
            PlayerId = playerId;
            PlayerName = playerName ?? string.Empty;
            PlayerPosition = playerPosition;
            PlayerTeamId = playerTeamId;
            PlayerTeamName = playerTeamName ?? string.Empty;
            PlayerTeamRank = playerTeamRank;
            PostseasonSeed = postseasonSeed;
            Standings = standings ?? Array.Empty<SeasonStandingSnapshot>();
            PlayerStatistics = playerStatistics;
            PostseasonSeries = Array.Empty<PostseasonSeriesReviewSnapshot>();
            PlayerTeamPostseasonGames = Array.Empty<PostseasonGameReviewSnapshot>();
            Awards = Array.Empty<SeasonAwardReviewSnapshot>();
            PlayerAwards = Array.Empty<SeasonAwardReviewSnapshot>();
            SettlementEntries = Array.Empty<SettlementEntry>();
            AbilityChanges = Array.Empty<SeasonAbilityChangeSnapshot>();
        }

        public int SeasonId { get; }
        public int Year { get; }
        public LeagueLevel LeagueLevel { get; }
        public int PlayerId { get; }
        public string PlayerName { get; }
        public PlayerPosition PlayerPosition { get; }
        public int PlayerTeamId { get; }
        public string PlayerTeamName { get; }
        public int PlayerTeamRank { get; }
        public int PostseasonSeed { get; }
        public IReadOnlyList<SeasonStandingSnapshot> Standings { get; }
        public PlayerSeasonReviewStatistics PlayerStatistics { get; }
        public bool IsPostseasonFinalized { get; private set; }
        public int ChampionTeamId { get; private set; }
        public string ChampionTeamName { get; private set; } = string.Empty;
        public int RunnerUpTeamId { get; private set; }
        public string RunnerUpTeamName { get; private set; } = string.Empty;
        public PlayerTeamPostseasonResult PlayerTeamPostseasonResult { get; private set; }
        public int PlayerTeamFinalRank { get; private set; }
        public bool IsIntegratedChampion => PlayerTeamRank == 1 &&
                                            PlayerTeamPostseasonResult == PlayerTeamPostseasonResult.Champion;
        public IReadOnlyList<PostseasonSeriesReviewSnapshot> PostseasonSeries { get; private set; }
        public IReadOnlyList<PostseasonGameReviewSnapshot> PlayerTeamPostseasonGames { get; private set; }
        public IReadOnlyList<SeasonAwardReviewSnapshot> Awards { get; private set; }
        public IReadOnlyList<SeasonAwardReviewSnapshot> PlayerAwards { get; private set; }
        public bool IsSettlementApplied { get; private set; }
        public IReadOnlyList<SettlementEntry> SettlementEntries { get; private set; }
        public IReadOnlyList<SeasonAbilityChangeSnapshot> AbilityChanges { get; private set; }
        public long SalaryIncome { get; private set; }
        public long BonusIncome { get; private set; }
        public int ContractEvaluationBonus { get; private set; }

        public static SeasonReviewSnapshot CaptureRegularSeason(CareerState career)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            LeagueState league = career.CurrentLeague;
            SeasonState season = league.CurrentSeason ??
                                 throw new InvalidOperationException("현재 시즌이 없습니다.");
            if (season.TeamRecords == null)
                throw new InvalidOperationException("확정할 정규시즌 순위가 없습니다.");

            var entries = new TeamStandingEntry[season.TeamRecords.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                TeamSeasonRecordState record = season.TeamRecords[index];
                entries[index] = new TeamStandingEntry(
                    record.TeamId,
                    record.Wins,
                    record.Losses,
                    record.RunsScored,
                    record.RunsAllowed,
                    record.FixedTiebreaker,
                    record.GetHeadToHeadEntries());
            }

            int[] orderedTeamIds = PostseasonBracket.SelectSeeds(entries, entries.Length);
            var standings = new SeasonStandingSnapshot[orderedTeamIds.Length];
            int playerTeamRank = 0;
            for (int index = 0; index < orderedTeamIds.Length; index++)
            {
                TeamSeasonRecordState record = season.GetTeamRecord(orderedTeamIds[index]);
                TeamState team = FindTeam(league, record.TeamId);
                standings[index] = new SeasonStandingSnapshot(
                    index + 1,
                    record.TeamId,
                    team.Name,
                    record.Wins,
                    record.Losses,
                    record.Ties);
                if (record.TeamId == career.MyPlayer.CurrentTeamId)
                    playerTeamRank = index + 1;
            }

            int postseasonSeed = 0;
            if (season.Postseason != null)
            {
                for (int index = 0; index < season.Postseason.SeedTeamIds.Count; index++)
                {
                    if (season.Postseason.SeedTeamIds[index] == career.MyPlayer.CurrentTeamId)
                    {
                        postseasonSeed = index + 1;
                        break;
                    }
                }
            }

            PlayerState player = career.MyPlayer;
            return new SeasonReviewSnapshot(
                season.SeasonId,
                season.Year,
                season.LeagueLevel,
                player.PlayerId,
                player.Name,
                player.PrimaryPosition,
                player.CurrentTeamId,
                FindTeam(league, player.CurrentTeamId).Name,
                playerTeamRank,
                postseasonSeed,
                standings,
                new PlayerSeasonReviewStatistics(
                    season.PlayerStatistics,
                    player.PrimaryPosition is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher));
        }

        /// <summary>이미 동결한 정규시즌 값 위에 확정된 포스트시즌과 수상 결과를 한 번만 붙인다.</summary>
        public void CompletePostseason(CareerState career)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (IsPostseasonFinalized)
                return;
            SeasonState season = career.CurrentLeague.CurrentSeason;
            PostseasonState postseason = season.Postseason ??
                                         throw new InvalidOperationException("포스트시즌 결과가 없습니다.");
            if (!postseason.IsCompleted)
                throw new InvalidOperationException("우승 구단이 확정되지 않았습니다.");

            ChampionTeamId = postseason.ChampionTeamId;
            ChampionTeamName = FindTeam(career.CurrentLeague, ChampionTeamId).Name;
            RunnerUpTeamId = postseason.RunnerUpTeamId;
            RunnerUpTeamName = RunnerUpTeamId > 0
                ? FindTeam(career.CurrentLeague, RunnerUpTeamId).Name
                : string.Empty;
            PlayerTeamPostseasonResult = postseason.PlayerTeamResult;
            PlayerTeamFinalRank = postseason.PlayerTeamResult switch
            {
                PlayerTeamPostseasonResult.Champion => 1,
                PlayerTeamPostseasonResult.RunnerUp => 2,
                PlayerTeamPostseasonResult.SemifinalElimination => 3,
                _ => PlayerTeamRank
            };

            var series = new PostseasonSeriesReviewSnapshot[postseason.Series.Count];
            int playerGameCount = CountPlayerTeamGames(postseason, PlayerTeamId);
            var playerGames = new PostseasonGameReviewSnapshot[playerGameCount];
            int playerGameIndex = 0;
            for (int seriesIndex = 0; seriesIndex < postseason.Series.Count; seriesIndex++)
            {
                PostseasonSeriesState source = postseason.Series[seriesIndex];
                series[seriesIndex] = new PostseasonSeriesReviewSnapshot(
                    source.Round,
                    source.HigherSeedTeamId,
                    FindTeam(career.CurrentLeague, source.HigherSeedTeamId).Name,
                    source.HigherSeedWins,
                    source.LowerSeedTeamId,
                    FindTeam(career.CurrentLeague, source.LowerSeedTeamId).Name,
                    source.LowerSeedWins,
                    source.WinnerTeamId);
                if (!source.IncludesTeam(PlayerTeamId))
                    continue;
                for (int gameIndex = 0; gameIndex < source.Games.Count; gameIndex++)
                {
                    ScheduledGameState game = source.Games[gameIndex];
                    if (!game.IsCompleted)
                        continue;
                    bool hasPlayerLine = TryFindPlayerLine(
                        season.PostseasonPlayerStatistics,
                        game.GameId,
                        out PlayerGameLogState playerLine);
                    playerGames[playerGameIndex++] = new PostseasonGameReviewSnapshot(
                        source.Round,
                        game.Round,
                        game.AwayTeamId,
                        FindTeam(career.CurrentLeague, game.AwayTeamId).Name,
                        game.AwayRuns,
                        game.HomeTeamId,
                        FindTeam(career.CurrentLeague, game.HomeTeamId).Name,
                        game.HomeRuns,
                        hasPlayerLine,
                        playerLine);
                }
            }
            PostseasonSeries = series;
            PlayerTeamPostseasonGames = playerGames;

            Awards = BuildAwards(career, season.Awards, playerOnly: false);
            PlayerAwards = BuildAwards(career, season.Awards, playerOnly: true);
            IsPostseasonFinalized = true;
        }

        /// <summary>지급 원장과 실제 성장 전후 값을 정산 직후 스냅샷에 고정한다.</summary>
        public void CompleteSettlement(
            SeasonSettlementState settlement,
            int[] abilitiesBefore,
            int[] abilitiesAfter)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (abilitiesBefore == null || abilitiesAfter == null ||
                abilitiesBefore.Length != abilitiesAfter.Length ||
                abilitiesBefore.Length != (int)PlayerAbility.Count)
            {
                throw new ArgumentException("모든 능력치의 정산 전후 값이 필요합니다.");
            }
            if (IsSettlementApplied)
                return;

            var entries = new SettlementEntry[settlement.Entries.Count];
            for (int index = 0; index < entries.Length; index++)
                entries[index] = settlement.Entries[index];
            var changes = new SeasonAbilityChangeSnapshot[abilitiesBefore.Length];
            for (int index = 0; index < changes.Length; index++)
            {
                changes[index] = new SeasonAbilityChangeSnapshot(
                    (PlayerAbility)index,
                    abilitiesBefore[index],
                    abilitiesAfter[index]);
            }

            SettlementEntries = entries;
            AbilityChanges = changes;
            SalaryIncome = settlement.SalaryIncome;
            BonusIncome = settlement.BonusIncome;
            ContractEvaluationBonus = settlement.ContractEvaluationBonus;
            IsSettlementApplied = true;
        }

        private static int CountPlayerTeamGames(PostseasonState postseason, int playerTeamId)
        {
            int count = 0;
            for (int index = 0; index < postseason.Series.Count; index++)
            {
                PostseasonSeriesState series = postseason.Series[index];
                if (!series.IncludesTeam(playerTeamId))
                    continue;
                for (int gameIndex = 0; gameIndex < series.Games.Count; gameIndex++)
                {
                    if (series.Games[gameIndex].IsCompleted)
                        count++;
                }
            }
            return count;
        }

        private static bool TryFindPlayerLine(
            PlayerSeasonStatisticsState statistics,
            int gameId,
            out PlayerGameLogState result)
        {
            if (statistics != null)
            {
                for (int index = 0; index < statistics.RecentGames.Count; index++)
                {
                    if (statistics.RecentGames[index].GameId != gameId)
                        continue;
                    result = statistics.RecentGames[index];
                    return true;
                }
            }
            result = default;
            return false;
        }

        private static SeasonAwardReviewSnapshot[] BuildAwards(
            CareerState career,
            SeasonAwardsState awards,
            bool playerOnly)
        {
            if (awards == null)
                return Array.Empty<SeasonAwardReviewSnapshot>();
            int count = 0;
            for (int index = 0; index < awards.Results.Count; index++)
            {
                if (!playerOnly || awards.Results[index].IncludesWinner(career.MyPlayer.PlayerId))
                    count++;
            }

            var result = new SeasonAwardReviewSnapshot[count];
            int target = 0;
            for (int index = 0; index < awards.Results.Count; index++)
            {
                SeasonAwardResultState award = awards.Results[index];
                bool isPlayerWinner = award.IncludesWinner(career.MyPlayer.PlayerId);
                if (playerOnly && !isPlayerWinner)
                    continue;
                AwardCandidateResult winner = FindWinner(award);
                int winnerTeamId = winner?.TeamId ?? 0;
                result[target++] = new SeasonAwardReviewSnapshot(
                    award.AwardId,
                    SeasonAwardNameFormatter.GetLabel(award.Category, award.Position),
                    award.Category,
                    award.WinnerPlayerId,
                    winner?.PlayerName ?? string.Empty,
                    winnerTeamId,
                    winnerTeamId > 0 ? FindTeam(career.CurrentLeague, winnerTeamId).Name : string.Empty,
                    isPlayerWinner);
            }
            Array.Sort(result, CompareAwards);
            return result;
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

        private static int CompareAwards(SeasonAwardReviewSnapshot left, SeasonAwardReviewSnapshot right)
        {
            int priority = GetAwardRevealPriority(left.Category).CompareTo(GetAwardRevealPriority(right.Category));
            return priority != 0 ? priority : string.CompareOrdinal(left.AwardId, right.AwardId);
        }

        private static int GetAwardRevealPriority(AwardCategory category)
        {
            return category switch
            {
                AwardCategory.PostseasonMvp => 0,
                AwardCategory.BattingAverage or AwardCategory.HomeRun or AwardCategory.RunsBattedIn or
                    AwardCategory.StolenBase or AwardCategory.EarnedRunAverage or AwardCategory.Win or
                    AwardCategory.Strikeout or AwardCategory.Save => 1,
                AwardCategory.GoldGlove => 2,
                AwardCategory.RookieOfYear => 3,
                AwardCategory.RegularSeasonMvp => 4,
                _ => 5
            };
        }

        private static TeamState FindTeam(LeagueState league, int teamId)
        {
            for (int index = 0; index < league.Teams.Count; index++)
            {
                if (league.Teams[index].TeamId == teamId)
                    return league.Teams[index];
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }
    }

    /// <summary>수상 명칭을 결산 UI와 뉴스가 같은 규칙으로 표시하게 한다.</summary>
    public static class SeasonAwardNameFormatter
    {
        public static string GetLabel(AwardCategory category, PlayerPosition position = PlayerPosition.Unknown)
        {
            if (category == AwardCategory.GoldGlove)
                return $"{position} 골든글러브";
            return category switch
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
                _ => category.ToString()
            };
        }
    }
}
