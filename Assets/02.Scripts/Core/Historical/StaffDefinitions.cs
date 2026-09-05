using System;
using System.Collections.Generic;
using Baseball.Core.Growth;

namespace Baseball.Core.Historical
{
    public enum StaffRole
    {
        HittingCoach,
        PitchingCoach,
        DevelopmentCoach,
        ConditioningCoach,
        ScoutingDirector
    }

    public enum StaffSpecialtyTag
    {
        ContactTraining,
        PowerTraining,
        PlateDiscipline,
        PitchCommand,
        PitchMovement,
        StarterDevelopment,
        BullpenDevelopment,
        ProspectDevelopment,
        VeteranManagement,
        RecoveryPlanning,
        VeteranRecovery,
        DataAnalysis
    }

    public enum StaffPhilosophyTag
    {
        Fundamentals,
        AggressiveDevelopment,
        LongTermDevelopment,
        WorkloadManagement,
        EvidenceBased,
        PlayerCentered
    }

    public enum StaffSalaryBand
    {
        Budget,
        Standard,
        Premium,
        Elite
    }

    public enum StaffContractPreference
    {
        ShortTerm,
        Balanced,
        LongTerm
    }

    public enum StaffMarketKind
    {
        Offseason,
        MidseasonReplacement
    }

    public enum StaffMoneyReason
    {
        Signing,
        ReplacementPenalty,
        SigningAndReplacement,
        Salary
    }

    public enum StaffServiceStatus
    {
        Succeeded,
        NoChange,
        InvalidState,
        StaffUnavailable,
        InsufficientMoney,
        SalaryNotSettled
    }

    public enum StaffTrainingDiscipline
    {
        Hitting,
        Pitching
    }

    /// <summary>가상 스태프 한 명의 변경되지 않는 정적 정의다.</summary>
    public sealed class StaffDefinition
    {
        public const int MinimumQualityTier = 1;
        public const int MaximumQualityTier = 5;

        private readonly StaffSpecialtyTag[] _specialties;
        private readonly StaffPhilosophyTag[] _philosophies;

        public StaffDefinition(
            string staffId,
            string fictionalName,
            StaffRole role,
            int qualityTier,
            StaffSalaryBand salaryBand,
            StaffContractPreference contractPreference,
            IReadOnlyList<StaffSpecialtyTag> specialties,
            IReadOnlyList<StaffPhilosophyTag> philosophies)
        {
            StaffId = RequireId(staffId, nameof(staffId));
            FictionalName = StaffNameCatalog.ValidateName(fictionalName, nameof(fictionalName));
            ValidateEnum(role, nameof(role));
            if (qualityTier < MinimumQualityTier || qualityTier > MaximumQualityTier)
                throw new ArgumentOutOfRangeException(nameof(qualityTier));
            ValidateEnum(salaryBand, nameof(salaryBand));
            ValidateEnum(contractPreference, nameof(contractPreference));

            Role = role;
            QualityTier = qualityTier;
            SalaryBand = salaryBand;
            ContractPreference = contractPreference;
            _specialties = CopyUnique(specialties, nameof(specialties));
            _philosophies = CopyUnique(philosophies, nameof(philosophies));
        }

        public string StaffId { get; }
        public string FictionalName { get; }
        public StaffRole Role { get; }
        public int QualityTier { get; }
        public StaffSalaryBand SalaryBand { get; }
        public StaffContractPreference ContractPreference { get; }
        public IReadOnlyList<StaffSpecialtyTag> Specialties => _specialties;
        public IReadOnlyList<StaffPhilosophyTag> Philosophies => _philosophies;

        private static T[] CopyUnique<T>(IReadOnlyList<T> source, string parameterName) where T : struct
        {
            if (source == null || source.Count == 0)
                throw new ArgumentException("하나 이상의 태그가 필요합니다.", parameterName);
            var result = new T[source.Count];
            var unique = new HashSet<T>();
            for (int index = 0; index < source.Count; index++)
            {
                T value = source[index];
                if (!Enum.IsDefined(typeof(T), value) || !unique.Add(value))
                    throw new ArgumentException("스태프 태그는 유효하고 중복되지 않아야 합니다.", parameterName);
                result[index] = value;
            }
            return result;
        }

        internal static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Stable ID는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }

        internal static void ValidateEnum<T>(T value, string parameterName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>Stable StaffId로 스태프 정의를 조회하는 World 공통 카탈로그다.</summary>
    public sealed class StaffCatalog
    {
        private readonly StaffDefinition[] _staff;
        private readonly Dictionary<string, StaffDefinition> _byId;

        public StaffCatalog(IReadOnlyList<StaffDefinition> staff)
        {
            if (staff == null || staff.Count == 0)
                throw new ArgumentException("하나 이상의 스태프 정의가 필요합니다.", nameof(staff));
            _staff = new StaffDefinition[staff.Count];
            _byId = new Dictionary<string, StaffDefinition>(staff.Count, StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < staff.Count; index++)
            {
                StaffDefinition definition = staff[index]
                    ?? throw new ArgumentException("null 스태프 정의가 있습니다.", nameof(staff));
                if (!_byId.TryAdd(definition.StaffId, definition))
                    throw new ArgumentException("StaffId는 중복될 수 없습니다.", nameof(staff));
                if (!names.Add(definition.FictionalName))
                    throw new ArgumentException("가상 스태프 이름은 한 World 안에서 중복될 수 없습니다.", nameof(staff));
                _staff[index] = definition;
            }
            Array.Sort(_staff, (left, right) => string.CompareOrdinal(left.StaffId, right.StaffId));
        }

        public IReadOnlyList<StaffDefinition> Staff => _staff;

        public bool TryGet(string staffId, out StaffDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(staffId))
            {
                definition = null;
                return false;
            }
            return _byId.TryGetValue(staffId.Trim(), out definition);
        }

        public StaffDefinition Get(string staffId)
        {
            string id = StaffDefinition.RequireId(staffId, nameof(staffId));
            if (!_byId.TryGetValue(id, out StaffDefinition definition))
                throw new KeyNotFoundException($"StaffId {id}의 정의가 없습니다.");
            return definition;
        }
    }

    /// <summary>Offline에서 실명 충돌을 검증한 가상 스태프 이름 후보를 보관한다.</summary>
    public sealed class StaffNameCatalog
    {
        private readonly string[] _names;

        public StaffNameCatalog(IReadOnlyList<string> fictionalNames)
        {
            if (fictionalNames == null || fictionalNames.Count == 0)
                throw new ArgumentException("하나 이상의 가상 스태프 이름이 필요합니다.", nameof(fictionalNames));
            _names = new string[fictionalNames.Count];
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < fictionalNames.Count; index++)
            {
                string name = ValidateName(fictionalNames[index], nameof(fictionalNames));
                if (!unique.Add(name))
                    throw new ArgumentException("가상 스태프 이름은 중복될 수 없습니다.", nameof(fictionalNames));
                _names[index] = name;
            }
        }

        public IReadOnlyList<string> Names => _names;

        internal static string ValidateName(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("가상 스태프 이름은 비어 있을 수 없습니다.", parameterName);
            string trimmed = value.Trim();
            if (trimmed.Length > 30)
                throw new ArgumentException("가상 스태프 이름은 30자를 넘을 수 없습니다.", parameterName);
            for (int index = 0; index < trimmed.Length; index++)
            {
                if (char.IsControl(trimmed[index]) || char.IsDigit(trimmed[index]))
                    throw new ArgumentException("가상 스태프 이름 형식이 올바르지 않습니다.", parameterName);
            }
            return trimmed;
        }
    }

    /// <summary>한 구단과 스태프 사이의 저장 가능한 계약 원본이다.</summary>
    public sealed class StaffContractState
    {
        public StaffContractState(
            string contractId,
            string staffId,
            string teamSeasonKey,
            int startSeason,
            int remainingSeasons,
            long annualSalary,
            int? lastSalaryPaidSeason = null)
        {
            ContractId = StaffDefinition.RequireId(contractId, nameof(contractId));
            StaffId = StaffDefinition.RequireId(staffId, nameof(staffId));
            TeamSeasonKey = StaffDefinition.RequireId(teamSeasonKey, nameof(teamSeasonKey));
            if (startSeason <= 0)
                throw new ArgumentOutOfRangeException(nameof(startSeason));
            if (remainingSeasons < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingSeasons));
            if (annualSalary <= 0)
                throw new ArgumentOutOfRangeException(nameof(annualSalary));
            if (lastSalaryPaidSeason.HasValue && lastSalaryPaidSeason.Value < startSeason)
                throw new ArgumentOutOfRangeException(nameof(lastSalaryPaidSeason));
            StartSeason = startSeason;
            RemainingSeasons = remainingSeasons;
            AnnualSalary = annualSalary;
            LastSalaryPaidSeason = lastSalaryPaidSeason;
        }

        public string ContractId { get; }
        public string StaffId { get; }
        public string TeamSeasonKey { get; }
        public int StartSeason { get; }
        public int RemainingSeasons { get; }
        public long AnnualSalary { get; }
        public int? LastSalaryPaidSeason { get; }
        public bool IsActive => RemainingSeasons > 0;

        public StaffContractState WithSalaryPaid(int season)
        {
            if (season < StartSeason)
                throw new ArgumentOutOfRangeException(nameof(season));
            if (LastSalaryPaidSeason.HasValue && LastSalaryPaidSeason.Value >= season)
                throw new InvalidOperationException("이미 정산된 시즌의 급여입니다.");
            return new StaffContractState(
                ContractId,
                StaffId,
                TeamSeasonKey,
                StartSeason,
                RemainingSeasons,
                AnnualSalary,
                season);
        }

        public StaffContractState WithRemainingSeasons(int remainingSeasons)
        {
            return new StaffContractState(
                ContractId,
                StaffId,
                TeamSeasonKey,
                StartSeason,
                remainingSeasons,
                AnnualSalary,
                LastSalaryPaidSeason);
        }
    }

    /// <summary>구단의 다섯 스태프 슬롯이 참조하는 Stable StaffId만 저장한다.</summary>
    public sealed class TeamStaffAssignmentState
    {
        private readonly string[] _assignedStaffIds;

        public TeamStaffAssignmentState(
            string teamSeasonKey,
            string hittingCoachStaffId = null,
            string pitchingCoachStaffId = null,
            string developmentCoachStaffId = null,
            string conditioningCoachStaffId = null,
            string scoutingDirectorStaffId = null)
        {
            TeamSeasonKey = StaffDefinition.RequireId(teamSeasonKey, nameof(teamSeasonKey));
            _assignedStaffIds = new[]
            {
                NormalizeOptionalId(hittingCoachStaffId),
                NormalizeOptionalId(pitchingCoachStaffId),
                NormalizeOptionalId(developmentCoachStaffId),
                NormalizeOptionalId(conditioningCoachStaffId),
                NormalizeOptionalId(scoutingDirectorStaffId)
            };
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < _assignedStaffIds.Length; index++)
            {
                string staffId = _assignedStaffIds[index];
                if (staffId != null && !unique.Add(staffId))
                    throw new ArgumentException("같은 스태프를 여러 역할에 배치할 수 없습니다.");
            }
        }

        public string TeamSeasonKey { get; }
        public string HittingCoachStaffId => _assignedStaffIds[(int)StaffRole.HittingCoach];
        public string PitchingCoachStaffId => _assignedStaffIds[(int)StaffRole.PitchingCoach];
        public string DevelopmentCoachStaffId => _assignedStaffIds[(int)StaffRole.DevelopmentCoach];
        public string ConditioningCoachStaffId => _assignedStaffIds[(int)StaffRole.ConditioningCoach];
        public string ScoutingDirectorStaffId => _assignedStaffIds[(int)StaffRole.ScoutingDirector];

        public string GetAssignedStaffId(StaffRole role)
        {
            StaffDefinition.ValidateEnum(role, nameof(role));
            return _assignedStaffIds[(int)role];
        }

        public TeamStaffAssignmentState WithAssignment(StaffRole role, string staffId)
        {
            StaffDefinition.ValidateEnum(role, nameof(role));
            string[] copy = CopyAssignments();
            copy[(int)role] = StaffDefinition.RequireId(staffId, nameof(staffId));
            return FromArray(TeamSeasonKey, copy);
        }

        public TeamStaffAssignmentState WithoutAssignment(StaffRole role)
        {
            StaffDefinition.ValidateEnum(role, nameof(role));
            string[] copy = CopyAssignments();
            copy[(int)role] = null;
            return FromArray(TeamSeasonKey, copy);
        }

        private string[] CopyAssignments()
        {
            var copy = new string[_assignedStaffIds.Length];
            Array.Copy(_assignedStaffIds, copy, copy.Length);
            return copy;
        }

        private static TeamStaffAssignmentState FromArray(string teamSeasonKey, string[] values)
        {
            return new TeamStaffAssignmentState(
                teamSeasonKey,
                values[0],
                values[1],
                values[2],
                values[3],
                values[4]);
        }

        private static string NormalizeOptionalId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    /// <summary>경기 능력치가 아닌 훈련·회복·분석 Context에만 소비되는 팀 스태프 효과다.</summary>
    public sealed class TeamStaffEffectProfile
    {
        public TeamStaffEffectProfile(
            double hittingTrainingEfficiency,
            double pitchingTrainingEfficiency,
            double developmentPointEfficiency,
            double conditionRecoveryEfficiency,
            double scoutingConfidenceModifier)
        {
            HittingTrainingEfficiency = ValidateEfficiency(hittingTrainingEfficiency, nameof(hittingTrainingEfficiency));
            PitchingTrainingEfficiency = ValidateEfficiency(pitchingTrainingEfficiency, nameof(pitchingTrainingEfficiency));
            DevelopmentPointEfficiency = ValidateEfficiency(developmentPointEfficiency, nameof(developmentPointEfficiency));
            ConditionRecoveryEfficiency = ValidateEfficiency(conditionRecoveryEfficiency, nameof(conditionRecoveryEfficiency));
            ScoutingConfidenceModifier = ValidateModifier(scoutingConfidenceModifier, nameof(scoutingConfidenceModifier));
        }

        public double HittingTrainingEfficiency { get; }
        public double PitchingTrainingEfficiency { get; }
        public double DevelopmentPointEfficiency { get; }
        public double ConditionRecoveryEfficiency { get; }
        public double ScoutingConfidenceModifier { get; }

        public static TeamStaffEffectProfile Neutral => new TeamStaffEffectProfile(1d, 1d, 1d, 1d, 0d);

        private static double ValidateEfficiency(double value, string parameterName)
        {
            if (value < 1d || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }

        private static double ValidateModifier(double value, string parameterName)
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    /// <summary>CardTraining이 능력치나 Ceiling을 넘기 전에 조회하는 효율 입력이다.</summary>
    public readonly struct StaffTrainingEfficiencyContext
    {
        public StaffTrainingEfficiencyContext(StaffTrainingDiscipline discipline, bool includeDevelopmentCoach)
        {
            StaffDefinition.ValidateEnum(discipline, nameof(discipline));
            Discipline = discipline;
            IncludeDevelopmentCoach = includeDevelopmentCoach;
        }

        public StaffTrainingDiscipline Discipline { get; }
        public bool IncludeDevelopmentCoach { get; }
    }

    public readonly struct StaffTrainingEfficiencyResult
    {
        public StaffTrainingEfficiencyResult(double efficiencyMultiplier)
        {
            if (efficiencyMultiplier < 1d || double.IsNaN(efficiencyMultiplier) || double.IsInfinity(efficiencyMultiplier))
                throw new ArgumentOutOfRangeException(nameof(efficiencyMultiplier));
            EfficiencyMultiplier = efficiencyMultiplier;
        }

        public double EfficiencyMultiplier { get; }
    }

    /// <summary>한 시장 기간에 한 구단이 받은 결정론적 스태프 계약 제안이다.</summary>
    public sealed class StaffMarketOffer
    {
        public StaffMarketOffer(
            string offerId,
            string staffId,
            string targetTeamSeasonKey,
            string marketPeriodId,
            StaffMarketKind marketKind,
            int contractYears,
            long annualSalary,
            long signingCost)
        {
            OfferId = StaffDefinition.RequireId(offerId, nameof(offerId));
            StaffId = StaffDefinition.RequireId(staffId, nameof(staffId));
            TargetTeamSeasonKey = StaffDefinition.RequireId(targetTeamSeasonKey, nameof(targetTeamSeasonKey));
            MarketPeriodId = StaffDefinition.RequireId(marketPeriodId, nameof(marketPeriodId));
            StaffDefinition.ValidateEnum(marketKind, nameof(marketKind));
            if (contractYears <= 0)
                throw new ArgumentOutOfRangeException(nameof(contractYears));
            if (annualSalary <= 0 || signingCost < 0)
                throw new ArgumentOutOfRangeException(nameof(annualSalary));
            MarketKind = marketKind;
            ContractYears = contractYears;
            AnnualSalary = annualSalary;
            SigningCost = signingCost;
        }

        public string OfferId { get; }
        public string StaffId { get; }
        public string TargetTeamSeasonKey { get; }
        public string MarketPeriodId { get; }
        public StaffMarketKind MarketKind { get; }
        public int ContractYears { get; }
        public long AnnualSalary { get; }
        public long SigningCost { get; }

        public static string CreateStableOfferId(string marketPeriodId, string teamSeasonKey, string staffId)
        {
            return $"staff-offer:{StaffDefinition.RequireId(marketPeriodId, nameof(marketPeriodId))}:" +
                   $"{StaffDefinition.RequireId(teamSeasonKey, nameof(teamSeasonKey))}:" +
                   StaffDefinition.RequireId(staffId, nameof(staffId));
        }
    }

    /// <summary>Runtime이 ManagerEconomyState에 한 번만 반영해야 하는 Money 차감 명령이다.</summary>
    public sealed class StaffMoneyCommand
    {
        public StaffMoneyCommand(
            string transactionId,
            string teamSeasonKey,
            StaffMoneyReason reason,
            long amount)
        {
            TransactionId = StaffDefinition.RequireId(transactionId, nameof(transactionId));
            TeamSeasonKey = StaffDefinition.RequireId(teamSeasonKey, nameof(teamSeasonKey));
            StaffDefinition.ValidateEnum(reason, nameof(reason));
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            Reason = reason;
            Amount = amount;
        }

        public string TransactionId { get; }
        public string TeamSeasonKey { get; }
        public StaffMoneyReason Reason { get; }
        public long Amount { get; }
    }

    public sealed class StaffSigningCommand
    {
        public StaffSigningCommand(
            string contractId,
            string transactionId,
            string targetTeamSeasonKey,
            int startSeason,
            long availableMoney)
        {
            ContractId = StaffDefinition.RequireId(contractId, nameof(contractId));
            TransactionId = StaffDefinition.RequireId(transactionId, nameof(transactionId));
            TargetTeamSeasonKey = StaffDefinition.RequireId(targetTeamSeasonKey, nameof(targetTeamSeasonKey));
            if (startSeason <= 0)
                throw new ArgumentOutOfRangeException(nameof(startSeason));
            if (availableMoney < 0)
                throw new ArgumentOutOfRangeException(nameof(availableMoney));
            StartSeason = startSeason;
            AvailableMoney = availableMoney;
        }

        public string ContractId { get; }
        public string TransactionId { get; }
        public string TargetTeamSeasonKey { get; }
        public int StartSeason { get; }
        public long AvailableMoney { get; }
    }

    public sealed class StaffSigningResult
    {
        private readonly StaffContractState[] _contracts;

        public StaffSigningResult(
            StaffServiceStatus status,
            IReadOnlyList<StaffContractState> contracts,
            TeamStaffAssignmentState assignment,
            StaffContractState signedContract = null,
            string terminatedContractId = null,
            StaffMoneyCommand moneyCommand = null)
        {
            StaffDefinition.ValidateEnum(status, nameof(status));
            Status = status;
            _contracts = CopyContracts(contracts);
            Assignment = assignment ?? throw new ArgumentNullException(nameof(assignment));
            SignedContract = signedContract;
            TerminatedContractId = string.IsNullOrWhiteSpace(terminatedContractId) ? null : terminatedContractId.Trim();
            MoneyCommand = moneyCommand;
        }

        public StaffServiceStatus Status { get; }
        public IReadOnlyList<StaffContractState> Contracts => _contracts;
        public TeamStaffAssignmentState Assignment { get; }
        public StaffContractState SignedContract { get; }
        public string TerminatedContractId { get; }
        public StaffMoneyCommand MoneyCommand { get; }
        public bool IsSuccess => Status == StaffServiceStatus.Succeeded;

        internal static StaffContractState[] CopyContracts(IReadOnlyList<StaffContractState> contracts)
        {
            if (contracts == null)
                throw new ArgumentNullException(nameof(contracts));
            var result = new StaffContractState[contracts.Count];
            var contractIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < contracts.Count; index++)
            {
                StaffContractState contract = contracts[index]
                    ?? throw new ArgumentException("null 스태프 계약이 있습니다.", nameof(contracts));
                if (!contractIds.Add(contract.ContractId))
                    throw new ArgumentException("ContractId는 중복될 수 없습니다.", nameof(contracts));
                result[index] = contract;
            }
            return result;
        }
    }

    public sealed class StaffSalarySettlementCommand
    {
        public StaffSalarySettlementCommand(
            string transactionId,
            string teamSeasonKey,
            int season,
            long availableMoney)
        {
            TransactionId = StaffDefinition.RequireId(transactionId, nameof(transactionId));
            TeamSeasonKey = StaffDefinition.RequireId(teamSeasonKey, nameof(teamSeasonKey));
            if (season <= 0)
                throw new ArgumentOutOfRangeException(nameof(season));
            if (availableMoney < 0)
                throw new ArgumentOutOfRangeException(nameof(availableMoney));
            Season = season;
            AvailableMoney = availableMoney;
        }

        public string TransactionId { get; }
        public string TeamSeasonKey { get; }
        public int Season { get; }
        public long AvailableMoney { get; }
    }

    public sealed class StaffSalarySettlementResult
    {
        private readonly StaffContractState[] _contracts;

        public StaffSalarySettlementResult(
            StaffServiceStatus status,
            IReadOnlyList<StaffContractState> contracts,
            long totalSalary,
            StaffMoneyCommand moneyCommand)
        {
            StaffDefinition.ValidateEnum(status, nameof(status));
            if (totalSalary < 0)
                throw new ArgumentOutOfRangeException(nameof(totalSalary));
            Status = status;
            _contracts = StaffSigningResult.CopyContracts(contracts);
            TotalSalary = totalSalary;
            MoneyCommand = moneyCommand;
        }

        public StaffServiceStatus Status { get; }
        public IReadOnlyList<StaffContractState> Contracts => _contracts;
        public long TotalSalary { get; }
        public StaffMoneyCommand MoneyCommand { get; }
        public bool IsSuccess => Status == StaffServiceStatus.Succeeded || Status == StaffServiceStatus.NoChange;
    }

    public sealed class StaffContractAdvanceResult
    {
        private readonly StaffContractState[] _contracts;
        private readonly string[] _expiredContractIds;

        public StaffContractAdvanceResult(
            StaffServiceStatus status,
            IReadOnlyList<StaffContractState> contracts,
            TeamStaffAssignmentState assignment,
            IReadOnlyList<string> expiredContractIds)
        {
            StaffDefinition.ValidateEnum(status, nameof(status));
            Status = status;
            _contracts = StaffSigningResult.CopyContracts(contracts);
            Assignment = assignment ?? throw new ArgumentNullException(nameof(assignment));
            _expiredContractIds = CopyIds(expiredContractIds);
        }

        public StaffServiceStatus Status { get; }
        public IReadOnlyList<StaffContractState> Contracts => _contracts;
        public TeamStaffAssignmentState Assignment { get; }
        public IReadOnlyList<string> ExpiredContractIds => _expiredContractIds;
        public bool IsSuccess => Status == StaffServiceStatus.Succeeded || Status == StaffServiceStatus.NoChange;

        private static string[] CopyIds(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();
            var result = new string[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = StaffDefinition.RequireId(source[index], nameof(source));
            return result;
        }
    }

    /// <summary>Quality별 효과·급여·시장 가중치를 한 행으로 보관한다.</summary>
    public sealed class StaffQualityBalance
    {
        public StaffQualityBalance(
            int qualityTier,
            StaffSalaryBand salaryBand,
            double effectBonus,
            long baseAnnualSalary,
            double marketWeight)
        {
            if (qualityTier < StaffDefinition.MinimumQualityTier || qualityTier > StaffDefinition.MaximumQualityTier)
                throw new ArgumentOutOfRangeException(nameof(qualityTier));
            StaffDefinition.ValidateEnum(salaryBand, nameof(salaryBand));
            if (effectBonus < 0d || double.IsNaN(effectBonus) || double.IsInfinity(effectBonus))
                throw new ArgumentOutOfRangeException(nameof(effectBonus));
            if (baseAnnualSalary <= 0)
                throw new ArgumentOutOfRangeException(nameof(baseAnnualSalary));
            if (marketWeight <= 0d || double.IsNaN(marketWeight) || double.IsInfinity(marketWeight))
                throw new ArgumentOutOfRangeException(nameof(marketWeight));
            QualityTier = qualityTier;
            SalaryBand = salaryBand;
            EffectBonus = effectBonus;
            BaseAnnualSalary = baseAnnualSalary;
            MarketWeight = marketWeight;
        }

        public int QualityTier { get; }
        public StaffSalaryBand SalaryBand { get; }
        public double EffectBonus { get; }
        public long BaseAnnualSalary { get; }
        public double MarketWeight { get; }
    }

    public sealed class StaffSalaryBandBalance
    {
        public StaffSalaryBandBalance(StaffSalaryBand salaryBand, double salaryMultiplier)
        {
            StaffDefinition.ValidateEnum(salaryBand, nameof(salaryBand));
            if (salaryMultiplier <= 0d || double.IsNaN(salaryMultiplier) || double.IsInfinity(salaryMultiplier))
                throw new ArgumentOutOfRangeException(nameof(salaryMultiplier));
            SalaryBand = salaryBand;
            SalaryMultiplier = salaryMultiplier;
        }

        public StaffSalaryBand SalaryBand { get; }
        public double SalaryMultiplier { get; }
    }

    /// <summary>역할별 효과·급여와 AI Club DNA 해석 가중치를 보관한다.</summary>
    public sealed class StaffRoleBalance
    {
        private readonly StaffSpecialtyTag[] _specialties;
        private readonly double[] _aiClubDnaWeights;

        public StaffRoleBalance(
            StaffRole role,
            double effectMultiplier,
            double salaryMultiplier,
            IReadOnlyList<StaffSpecialtyTag> specialties,
            IReadOnlyList<double> aiClubDnaWeights)
        {
            StaffDefinition.ValidateEnum(role, nameof(role));
            if (effectMultiplier <= 0d || salaryMultiplier <= 0d ||
                double.IsNaN(effectMultiplier) || double.IsInfinity(effectMultiplier) ||
                double.IsNaN(salaryMultiplier) || double.IsInfinity(salaryMultiplier))
                throw new ArgumentOutOfRangeException(nameof(effectMultiplier));
            Role = role;
            EffectMultiplier = effectMultiplier;
            SalaryMultiplier = salaryMultiplier;
            _specialties = CopySpecialties(specialties);
            _aiClubDnaWeights = CopyWeights(aiClubDnaWeights, 8, nameof(aiClubDnaWeights));
        }

        public StaffRole Role { get; }
        public double EffectMultiplier { get; }
        public double SalaryMultiplier { get; }
        public IReadOnlyList<StaffSpecialtyTag> Specialties => _specialties;

        public double GetAiClubDnaWeight(int index)
        {
            if (index < 0 || index >= _aiClubDnaWeights.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _aiClubDnaWeights[index];
        }

        private static StaffSpecialtyTag[] CopySpecialties(IReadOnlyList<StaffSpecialtyTag> source)
        {
            if (source == null || source.Count == 0)
                throw new ArgumentException("역할별 Specialty 후보가 필요합니다.", nameof(source));
            var result = new StaffSpecialtyTag[source.Count];
            var unique = new HashSet<StaffSpecialtyTag>();
            for (int index = 0; index < source.Count; index++)
            {
                StaffDefinition.ValidateEnum(source[index], nameof(source));
                if (!unique.Add(source[index]))
                    throw new ArgumentException("역할별 Specialty 후보는 중복될 수 없습니다.", nameof(source));
                result[index] = source[index];
            }
            return result;
        }

        internal static double[] CopyWeights(IReadOnlyList<double> source, int count, string parameterName)
        {
            if (source == null || source.Count != count)
                throw new ArgumentException("가중치 개수가 올바르지 않습니다.", parameterName);
            var result = new double[count];
            double sum = 0d;
            for (int index = 0; index < source.Count; index++)
            {
                double value = source[index];
                if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(parameterName);
                result[index] = value;
                sum += value;
            }
            if (sum <= 0d)
                throw new ArgumentException("하나 이상의 양수 가중치가 필요합니다.", parameterName);
            return result;
        }
    }

    public sealed class StaffSpecialtyBalance
    {
        public StaffSpecialtyBalance(StaffSpecialtyTag specialty, StaffRole role, double effectBonus)
        {
            StaffDefinition.ValidateEnum(specialty, nameof(specialty));
            StaffDefinition.ValidateEnum(role, nameof(role));
            if (effectBonus < 0d || double.IsNaN(effectBonus) || double.IsInfinity(effectBonus))
                throw new ArgumentOutOfRangeException(nameof(effectBonus));
            Specialty = specialty;
            Role = role;
            EffectBonus = effectBonus;
        }

        public StaffSpecialtyTag Specialty { get; }
        public StaffRole Role { get; }
        public double EffectBonus { get; }
    }

    public sealed class StaffPhilosophyBalance
    {
        private readonly double[] _effectBonusByRole;

        public StaffPhilosophyBalance(StaffPhilosophyTag philosophy, IReadOnlyList<double> effectBonusByRole)
        {
            StaffDefinition.ValidateEnum(philosophy, nameof(philosophy));
            Philosophy = philosophy;
            _effectBonusByRole = StaffRoleBalanceExtensions.CopyWeightsAllowZero(effectBonusByRole, 5, nameof(effectBonusByRole));
        }

        public StaffPhilosophyTag Philosophy { get; }

        public double GetEffectBonus(StaffRole role)
        {
            StaffDefinition.ValidateEnum(role, nameof(role));
            return _effectBonusByRole[(int)role];
        }
    }

    internal static class StaffRoleBalanceExtensions
    {
        public static double[] CopyWeightsAllowZero(
            IReadOnlyList<double> source,
            int count,
            string parameterName)
        {
            if (source == null || source.Count != count)
                throw new ArgumentException("효과 값 개수가 올바르지 않습니다.", parameterName);
            var result = new double[count];
            for (int index = 0; index < source.Count; index++)
            {
                double value = source[index];
                if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(parameterName);
                result[index] = value;
            }
            return result;
        }
    }

    /// <summary>시장 규모·계약 기간·급여 비용을 조정하는 데이터 묶음이다.</summary>
    public sealed class StaffMarketBalance
    {
        private readonly int[] _preferredContractYears;
        private readonly double[] _contractPreferenceWeights;
        private readonly double[] _marketSalaryMultipliers;

        public StaffMarketBalance(
            int offseasonOfferCount,
            int midseasonOfferCount,
            int minimumContractYears,
            int maximumContractYears,
            IReadOnlyList<int> preferredContractYears,
            IReadOnlyList<double> contractPreferenceWeights,
            IReadOnlyList<double> marketSalaryMultipliers,
            double minimumSalaryVariance,
            double maximumSalaryVariance,
            double signingCostRate,
            double replacementPenaltyRate,
            double leagueQualityBiasPerGrade,
            long salaryRoundingUnit)
        {
            if (offseasonOfferCount <= 0 || midseasonOfferCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(offseasonOfferCount));
            if (minimumContractYears <= 0 || maximumContractYears < minimumContractYears)
                throw new ArgumentOutOfRangeException(nameof(minimumContractYears));
            _preferredContractYears = CopyContractYears(
                preferredContractYears,
                minimumContractYears,
                maximumContractYears);
            _contractPreferenceWeights = StaffRoleBalance.CopyWeights(
                contractPreferenceWeights,
                Enum.GetValues(typeof(StaffContractPreference)).Length,
                nameof(contractPreferenceWeights));
            _marketSalaryMultipliers = CopyPositive(
                marketSalaryMultipliers,
                Enum.GetValues(typeof(StaffMarketKind)).Length,
                nameof(marketSalaryMultipliers));
            if (minimumSalaryVariance <= 0d || maximumSalaryVariance < minimumSalaryVariance ||
                double.IsNaN(minimumSalaryVariance) || double.IsInfinity(minimumSalaryVariance) ||
                double.IsNaN(maximumSalaryVariance) || double.IsInfinity(maximumSalaryVariance))
                throw new ArgumentOutOfRangeException(nameof(minimumSalaryVariance));
            if (signingCostRate < 0d || replacementPenaltyRate < 0d || leagueQualityBiasPerGrade < 0d ||
                double.IsNaN(signingCostRate) || double.IsInfinity(signingCostRate) ||
                double.IsNaN(replacementPenaltyRate) || double.IsInfinity(replacementPenaltyRate) ||
                double.IsNaN(leagueQualityBiasPerGrade) || double.IsInfinity(leagueQualityBiasPerGrade))
                throw new ArgumentOutOfRangeException(nameof(signingCostRate));
            if (salaryRoundingUnit <= 0)
                throw new ArgumentOutOfRangeException(nameof(salaryRoundingUnit));
            OffseasonOfferCount = offseasonOfferCount;
            MidseasonOfferCount = midseasonOfferCount;
            MinimumContractYears = minimumContractYears;
            MaximumContractYears = maximumContractYears;
            MinimumSalaryVariance = minimumSalaryVariance;
            MaximumSalaryVariance = maximumSalaryVariance;
            SigningCostRate = signingCostRate;
            ReplacementPenaltyRate = replacementPenaltyRate;
            LeagueQualityBiasPerGrade = leagueQualityBiasPerGrade;
            SalaryRoundingUnit = salaryRoundingUnit;
        }

        public int OffseasonOfferCount { get; }
        public int MidseasonOfferCount { get; }
        public int MinimumContractYears { get; }
        public int MaximumContractYears { get; }
        public double MinimumSalaryVariance { get; }
        public double MaximumSalaryVariance { get; }
        public double SigningCostRate { get; }
        public double ReplacementPenaltyRate { get; }
        public double LeagueQualityBiasPerGrade { get; }
        public long SalaryRoundingUnit { get; }

        public int GetOfferCount(StaffMarketKind marketKind)
        {
            StaffDefinition.ValidateEnum(marketKind, nameof(marketKind));
            return marketKind == StaffMarketKind.Offseason ? OffseasonOfferCount : MidseasonOfferCount;
        }

        public int GetPreferredContractYears(StaffContractPreference preference)
        {
            StaffDefinition.ValidateEnum(preference, nameof(preference));
            return _preferredContractYears[(int)preference];
        }

        public double GetContractPreferenceWeight(StaffContractPreference preference)
        {
            StaffDefinition.ValidateEnum(preference, nameof(preference));
            return _contractPreferenceWeights[(int)preference];
        }

        public double GetMarketSalaryMultiplier(StaffMarketKind marketKind)
        {
            StaffDefinition.ValidateEnum(marketKind, nameof(marketKind));
            return _marketSalaryMultipliers[(int)marketKind];
        }

        private static int[] CopyContractYears(IReadOnlyList<int> source, int minimum, int maximum)
        {
            int count = Enum.GetValues(typeof(StaffContractPreference)).Length;
            if (source == null || source.Count != count)
                throw new ArgumentException("모든 계약 선호의 기간이 필요합니다.", nameof(source));
            var result = new int[count];
            for (int index = 0; index < count; index++)
            {
                if (source[index] < minimum || source[index] > maximum)
                    throw new ArgumentOutOfRangeException(nameof(source));
                result[index] = source[index];
            }
            return result;
        }

        private static double[] CopyPositive(IReadOnlyList<double> source, int count, string parameterName)
        {
            if (source == null || source.Count != count)
                throw new ArgumentException("값 개수가 올바르지 않습니다.", parameterName);
            var result = new double[count];
            for (int index = 0; index < count; index++)
            {
                double value = source[index];
                if (value <= 0d || double.IsNaN(value) || double.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(parameterName);
                result[index] = value;
            }
            return result;
        }
    }

    /// <summary>AI 스태프 효과의 리그·감독·Club DNA 기여와 Seed 분산을 보관한다.</summary>
    public sealed class AiStaffBalance
    {
        private readonly double[] _gradeEffectBonus;

        public AiStaffBalance(
            IReadOnlyList<double> gradeEffectBonus,
            double managerQualityCoefficient,
            double clubDnaCoefficient,
            double seedVariance)
        {
            _gradeEffectBonus = StaffRoleBalanceExtensions.CopyWeightsAllowZero(
                gradeEffectBonus,
                Enum.GetValues(typeof(LeagueGrade)).Length,
                nameof(gradeEffectBonus));
            if (managerQualityCoefficient < 0d || clubDnaCoefficient < 0d || seedVariance < 0d ||
                double.IsNaN(managerQualityCoefficient) || double.IsInfinity(managerQualityCoefficient) ||
                double.IsNaN(clubDnaCoefficient) || double.IsInfinity(clubDnaCoefficient) ||
                double.IsNaN(seedVariance) || double.IsInfinity(seedVariance))
                throw new ArgumentOutOfRangeException(nameof(managerQualityCoefficient));
            ManagerQualityCoefficient = managerQualityCoefficient;
            ClubDnaCoefficient = clubDnaCoefficient;
            SeedVariance = seedVariance;
        }

        public double ManagerQualityCoefficient { get; }
        public double ClubDnaCoefficient { get; }
        public double SeedVariance { get; }

        public double GetGradeEffectBonus(LeagueGrade grade)
        {
            StaffDefinition.ValidateEnum(grade, nameof(grade));
            return _gradeEffectBonus[(int)grade];
        }
    }

    /// <summary>스태프 효과·급여·시장·AI 곡선을 Resolver에 주입하는 밸런스 정본이다.</summary>
    public sealed class StaffBalanceTable
    {
        private readonly StaffQualityBalance[] _qualityByTier;
        private readonly StaffSalaryBandBalance[] _salaryBands;
        private readonly StaffRoleBalance[] _roles;
        private readonly StaffSpecialtyBalance[] _specialties;
        private readonly StaffPhilosophyBalance[] _philosophies;

        public StaffBalanceTable(
            IReadOnlyList<StaffQualityBalance> qualities,
            IReadOnlyList<StaffSalaryBandBalance> salaryBands,
            IReadOnlyList<StaffRoleBalance> roles,
            IReadOnlyList<StaffSpecialtyBalance> specialties,
            IReadOnlyList<StaffPhilosophyBalance> philosophies,
            StaffMarketBalance market,
            AiStaffBalance ai,
            double maximumEffectBonus,
            double maximumScoutingConfidenceModifier)
        {
            _qualityByTier = CopyQualities(qualities);
            _salaryBands = CopyEnumRows(
                salaryBands,
                Enum.GetValues(typeof(StaffSalaryBand)).Length,
                row => (int)row.SalaryBand,
                "SalaryBand");
            _roles = CopyEnumRows(
                roles,
                Enum.GetValues(typeof(StaffRole)).Length,
                row => (int)row.Role,
                "StaffRole");
            _specialties = CopyEnumRows(
                specialties,
                Enum.GetValues(typeof(StaffSpecialtyTag)).Length,
                row => (int)row.Specialty,
                "StaffSpecialtyTag");
            _philosophies = CopyEnumRows(
                philosophies,
                Enum.GetValues(typeof(StaffPhilosophyTag)).Length,
                row => (int)row.Philosophy,
                "StaffPhilosophyTag");
            Market = market ?? throw new ArgumentNullException(nameof(market));
            Ai = ai ?? throw new ArgumentNullException(nameof(ai));
            if (maximumEffectBonus <= 0d || maximumScoutingConfidenceModifier <= 0d ||
                double.IsNaN(maximumEffectBonus) || double.IsInfinity(maximumEffectBonus) ||
                double.IsNaN(maximumScoutingConfidenceModifier) || double.IsInfinity(maximumScoutingConfidenceModifier))
                throw new ArgumentOutOfRangeException(nameof(maximumEffectBonus));
            MaximumEffectBonus = maximumEffectBonus;
            MaximumScoutingConfidenceModifier = maximumScoutingConfidenceModifier;
            ValidateSpecialtyRoles();
        }

        public StaffMarketBalance Market { get; }
        public AiStaffBalance Ai { get; }
        public double MaximumEffectBonus { get; }
        public double MaximumScoutingConfidenceModifier { get; }

        public StaffQualityBalance GetQuality(int qualityTier)
        {
            if (qualityTier < StaffDefinition.MinimumQualityTier || qualityTier > StaffDefinition.MaximumQualityTier)
                throw new ArgumentOutOfRangeException(nameof(qualityTier));
            return _qualityByTier[qualityTier];
        }

        public StaffSalaryBandBalance GetSalaryBand(StaffSalaryBand salaryBand)
        {
            StaffDefinition.ValidateEnum(salaryBand, nameof(salaryBand));
            return _salaryBands[(int)salaryBand];
        }

        public StaffRoleBalance GetRole(StaffRole role)
        {
            StaffDefinition.ValidateEnum(role, nameof(role));
            return _roles[(int)role];
        }

        public StaffSpecialtyBalance GetSpecialty(StaffSpecialtyTag specialty)
        {
            StaffDefinition.ValidateEnum(specialty, nameof(specialty));
            return _specialties[(int)specialty];
        }

        public StaffPhilosophyBalance GetPhilosophy(StaffPhilosophyTag philosophy)
        {
            StaffDefinition.ValidateEnum(philosophy, nameof(philosophy));
            return _philosophies[(int)philosophy];
        }

        public static StaffBalanceTable CreateInitial()
        {
            var qualities = new[]
            {
                new StaffQualityBalance(1, StaffSalaryBand.Budget, 0.02d, MoneyAmount.FromTenThousandWon(8_000L), 32d),
                new StaffQualityBalance(2, StaffSalaryBand.Standard, 0.04d, MoneyAmount.FromTenThousandWon(12_000L), 28d),
                new StaffQualityBalance(3, StaffSalaryBand.Standard, 0.06d, MoneyAmount.FromTenThousandWon(18_000L), 21d),
                new StaffQualityBalance(4, StaffSalaryBand.Premium, 0.08d, MoneyAmount.FromTenThousandWon(27_000L), 13d),
                new StaffQualityBalance(5, StaffSalaryBand.Elite, 0.10d, MoneyAmount.FromTenThousandWon(40_000L), 6d)
            };
            var salaryBands = new[]
            {
                new StaffSalaryBandBalance(StaffSalaryBand.Budget, 0.90d),
                new StaffSalaryBandBalance(StaffSalaryBand.Standard, 1.00d),
                new StaffSalaryBandBalance(StaffSalaryBand.Premium, 1.12d),
                new StaffSalaryBandBalance(StaffSalaryBand.Elite, 1.25d)
            };
            var roles = new[]
            {
                new StaffRoleBalance(StaffRole.HittingCoach, 1.00d, 1.05d,
                    new[] { StaffSpecialtyTag.ContactTraining, StaffSpecialtyTag.PowerTraining, StaffSpecialtyTag.PlateDiscipline },
                    new[] { 0.45d, 0.45d, 0.05d, 0.05d, 0d, 0d, 0d, 0d }),
                new StaffRoleBalance(StaffRole.PitchingCoach, 1.00d, 1.05d,
                    new[] { StaffSpecialtyTag.PitchCommand, StaffSpecialtyTag.PitchMovement, StaffSpecialtyTag.StarterDevelopment, StaffSpecialtyTag.BullpenDevelopment },
                    new[] { 0d, 0d, 0d, 0d, 0.45d, 0.45d, 0.05d, 0.05d }),
                new StaffRoleBalance(StaffRole.DevelopmentCoach, 1.00d, 1.00d,
                    new[] { StaffSpecialtyTag.ProspectDevelopment, StaffSpecialtyTag.VeteranManagement },
                    new[] { 0.05d, 0.05d, 0.05d, 0.05d, 0.05d, 0.05d, 0.55d, 0.15d }),
                new StaffRoleBalance(StaffRole.ConditioningCoach, 0.90d, 0.95d,
                    new[] { StaffSpecialtyTag.RecoveryPlanning, StaffSpecialtyTag.VeteranRecovery },
                    new[] { 0d, 0d, 0.05d, 0.05d, 0.15d, 0.15d, 0.25d, 0.35d }),
                new StaffRoleBalance(StaffRole.ScoutingDirector, 0.75d, 1.00d,
                    new[] { StaffSpecialtyTag.DataAnalysis },
                    new[] { 0.10d, 0.10d, 0.10d, 0.20d, 0.10d, 0.10d, 0.15d, 0.15d })
            };
            var specialties = new[]
            {
                new StaffSpecialtyBalance(StaffSpecialtyTag.ContactTraining, StaffRole.HittingCoach, 0.010d),
                new StaffSpecialtyBalance(StaffSpecialtyTag.PowerTraining, StaffRole.HittingCoach, 0.010d),
                new StaffSpecialtyBalance(StaffSpecialtyTag.PlateDiscipline, StaffRole.HittingCoach, 0.010d),
                new StaffSpecialtyBalance(StaffSpecialtyTag.PitchCommand, StaffRole.PitchingCoach, 0.010d),
                new StaffSpecialtyBalance(StaffSpecialtyTag.PitchMovement, StaffRole.PitchingCoach, 0.010d),
                new StaffSpecialtyBalance(StaffSpecialtyTag.StarterDevelopment, StaffRole.PitchingCoach, 0.010d),
                new StaffSpecialtyBalance(StaffSpecialtyTag.BullpenDevelopment, StaffRole.PitchingCoach, 0.010d),
                new StaffSpecialtyBalance(StaffSpecialtyTag.ProspectDevelopment, StaffRole.DevelopmentCoach, 0.010d),
                new StaffSpecialtyBalance(StaffSpecialtyTag.VeteranManagement, StaffRole.DevelopmentCoach, 0.008d),
                new StaffSpecialtyBalance(StaffSpecialtyTag.RecoveryPlanning, StaffRole.ConditioningCoach, 0.010d),
                new StaffSpecialtyBalance(StaffSpecialtyTag.VeteranRecovery, StaffRole.ConditioningCoach, 0.008d),
                new StaffSpecialtyBalance(StaffSpecialtyTag.DataAnalysis, StaffRole.ScoutingDirector, 0.010d)
            };
            var philosophies = new[]
            {
                new StaffPhilosophyBalance(StaffPhilosophyTag.Fundamentals, new[] { 0.006d, 0.006d, 0.002d, 0d, 0d }),
                new StaffPhilosophyBalance(StaffPhilosophyTag.AggressiveDevelopment, new[] { 0.003d, 0.003d, 0.007d, 0d, 0d }),
                new StaffPhilosophyBalance(StaffPhilosophyTag.LongTermDevelopment, new[] { 0.001d, 0.001d, 0.008d, 0.002d, 0d }),
                new StaffPhilosophyBalance(StaffPhilosophyTag.WorkloadManagement, new[] { 0d, 0.002d, 0.002d, 0.008d, 0d }),
                new StaffPhilosophyBalance(StaffPhilosophyTag.EvidenceBased, new[] { 0.002d, 0.002d, 0.003d, 0d, 0.008d }),
                new StaffPhilosophyBalance(StaffPhilosophyTag.PlayerCentered, new[] { 0.002d, 0.002d, 0.005d, 0.005d, 0d })
            };
            var market = new StaffMarketBalance(
                10,
                5,
                1,
                3,
                new[] { 1, 2, 3 },
                new[] { 0.30d, 0.45d, 0.25d },
                new[] { 1.00d, 1.08d },
                0.92d,
                1.08d,
                0.10d,
                0.25d,
                0.025d,
                MoneyAmount.FromTenThousandWon(100L));
            var ai = new AiStaffBalance(
                new[] { 0.010d, 0.015d, 0.020d, 0.025d, 0.030d, 0.035d, 0.040d, 0.045d, 0.050d, 0.055d },
                0.030d,
                0.035d,
                0.005d);
            return new StaffBalanceTable(
                qualities,
                salaryBands,
                roles,
                specialties,
                philosophies,
                market,
                ai,
                0.12d,
                0.10d);
        }

        private static StaffQualityBalance[] CopyQualities(IReadOnlyList<StaffQualityBalance> source)
        {
            int count = StaffDefinition.MaximumQualityTier + 1;
            if (source == null || source.Count != StaffDefinition.MaximumQualityTier)
                throw new ArgumentException("Quality 1~5의 모든 행이 필요합니다.", nameof(source));
            var result = new StaffQualityBalance[count];
            for (int index = 0; index < source.Count; index++)
            {
                StaffQualityBalance row = source[index]
                    ?? throw new ArgumentException("null Quality 밸런스가 있습니다.", nameof(source));
                if (result[row.QualityTier] != null)
                    throw new ArgumentException("Quality 밸런스는 중복될 수 없습니다.", nameof(source));
                result[row.QualityTier] = row;
            }
            for (int tier = StaffDefinition.MinimumQualityTier; tier <= StaffDefinition.MaximumQualityTier; tier++)
                if (result[tier] == null) throw new ArgumentException("Quality 밸런스가 누락되었습니다.", nameof(source));
            return result;
        }

        private static T[] CopyEnumRows<T>(
            IReadOnlyList<T> source,
            int expectedCount,
            Func<T, int> getIndex,
            string label) where T : class
        {
            if (source == null || source.Count != expectedCount)
                throw new ArgumentException($"모든 {label} 밸런스 행이 필요합니다.", nameof(source));
            var result = new T[expectedCount];
            for (int index = 0; index < source.Count; index++)
            {
                T row = source[index] ?? throw new ArgumentException($"null {label} 밸런스가 있습니다.", nameof(source));
                int rowIndex = getIndex(row);
                if (rowIndex < 0 || rowIndex >= result.Length || result[rowIndex] != null)
                    throw new ArgumentException($"{label} 밸런스가 누락되거나 중복되었습니다.", nameof(source));
                result[rowIndex] = row;
            }
            for (int index = 0; index < result.Length; index++)
                if (result[index] == null) throw new ArgumentException($"{label} 밸런스가 누락되었습니다.", nameof(source));
            return result;
        }

        private void ValidateSpecialtyRoles()
        {
            for (int roleIndex = 0; roleIndex < _roles.Length; roleIndex++)
            {
                StaffRoleBalance role = _roles[roleIndex];
                for (int index = 0; index < role.Specialties.Count; index++)
                {
                    StaffSpecialtyBalance specialty = GetSpecialty(role.Specialties[index]);
                    if (specialty.Role != role.Role)
                        throw new ArgumentException("Role의 Specialty 후보와 Specialty 효과 역할이 일치하지 않습니다.");
                }
            }
        }
    }
}
