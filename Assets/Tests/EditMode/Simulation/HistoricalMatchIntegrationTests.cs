using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>공통 역사 규칙이 DetailedMatchEngine 단일 경로에 연결되는지 검증한다.</summary>
    public sealed class HistoricalMatchIntegrationTests
    {
        [Test]
        public void TacticRuntime_지속효과를중복가산하지않고팀공격에만적용한다()
        {
            TacticLoadoutState home = ConfirmedLoadout(CreateContactCard());
            var configuration = new HistoricalMatchConfiguration(homeTacticLoadout: home);
            var runtime = new DetailedMatchTacticRuntime(configuration);
            Player pitcher = CreatePitcher(9001, PlayerPosition.StartingPitcher);

            MatchPlateAppearanceModifiers first = runtime.Resolve(
                8, InningHalf.Bottom, -1, 1, false, pitcher, PitcherRole.Starter);
            Assert.That(first.GetBatter(PlayerAbility.Contact), Is.EqualTo(6));
            MatchPlateAppearanceModifiers second = runtime.Resolve(
                8, InningHalf.Bottom, 0, 2, false, pitcher, PitcherRole.Starter);
            Assert.That(second.GetBatter(PlayerAbility.Contact), Is.EqualTo(6));
            MatchPlateAppearanceModifiers opponent = runtime.Resolve(
                8, InningHalf.Top, 0, 1, false, pitcher, PitcherRole.Starter);
            Assert.That(opponent.GetBatter(PlayerAbility.Contact), Is.Zero);
        }

        [Test]
        public void AssignmentAdapter_비주포지션은허용비용이고DH는비용이없다()
        {
            PositionAssignmentRule rule = CreateAssignmentRule();
            MatchRosterSnapshot offPositionRoster = CreateRoster(
                teamId: 1,
                firstBatterNaturalPosition: PlayerPosition.Catcher,
                firstBatterAssignedPosition: PlayerPosition.Shortstop);
            var state = new DetailedTeamGameState(
                offPositionRoster,
                new PitcherFatigueResolver(BalanceTable.CreateDefault().Match),
                new HistoricalMatchConfiguration(positionAssignmentRule: rule));

            PositionAssignmentPenalty penalty = state.GetHitterAssignmentPenalty(0);
            Assert.That(penalty.IsAllowed, Is.True);
            Assert.That(penalty.ConditionPenalty, Is.EqualTo(7));
            Assert.That(penalty.FieldingErrorProbabilityMultiplier, Is.EqualTo(3d));

            MatchRosterSnapshot designatedHitterRoster = CreateRoster(
                teamId: 2,
                firstBatterNaturalPosition: PlayerPosition.Catcher,
                firstBatterAssignedPosition: PlayerPosition.DesignatedHitter);
            var designatedHitterState = new DetailedTeamGameState(
                designatedHitterRoster,
                new PitcherFatigueResolver(BalanceTable.CreateDefault().Match),
                new HistoricalMatchConfiguration(positionAssignmentRule: rule));
            Assert.That(designatedHitterState.GetHitterAssignmentPenalty(0).IsOffPosition, Is.False);
        }

        [Test]
        public void AssignmentAdapter_투수역할불일치는기용을막지않고비용을반환한다()
        {
            PositionAssignmentRule rule = CreateAssignmentRule();
            MatchRosterSnapshot roster = CreateRoster(3);
            PitcherRosterEntry mismatch = new PitcherRosterEntry(
                roster.StartingPitcher.Player,
                PitcherRole.Starter,
                naturalRole: PitcherRole.Closer);
            var mismatchRoster = new MatchRosterSnapshot(
                roster.TeamId,
                roster.TeamName,
                roster.StartingLineup,
                mismatch,
                roster.Bullpen,
                roster.Bench,
                roster.ManagerProfile,
                roster.RunningApproach);
            var state = new DetailedTeamGameState(
                mismatchRoster,
                new PitcherFatigueResolver(BalanceTable.CreateDefault().Match),
                new HistoricalMatchConfiguration(positionAssignmentRule: rule));

            PositionAssignmentPenalty penalty = state.GetActivePitcherAssignmentPenalty();
            Assert.That(penalty.IsAllowed, Is.True);
            Assert.That(penalty.ConditionPenalty, Is.EqualTo(5));
        }

        [Test]
        public void AssignmentAdapter_낮은NaturalRole신뢰도는경기비용을완화한다()
        {
            PositionAssignmentRule rule = CreateAssignmentRule();
            MatchRosterSnapshot roster = CreateRoster(4);
            PitcherRosterEntry mismatch = new PitcherRosterEntry(
                roster.StartingPitcher.Player,
                PitcherRole.Starter,
                naturalRole: PitcherRole.MiddleRelief,
                naturalRoleConfidence: PitcherRoleConfidence.Low);
            var mismatchRoster = new MatchRosterSnapshot(
                roster.TeamId,
                roster.TeamName,
                roster.StartingLineup,
                mismatch,
                roster.Bullpen,
                roster.Bench,
                roster.ManagerProfile,
                roster.RunningApproach);
            var state = new DetailedTeamGameState(
                mismatchRoster,
                new PitcherFatigueResolver(BalanceTable.CreateDefault().Match),
                new HistoricalMatchConfiguration(positionAssignmentRule: rule));

            PositionAssignmentPenalty penalty = state.GetActivePitcherAssignmentPenalty();

            Assert.That(penalty.ConditionPenalty, Is.EqualTo(2));
        }

        [Test]
        public void BullpenPolicy_공통Bullpen1부터4우선순위를경기교체후보에적용한다()
        {
            BullpenUsagePolicy policy = new BullpenUsagePolicy(new[]
            {
                new BullpenUsageBand(
                    1, 12, -20, 20, 0d, 5d, 40,
                    new[]
                    {
                        ActiveRosterRole.Bullpen2,
                        ActiveRosterRole.Bullpen1,
                        ActiveRosterRole.Bullpen3,
                        ActiveRosterRole.Bullpen4
                    })
            });
            var configuration = new HistoricalMatchConfiguration(
                bullpenUsagePolicy: policy,
                leverageIndexByTier: new[] { 0.5d, 1d, 2d, 3d });
            MatchRosterSnapshot roster = CreateRoster(4, tagsBullpenRoles: true);
            BalanceTable balance = BalanceTable.CreateDefault();
            var state = new DetailedTeamGameState(
                roster,
                new PitcherFatigueResolver(balance.Match),
                configuration);

            int selected = state.SelectReliever(
                new PitcherManagementAi(balance.Match.BullpenManagement),
                balance.Match.BullpenManagement,
                LeverageTier.Medium,
                remainingInnings: 4,
                inning: 6,
                runDifferential: 0);

            Assert.That(selected, Is.EqualTo(2));
            Assert.That(state.Roster.Bullpen[selected - 1].ActiveRosterRole,
                Is.EqualTo(ActiveRosterRole.Bullpen2));
        }

        [Test]
        public void Fielding_비주포지션ErrorMultiplier는도달률이아닌실책확률만높인다()
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            Player fielder = CreateBatter(9101, PlayerPosition.Shortstop, 70);
            var ball = new BattedBallDescriptor(
                BattedBallType.GroundBall,
                BattedBallDirection.Center,
                FieldZone.Shortstop,
                quality: 45d,
                BallFlightBand.Short,
                BallPaceBand.Medium,
                isHomeRun: false);
            FieldingPlayOutcome normal = new FieldingPlayResolver(
                balance.Match.Fielding,
                new SequenceRandom(0d, 0.03d, 0.5d)).Resolve(
                ball, fielder, PlayerPosition.Shortstop, DefensiveAlignment.Standard,
                50, 50, false);
            FieldingPlayOutcome penalized = new FieldingPlayResolver(
                balance.Match.Fielding,
                new SequenceRandom(0d, 0.03d, 0.5d)).Resolve(
                ball, fielder, PlayerPosition.Shortstop, DefensiveAlignment.Standard,
                50, 50, false, fieldingErrorProbabilityMultiplier: 8d);

            Assert.That(penalized.ReachChance, Is.EqualTo(normal.ReachChance));
            Assert.That(normal.FailureType, Is.EqualTo(FieldingFailureType.None));
            Assert.That(penalized.FailureType, Is.EqualTo(FieldingFailureType.FieldingError));
        }

        [Test]
        public void DetailedMatch_같은Seed와HistoricalInput은완전히결정론적이다()
        {
            HistoricalMatchConfiguration configuration = new HistoricalMatchConfiguration(
                positionAssignmentRule: CreateAssignmentRule(),
                awayTacticLoadout: ConfirmedLoadout(CreateContactCard()));
            var input = new MatchInput(
                1,
                77,
                918273UL,
                CreateRoster(7),
                CreateRoster(8),
                MatchRules.CreateDefault(requiresWinner: false),
                historicalConfiguration: configuration);
            MatchResult first = new MatchSimulator(
                BalanceTable.CreateDefault(), MatchRandomStreams.Create(input.RandomSeed)).Simulate(input);
            MatchResult second = new MatchSimulator(
                BalanceTable.CreateDefault(), MatchRandomStreams.Create(input.RandomSeed)).Simulate(input);

            Assert.That(second.AwayBoxScore.Runs, Is.EqualTo(first.AwayBoxScore.Runs));
            Assert.That(second.HomeBoxScore.Runs, Is.EqualTo(first.HomeBoxScore.Runs));
            Assert.That(second.Events.Count, Is.EqualTo(first.Events.Count));
            for (int index = 0; index < first.Events.Count; index++)
                Assert.That(second.Events[index], Is.EqualTo(first.Events[index]), $"Event {index}");
        }

        private static TacticCardDefinition CreateContactCard()
        {
            return new TacticCardDefinition(
                "tactic.promise.eighth",
                "약속의 8회",
                TacticCardCategory.Batting,
                TacticTier.Normal,
                "8회 공격 강화",
                "Contact +6",
                new[]
                {
                    new TacticTriggerCondition(
                        TacticTriggerField.Inning,
                        TacticComparison.Equal,
                        8)
                },
                TacticTargetRule.BattingTeam,
                new[] { new TacticStatModifier(PlayerAbility.Contact, 6) },
                Array.Empty<TacticBehaviorModifier>(),
                TacticDurationRule.UntilInningEnd,
                Array.Empty<string>(),
                isDisruption: false);
        }

        private static TacticLoadoutState ConfirmedLoadout(params TacticCardDefinition[] cards)
        {
            var loadout = new TacticLoadoutState(cards);
            loadout.ConfirmGame();
            return loadout;
        }

        private static PositionAssignmentRule CreateAssignmentRule()
        {
            return new PositionAssignmentRule(
                new OffPositionPenaltyDefinition(7, 3d),
                new PitcherRoleMismatchPenaltyDefinition(5));
        }

        private static MatchRosterSnapshot CreateRoster(
            int teamId,
            PlayerPosition firstBatterNaturalPosition = PlayerPosition.Catcher,
            PlayerPosition firstBatterAssignedPosition = PlayerPosition.Catcher,
            bool tagsBullpenRoles = false)
        {
            var slots = new LineupSlot[9];
            var assigned = new[]
            {
                firstBatterAssignedPosition,
                PlayerPosition.FirstBase,
                PlayerPosition.SecondBase,
                PlayerPosition.ThirdBase,
                PlayerPosition.Catcher,
                PlayerPosition.LeftField,
                PlayerPosition.CenterField,
                PlayerPosition.RightField,
                PlayerPosition.DesignatedHitter
            };
            if (firstBatterAssignedPosition == PlayerPosition.Catcher)
                assigned[4] = PlayerPosition.Shortstop;
            else if (firstBatterAssignedPosition == PlayerPosition.Shortstop)
                assigned[4] = PlayerPosition.Catcher;
            else if (firstBatterAssignedPosition == PlayerPosition.DesignatedHitter)
            {
                assigned[4] = PlayerPosition.Shortstop;
                assigned[8] = PlayerPosition.Catcher;
            }

            for (int index = 0; index < slots.Length; index++)
            {
                PlayerPosition natural = index == 0 ? firstBatterNaturalPosition : assigned[index];
                slots[index] = new LineupSlot(
                    CreateBatter(teamId * 100 + index + 1, natural, 55),
                    assigned[index]);
            }

            Player starter = CreatePitcher(teamId * 1000 + 90, PlayerPosition.StartingPitcher);
            var bullpen = new List<PitcherRosterEntry>();
            for (int index = 0; index < 4; index++)
            {
                ActiveRosterRole? activeRole = tagsBullpenRoles
                    ? (ActiveRosterRole)((int)ActiveRosterRole.Bullpen1 + index)
                    : null;
                bullpen.Add(new PitcherRosterEntry(
                    CreatePitcher(teamId * 1000 + index + 1, PlayerPosition.ReliefPitcher),
                    PitcherRole.MiddleRelief,
                    activeRosterRole: activeRole,
                    playerSeasonId: activeRole.HasValue ? $"season-{teamId}-{index + 1}" : null));
            }
            return new MatchRosterSnapshot(
                teamId,
                $"역사 통합 {teamId}",
                new Lineup(slots),
                new PitcherRosterEntry(starter, PitcherRole.Starter),
                bullpen,
                Array.Empty<Player>(),
                ManagerTacticalProfile.Balanced,
                RunningApproach.Balanced);
        }

        private static Player CreateBatter(int id, PlayerPosition position, int rating)
        {
            return new Player(
                id,
                $"타자 {id}",
                position,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(rating, rating, rating, rating, rating, rating),
                new PitcherAttributes(20, 20, 20, 20, 20, 20));
        }

        private static Player CreatePitcher(int id, PlayerPosition position)
        {
            return new Player(
                id,
                $"투수 {id}",
                position,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(20, 20, 30, 30, 40, 40),
                new PitcherAttributes(60, 60, 60, 60, 60, 60));
        }
    }
}
