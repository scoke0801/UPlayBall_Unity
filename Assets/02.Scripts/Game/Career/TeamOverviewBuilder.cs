using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Growth;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 세이브 상태와 고정된 다음 경기 기용 계획을 구단 화면용 읽기 모델로 투영한다.
    /// </summary>
    public sealed class TeamOverviewBuilder
    {
        private readonly PlayerValueEvaluator _playerValueEvaluator;
        private readonly ManagerLineupAi _managerLineupAi;
        private readonly SkillBoardService _skillBoardService;

        public TeamOverviewBuilder(BalanceTable balance)
        {
            _playerValueEvaluator = new PlayerValueEvaluator(balance.PlayerEvaluation);
            _managerLineupAi = new ManagerLineupAi(balance.ManagerLineup);
            _skillBoardService = new SkillBoardService(
                balance.Growth.SkillBoard,
                balance.Growth.SkillBlocks);
        }

        /// <summary>
        /// 실제 Match 입력과 같은 선발 선택 규칙을 사용해 열람 전용 구단 정보를 만든다.
        /// </summary>
        public TeamOverviewView Build(CareerState career)
        {
            if (career == null)
                throw new ArgumentNullException(nameof(career));

            PlayerState myPlayer = career.MyPlayer;
            TeamState team = GetTeam(career, myPlayer.CurrentTeamId);
            SeasonState season = career.CurrentLeague.CurrentSeason;
            if (season == null)
                throw new InvalidOperationException("진행 중인 시즌이 없습니다.");
            Player stableMyPlayer = myPlayer.ToPlayer(_skillBoardService);

            ScheduledGameState nextGame = season.Schedule?.GetNextGameForTeam(team.TeamId);
            PlayerGameRole plannedRole = nextGame?.HasPlayerRolePlan == true
                ? nextGame.PlannedPlayerRole
                : PlayerGameRole.Inactive;
            bool hasNextGamePlan = nextGame?.HasPlayerRolePlan == true;
            int nextStartingPitcherId = GetNextPitcherId(
                team,
                myPlayer,
                nextGame,
                plannedRole,
                PlayerPosition.StartingPitcher);
            int nextReliefPitcherId = GetNextPitcherId(
                team,
                myPlayer,
                nextGame,
                plannedRole,
                PlayerPosition.ReliefPitcher);
            TeamRosterPlayerView[] roster = BuildRoster(
                career,
                team,
                myPlayer,
                stableMyPlayer,
                season,
                hasNextGamePlan,
                plannedRole,
                nextStartingPitcherId,
                nextReliefPitcherId);
            Lineup lineup = CareerLineupPlan.BuildStartingLineup(
                team,
                stableMyPlayer,
                plannedRole,
                _managerLineupAi);
            TeamLineupSlotView[] startingLineup = BuildStartingLineup(lineup, roster);
            TeamSeasonRecordState record = season.GetTeamRecord(team.TeamId);

            return new TeamOverviewView
            {
                TeamId = team.TeamId,
                TeamName = team.Name,
                PrimaryColor = team.PrimaryColor,
                Archetype = team.Archetype,
                SeasonYear = season.Year,
                LeagueLevel = season.LeagueLevel,
                TeamRank = CalculateRank(season, record),
                Wins = record?.Wins ?? 0,
                Losses = record?.Losses ?? 0,
                Ties = record?.Ties ?? 0,
                RunsScored = record?.RunsScored ?? 0,
                RunsAllowed = record?.RunsAllowed ?? 0,
                MyPlayerId = myPlayer.PlayerId,
                MyPlayerPosition = myPlayer.PrimaryPosition,
                MyPlayerExpectedRole = career.CurrentExpectedRole,
                HasNextGamePlan = hasNextGamePlan,
                NextGameRound = nextGame?.Round ?? 0,
                PlannedPlayerRole = plannedRole,
                MyPlayerBattingOrder = CareerLineupPlan.GetPlayerBattingOrder(lineup, myPlayer.PlayerId),
                FieldPlayerOverall = CalculateAverage(roster, PlayerGroup.FieldPlayer),
                StartingPitcherOverall = CalculateAverage(roster, PlayerGroup.StartingPitcher),
                ReliefPitcherOverall = CalculateAverage(roster, PlayerGroup.ReliefPitcher),
                Roster = roster,
                StartingLineup = startingLineup,
                StartingRotation = FilterPitchers(roster, PlayerPosition.StartingPitcher),
                Bullpen = FilterPitchers(roster, PlayerPosition.ReliefPitcher),
                TradePreference = career.TradeState.Preference,
                IsOnTradeBlock = career.TradeState.IsOnTradeBlock,
                TradeDeadlineGameIndex = career.TradeState.TradeDeadlineGameIndex,
                CurrentTeamGameIndex = record?.GamesPlayed ?? 0,
                TradeInterests = CopyTradeInterests(career.TradeState.Interests),
                TopTradeInterestTeamName = career.TradeState.Interests.Count > 0
                    ? GetTeam(career, career.TradeState.Interests[0].InterestedTeamId).Name
                    : string.Empty,
                CanChangeTradePreference = season.Phase == SeasonPhase.RegularSeason &&
                    (record?.GamesPlayed ?? 0) <= career.TradeState.TradeDeadlineGameIndex
            };
        }

        private static TradeInterestRecord[] CopyTradeInterests(
            System.Collections.Generic.IReadOnlyList<TradeInterestRecord> source)
        {
            var result = new TradeInterestRecord[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }

        private TeamRosterPlayerView[] BuildRoster(
            CareerState career,
            TeamState team,
            PlayerState myPlayer,
            Player stableMyPlayer,
            SeasonState season,
            bool hasNextGamePlan,
            PlayerGameRole plannedRole,
            int nextStartingPitcherId,
            int nextReliefPitcherId)
        {
            var result = new TeamRosterPlayerView[team.RosterCompetitors.Count + 1];
            int myOverall = _playerValueEvaluator.CalculatePositionValue(stableMyPlayer);
            bool isMyPlayerPlanned = hasNextGamePlan && IsMyPlayerInPlan(plannedRole);
            result[0] = CreateRosterView(
                myPlayer.PlayerId,
                myPlayer.Name,
                myPlayer.PrimaryPosition,
                myOverall,
                GetMyPlayerRosterRole(career.CurrentExpectedRole),
                isMyPlayer: true,
                isInNextGamePlan: isMyPlayerPlanned,
                hasCondition: true,
                myPlayer.Condition,
                season.LeagueStatistics.RegularSeason.GetPlayer(myPlayer.PlayerId));

            for (int index = 0; index < team.RosterCompetitors.Count; index++)
            {
                RosterCompetitorState competitor = team.RosterCompetitors[index];
                bool isInNextGamePlan = IsCompetitorInNextGamePlan(
                    team,
                    myPlayer,
                    competitor,
                    hasNextGamePlan,
                    plannedRole,
                    nextStartingPitcherId,
                    nextReliefPitcherId);
                result[index + 1] = CreateRosterView(
                    competitor.PlayerId,
                    competitor.Name,
                    competitor.Position,
                    competitor.Overall,
                    GetCompetitorRosterRole(team, competitor),
                    isMyPlayer: false,
                    isInNextGamePlan,
                    hasCondition: false,
                    condition: 0,
                    season.LeagueStatistics.RegularSeason.GetPlayer(competitor.PlayerId));
            }

            return result;
        }

        private static TeamRosterPlayerView CreateRosterView(
            int playerId,
            string name,
            PlayerPosition position,
            int overall,
            TeamRosterRole rosterRole,
            bool isMyPlayer,
            bool isInNextGamePlan,
            bool hasCondition,
            int condition,
            PlayerCompetitionStatisticsState statistics)
        {
            bool hasBattingRecord = statistics?.Batting.AtBats > 0;
            bool hasPitchingRecord = statistics?.Pitching.OutsRecorded > 0;
            return new TeamRosterPlayerView(
                playerId,
                name,
                position,
                overall,
                rosterRole,
                isMyPlayer,
                isInNextGamePlan,
                hasCondition,
                condition,
                hasBattingRecord,
                statistics?.Batting.BattingAverage ?? 0d,
                hasPitchingRecord,
                statistics?.Pitching.EarnedRunAverage ?? 0d);
        }

        private static TeamLineupSlotView[] BuildStartingLineup(
            Lineup lineup,
            TeamRosterPlayerView[] roster)
        {
            var result = new TeamLineupSlotView[lineup.Count];
            for (int index = 0; index < lineup.Count; index++)
            {
                LineupSlot slot = lineup[index];
                result[index] = new TeamLineupSlotView(
                    index + 1,
                    slot.FieldingPosition,
                    FindRosterPlayer(roster, slot.Player.PlayerId));
            }
            return result;
        }

        private static int GetNextPitcherId(
            TeamState team,
            PlayerState myPlayer,
            ScheduledGameState nextGame,
            PlayerGameRole plannedRole,
            PlayerPosition pitcherPosition)
        {
            if (pitcherPosition == PlayerPosition.StartingPitcher &&
                plannedRole == PlayerGameRole.StartingPitcher)
            {
                return myPlayer.PlayerId;
            }
            if (pitcherPosition == PlayerPosition.ReliefPitcher &&
                plannedRole == PlayerGameRole.ReliefPitcher)
            {
                return myPlayer.PlayerId;
            }

            int round = nextGame?.Round ?? 1;
            int selectionIndex = pitcherPosition == PlayerPosition.StartingPitcher
                ? round % 2
                : (round + 1) % 2;
            return team.GetCompetitor(pitcherPosition, selectionIndex).PlayerId;
        }

        private static TeamRosterRole GetMyPlayerRosterRole(ExpectedRole expectedRole)
        {
            return expectedRole switch
            {
                ExpectedRole.BenchCompetition => TeamRosterRole.Backup,
                _ => TeamRosterRole.Competition
            };
        }

        private static TeamRosterRole GetCompetitorRosterRole(
            TeamState team,
            RosterCompetitorState competitor)
        {
            if (competitor.Position == PlayerPosition.StartingPitcher)
                return TeamRosterRole.Rotation;
            if (competitor.Position == PlayerPosition.ReliefPitcher)
                return TeamRosterRole.Bullpen;
            return team.GetStrongestCompetitor(competitor.Position).PlayerId == competitor.PlayerId
                ? TeamRosterRole.Starting
                : TeamRosterRole.Backup;
        }

        private static bool IsCompetitorInNextGamePlan(
            TeamState team,
            PlayerState myPlayer,
            RosterCompetitorState competitor,
            bool hasNextGamePlan,
            PlayerGameRole plannedRole,
            int nextStartingPitcherId,
            int nextReliefPitcherId)
        {
            if (!hasNextGamePlan)
                return false;
            if (competitor.Position == PlayerPosition.StartingPitcher)
                return competitor.PlayerId == nextStartingPitcherId;
            if (competitor.Position == PlayerPosition.ReliefPitcher)
                return competitor.PlayerId == nextReliefPitcherId;
            if (plannedRole == PlayerGameRole.StartingBatter &&
                competitor.Position == myPlayer.PrimaryPosition)
            {
                return false;
            }
            return team.GetStrongestCompetitor(competitor.Position).PlayerId == competitor.PlayerId;
        }

        private static bool IsMyPlayerInPlan(PlayerGameRole role)
        {
            return role is PlayerGameRole.StartingBatter or
                PlayerGameRole.StartingPitcher or
                PlayerGameRole.ReliefPitcher;
        }

        private static TeamRosterPlayerView[] FilterPitchers(
            TeamRosterPlayerView[] roster,
            PlayerPosition position)
        {
            int count = 0;
            for (int index = 0; index < roster.Length; index++)
            {
                if (roster[index].Position == position)
                    count++;
            }

            var result = new TeamRosterPlayerView[count];
            int resultIndex = 0;
            for (int index = 0; index < roster.Length; index++)
            {
                if (roster[index].Position == position)
                    result[resultIndex++] = roster[index];
            }
            return result;
        }

        private static TeamRosterPlayerView FindRosterPlayer(TeamRosterPlayerView[] roster, int playerId)
        {
            for (int index = 0; index < roster.Length; index++)
            {
                if (roster[index].PlayerId == playerId)
                    return roster[index];
            }
            throw new InvalidOperationException($"PlayerId {playerId}를 로스터에서 찾을 수 없습니다.");
        }

        private static TeamState GetTeam(CareerState career, int teamId)
        {
            for (int index = 0; index < career.CurrentLeague.Teams.Count; index++)
            {
                TeamState team = career.CurrentLeague.Teams[index];
                if (team.TeamId == teamId)
                    return team;
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }

        private static int CalculateRank(SeasonState season, TeamSeasonRecordState playerRecord)
        {
            if (playerRecord == null || season.TeamRecords == null)
                return 1;

            int rank = 1;
            for (int index = 0; index < season.TeamRecords.Count; index++)
            {
                TeamSeasonRecordState other = season.TeamRecords[index];
                if (other.TeamId == playerRecord.TeamId)
                    continue;
                if (other.WinningPercentage > playerRecord.WinningPercentage ||
                    Math.Abs(other.WinningPercentage - playerRecord.WinningPercentage) < 0.000001d &&
                    other.Wins > playerRecord.Wins)
                {
                    rank++;
                }
            }
            return rank;
        }

        private static int CalculateAverage(TeamRosterPlayerView[] roster, PlayerGroup group)
        {
            int total = 0;
            int count = 0;
            for (int index = 0; index < roster.Length; index++)
            {
                PlayerPosition position = roster[index].Position;
                bool matches = group switch
                {
                    PlayerGroup.FieldPlayer => position is not PlayerPosition.StartingPitcher and
                        not PlayerPosition.ReliefPitcher,
                    PlayerGroup.StartingPitcher => position == PlayerPosition.StartingPitcher,
                    _ => position == PlayerPosition.ReliefPitcher
                };
                if (!matches)
                    continue;
                total += roster[index].Overall;
                count++;
            }
            return count == 0 ? 0 : (int)Math.Round(total / (double)count);
        }

        private enum PlayerGroup
        {
            FieldPlayer,
            StartingPitcher,
            ReliefPitcher
        }
    }
}
