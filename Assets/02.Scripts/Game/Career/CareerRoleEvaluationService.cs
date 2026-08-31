using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Growth;

namespace Baseball.Game.Career
{
    /// <summary>현재 기량·실제 기록·경쟁자 스냅샷을 시즌 역할 평가로 연결한다.</summary>
    public sealed class CareerRoleEvaluationService
    {
        private readonly CareerState _career;
        private readonly BalanceTable _balance;
        private readonly PlayerValueEvaluator _playerValueEvaluator;
        private readonly SkillBoardService _skillBoardService;
        private readonly ManagerRoleEvaluator _roleEvaluator;

        public CareerRoleEvaluationService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _playerValueEvaluator = new PlayerValueEvaluator(balance.PlayerEvaluation);
            _skillBoardService = new SkillBoardService(balance.Growth.SkillBoard, balance.Growth.SkillBlocks);
            _roleEvaluator = new ManagerRoleEvaluator(balance.ManagerRoleEvaluation);
        }

        /// <summary>새 시즌 계약 역할을 열고 스프링캠프 경쟁 결과를 즉시 적용한다.</summary>
        public CareerRoleEvaluationRecord BeginSeason(bool requiresInjuryReturnObservation)
        {
            SeasonState season = _career.CurrentLeague.CurrentSeason;
            ExpectedRole contractedRole = _career.TradeState.CurrentTeamRole ??
                                          _career.CurrentContract.ExpectedRole;
            _career.RoleState.BeginSeason(
                season.SeasonId,
                contractedRole,
                requiresInjuryReturnObservation);
            return Evaluate(0, CareerRoleEvaluationTrigger.SpringCamp);
        }

        /// <summary>10·20·40경기 평가 시점에만 역할을 다시 계산한다.</summary>
        public CareerRoleEvaluationRecord TryEvaluateAfterRound(int round)
        {
            SeasonState season = _career.CurrentLeague.CurrentSeason;
            if (!_career.RoleState.ShouldEvaluateAfterRound(season.SeasonId, round))
                return null;
            return Evaluate(round, _career.RoleState.ResolveTrigger(round));
        }

        private CareerRoleEvaluationRecord Evaluate(
            int round,
            CareerRoleEvaluationTrigger trigger)
        {
            Player current = _career.MyPlayer.ToPlayer(_skillBoardService);
            TeamState team = GetCurrentTeam();
            ManagerRoleEvaluationInput player = BuildPlayerInput(current, team, trigger);
            ManagerRoleEvaluationInput[] competitors = BuildCompetitorInputs(team, current.PrimaryPosition);
            ManagerRoleEvaluationResult result = _roleEvaluator.Evaluate(
                player,
                competitors,
                ResolveManagerStyle(team.Archetype.Archetype));
            ExpectedRole recommendedRole = ToExpectedRole(result.Role);
            SeasonState season = _career.CurrentLeague.CurrentSeason;
            CareerRoleEvaluationRecord record = _career.RoleState.ApplyEvaluation(
                season.SeasonId,
                round,
                trigger,
                recommendedRole,
                result.Score,
                result.StrongestCompetitorScore,
                result.Explanation);
            // SeasonId는 리그마다 독립적으로 증가해 승강 뒤 이전에 쓴 값이 다시 나온다.
            // 커리어 안에서 유일한 키는 연도이므로 저널 EventId는 다른 커리어 이벤트와 같이 연도로 만든다.
            _career.World.DomainEvents.Append(new WorldDomainEvent(
                $"role-evaluation:{season.Year}:{_career.MyPlayerId}:{round}:{(int)trigger}",
                record.AppliedRole == record.PreviousRole
                    ? "PlayerRoleRetained"
                    : "PlayerRoleChanged",
                _career.World.Calendar.CurrentDate,
                _career.MyPlayerId,
                (int)record.AppliedRole));
            return record;
        }

        private ManagerRoleEvaluationInput BuildPlayerInput(
            Player player,
            TeamState team,
            CareerRoleEvaluationTrigger trigger)
        {
            double currentAbility = _playerValueEvaluator.CalculatePositionValue(player);
            double performance = CalculateRecentPerformance(trigger);
            double roleFit = Clamp(
                50d + (_playerValueEvaluator.CalculateTeamPreferenceFactor(
                    player,
                    team.Archetype.Archetype) - 1d) * 200d,
                0d,
                100d);
            double growthOutlook = CalculateGrowthOutlook(currentAbility);
            double incumbentBonus = _career.RoleState.ActiveRole == ExpectedRole.StartingCompetition
                ? 2d
                : 0d;
            return new ManagerRoleEvaluationInput(
                currentAbility,
                performance,
                _career.MyPlayer.Condition,
                _career.MyPlayer.ManagerEvaluation,
                roleFit,
                growthOutlook,
                IsPitcher(player.PrimaryPosition),
                incumbentBonus);
        }

        private ManagerRoleEvaluationInput[] BuildCompetitorInputs(
            TeamState team,
            PlayerPosition position)
        {
            int count = 0;
            for (int index = 0; index < team.RosterCompetitors.Count; index++)
                if (team.RosterCompetitors[index].Position == position) count++;
            var result = new ManagerRoleEvaluationInput[count];
            int writeIndex = 0;
            for (int index = 0; index < team.RosterCompetitors.Count; index++)
            {
                RosterCompetitorState competitor = team.RosterCompetitors[index];
                if (competitor.Position != position)
                    continue;
                bool isIncumbent = writeIndex == 0;
                result[writeIndex++] = new ManagerRoleEvaluationInput(
                    competitor.Overall,
                    50d,
                    75d,
                    50d,
                    50d,
                    50d,
                    IsPitcher(position),
                    incumbentBonus: isIncumbent ? 2d : 0d);
            }
            return result;
        }

        private double CalculateRecentPerformance(CareerRoleEvaluationTrigger trigger)
        {
            PlayerSeasonStatisticsState current = _career.CurrentLeague.CurrentSeason.PlayerStatistics;
            bool hasCurrentSample = IsPitcher(_career.MyPlayer.PrimaryPosition)
                ? current.OutsRecorded >= 9
                : current.PlateAppearances >= 15;
            if (hasCurrentSample)
                return CalculatePerformance(current);

            if (_career.SeasonHistory.Count > 0)
            {
                CareerSeasonHistoryRecord previous = _career.SeasonHistory[^1];
                if (previous.AdjustedPerformance.PlayerId == _career.MyPlayerId)
                    return Clamp(previous.AdjustedPerformance.AdjustedPerformance, 0d, 100d);
                if (previous.Statistics != null)
                    return CalculatePerformance(previous.Statistics);
            }

            // 부상 복귀 평가는 결장을 0점으로 보지 않고 중립 표본으로 처리한다.
            return trigger == CareerRoleEvaluationTrigger.InjuryReturn
                ? 55d
                : _career.MyPlayer.ManagerEvaluation;
        }

        private double CalculatePerformance(PlayerSeasonStatisticsState statistics)
        {
            if (IsPitcher(_career.MyPlayer.PrimaryPosition))
            {
                if (statistics.OutsRecorded < 9)
                    return 50d;
                return Clamp(100d - (statistics.EarnedRunAverage - 2d) * (100d / 6d), 0d, 100d);
            }
            if (statistics.PlateAppearances < 15)
                return 50d;
            return Clamp((statistics.OnBasePlusSlugging - 0.45d) * (100d / 0.65d), 0d, 100d);
        }

        private double CalculateGrowthOutlook(double currentAbility)
        {
            PlayerGrowthState growth = _career.MyPlayer.GrowthState;
            if (growth == null)
                return 50d;
            int[] potential = growth.PotentialByAbility.ToArray();
            int total = 0;
            for (int index = 0; index < potential.Length; index++)
                total += potential[index];
            double averagePotential = potential.Length == 0 ? currentAbility : total / (double)potential.Length;
            return Clamp(50d + (averagePotential - currentAbility) * 4d, 0d, 100d);
        }

        private TeamState GetCurrentTeam()
        {
            for (int index = 0; index < _career.CurrentLeague.Teams.Count; index++)
            {
                TeamState team = _career.CurrentLeague.Teams[index];
                if (team.TeamId == _career.MyPlayer.CurrentTeamId)
                    return team;
            }
            throw new InvalidOperationException("현재 소속 구단을 찾을 수 없습니다.");
        }

        private static ExpectedRole ToExpectedRole(OpportunityRole role)
        {
            return role switch
            {
                OpportunityRole.KeyStarter or OpportunityRole.Starter or
                    OpportunityRole.StartingRotation => ExpectedRole.StartingCompetition,
                OpportunityRole.Platoon or OpportunityRole.HighLeverageRelief =>
                    ExpectedRole.RosterCompetition,
                _ => ExpectedRole.BenchCompetition
            };
        }

        private static ManagerDevelopmentStyle ResolveManagerStyle(TeamArchetype archetype)
        {
            return archetype switch
            {
                TeamArchetype.Development => ManagerDevelopmentStyle.Development,
                TeamArchetype.OffenseFocused => ManagerDevelopmentStyle.DataDriven,
                TeamArchetype.PitchingFocused => ManagerDevelopmentStyle.VeteranPreference,
                _ => ManagerDevelopmentStyle.Balanced
            };
        }

        private static bool IsPitcher(PlayerPosition position) =>
            position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
