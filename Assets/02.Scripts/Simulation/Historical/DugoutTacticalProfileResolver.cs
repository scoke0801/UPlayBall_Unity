using System;
using Baseball.Core.Historical;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Historical
{
    /// <summary>감독 원형, 구단주 지시, 수석코치 보정을 경기 시작 시점의 판단값으로 합성한다.</summary>
    public sealed class DugoutTacticalProfileResolver
    {
        public const int PolicyStepValue = 5;

        public ManagerTacticalProfile Resolve(
            DugoutManagementState state,
            DugoutStaffCatalog catalog)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            ManagerTacticalProfile source = catalog.GetManager(state.ManagerId).BaseProfile;
            HeadCoachDefinition coach = catalog.GetHeadCoach(state.HeadCoachId);
            DugoutPolicySettings policy = state.Policy;
            return new ManagerTacticalProfile(
                ResolveAxis(source.HookSpeed, DugoutPolicyAxis.HookSpeed, policy, coach),
                ResolveAxis(source.BullpenAggression, DugoutPolicyAxis.BullpenAggression, policy, coach),
                source.BullpenRoleRigidity,
                ResolveAxis(source.SmallBallPreference, DugoutPolicyAxis.SmallBallPreference, policy, coach),
                ResolveAxis(source.RunningAggression, DugoutPolicyAxis.RunningAggression, policy, coach),
                source.MatchupPreference,
                source.DefensiveAggression,
                source.StarTrust,
                ResolveAxis(source.BattingApproach, DugoutPolicyAxis.BattingApproach, policy, coach),
                ResolveAxis(source.PinchHitAggression, DugoutPolicyAxis.PinchHitAggression, policy, coach));
        }

        /// <summary>AI 구단에도 TeamId만으로 고정되는 감독 조합을 배정해 같은 입력의 재현성을 지킨다.</summary>
        public DugoutManagementState CreateAiState(int teamId, DugoutStaffCatalog catalog)
        {
            if (teamId <= 0) throw new ArgumentOutOfRangeException(nameof(teamId));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            int managerIndex = (teamId - 1) % catalog.Managers.Count;
            int coachIndex = ((teamId - 1) * 3 + 1) % catalog.HeadCoaches.Count;
            return new DugoutManagementState(
                catalog.Managers[managerIndex].ManagerId,
                catalog.HeadCoaches[coachIndex].HeadCoachId,
                DugoutPolicySettings.Neutral,
                managerTrust: 100);
        }

        /// <summary>인선 효과를 다시 계산하지 않고 현재 적용값에 방침 변경분만 더해 UI Preview를 만든다.</summary>
        public static ManagerTacticalProfile PreviewPolicyChange(
            ManagerTacticalProfile current,
            DugoutPolicySettings currentPolicy,
            DugoutPolicySettings draftPolicy)
        {
            return new ManagerTacticalProfile(
                ApplyPolicyDelta(current.HookSpeed, DugoutPolicyAxis.HookSpeed, currentPolicy, draftPolicy),
                ApplyPolicyDelta(current.BullpenAggression, DugoutPolicyAxis.BullpenAggression, currentPolicy, draftPolicy),
                current.BullpenRoleRigidity,
                ApplyPolicyDelta(current.SmallBallPreference, DugoutPolicyAxis.SmallBallPreference, currentPolicy, draftPolicy),
                ApplyPolicyDelta(current.RunningAggression, DugoutPolicyAxis.RunningAggression, currentPolicy, draftPolicy),
                current.MatchupPreference,
                current.DefensiveAggression,
                current.StarTrust,
                ApplyPolicyDelta(current.BattingApproach, DugoutPolicyAxis.BattingApproach, currentPolicy, draftPolicy),
                ApplyPolicyDelta(current.PinchHitAggression, DugoutPolicyAxis.PinchHitAggression, currentPolicy, draftPolicy));
        }

        private static int ResolveAxis(
            int baseValue,
            DugoutPolicyAxis axis,
            DugoutPolicySettings policy,
            HeadCoachDefinition coach)
        {
            int ownerModifier = (policy.GetLevel(axis) - DugoutPolicySettings.NeutralLevel) * PolicyStepValue;
            return Clamp(baseValue + ownerModifier + coach.GetModifier(axis));
        }

        private static int Clamp(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }

        private static int ApplyPolicyDelta(
            int currentValue,
            DugoutPolicyAxis axis,
            DugoutPolicySettings currentPolicy,
            DugoutPolicySettings draftPolicy)
        {
            int levelDelta = draftPolicy.GetLevel(axis) - currentPolicy.GetLevel(axis);
            return Clamp(currentValue + levelDelta * PolicyStepValue);
        }
    }
}
