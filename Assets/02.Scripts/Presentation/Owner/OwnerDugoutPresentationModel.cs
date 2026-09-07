using System;
using Baseball.Core.Historical;
using Baseball.Core.Teams;

namespace Baseball.Presentation.Owner
{
    /// <summary>덕아웃 선택 창에 표시할 한 명의 감독 또는 수석코치 카드다.</summary>
    public sealed class OwnerDugoutStaffCandidate
    {
        public OwnerDugoutStaffCandidate(
            string id,
            string displayName,
            string specialty,
            string description,
            string effectDescription)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Specialty = specialty ?? throw new ArgumentNullException(nameof(specialty));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            EffectDescription = effectDescription ?? throw new ArgumentNullException(nameof(effectDescription));
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Specialty { get; }
        public string Description { get; }
        public string EffectDescription { get; }
    }

    /// <summary>현재 덕아웃 원본과 합성된 경기 판단값을 UI에 전달하는 불변 Snapshot이다.</summary>
    public sealed class OwnerDugoutSnapshot
    {
        public OwnerDugoutSnapshot(
            OwnerDugoutStaffCandidate[] managers,
            OwnerDugoutStaffCandidate[] headCoaches,
            string selectedManagerId,
            string selectedHeadCoachId,
            DugoutPolicySettings policy,
            int managerTrust,
            int allowedPolicyOffset,
            ManagerTacticalProfile effectiveProfile)
        {
            Managers = managers ?? throw new ArgumentNullException(nameof(managers));
            HeadCoaches = headCoaches ?? throw new ArgumentNullException(nameof(headCoaches));
            SelectedManagerId = selectedManagerId ?? throw new ArgumentNullException(nameof(selectedManagerId));
            SelectedHeadCoachId = selectedHeadCoachId ?? throw new ArgumentNullException(nameof(selectedHeadCoachId));
            Policy = policy;
            ManagerTrust = managerTrust;
            AllowedPolicyOffset = allowedPolicyOffset;
            EffectiveProfile = effectiveProfile;
        }

        public OwnerDugoutStaffCandidate[] Managers { get; }
        public OwnerDugoutStaffCandidate[] HeadCoaches { get; }
        public string SelectedManagerId { get; }
        public string SelectedHeadCoachId { get; }
        public DugoutPolicySettings Policy { get; }
        public int ManagerTrust { get; }
        public int AllowedPolicyOffset { get; }
        public ManagerTacticalProfile EffectiveProfile { get; }

        public OwnerDugoutStaffCandidate GetManager(string id) => Find(Managers, id);
        public OwnerDugoutStaffCandidate GetHeadCoach(string id) => Find(HeadCoaches, id);

        private static OwnerDugoutStaffCandidate Find(OwnerDugoutStaffCandidate[] source, string id)
        {
            for (int index = 0; index < source.Length; index++)
                if (string.Equals(source[index].Id, id, StringComparison.Ordinal)) return source[index];
            throw new InvalidOperationException($"덕아웃 후보 {id}가 없습니다.");
        }
    }

    /// <summary>화면의 임시 선택을 Game 레이어에 한 번에 전달한다.</summary>
    public readonly struct OwnerDugoutConfigurationCommand
    {
        public OwnerDugoutConfigurationCommand(
            string managerId,
            string headCoachId,
            DugoutPolicySettings policy)
        {
            ManagerId = managerId;
            HeadCoachId = headCoachId;
            Policy = policy;
        }

        public string ManagerId { get; }
        public string HeadCoachId { get; }
        public DugoutPolicySettings Policy { get; }
    }

    /// <summary>Core 정의와 현재 Runtime을 표시용 카드 문구로 변환한다.</summary>
    public static class OwnerDugoutPresentationBuilder
    {
        public static OwnerDugoutSnapshot Build(Baseball.Game.Historical.OwnerModeManager ownerManager)
        {
            if (ownerManager == null) throw new ArgumentNullException(nameof(ownerManager));
            DugoutStaffCatalog catalog = ownerManager.GetDugoutStaffCatalog();
            DugoutManagementState state = ownerManager.Runtime.ManagerMode.Dugout;
            var managers = new OwnerDugoutStaffCandidate[catalog.Managers.Count];
            for (int index = 0; index < managers.Length; index++)
            {
                ManagerDefinition item = catalog.Managers[index];
                managers[index] = new OwnerDugoutStaffCandidate(
                    item.ManagerId,
                    item.DisplayName,
                    item.StyleName,
                    item.Description,
                    item.TraitDescription);
            }
            var coaches = new OwnerDugoutStaffCandidate[catalog.HeadCoaches.Count];
            for (int index = 0; index < coaches.Length; index++)
            {
                HeadCoachDefinition item = catalog.HeadCoaches[index];
                coaches[index] = new OwnerDugoutStaffCandidate(
                    item.HeadCoachId,
                    item.DisplayName,
                    item.SpecialtyName,
                    item.Description,
                    DescribeCoachEffect(item) + (item.HasConditionSupport
                        ? $" · 선수단 경기 컨디션 +{ownerManager.Balance.ConditionChemistry.HeadCoachConditionBonus}" : string.Empty));
            }
            return new OwnerDugoutSnapshot(
                managers,
                coaches,
                state.ManagerId,
                state.HeadCoachId,
                state.Policy,
                state.ManagerTrust,
                state.AllowedPolicyOffset,
                ownerManager.GetEffectiveManagerProfile());
        }

        private static string DescribeCoachEffect(HeadCoachDefinition coach)
        {
            return $"{GetAxisName(coach.PrimaryAxis)} {FormatModifier(coach.PrimaryModifier)} · " +
                   $"{GetAxisName(coach.SecondaryAxis)} {FormatModifier(coach.SecondaryModifier)}";
        }

        private static string GetAxisName(DugoutPolicyAxis axis)
        {
            return axis switch
            {
                DugoutPolicyAxis.BattingApproach => "타격방침",
                DugoutPolicyAxis.RunningAggression => "도루시도",
                DugoutPolicyAxis.SmallBallPreference => "번트시도",
                DugoutPolicyAxis.PinchHitAggression => "대타기용",
                DugoutPolicyAxis.HookSpeed => "선발교체",
                DugoutPolicyAxis.BullpenAggression => "중간교체",
                _ => "운영 방침"
            };
        }

        private static string FormatModifier(int value) => value >= 0 ? $"+{value}" : value.ToString();
    }
}
