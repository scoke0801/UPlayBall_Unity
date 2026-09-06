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

        [Test]
        public void Configure_RejectsPolicyOutsideCurrentTrustRange()
        {
            DugoutStaffCatalog catalog = DugoutStaffCatalog.CreateDefault();
            DugoutManagementState state = DugoutManagementState.CreateDefault();

            Assert.Throws<System.InvalidOperationException>(() => state.Configure(
                state.ManagerId,
                state.HeadCoachId,
                new DugoutPolicySettings(4, 2, 2, 2, 2, 2),
                catalog));
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
        public void Trust_UnlocksSecondPolicyStepAfterFortyGames()
        {
            DugoutManagementState state = DugoutManagementState.CreateDefault();
            for (int index = 0; index < 40; index++) state.RecordMatchCompleted();

            Assert.That(state.ManagerTrust, Is.EqualTo(60));
            Assert.That(state.AllowedPolicyOffset, Is.EqualTo(2));
        }
    }
}
