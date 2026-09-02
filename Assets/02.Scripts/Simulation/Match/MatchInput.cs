using System;
using Baseball.Core.Rules;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Match
{
    /// <summary>
    /// 동일 Seed 재현에 필요한 경기 식별자와 두 팀 입력을 불변으로 보관한다.
    /// </summary>
    public sealed class MatchInput
    {
        /// <summary>
        /// 한 경기의 잠금된 입력을 생성한다.
        /// </summary>
        public MatchInput(int seasonId, int gameId, ulong randomSeed, Team awayTeam, Team homeTeam)
            : this(seasonId, gameId, randomSeed, awayTeam, homeTeam, requiresWinner: false)
        {
        }

        /// <summary>
        /// 정규 시즌 무승부 허용 여부까지 포함해 한 경기의 잠금된 입력을 생성한다.
        /// </summary>
        public MatchInput(
            int seasonId,
            int gameId,
            ulong randomSeed,
            Team awayTeam,
            Team homeTeam,
            bool requiresWinner,
            HistoricalMatchConfiguration historicalConfiguration = null)
        {
            if (seasonId <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonId));
            if (gameId <= 0)
                throw new ArgumentOutOfRangeException(nameof(gameId));

            SeasonId = seasonId;
            GameId = gameId;
            RandomSeed = randomSeed;
            AwayTeam = awayTeam ?? throw new ArgumentNullException(nameof(awayTeam));
            HomeTeam = homeTeam ?? throw new ArgumentNullException(nameof(homeTeam));
            RequiresWinner = requiresWinner;
            RulesVersion = SimulationRulesVersion.DetailedV2;
            VersionStamp = SimulationVersionStamp.CreateCurrent(balanceVersion: 0);
            Rules = MatchRules.CreateDefault(requiresWinner);
            HistoricalConfiguration = historicalConfiguration;
            AwayRoster = MatchRosterSnapshot.FromTeam(AwayTeam);
            HomeRoster = MatchRosterSnapshot.FromTeam(HomeTeam);

            if (AwayTeam.TeamId == HomeTeam.TeamId)
                throw new ArgumentException("서로 다른 두 팀이 경기해야 합니다.", nameof(homeTeam));
        }

        /// <summary>
        /// 세부 경기 V2 로스터와 리그 규칙을 직접 잠가 경기 입력을 생성한다.
        /// </summary>
        public MatchInput(
            int seasonId,
            int gameId,
            ulong randomSeed,
            MatchRosterSnapshot awayRoster,
            MatchRosterSnapshot homeRoster,
            MatchRules rules,
            SimulationRulesVersion rulesVersion = SimulationRulesVersion.DetailedV2,
            SimulationVersionStamp? versionStamp = null,
            HistoricalMatchConfiguration historicalConfiguration = null)
        {
            if (seasonId <= 0) throw new ArgumentOutOfRangeException(nameof(seasonId));
            if (gameId <= 0) throw new ArgumentOutOfRangeException(nameof(gameId));
            SeasonId = seasonId;
            GameId = gameId;
            RandomSeed = randomSeed;
            AwayRoster = awayRoster ?? throw new ArgumentNullException(nameof(awayRoster));
            HomeRoster = homeRoster ?? throw new ArgumentNullException(nameof(homeRoster));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            HistoricalConfiguration = historicalConfiguration;
            RulesVersion = rulesVersion;
            VersionStamp = versionStamp ?? SimulationVersionStamp.CreateCurrent(
                balanceVersion: 0,
                rulesVersion: (int)rulesVersion);
            RequiresWinner = rules.ExtraInningPolicy != ExtraInningPolicy.DrawAtLimit;
            AwayTeam = AwayRoster.ToCompatibilityTeam();
            HomeTeam = HomeRoster.ToCompatibilityTeam();
            if (AwayRoster.TeamId == HomeRoster.TeamId)
                throw new ArgumentException("서로 다른 두 팀이 경기해야 합니다.", nameof(homeRoster));
        }

        public int SeasonId { get; }
        public int GameId { get; }
        public ulong RandomSeed { get; }
        public Team AwayTeam { get; }
        public Team HomeTeam { get; }
        public bool RequiresWinner { get; }
        public MatchRosterSnapshot AwayRoster { get; }
        public MatchRosterSnapshot HomeRoster { get; }
        public MatchRules Rules { get; }
        public HistoricalMatchConfiguration HistoricalConfiguration { get; }
        public SimulationRulesVersion RulesVersion { get; }
        public SimulationVersionStamp VersionStamp { get; }
    }
}
