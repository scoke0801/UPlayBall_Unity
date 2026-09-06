using Baseball.Core.Historical;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>덕아웃 인선과 방침이 결정론적인 경기 판단값으로 합성되는 계약을 고정한다.</summary>
    [TestFixture]
    public sealed class DugoutManagementTests
    {
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
