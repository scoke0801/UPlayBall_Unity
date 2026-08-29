using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Career.Narrative
{
    /// <summary>코칭스태프 시뮬레이션 없이 구단 운영 철학에서 파생하는 감독 코멘트 관점이다.</summary>
    public enum ManagerNarrativeStyle
    {
        Results,
        Development,
        Analytical,
        Conservative
    }

    /// <summary>경기 종료 사실을 문구 선택 전에 의미 단위로 분류한다.</summary>
    public enum NarrativeTag
    {
        TeamWin,
        TeamLoss,
        TeamTie,
        OneRunGame,
        LargeMarginGame,
        StarterAppearance,
        PinchHitAppearance,
        DidNotAppear,
        PitcherAppearance,
        HomeRun,
        MultiHit,
        ExtraBaseHit,
        Hit,
        WalkOnly,
        Hitless,
        MultiWalk,
        StrikeoutHeavy,
        NoStrikeout,
        RunBattedIn,
        RunScored,
        ScorelessPitching,
        HitStreak,
        HotStreak,
        HitlessStreak,
        SlumpEnded,
        RoleCompetition,
        RoleAtRisk,
        RoleStable,
        ManagerTrustUp,
        ManagerTrustDown,
        ManagerTrustStable,
        ConditionUp,
        ConditionDown,
        ConditionStable
    }

    /// <summary>경기 시작 순간의 순위·기록·최근 흐름을 고정해 종료 뒤 비교 기준으로 쓴다.</summary>
    public sealed class MatchNarrativeBaseline
    {
        private readonly PlayerGameLogState[] _recentGames;

        public MatchNarrativeBaseline(
            int seasonId,
            int gameId,
            int teamId,
            string teamName,
            int opponentTeamId,
            string opponentName,
            string playerName,
            PlayerPosition playerPosition,
            PlayerGameRole role,
            CompetitionScope competitionScope,
            int teamRank,
            double seasonBattingAverage,
            double seasonEarnedRunAverage,
            int condition,
            int managerTrust,
            IReadOnlyList<PlayerGameLogState> recentGames,
            ManagerNarrativeStyle managerStyle = ManagerNarrativeStyle.Analytical,
            ExpectedRole expectedRole = ExpectedRole.StartingCompetition)
        {
            SeasonId = seasonId;
            GameId = gameId;
            TeamId = teamId;
            TeamName = teamName ?? string.Empty;
            OpponentTeamId = opponentTeamId;
            OpponentName = opponentName ?? string.Empty;
            PlayerName = playerName ?? string.Empty;
            PlayerPosition = playerPosition;
            Role = role;
            CompetitionScope = competitionScope;
            TeamRank = teamRank;
            SeasonBattingAverage = seasonBattingAverage;
            SeasonEarnedRunAverage = seasonEarnedRunAverage;
            Condition = condition;
            ManagerTrust = managerTrust;
            ManagerStyle = managerStyle;
            ExpectedRole = expectedRole;
            _recentGames = Copy(recentGames);
        }

        public int SeasonId { get; }
        public int GameId { get; }
        public int TeamId { get; }
        public string TeamName { get; }
        public int OpponentTeamId { get; }
        public string OpponentName { get; }
        public string PlayerName { get; }
        public PlayerPosition PlayerPosition { get; }
        public PlayerGameRole Role { get; }
        public CompetitionScope CompetitionScope { get; }
        public int TeamRank { get; }
        public double SeasonBattingAverage { get; }
        public double SeasonEarnedRunAverage { get; }
        public int Condition { get; }
        public int ManagerTrust { get; }
        public ManagerNarrativeStyle ManagerStyle { get; }
        public ExpectedRole ExpectedRole { get; }
        public IReadOnlyList<PlayerGameLogState> RecentGames => _recentGames;

        private static PlayerGameLogState[] Copy(IReadOnlyList<PlayerGameLogState> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<PlayerGameLogState>();
            var result = new PlayerGameLogState[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }
    }

    /// <summary>경기 종료 시점의 사실과 선택된 문장을 함께 고정해 화면과 뉴스가 같은 해석을 쓴다.</summary>
    public sealed class MatchNarrativeSnapshot
    {
        private readonly NarrativeTag[] _tags;

        internal MatchNarrativeSnapshot(
            MatchNarrativeBaseline baseline,
            CareerGameAdvanceResult playerLine,
            int teamRankAfter,
            double seasonBattingAverageAfter,
            double seasonEarnedRunAverageAfter,
            int hitStreak,
            int hitlessStreak,
            int previousHitlessStreak,
            int recentGameCount,
            int recentAtBats,
            int recentHits,
            NarrativeTag[] tags,
            string headline,
            string performanceEvaluation,
            string performanceDetail,
            string gameImpact,
            string recentForm,
            string managerComment,
            string managerTrustReason,
            string conditionReason,
            string roleReason)
        {
            EventId = $"season_{baseline.SeasonId}_game_{baseline.GameId}_narrative";
            SeasonId = baseline.SeasonId;
            GameId = baseline.GameId;
            TeamId = baseline.TeamId;
            TeamName = baseline.TeamName;
            OpponentTeamId = baseline.OpponentTeamId;
            OpponentName = baseline.OpponentName;
            PlayerName = baseline.PlayerName;
            PlayerPosition = baseline.PlayerPosition;
            CompetitionScope = baseline.CompetitionScope;
            PlayerLine = playerLine;
            TeamRankBefore = baseline.TeamRank;
            TeamRankAfter = teamRankAfter;
            SeasonBattingAverageBefore = baseline.SeasonBattingAverage;
            SeasonBattingAverageAfter = seasonBattingAverageAfter;
            SeasonEarnedRunAverageBefore = baseline.SeasonEarnedRunAverage;
            SeasonEarnedRunAverageAfter = seasonEarnedRunAverageAfter;
            ManagerTrustBefore = playerLine.ManagerEvaluationBefore;
            ManagerTrustAfter = playerLine.ManagerEvaluationAfter;
            ConditionBefore = playerLine.ConditionBefore;
            ConditionAfter = playerLine.ConditionAfter;
            RoleBefore = baseline.Role;
            RoleAfter = baseline.Role;
            ManagerStyle = baseline.ManagerStyle;
            HitStreak = hitStreak;
            HitlessStreak = hitlessStreak;
            PreviousHitlessStreak = previousHitlessStreak;
            RecentGameCount = recentGameCount;
            RecentAtBats = recentAtBats;
            RecentHits = recentHits;
            _tags = tags ?? Array.Empty<NarrativeTag>();
            Headline = headline ?? string.Empty;
            PerformanceEvaluation = performanceEvaluation ?? string.Empty;
            PerformanceDetail = performanceDetail ?? string.Empty;
            GameImpact = gameImpact ?? string.Empty;
            RecentForm = recentForm ?? string.Empty;
            ManagerComment = managerComment ?? string.Empty;
            ManagerTrustReason = managerTrustReason ?? string.Empty;
            ConditionReason = conditionReason ?? string.Empty;
            RoleReason = roleReason ?? string.Empty;
        }

        public string EventId { get; }
        public int SeasonId { get; }
        public int GameId { get; }
        public int TeamId { get; }
        public string TeamName { get; }
        public int OpponentTeamId { get; }
        public string OpponentName { get; }
        public string PlayerName { get; }
        public PlayerPosition PlayerPosition { get; }
        public CompetitionScope CompetitionScope { get; }
        public CareerGameAdvanceResult PlayerLine { get; }
        public int TeamRankBefore { get; }
        public int TeamRankAfter { get; }
        public double SeasonBattingAverageBefore { get; }
        public double SeasonBattingAverageAfter { get; }
        public double SeasonEarnedRunAverageBefore { get; }
        public double SeasonEarnedRunAverageAfter { get; }
        public int ManagerTrustBefore { get; }
        public int ManagerTrustAfter { get; }
        public int ConditionBefore { get; }
        public int ConditionAfter { get; }
        public PlayerGameRole RoleBefore { get; }
        public PlayerGameRole RoleAfter { get; }
        public ManagerNarrativeStyle ManagerStyle { get; }
        public int HitStreak { get; }
        public int HitlessStreak { get; }
        public int PreviousHitlessStreak { get; }
        public int RecentGameCount { get; }
        public int RecentAtBats { get; }
        public int RecentHits { get; }
        public IReadOnlyList<NarrativeTag> Tags => _tags;
        public string Headline { get; }
        public string PerformanceEvaluation { get; }
        public string PerformanceDetail { get; }
        public string GameImpact { get; }
        public string RecentForm { get; }
        public string ManagerComment { get; }
        public string ManagerTrustReason { get; }
        public string ConditionReason { get; }
        public string RoleReason { get; }

        public bool HasTag(NarrativeTag tag)
        {
            for (int index = 0; index < _tags.Length; index++)
            {
                if (_tags[index] == tag)
                    return true;
            }
            return false;
        }
    }

    /// <summary>라이브 상태를 참조하지 않고 경기 시작 기준과 확정 결과만으로 서사 스냅샷을 만든다.</summary>
    public static class MatchNarrativeService
    {
        private const int LargeMarginRuns = 5;
        private const int HeavyStrikeoutCount = 2;

        public static MatchNarrativeBaseline CaptureBaseline(
            CareerState career,
            ScheduledGameState game,
            PlayerGameRole role,
            CompetitionScope competitionScope)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (game == null) throw new ArgumentNullException(nameof(game));

            SeasonState season = career.CurrentLeague.CurrentSeason;
            int teamId = career.MyPlayer.CurrentTeamId;
            int opponentTeamId = game.HomeTeamId == teamId ? game.AwayTeamId : game.HomeTeamId;
            TeamState team = FindTeam(career, teamId);
            TeamState opponent = FindTeam(career, opponentTeamId);
            PlayerSeasonStatisticsState statistics = GetStatistics(season, competitionScope);
            return new MatchNarrativeBaseline(
                season.SeasonId,
                game.GameId,
                teamId,
                team.Name,
                opponentTeamId,
                opponent.Name,
                career.MyPlayer.Name,
                career.MyPlayer.PrimaryPosition,
                role,
                competitionScope,
                competitionScope == CompetitionScope.RegularSeason ? CalculateRank(season, teamId) : 0,
                statistics?.BattingAverage ?? 0d,
                statistics?.EarnedRunAverage ?? 0d,
                career.MyPlayer.Condition,
                career.MyPlayer.ManagerEvaluation,
                statistics?.RecentGames,
                ResolveManagerStyle(team.Archetype.Archetype),
                career.CurrentExpectedRole);
        }

        public static MatchNarrativeSnapshot CreateSnapshot(
            CareerState career,
            MatchNarrativeBaseline baseline,
            CareerGameAdvanceResult result)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            if (baseline.GameId != result.GameId)
                throw new InvalidOperationException("경기 내러티브 기준과 결과의 GameId가 다릅니다.");

            SeasonState season = career.CurrentLeague.CurrentSeason;
            PlayerSeasonStatisticsState statistics = GetStatistics(season, baseline.CompetitionScope);
            int previousHitlessStreak = CountPreviousStreak(baseline.RecentGames, requireHit: false);
            int previousHitStreak = CountPreviousStreak(baseline.RecentGames, requireHit: true);
            bool isPitcher = IsPitcher(baseline.PlayerPosition);
            bool didAppear = DidAppear(result, isPitcher);
            int hitlessStreak = !isPitcher && didAppear && result.Hits == 0
                ? previousHitlessStreak + 1
                : 0;
            int hitStreak = !isPitcher && didAppear && result.Hits > 0
                ? previousHitStreak + 1
                : 0;
            int recentGames = !isPitcher && didAppear ? 1 : 0;
            int recentAtBats = !isPitcher && didAppear ? result.AtBats : 0;
            int recentHits = !isPitcher && didAppear ? result.Hits : 0;
            if (!isPitcher)
                AccumulateRecentFive(baseline.RecentGames, ref recentGames, ref recentAtBats, ref recentHits);

            NarrativeTag[] tags = ResolveTags(
                result,
                baseline,
                isPitcher,
                didAppear,
                hitStreak,
                hitlessStreak,
                previousHitlessStreak);
            return new MatchNarrativeSnapshot(
                baseline,
                result,
                baseline.CompetitionScope == CompetitionScope.RegularSeason
                    ? CalculateRank(season, baseline.TeamId)
                    : 0,
                statistics?.BattingAverage ?? baseline.SeasonBattingAverage,
                statistics?.EarnedRunAverage ?? baseline.SeasonEarnedRunAverage,
                hitStreak,
                hitlessStreak,
                previousHitlessStreak,
                recentGames,
                recentAtBats,
                recentHits,
                tags,
                BuildHeadline(result, baseline, didAppear, isPitcher),
                BuildPerformance(result, baseline, isPitcher, didAppear),
                BuildPerformanceDetail(result, baseline, isPitcher, didAppear),
                BuildGameImpact(result, didAppear, isPitcher),
                BuildRecentForm(result, hitStreak, hitlessStreak, previousHitlessStreak),
                BuildManagerComment(result, baseline, didAppear, isPitcher),
                BuildManagerTrustReason(result, baseline, didAppear, isPitcher),
                result.ConditionAfter == result.ConditionBefore
                    ? "이번 경기로 컨디션 변화 없음"
                    : didAppear ? "경기 출전" : "경기 휴식",
                $"{GetRoleLabel(baseline.Role)} 유지");
        }

        private static NarrativeTag[] ResolveTags(
            CareerGameAdvanceResult result,
            MatchNarrativeBaseline baseline,
            bool isPitcher,
            bool didAppear,
            int hitStreak,
            int hitlessStreak,
            int previousHitlessStreak)
        {
            var tags = new List<NarrativeTag>(16);
            tags.Add(result.TeamRuns > result.OpponentRuns
                ? NarrativeTag.TeamWin
                : result.TeamRuns < result.OpponentRuns ? NarrativeTag.TeamLoss : NarrativeTag.TeamTie);
            int margin = Math.Abs(result.TeamRuns - result.OpponentRuns);
            if (margin == 1) tags.Add(NarrativeTag.OneRunGame);
            if (margin >= LargeMarginRuns) tags.Add(NarrativeTag.LargeMarginGame);
            if (baseline.Role is PlayerGameRole.StartingBatter or PlayerGameRole.StartingPitcher)
                tags.Add(NarrativeTag.StarterAppearance);
            if (!didAppear) tags.Add(NarrativeTag.DidNotAppear);
            if (baseline.Role == PlayerGameRole.Bench && didAppear)
                tags.Add(NarrativeTag.PinchHitAppearance);

            if (isPitcher)
            {
                if (didAppear) tags.Add(NarrativeTag.PitcherAppearance);
                if (result.OutsRecorded > 0 && result.EarnedRuns == 0)
                    tags.Add(NarrativeTag.ScorelessPitching);
            }
            else if (didAppear)
            {
                if (result.HomeRuns > 0) tags.Add(NarrativeTag.HomeRun);
                if (result.Hits >= 2) tags.Add(NarrativeTag.MultiHit);
                if (result.Doubles + result.Triples + result.HomeRuns > 0)
                    tags.Add(NarrativeTag.ExtraBaseHit);
                if (result.Hits > 0) tags.Add(NarrativeTag.Hit);
                if (result.Hits == 0) tags.Add(NarrativeTag.Hitless);
                if (result.Hits == 0 && result.Walks + result.HitByPitches > 0)
                    tags.Add(NarrativeTag.WalkOnly);
                if (result.Walks + result.HitByPitches >= 2) tags.Add(NarrativeTag.MultiWalk);
                if (result.Strikeouts >= HeavyStrikeoutCount) tags.Add(NarrativeTag.StrikeoutHeavy);
                if (result.Strikeouts == 0) tags.Add(NarrativeTag.NoStrikeout);
                if (result.RunsBattedIn > 0) tags.Add(NarrativeTag.RunBattedIn);
                if (result.Runs > 0) tags.Add(NarrativeTag.RunScored);
                if (hitStreak >= 2) tags.Add(NarrativeTag.HitStreak);
                if (hitStreak >= 4) tags.Add(NarrativeTag.HotStreak);
                if (hitlessStreak >= 3) tags.Add(NarrativeTag.HitlessStreak);
                if (result.Hits > 0 && previousHitlessStreak >= 3) tags.Add(NarrativeTag.SlumpEnded);
            }

            if (baseline.ExpectedRole == ExpectedRole.StartingCompetition &&
                result.ManagerEvaluationAfter <= 60)
            {
                tags.Add(NarrativeTag.RoleCompetition);
                if (result.ManagerEvaluationAfter <= 55)
                    tags.Add(NarrativeTag.RoleAtRisk);
            }

            tags.Add(NarrativeTag.RoleStable);
            tags.Add(result.ManagerEvaluationAfter > result.ManagerEvaluationBefore
                ? NarrativeTag.ManagerTrustUp
                : result.ManagerEvaluationAfter < result.ManagerEvaluationBefore
                    ? NarrativeTag.ManagerTrustDown
                    : NarrativeTag.ManagerTrustStable);
            tags.Add(result.ConditionAfter > result.ConditionBefore
                ? NarrativeTag.ConditionUp
                : result.ConditionAfter < result.ConditionBefore
                    ? NarrativeTag.ConditionDown
                    : NarrativeTag.ConditionStable);
            return tags.ToArray();
        }

        private static string BuildHeadline(
            CareerGameAdvanceResult result,
            MatchNarrativeBaseline baseline,
            bool didAppear,
            bool isPitcher)
        {
            string score = $"{result.TeamRuns}-{result.OpponentRuns}";
            bool didWin = result.TeamRuns > result.OpponentRuns;
            bool didLose = result.TeamRuns < result.OpponentRuns;
            bool isStrong = IsStrongPerformance(result, isPitcher);
            if (Math.Abs(result.TeamRuns - result.OpponentRuns) == 1 && didLose &&
                !isPitcher && result.Hits == 0 && result.Walks + result.HitByPitches > 0)
            {
                return $"한 점 차 패배, 한 차례 출루에 그친 {baseline.PlayerName}.";
            }
            if (Math.Abs(result.TeamRuns - result.OpponentRuns) == 1)
            {
                return didWin
                    ? $"끝까지 이어진 한 점 승부, {baseline.TeamName}, {score} 승리."
                    : $"한 점이 승부를 갈랐다. {baseline.TeamName}, {baseline.OpponentName}에 {score} 패배.";
            }
            if (didWin && isStrong)
                return $"{baseline.PlayerName}의 활약, {baseline.TeamName}의 {score} 승리를 이끌었다.";
            if (didLose && isStrong)
                return $"{baseline.PlayerName}의 활약에도 {baseline.TeamName}, {baseline.OpponentName}에 {score} 패배.";
            if (didWin && didAppear && !isPitcher && result.Hits == 0)
                return $"{baseline.TeamName} 승리 속 {baseline.PlayerName}, 타석에서는 아쉬움을 남겼다.";
            if (didLose && didAppear && !isPitcher && result.Hits == 0)
                return $"{baseline.TeamName} 패배, {baseline.PlayerName}도 돌파구를 찾지 못했다.";
            if (!didAppear)
                return $"{baseline.TeamName}의 {score} 경기, 출전 기회를 기다린 {baseline.PlayerName}.";
            return didWin
                ? $"{baseline.TeamName}, {baseline.OpponentName}에 {score} 승리."
                : didLose
                    ? $"{baseline.TeamName}, {baseline.OpponentName}에 {score} 패배."
                    : $"{baseline.TeamName}과 {baseline.OpponentName}, {score} 무승부.";
        }

        private static string BuildPerformance(
            CareerGameAdvanceResult result,
            MatchNarrativeBaseline baseline,
            bool isPitcher,
            bool didAppear)
        {
            if (!didAppear)
            {
                return baseline.Role == PlayerGameRole.Bench
                    ? "벤치에서 출전 기회를 기다렸다."
                    : "감독의 기용 계획에 따라 오늘 경기는 휴식을 취했다.";
            }
            if (isPitcher)
            {
                string innings = FormatInnings(result.OutsRecorded);
                if (result.OutsRecorded >= 27 && result.EarnedRuns == 0)
                    return $"{innings}이닝 무실점으로 마운드를 끝까지 책임졌다.";
                if (result.EarnedRuns == 0)
                    return $"{innings}이닝 무실점으로 자신의 역할을 해냈다.";
                return $"{innings}이닝 {result.EarnedRuns}자책으로 등판을 마쳤다.";
            }
            if (baseline.Role == PlayerGameRole.Bench)
            {
                if (result.Hits > 0)
                    return "대타로 나선 기회에서 안타를 만들어냈다.";
                if (result.Walks + result.HitByPitches > 0)
                    return "대타 한 번의 기회에서 출루에 성공했다.";
                return "대타 기회를 결과로 연결하지 못했다.";
            }
            if (result.HomeRuns > 0)
                return $"홈런 {result.HomeRuns}개로 {result.RunsBattedIn}타점을 올렸다.";
            if (result.Hits >= 2)
                return $"안타 {result.Hits}개를 기록하며 여러 차례 공격 기회를 만들었다.";
            if (result.Hits == 1)
                return "안타 하나를 기록하며 출루했다.";
            int freePasses = result.Walks + result.HitByPitches;
            if (freePasses > 0)
            {
                string kind = result.Walks > 0 && result.HitByPitches == 0 ? "볼넷" : "사사구";
                return freePasses == 1
                    ? $"안타는 없었지만 {kind}으로 한 차례 출루했다."
                    : $"안타는 없었지만 {kind} {freePasses}개로 출루했다.";
            }
            if (result.Strikeouts >= HeavyStrikeoutCount)
                return $"삼진 {result.Strikeouts}개를 당하며 타석에서 돌파구를 찾지 못했다.";
            if (result.Strikeouts == 0)
                return "타구는 만들어냈지만 안타로 이어지지 않았다.";
            return "타석에서 돌파구를 찾지 못했다.";
        }

        private static string BuildPerformanceDetail(
            CareerGameAdvanceResult result,
            MatchNarrativeBaseline baseline,
            bool isPitcher,
            bool didAppear)
        {
            if (!didAppear)
                return baseline.Role == PlayerGameRole.Bench
                    ? "대기 명단에는 포함됐지만 경기에 나서지 않았다."
                    : "출전 기록 없이 다음 경기를 준비했다.";
            if (isPitcher)
                return result.Strikeouts > 0
                    ? $"삼진 {result.Strikeouts}개를 잡고 볼넷 {result.WalksAllowed}개를 허용했다."
                    : $"볼넷 {result.WalksAllowed}개를 허용하며 등판을 마쳤다.";
            if (result.Hits == 0 && result.Strikeouts == 0 && result.AtBats > 0)
                return "삼진 없이 승부를 이어갔으나 타구가 안타로 연결되지는 않았다.";
            if (result.Hits == 0 && result.Walks + result.HitByPitches > 0)
                return "타격 결과는 없었지만 출루로 공격 흐름을 이어갔다.";
            if (result.RunsBattedIn > 0)
                return $"주자 {result.RunsBattedIn}명을 홈으로 불러들이며 득점에 기여했다.";
            if (result.Runs > 0)
                return $"출루 뒤 {result.Runs}차례 홈을 밟았다.";
            return result.Hits > 0
                ? "안타로 공격에 힘을 보탰지만 추가 기록으로 이어지지는 않았다."
                : "결과가 따르지 않은 타석이 이어졌다.";
        }

        private static string BuildGameImpact(
            CareerGameAdvanceResult result,
            bool didAppear,
            bool isPitcher)
        {
            bool didWin = result.TeamRuns > result.OpponentRuns;
            bool didLose = result.TeamRuns < result.OpponentRuns;
            if (!didAppear)
                return didWin ? "팀은 승리를 챙겼다." : didLose ? "팀은 패배를 기록했다." : "팀은 무승부를 기록했다.";
            if (Math.Abs(result.TeamRuns - result.OpponentRuns) == 1 && !isPitcher &&
                result.Hits == 0 && result.Walks + result.HitByPitches > 0 && result.Runs == 0)
            {
                return "한 점 차 승부에서 만든 출루가 득점으로 이어지지 못한 점이 아쉬웠다.";
            }
            if (didWin && !isPitcher && result.Hits == 0)
                return "개인 기록은 아쉬웠지만 팀은 승리를 챙겼다.";
            if (didLose && IsStrongPerformance(result, isPitcher))
                return "개인 활약은 분명했지만 팀 패배를 막지는 못했다.";
            if (didWin && IsStrongPerformance(result, isPitcher))
                return "개인 수행이 팀 승리와 같은 방향으로 이어졌다.";
            if (didLose)
                return "팀 패배 속에서 다음 경기의 반전이 더 중요해졌다.";
            return didWin ? "팀 승리에 필요한 몫을 함께 나눴다." : "승부는 끝내 갈리지 않았다.";
        }

        private static string BuildRecentForm(
            CareerGameAdvanceResult result,
            int hitStreak,
            int hitlessStreak,
            int previousHitlessStreak)
        {
            if (result.Hits > 0 && previousHitlessStreak >= 3)
                return $"{previousHitlessStreak + 1}경기 만에 안타를 기록하며 침묵을 깼다.";
            if (hitlessStreak >= 3)
                return $"무안타 흐름이 {hitlessStreak}경기째 이어졌다.";
            if (hitStreak >= 2)
                return $"연속 안타 기록을 {hitStreak}경기로 늘렸다.";
            return string.Empty;
        }

        private static string BuildManagerComment(
            CareerGameAdvanceResult result,
            MatchNarrativeBaseline baseline,
            bool didAppear,
            bool isPitcher)
        {
            if (!didAppear)
                return "\"오늘은 준비를 이어갔다. 다음 기회를 지켜보겠다.\"";
            bool trustIncreased = result.ManagerEvaluationAfter > result.ManagerEvaluationBefore;
            bool trustDecreased = result.ManagerEvaluationAfter < result.ManagerEvaluationBefore;
            bool reachedBaseWithoutHit = !isPitcher && result.Hits == 0 &&
                                         result.Walks + result.HitByPitches > 0;
            return baseline.ManagerStyle switch
            {
                ManagerNarrativeStyle.Results => trustIncreased
                    ? "\"오늘은 필요한 결과를 만들었다. 다음 경기에서도 이어가야 한다.\""
                    : trustDecreased
                        ? "\"선발로 나섰다면 결과로 증명해야 한다. 다음 경기가 중요하다.\""
                        : reachedBaseWithoutHit
                            ? "\"출루는 기록했지만 선발에게는 안타도 필요하다.\""
                            : "\"역할은 유지한다. 다음 경기의 결과를 보겠다.\"",
                ManagerNarrativeStyle.Development => trustIncreased
                    ? "\"주어진 기회를 살렸다. 이 경험을 다음 경기로 이어가길 바란다.\""
                    : trustDecreased
                        ? "\"오늘 결과는 아쉽지만 준비할 기회는 남아 있다.\""
                        : "\"한 경기로 판단하지 않겠다. 다음 기회도 준비해라.\"",
                ManagerNarrativeStyle.Conservative => trustIncreased
                    ? "\"맡은 역할을 해냈다. 같은 모습을 꾸준히 보여줘야 한다.\""
                    : trustDecreased
                        ? "\"같은 흐름이 반복되면 기용을 다시 검토할 수밖에 없다.\""
                        : "\"현재 역할은 유지한다. 안정적인 결과가 더 필요하다.\"",
                _ => BuildAnalyticalManagerComment(result, trustIncreased, trustDecreased, reachedBaseWithoutHit)
            };
        }

        private static string BuildAnalyticalManagerComment(
            CareerGameAdvanceResult result,
            bool trustIncreased,
            bool trustDecreased,
            bool reachedBaseWithoutHit)
        {
            if (trustIncreased)
                return "\"기록으로 확인되는 기여가 있었다. 다음 경기에서도 같은 내용을 기대한다.\"";
            if (trustDecreased)
                return "\"이번 경기 지표는 기대에 미치지 못했다. 다음 출전을 다시 평가하겠다.\"";
            if (reachedBaseWithoutHit)
            {
                return result.Strikeouts == 0
                    ? "\"삼진 없이 한 차례 출루했다. 결과보다 내용은 나쁘지 않았다.\""
                    : "\"안타는 없었지만 출루 한 번은 기록했다.\"";
            }
            return "\"이번 경기 기록만으로 역할을 바꾸지는 않겠다.\"";
        }

        private static string BuildManagerTrustReason(
            CareerGameAdvanceResult result,
            MatchNarrativeBaseline baseline,
            bool didAppear,
            bool isPitcher)
        {
            if (result.ManagerEvaluationAfter == result.ManagerEvaluationBefore)
                return didAppear ? "이번 경기로 신뢰도 변화 없음" : "출장 기회 없음";
            if (result.ManagerEvaluationAfter > result.ManagerEvaluationBefore)
            {
                if (isPitcher) return result.EarnedRuns == 0 ? "무실점 투구" : "등판 내용 반영";
                if (result.HomeRuns > 0) return "홈런 기록";
                if (result.Hits >= 2) return "멀티히트 기록";
                return "출전 결과 반영";
            }
            if (!isPitcher && result.Hits == 0)
                return baseline.Role == PlayerGameRole.StartingBatter ? "선발 출전에서 무안타" : "대타 기회에서 무안타";
            return isPitcher ? "등판 실점 반영" : "출전 결과 반영";
        }

        private static bool IsStrongPerformance(CareerGameAdvanceResult result, bool isPitcher)
        {
            return isPitcher
                ? result.OutsRecorded >= 18 && result.EarnedRuns <= 2 ||
                  result.Role == PlayerGameRole.ReliefPitcher && result.OutsRecorded > 0 && result.EarnedRuns == 0
                : result.HomeRuns > 0 || result.Hits >= 2 || result.RunsBattedIn >= 2;
        }

        private static bool DidAppear(CareerGameAdvanceResult result, bool isPitcher)
        {
            return isPitcher
                ? result.OutsRecorded > 0 || result.Role is PlayerGameRole.StartingPitcher or PlayerGameRole.ReliefPitcher
                : result.PlateAppearances > 0;
        }

        private static int CountPreviousStreak(IReadOnlyList<PlayerGameLogState> games, bool requireHit)
        {
            int streak = 0;
            if (games == null) return streak;
            for (int index = games.Count - 1; index >= 0; index--)
            {
                PlayerGameLogState game = games[index];
                if (!DidBatterAppear(game))
                    continue;
                bool matches = requireHit ? game.Hits > 0 : game.Hits == 0;
                if (!matches)
                    break;
                streak++;
            }
            return streak;
        }

        private static bool DidBatterAppear(PlayerGameLogState game)
        {
            return game.Role == PlayerGameRole.StartingBatter || game.AtBats > 0 ||
                   game.Walks > 0 || game.HitByPitches > 0;
        }

        private static void AccumulateRecentFive(
            IReadOnlyList<PlayerGameLogState> games,
            ref int gameCount,
            ref int atBats,
            ref int hits)
        {
            if (games == null) return;
            for (int index = games.Count - 1; index >= 0 && gameCount < 5; index--)
            {
                PlayerGameLogState game = games[index];
                if (!DidBatterAppear(game))
                    continue;
                gameCount++;
                atBats += game.AtBats;
                hits += game.Hits;
            }
        }

        private static PlayerSeasonStatisticsState GetStatistics(
            SeasonState season,
            CompetitionScope competitionScope)
        {
            return competitionScope == CompetitionScope.Postseason
                ? season.PostseasonPlayerStatistics
                : season.PlayerStatistics;
        }

        private static bool IsPitcher(PlayerPosition position)
        {
            return position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
        }

        private static ManagerNarrativeStyle ResolveManagerStyle(TeamArchetype archetype)
        {
            return archetype switch
            {
                TeamArchetype.Development => ManagerNarrativeStyle.Development,
                TeamArchetype.Contender => ManagerNarrativeStyle.Results,
                TeamArchetype.SmallMarket => ManagerNarrativeStyle.Conservative,
                _ => ManagerNarrativeStyle.Analytical
            };
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

        private static int CalculateRank(SeasonState season, int teamId)
        {
            if (season.TeamRecords == null)
                return 0;
            TeamSeasonRecordState target = season.GetTeamRecord(teamId);
            int rank = 1;
            for (int index = 0; index < season.TeamRecords.Count; index++)
            {
                TeamSeasonRecordState other = season.TeamRecords[index];
                if (other.TeamId == target.TeamId)
                    continue;
                if (other.WinningPercentage > target.WinningPercentage ||
                    Math.Abs(other.WinningPercentage - target.WinningPercentage) < 0.000001d &&
                    (other.Wins > target.Wins ||
                     other.Wins == target.Wins && other.FixedTiebreaker < target.FixedTiebreaker))
                {
                    rank++;
                }
            }
            return rank;
        }

        private static string GetRoleLabel(PlayerGameRole role)
        {
            return role switch
            {
                PlayerGameRole.StartingBatter => "선발",
                PlayerGameRole.Bench => "대타·벤치",
                PlayerGameRole.StartingPitcher => "선발 로테이션",
                PlayerGameRole.ReliefPitcher => "불펜",
                _ => "휴식"
            };
        }

        private static string FormatInnings(int outs) => $"{outs / 3}.{outs % 3}";
    }
}
