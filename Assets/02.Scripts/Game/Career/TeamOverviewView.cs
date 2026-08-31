using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 구단 로스터에서 감독 AI가 부여한 현재 깊이 역할을 구분한다.
    /// </summary>
    public enum TeamRosterRole
    {
        Starting,
        Rotation,
        Bullpen,
        Competition,
        Backup
    }

    /// <summary>
    /// 구단 화면의 로스터와 기용 패널이 공유하는 선수 한 줄이다.
    /// </summary>
    public readonly struct TeamRosterPlayerView
    {
        public TeamRosterPlayerView(
            int playerId,
            string name,
            PlayerPosition position,
            int overall,
            TeamRosterRole rosterRole,
            bool isMyPlayer,
            bool isInNextGamePlan,
            bool hasCondition,
            int condition,
            bool hasBattingRecord,
            double battingAverage,
            bool hasPitchingRecord,
            double earnedRunAverage)
        {
            PlayerId = playerId;
            Name = name;
            Position = position;
            Overall = overall;
            RosterRole = rosterRole;
            IsMyPlayer = isMyPlayer;
            IsInNextGamePlan = isInNextGamePlan;
            HasCondition = hasCondition;
            Condition = condition;
            HasBattingRecord = hasBattingRecord;
            BattingAverage = battingAverage;
            HasPitchingRecord = hasPitchingRecord;
            EarnedRunAverage = earnedRunAverage;
        }

        public int PlayerId { get; }
        public string Name { get; }
        public PlayerPosition Position { get; }
        public int Overall { get; }
        public TeamRosterRole RosterRole { get; }
        public bool IsMyPlayer { get; }
        public bool IsInNextGamePlan { get; }
        public bool HasCondition { get; }
        public int Condition { get; }
        public bool HasBattingRecord { get; }
        public double BattingAverage { get; }
        public bool HasPitchingRecord { get; }
        public double EarnedRunAverage { get; }
    }

    /// <summary>
    /// 다음 경기에서 실제 Match 입력에 들어갈 타순 한 자리다.
    /// </summary>
    public readonly struct TeamLineupSlotView
    {
        public TeamLineupSlotView(int battingOrder, PlayerPosition position, TeamRosterPlayerView player)
        {
            BattingOrder = battingOrder;
            Position = position;
            Player = player;
        }

        public int BattingOrder { get; }
        public PlayerPosition Position { get; }
        public TeamRosterPlayerView Player { get; }
    }

    /// <summary>
    /// 구단 화면이 한 번의 Render에서 소비하는 읽기 전용 구단 정보다.
    /// </summary>
    public sealed class TeamOverviewView
    {
        public int TeamId { get; internal set; }
        public string TeamName { get; internal set; }
        public TeamColor PrimaryColor { get; internal set; }
        public int EmblemId { get; internal set; }
        public TeamArchetypeProfile Archetype { get; internal set; }
        public int SeasonYear { get; internal set; }
        public LeagueLevel LeagueLevel { get; internal set; }
        public int TeamRank { get; internal set; }
        public int Wins { get; internal set; }
        public int Losses { get; internal set; }
        public int Ties { get; internal set; }
        public int RunsScored { get; internal set; }
        public int RunsAllowed { get; internal set; }
        public int MyPlayerId { get; internal set; }
        public PlayerPosition MyPlayerPosition { get; internal set; }
        public ExpectedRole MyPlayerExpectedRole { get; internal set; }
        public bool HasNextGamePlan { get; internal set; }
        public int NextGameRound { get; internal set; }
        public PlayerGameRole PlannedPlayerRole { get; internal set; }
        public int MyPlayerBattingOrder { get; internal set; }
        public int FieldPlayerOverall { get; internal set; }
        public int StartingPitcherOverall { get; internal set; }
        public int ReliefPitcherOverall { get; internal set; }
        public TeamRosterPlayerView[] Roster { get; internal set; }
        public TeamLineupSlotView[] StartingLineup { get; internal set; }
        public TeamRosterPlayerView[] StartingRotation { get; internal set; }
        public TeamRosterPlayerView[] Bullpen { get; internal set; }
        public TradePreference TradePreference { get; internal set; }
        public bool IsOnTradeBlock { get; internal set; }
        public int TradeDeadlineGameIndex { get; internal set; }
        public int CurrentTeamGameIndex { get; internal set; }
        public TradeInterestRecord[] TradeInterests { get; internal set; }
        public string TopTradeInterestTeamName { get; internal set; }
        public bool CanChangeTradePreference { get; internal set; }
    }
}
