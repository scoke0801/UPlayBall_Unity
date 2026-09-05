using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Owner
{
    /// <summary>Staff Market 서비스가 판단한 계약 가능 상태와 효과 Preview를 UI에 전달한다.</summary>
    public sealed class OwnerStaffMarketOfferSnapshot
    {
        public OwnerStaffMarketOfferSnapshot(
            StaffMarketOffer offer,
            bool canSign,
            string disabledReason,
            string effectPreview,
            string portraitAssetKey = null)
        {
            Offer = offer ?? throw new ArgumentNullException(nameof(offer));
            if (!canSign && string.IsNullOrWhiteSpace(disabledReason))
                throw new ArgumentException("계약 불가 사유가 필요합니다.", nameof(disabledReason));
            CanSign = canSign;
            DisabledReason = disabledReason ?? string.Empty;
            EffectPreview = effectPreview ?? string.Empty;
            PortraitAssetKey = portraitAssetKey ?? string.Empty;
        }

        public StaffMarketOffer Offer { get; }
        public bool CanSign { get; }
        public string DisabledReason { get; }
        public string EffectPreview { get; }
        public string PortraitAssetKey { get; }
    }

    /// <summary>현재 다섯 역할, 계약, Resolver 효과와 시장 제안을 묶는 불변 UI 입력이다.</summary>
    public sealed class OwnerStaffOfficeSnapshot
    {
        private readonly StaffContractState[] _contracts;
        private readonly OwnerStaffMarketOfferSnapshot[] _offers;
        private readonly Dictionary<string, string> _portraitKeys;

        public OwnerStaffOfficeSnapshot(
            UiContentStateModel contentState,
            StaffCatalog catalog,
            IReadOnlyList<StaffContractState> contracts,
            TeamStaffAssignmentState assignment,
            TeamStaffEffectProfile effects,
            IReadOnlyList<OwnerStaffMarketOfferSnapshot> offers,
            IReadOnlyDictionary<string, string> portraitKeys = null)
        {
            ContentState = contentState ?? throw new ArgumentNullException(nameof(contentState));
            Catalog = catalog;
            Assignment = assignment;
            Effects = effects;
            _contracts = CopyRequired(contracts, nameof(contracts));
            _offers = CopyRequired(offers, nameof(offers));
            _portraitKeys = CopyMap(portraitKeys);
            if (contentState.Kind == UiContentStateKind.Ready)
            {
                if (catalog == null) throw new ArgumentNullException(nameof(catalog));
                if (assignment == null) throw new ArgumentNullException(nameof(assignment));
                if (effects == null) throw new ArgumentNullException(nameof(effects));
            }
        }

        public UiContentStateModel ContentState { get; }
        public StaffCatalog Catalog { get; }
        public IReadOnlyList<StaffContractState> Contracts => _contracts;
        public TeamStaffAssignmentState Assignment { get; }
        public TeamStaffEffectProfile Effects { get; }
        public IReadOnlyList<OwnerStaffMarketOfferSnapshot> Offers => _offers;

        public string GetPortraitKey(string staffId)
        {
            return !string.IsNullOrWhiteSpace(staffId) && _portraitKeys.TryGetValue(staffId, out string key)
                ? key
                : string.Empty;
        }

        private static T[] CopyRequired<T>(IReadOnlyList<T> source, string parameterName) where T : class
        {
            if (source == null) return Array.Empty<T>();
            var result = new T[source.Count];
            for (int index = 0; index < result.Length; index++)
                result[index] = source[index] ?? throw new ArgumentException("null 항목이 있습니다.", parameterName);
            return result;
        }

        private static Dictionary<string, string> CopyMap(IReadOnlyDictionary<string, string> source)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (source == null) return result;
            foreach (KeyValuePair<string, string> pair in source)
                if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                    result.Add(pair.Key.Trim(), pair.Value.Trim());
            return result;
        }
    }

    public sealed class OwnerStaffSlotModel
    {
        internal OwnerStaffSlotModel(
            StaffRole role,
            string roleText,
            string staffId,
            string name,
            string qualityText,
            string specialtyText,
            string philosophyText,
            string effectText,
            string salaryText,
            string termText,
            string portraitAssetKey)
        {
            Role = role;
            RoleText = roleText;
            StaffId = staffId;
            Name = name;
            QualityText = qualityText;
            SpecialtyText = specialtyText;
            PhilosophyText = philosophyText;
            EffectText = effectText;
            SalaryText = salaryText;
            TermText = termText;
            PortraitAssetKey = portraitAssetKey;
        }

        public StaffRole Role { get; }
        public string RoleText { get; }
        public string StaffId { get; }
        public string Name { get; }
        public string QualityText { get; }
        public string SpecialtyText { get; }
        public string PhilosophyText { get; }
        public string EffectText { get; }
        public string SalaryText { get; }
        public string TermText { get; }
        public string PortraitAssetKey { get; }
        public bool IsVacant => string.IsNullOrEmpty(StaffId);
    }

    public sealed class OwnerStaffMarketOfferModel
    {
        internal OwnerStaffMarketOfferModel(
            string offerId,
            string staffId,
            string roleText,
            string name,
            string qualityText,
            string specialtyText,
            string philosophyText,
            string effectText,
            string salaryText,
            string termText,
            string signingCostText,
            string portraitAssetKey,
            bool canSign,
            string disabledReason)
        {
            OfferId = offerId;
            StaffId = staffId;
            RoleText = roleText;
            Name = name;
            QualityText = qualityText;
            SpecialtyText = specialtyText;
            PhilosophyText = philosophyText;
            EffectText = effectText;
            SalaryText = salaryText;
            TermText = termText;
            SigningCostText = signingCostText;
            PortraitAssetKey = portraitAssetKey;
            CanSign = canSign;
            DisabledReason = disabledReason;
        }

        public string OfferId { get; }
        public string StaffId { get; }
        public string RoleText { get; }
        public string Name { get; }
        public string QualityText { get; }
        public string SpecialtyText { get; }
        public string PhilosophyText { get; }
        public string EffectText { get; }
        public string SalaryText { get; }
        public string TermText { get; }
        public string SigningCostText { get; }
        public string PortraitAssetKey { get; }
        public bool CanSign { get; }
        public string DisabledReason { get; }
    }

    /// <summary>Staff Office View가 도메인 객체를 조회하지 않게 다섯 슬롯과 시장 표시를 동결한다.</summary>
    public sealed class OwnerStaffOfficePresentationModel
    {
        internal OwnerStaffOfficePresentationModel(
            OwnerStaffOfficeSnapshot snapshot,
            IReadOnlyList<OwnerStaffSlotModel> slots,
            IReadOnlyList<OwnerStaffMarketOfferModel> offers)
        {
            Snapshot = snapshot;
            Slots = Copy(slots);
            Offers = Copy(offers);
        }

        public OwnerStaffOfficeSnapshot Snapshot { get; }
        public IReadOnlyList<OwnerStaffSlotModel> Slots { get; }
        public IReadOnlyList<OwnerStaffMarketOfferModel> Offers { get; }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var result = new T[source?.Count ?? 0];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
    }

    /// <summary>TeamStaffEffectProfile을 훈련·회복·분석 효율 문구로만 표현한다.</summary>
    public static class OwnerStaffOfficePresentationBuilder
    {
        public static OwnerStaffOfficePresentationModel Build(OwnerStaffOfficeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.ContentState.Kind != UiContentStateKind.Ready)
                return new OwnerStaffOfficePresentationModel(snapshot, Array.Empty<OwnerStaffSlotModel>(),
                    Array.Empty<OwnerStaffMarketOfferModel>());

            int roleCount = Enum.GetValues(typeof(StaffRole)).Length;
            var slots = new OwnerStaffSlotModel[roleCount];
            for (int index = 0; index < roleCount; index++)
            {
                var role = (StaffRole)index;
                string staffId = snapshot.Assignment.GetAssignedStaffId(role);
                slots[index] = string.IsNullOrEmpty(staffId)
                    ? CreateVacantSlot(role)
                    : CreateAssignedSlot(snapshot, role, snapshot.Catalog.Get(staffId));
            }

            var offers = new OwnerStaffMarketOfferModel[snapshot.Offers.Count];
            for (int index = 0; index < offers.Length; index++)
            {
                OwnerStaffMarketOfferSnapshot source = snapshot.Offers[index];
                StaffDefinition staff = snapshot.Catalog.Get(source.Offer.StaffId);
                offers[index] = new OwnerStaffMarketOfferModel(
                    source.Offer.OfferId,
                    staff.StaffId,
                    FormatRole(staff.Role),
                    staff.FictionalName,
                    FormatQuality(staff.QualityTier),
                    JoinSpecialties(staff.Specialties),
                    JoinPhilosophies(staff.Philosophies),
                    string.IsNullOrWhiteSpace(source.EffectPreview) ? "예상 효과 확인 필요" : source.EffectPreview,
                    $"연봉 {OwnerMoneyFormatter.Format(source.Offer.AnnualSalary)}",
                    $"{source.Offer.ContractYears}시즌 계약",
                    $"계약 비용 {OwnerMoneyFormatter.Format(source.Offer.SigningCost)}",
                    source.PortraitAssetKey,
                    source.CanSign,
                    source.DisabledReason);
            }
            return new OwnerStaffOfficePresentationModel(snapshot, slots, offers);
        }

        private static OwnerStaffSlotModel CreateVacantSlot(StaffRole role)
        {
            return new OwnerStaffSlotModel(role, FormatRole(role), string.Empty, "미배치", "등급 -",
                "전문 분야 없음", "운영 철학 없음", "현재 적용 효과 없음", "연봉 -", "계약 -", string.Empty);
        }

        private static OwnerStaffSlotModel CreateAssignedSlot(
            OwnerStaffOfficeSnapshot snapshot,
            StaffRole role,
            StaffDefinition staff)
        {
            StaffContractState contract = FindContract(snapshot.Contracts, staff.StaffId, snapshot.Assignment.TeamSeasonKey);
            return new OwnerStaffSlotModel(
                role,
                FormatRole(role),
                staff.StaffId,
                staff.FictionalName,
                FormatQuality(staff.QualityTier),
                JoinSpecialties(staff.Specialties),
                JoinPhilosophies(staff.Philosophies),
                FormatCurrentEffect(role, snapshot.Effects),
                contract == null ? "연봉 확인 불가" : $"연봉 {OwnerMoneyFormatter.Format(contract.AnnualSalary)}",
                contract == null ? "계약 확인 불가" : $"잔여 {contract.RemainingSeasons}시즌",
                snapshot.GetPortraitKey(staff.StaffId));
        }

        private static StaffContractState FindContract(
            IReadOnlyList<StaffContractState> contracts,
            string staffId,
            string teamSeasonKey)
        {
            for (int index = 0; index < contracts.Count; index++)
            {
                StaffContractState contract = contracts[index];
                if (contract.IsActive && string.Equals(contract.StaffId, staffId, StringComparison.Ordinal) &&
                    string.Equals(contract.TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal)) return contract;
            }
            return null;
        }

        private static string FormatCurrentEffect(StaffRole role, TeamStaffEffectProfile effects)
        {
            return role switch
            {
                StaffRole.HittingCoach => $"타자 훈련 효율 +{ToBonusPercent(effects.HittingTrainingEfficiency):0.#}%",
                StaffRole.PitchingCoach => $"투수 훈련 효율 +{ToBonusPercent(effects.PitchingTrainingEfficiency):0.#}%",
                StaffRole.DevelopmentCoach => $"육성 포인트 사용 효율 +{ToBonusPercent(effects.DevelopmentPointEfficiency):0.#}%",
                StaffRole.ConditioningCoach => $"회복 효율 +{ToBonusPercent(effects.ConditionRecoveryEfficiency):0.#}%",
                _ => $"상대 분석 신뢰도 +{effects.ScoutingConfidenceModifier * 100d:0.#}%"
            };
        }

        private static double ToBonusPercent(double multiplier) => Math.Max(0d, (multiplier - 1d) * 100d);

        private static string FormatRole(StaffRole role)
        {
            return role switch
            {
                StaffRole.HittingCoach => "타격코치",
                StaffRole.PitchingCoach => "투수코치",
                StaffRole.DevelopmentCoach => "육성코치",
                StaffRole.ConditioningCoach => "컨디셔닝코치",
                _ => "스카우팅 디렉터"
            };
        }

        private static string FormatQuality(int quality) => $"등급 {quality}/{StaffDefinition.MaximumQualityTier}";

        private static string JoinSpecialties(IReadOnlyList<StaffSpecialtyTag> values)
        {
            var result = new string[values.Count];
            for (int index = 0; index < result.Length; index++) result[index] = FormatSpecialty(values[index]);
            return string.Join(" · ", result);
        }

        private static string JoinPhilosophies(IReadOnlyList<StaffPhilosophyTag> values)
        {
            var result = new string[values.Count];
            for (int index = 0; index < result.Length; index++) result[index] = FormatPhilosophy(values[index]);
            return string.Join(" · ", result);
        }

        private static string FormatSpecialty(StaffSpecialtyTag value)
        {
            return value switch
            {
                StaffSpecialtyTag.ContactTraining => "컨택 훈련",
                StaffSpecialtyTag.PowerTraining => "장타 훈련",
                StaffSpecialtyTag.PlateDiscipline => "선구안 훈련",
                StaffSpecialtyTag.PitchCommand => "제구 훈련",
                StaffSpecialtyTag.PitchMovement => "구위 훈련",
                StaffSpecialtyTag.StarterDevelopment => "선발 육성",
                StaffSpecialtyTag.BullpenDevelopment => "불펜 육성",
                StaffSpecialtyTag.ProspectDevelopment => "유망주 육성",
                StaffSpecialtyTag.VeteranManagement => "베테랑 관리",
                StaffSpecialtyTag.RecoveryPlanning => "회복 계획",
                StaffSpecialtyTag.VeteranRecovery => "베테랑 회복",
                _ => "데이터 분석"
            };
        }

        private static string FormatPhilosophy(StaffPhilosophyTag value)
        {
            return value switch
            {
                StaffPhilosophyTag.Fundamentals => "기본기 중시",
                StaffPhilosophyTag.AggressiveDevelopment => "적극 육성",
                StaffPhilosophyTag.LongTermDevelopment => "장기 성장",
                StaffPhilosophyTag.WorkloadManagement => "부하 관리",
                StaffPhilosophyTag.EvidenceBased => "근거 중심",
                _ => "선수 중심"
            };
        }
    }
}
