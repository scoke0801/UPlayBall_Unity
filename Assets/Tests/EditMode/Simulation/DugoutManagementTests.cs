using Baseball.Core.Historical;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Match;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>덕아웃 인선과 방침이 결정론적인 경기 판단값으로 합성되는 계약을 고정한다.</summary>
    [TestFixture]
    public sealed class DugoutManagementTests
    {
        [TestCase(0, 0)]
        [TestCase(100, 1)]
        public void 대타선택은감독의상대맞춤성향과실제상대투수손을사용한다(int preference, int expected)
        {
            var balance = BalanceTable.CreateDefault();
            var source = SimulationTestFactory.CreateDetailedRoster(SimulationTestFactory.CreateTeam(1, 50, 50));
            var position = source.StartingLineup[0].FieldingPosition;
            Player Candidate(int id, Handedness hand) => new Player(id, "검증 타자", position, hand, Handedness.Right,
                new BatterAttributes(70,70,50,50,50,70), new PitcherAttributes(20,20,20,20,20,20));
            var bench = new[] { Candidate(9991, Handedness.Right), Candidate(9992, Handedness.Left) };
            var roster = new MatchRosterSnapshot(1, "검증팀", source.StartingLineup, source.StartingPitcher,
                source.Bullpen, bench, new ManagerTacticalProfile(50,50,50,50,50,preference,50,50), RunningApproach.Balanced);
            var state = new DetailedTeamGameState(roster, new PitcherFatigueResolver(balance.Match), null);
            Assert.That(state.TryFindPinchHitter(0, 8, LeverageTier.High, Handedness.Right,
                balance.PlateDiscipline, out int selected, out _, out _), Is.True);
            Assert.That(selected, Is.EqualTo(expected));
        }

        [Test]
        public void 코치의고유운영보정은실제경기성향에합성된다()
        {
            var defaults = DugoutStaffCatalog.CreateDefault();
            var coach = new HeadCoachDefinition("TEST", "검증 코치", "수비 운영", "검증용",
                DugoutPolicyAxis.RunningAggression, 0, DugoutPolicyAxis.HookSpeed, 0,
                matchupModifier: 8, defenseModifier: 15, roleModifier: -10, trustModifier: -5);
            var catalog = new DugoutStaffCatalog(defaults.Managers, new[] { coach });
            var profile = new DugoutTacticalProfileResolver().Resolve(
                new DugoutManagementState("MGR-BALANCED", "TEST", DugoutPolicySettings.Neutral), catalog);
            Assert.That(profile.MatchupPreference, Is.EqualTo(63));
            Assert.That(profile.DefensiveAggression, Is.EqualTo(67));
            Assert.That(profile.BullpenRoleRigidity, Is.EqualTo(45));
            Assert.That(profile.StarTrust, Is.EqualTo(50));
        }

        [Test]
        public void 상대맞춤은실제좌우타석계수를재사용하고스위치타자를인식한다()
        {
            var tuning = Baseball.Core.Balance.BalanceTable.CreateDefault().PlateDiscipline;
            var left = Baseball.Core.Players.Handedness.Left;
            var right = Baseball.Core.Players.Handedness.Right;
            var both = Baseball.Core.Players.Handedness.Switch;
            Assert.That(Baseball.Simulation.Match.ManagerMatchupAi.EvaluateContactAdjustment(left, right, 100, tuning),
                Is.EqualTo(tuning.OppositeHandedContactBonus));
            Assert.That(Baseball.Simulation.Match.ManagerMatchupAi.EvaluateContactAdjustment(right, right, 100, tuning),
                Is.EqualTo(-tuning.SameHandedContactPenalty));
            Assert.That(Baseball.Simulation.Match.ManagerMatchupAi.EvaluateContactAdjustment(both, left, 50, tuning),
                Is.EqualTo(tuning.OppositeHandedContactBonus * .5));
            Assert.That(Baseball.Simulation.Match.ManagerMatchupAi.EvaluateContactAdjustment(right, right, 0, tuning), Is.Zero);
        }

        [Test]
        public void 추가인선도저장설정과결정론적AI배정에포함된다()
        {
            var catalog = DugoutStaffCatalog.CreateDefault();
            var state = DugoutManagementState.CreateDefault();
            state.Configure("MGR-FLEXIBLE", "HC-DEFENSE", DugoutPolicySettings.Neutral, catalog);
            Assert.That(state.ManagerId, Is.EqualTo("MGR-FLEXIBLE"));
            Assert.That(state.HeadCoachId, Is.EqualTo("HC-DEFENSE"));
            var managers = new System.Collections.Generic.HashSet<string>();
            var coaches = new System.Collections.Generic.HashSet<string>();
            for (int team = 1; team <= 42; team++)
            {
                var ai = new DugoutTacticalProfileResolver().CreateAiState(team, catalog);
                managers.Add(ai.ManagerId); coaches.Add(ai.HeadCoachId);
            }
            Assert.That(managers.Count, Is.EqualTo(catalog.Managers.Count));
            Assert.That(coaches.Count, Is.EqualTo(catalog.HeadCoaches.Count));
        }

        [Test]
        public void Resolve_AppliesManagerPolicyAndHeadCoachInOrder()
        {
            DugoutStaffCatalog catalog = DugoutStaffCatalog.CreateDefault();
            var state = new DugoutManagementState(
                "MGR-BALANCED",
                "HC-CONTACT",
                new DugoutPolicySettings(2, 3, 2, 2, 2, 2));

            Baseball.Core.Teams.ManagerTacticalProfile profile =
                new DugoutTacticalProfileResolver().Resolve(state, catalog);

            Assert.That(profile.BattingApproach, Is.EqualTo(40));
            Assert.That(profile.RunningAggression, Is.EqualTo(55));
            Assert.That(profile.PinchHitAggression, Is.EqualTo(53));
            Assert.That(profile.HookSpeed, Is.EqualTo(50));
        }

        [TestCase(0)]
        [TestCase(4)]
        public void Configure_AllowsExtremePoliciesBeforeAndAfterManagerChange(int level)
        {
            DugoutStaffCatalog catalog = DugoutStaffCatalog.CreateDefault();
            DugoutManagementState state = DugoutManagementState.CreateDefault();

            var policy = new DugoutPolicySettings(level, level, level, level, level, level);
            state.Configure(state.ManagerId, state.HeadCoachId, policy, catalog);
            Assert.That(state.Policy, Is.EqualTo(policy));
            Assert.That(state.AllowedPolicyOffset, Is.EqualTo(2));

            state.Configure("MGR-ATTACK", state.HeadCoachId, policy, catalog);
            Assert.That(state.Policy, Is.EqualTo(policy));
            Assert.That(state.ManagerTrust, Is.EqualTo(DugoutManagementState.InitialTrust));
            Assert.That(state.AllowedPolicyOffset, Is.EqualTo(2));
        }

        [Test]
        public void CreateAiState_SameTeamAlwaysReturnsSameAssignment()
        {
            DugoutStaffCatalog catalog = DugoutStaffCatalog.CreateDefault();
            var resolver = new DugoutTacticalProfileResolver();

            DugoutManagementState first = resolver.CreateAiState(7, catalog);
            DugoutManagementState second = resolver.CreateAiState(7, catalog);

            Assert.That(second.ManagerId, Is.EqualTo(first.ManagerId));
            Assert.That(second.HeadCoachId, Is.EqualTo(first.HeadCoachId));
            Assert.That(resolver.Resolve(second, catalog).HookSpeed,
                Is.EqualTo(resolver.Resolve(first, catalog).HookSpeed));
        }

        [Test]
        public void Trust_AccumulatesWithoutRestrictingPolicyRange()
        {
            DugoutManagementState state = DugoutManagementState.CreateDefault();
            for (int index = 0; index < 40; index++) state.RecordMatchCompleted();

            Assert.That(state.ManagerTrust, Is.EqualTo(60));
            Assert.That(state.AllowedPolicyOffset, Is.EqualTo(2));
        }
    }
}
