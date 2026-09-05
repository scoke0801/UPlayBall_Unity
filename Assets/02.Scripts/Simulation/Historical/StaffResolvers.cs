using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>검증된 가상 이름 후보와 World Seed로 Stable Staff 카탈로그를 생성한다.</summary>
    public sealed class StaffCatalogGenerator
    {
        public const string CurrentVersion = "staff-catalog-v1";

        private const ulong NameStream = 0x53544146464E414DUL;
        private const ulong AttributeStream = 0x5354414646415454UL;

        public StaffCatalog Generate(
            StaffNameCatalog names,
            int countPerRole,
            ulong worldSeed,
            StaffBalanceTable balance)
        {
            if (names == null)
                throw new ArgumentNullException(nameof(names));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));
            if (countPerRole <= 0)
                throw new ArgumentOutOfRangeException(nameof(countPerRole));

            int roleCount = Enum.GetValues(typeof(StaffRole)).Length;
            int totalCount = checked(roleCount * countPerRole);
            if (names.Names.Count < totalCount)
                throw new InvalidOperationException("가상 스태프 이름 후보가 생성 인원보다 적습니다.");

            string[] shuffledNames = ShuffleNames(names.Names, worldSeed);
            var random = new Pcg32Random(DeterministicSeed.Derive(worldSeed, AttributeStream));
            var definitions = new StaffDefinition[totalCount];
            int resultIndex = 0;
            for (int roleIndex = 0; roleIndex < roleCount; roleIndex++)
            {
                var role = (StaffRole)roleIndex;
                StaffRoleBalance roleBalance = balance.GetRole(role);
                for (int ordinal = 0; ordinal < countPerRole; ordinal++)
                {
                    int qualityTier = RollQuality(random, balance);
                    StaffQualityBalance quality = balance.GetQuality(qualityTier);
                    StaffSpecialtyTag specialty = roleBalance.Specialties[
                        NextIndex(random, roleBalance.Specialties.Count)];
                    var philosophy = (StaffPhilosophyTag)NextIndex(
                        random,
                        Enum.GetValues(typeof(StaffPhilosophyTag)).Length);
                    StaffContractPreference preference = RollContractPreference(random, balance.Market);
                    string staffId = CreateStableStaffId(worldSeed, role, ordinal);
                    definitions[resultIndex] = new StaffDefinition(
                        staffId,
                        shuffledNames[resultIndex],
                        role,
                        qualityTier,
                        quality.SalaryBand,
                        preference,
                        new[] { specialty },
                        new[] { philosophy });
                    resultIndex++;
                }
            }
            return new StaffCatalog(definitions);
        }

        public static string CreateStableStaffId(ulong worldSeed, StaffRole role, int ordinal)
        {
            if (!Enum.IsDefined(typeof(StaffRole), role))
                throw new ArgumentOutOfRangeException(nameof(role));
            if (ordinal < 0)
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            return $"staff:{worldSeed:x16}:{(int)role:D2}:{ordinal:D4}";
        }

        private static string[] ShuffleNames(IReadOnlyList<string> names, ulong worldSeed)
        {
            var result = new string[names.Count];
            for (int index = 0; index < names.Count; index++)
                result[index] = names[index];
            var random = new Pcg32Random(DeterministicSeed.Derive(worldSeed, NameStream));
            for (int index = result.Length - 1; index > 0; index--)
            {
                int selected = NextIndex(random, index + 1);
                string value = result[index];
                result[index] = result[selected];
                result[selected] = value;
            }
            return result;
        }

        private static int RollQuality(IRandomSource random, StaffBalanceTable balance)
        {
            double totalWeight = 0d;
            for (int tier = StaffDefinition.MinimumQualityTier; tier <= StaffDefinition.MaximumQualityTier; tier++)
                totalWeight += balance.GetQuality(tier).MarketWeight;
            double roll = RequireUnitRandom(random.NextDouble()) * totalWeight;
            double cumulative = 0d;
            for (int tier = StaffDefinition.MinimumQualityTier; tier <= StaffDefinition.MaximumQualityTier; tier++)
            {
                cumulative += balance.GetQuality(tier).MarketWeight;
                if (roll < cumulative)
                    return tier;
            }
            return StaffDefinition.MaximumQualityTier;
        }

        private static StaffContractPreference RollContractPreference(
            IRandomSource random,
            StaffMarketBalance balance)
        {
            int count = Enum.GetValues(typeof(StaffContractPreference)).Length;
            double totalWeight = 0d;
            for (int index = 0; index < count; index++)
                totalWeight += balance.GetContractPreferenceWeight((StaffContractPreference)index);
            double roll = RequireUnitRandom(random.NextDouble()) * totalWeight;
            double cumulative = 0d;
            for (int index = 0; index < count; index++)
            {
                var preference = (StaffContractPreference)index;
                cumulative += balance.GetContractPreferenceWeight(preference);
                if (roll < cumulative)
                    return preference;
            }
            return StaffContractPreference.LongTerm;
        }

        internal static int NextIndex(IRandomSource random, int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            return (int)(RequireUnitRandom(random.NextDouble()) * count);
        }

        internal static double RequireUnitRandom(double value)
        {
            if (value < 0d || value >= 1d || double.IsNaN(value))
                throw new InvalidOperationException("IRandomSource는 0 이상 1 미만의 값을 반환해야 합니다.");
            return value;
        }
    }

    /// <summary>계약 중이 아닌 스태프 중 구단·기간·Seed에 맞는 시장 제안을 생성한다.</summary>
    public sealed class StaffMarketResolver
    {
        private const ulong MarketStream = 0x53544146464D4B54UL;

        public IReadOnlyList<StaffMarketOffer> CreateOffers(
            StaffCatalog catalog,
            IReadOnlyList<StaffContractState> contracts,
            string targetTeamSeasonKey,
            string marketPeriodId,
            StaffMarketKind marketKind,
            LeagueGrade leagueGrade,
            ulong seed,
            StaffBalanceTable balance)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (contracts == null)
                throw new ArgumentNullException(nameof(contracts));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));
            if (!Enum.IsDefined(typeof(StaffMarketKind), marketKind))
                throw new ArgumentOutOfRangeException(nameof(marketKind));
            if (!Enum.IsDefined(typeof(LeagueGrade), leagueGrade))
                throw new ArgumentOutOfRangeException(nameof(leagueGrade));
            string teamKey = RequireId(targetTeamSeasonKey, nameof(targetTeamSeasonKey));
            string periodId = RequireId(marketPeriodId, nameof(marketPeriodId));

            var unavailableStaffIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < contracts.Count; index++)
            {
                StaffContractState contract = contracts[index]
                    ?? throw new ArgumentException("null 스태프 계약이 있습니다.", nameof(contracts));
                if (contract.IsActive && !unavailableStaffIds.Add(contract.StaffId))
                    throw new InvalidOperationException("한 스태프에 활성 계약이 둘 이상 존재합니다.");
            }

            var candidates = new List<StaffDefinition>();
            IReadOnlyList<StaffDefinition> staff = catalog.Staff;
            for (int index = 0; index < staff.Count; index++)
                if (!unavailableStaffIds.Contains(staff[index].StaffId)) candidates.Add(staff[index]);

            int offerCount = Math.Min(balance.Market.GetOfferCount(marketKind), candidates.Count);
            var offers = new StaffMarketOffer[offerCount];
            ulong marketKey = StaffDeterministicKey.Hash($"{teamKey}|{periodId}|{(int)marketKind}");
            ulong marketSeed = DeterministicSeed.Derive(DeterministicSeed.Derive(seed, MarketStream), marketKey);
            var random = new Pcg32Random(marketSeed);
            for (int index = 0; index < offers.Length; index++)
            {
                var preferredRole = (StaffRole)(index % Enum.GetValues(typeof(StaffRole)).Length);
                int selectedIndex = SelectCandidate(candidates, preferredRole, leagueGrade, random, balance);
                StaffDefinition definition = candidates[selectedIndex];
                candidates.RemoveAt(selectedIndex);
                int contractYears = balance.Market.GetPreferredContractYears(definition.ContractPreference);
                long annualSalary = CalculateAnnualSalary(definition, marketKind, random, balance);
                long signingCost = RoundCurrency(
                    annualSalary * balance.Market.SigningCostRate,
                    balance.Market.SalaryRoundingUnit);
                string offerId = StaffMarketOffer.CreateStableOfferId(periodId, teamKey, definition.StaffId);
                offers[index] = new StaffMarketOffer(
                    offerId,
                    definition.StaffId,
                    teamKey,
                    periodId,
                    marketKind,
                    contractYears,
                    annualSalary,
                    signingCost);
            }
            return offers;
        }

        private static int SelectCandidate(
            IReadOnlyList<StaffDefinition> candidates,
            StaffRole preferredRole,
            LeagueGrade leagueGrade,
            IRandomSource random,
            StaffBalanceTable balance)
        {
            bool hasPreferredRole = false;
            for (int index = 0; index < candidates.Count; index++)
                if (candidates[index].Role == preferredRole) hasPreferredRole = true;
            double totalWeight = 0d;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (!hasPreferredRole || candidates[index].Role == preferredRole)
                    totalWeight += GetCandidateWeight(candidates[index], leagueGrade, balance);
            }
            double roll = StaffCatalogGenerator.RequireUnitRandom(random.NextDouble()) * totalWeight;
            double cumulative = 0d;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (hasPreferredRole && candidates[index].Role != preferredRole)
                    continue;
                cumulative += GetCandidateWeight(candidates[index], leagueGrade, balance);
                if (roll < cumulative)
                    return index;
            }
            for (int index = candidates.Count - 1; index >= 0; index--)
                if (!hasPreferredRole || candidates[index].Role == preferredRole) return index;
            throw new InvalidOperationException("스태프 시장 후보가 없습니다.");
        }

        private static double GetCandidateWeight(
            StaffDefinition definition,
            LeagueGrade leagueGrade,
            StaffBalanceTable balance)
        {
            StaffQualityBalance quality = balance.GetQuality(definition.QualityTier);
            double leagueBias = 1d +
                (int)leagueGrade *
                balance.Market.LeagueQualityBiasPerGrade *
                (definition.QualityTier - StaffDefinition.MinimumQualityTier);
            return quality.MarketWeight * leagueBias;
        }

        private static long CalculateAnnualSalary(
            StaffDefinition definition,
            StaffMarketKind marketKind,
            IRandomSource random,
            StaffBalanceTable balance)
        {
            StaffQualityBalance quality = balance.GetQuality(definition.QualityTier);
            double varianceRoll = StaffCatalogGenerator.RequireUnitRandom(random.NextDouble());
            double variance = balance.Market.MinimumSalaryVariance +
                (balance.Market.MaximumSalaryVariance - balance.Market.MinimumSalaryVariance) * varianceRoll;
            double salary = quality.BaseAnnualSalary *
                balance.GetSalaryBand(definition.SalaryBand).SalaryMultiplier *
                balance.GetRole(definition.Role).SalaryMultiplier *
                balance.Market.GetMarketSalaryMultiplier(marketKind) *
                variance;
            return Math.Max(balance.Market.SalaryRoundingUnit, RoundCurrency(salary, balance.Market.SalaryRoundingUnit));
        }

        internal static long RoundCurrency(double amount, long roundingUnit)
        {
            if (amount < 0d || double.IsNaN(amount) || double.IsInfinity(amount))
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (roundingUnit <= 0)
                throw new ArgumentOutOfRangeException(nameof(roundingUnit));
            double rounded = Math.Ceiling(amount / roundingUnit) * roundingUnit;
            if (rounded > long.MaxValue)
                throw new OverflowException("스태프 비용이 long 범위를 넘었습니다.");
            return (long)rounded;
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Stable ID는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>활성 계약과 다섯 배치 슬롯을 검증해 팀 단위 스태프 효과를 한 번 계산한다.</summary>
    public sealed class TeamStaffEffectResolver
    {
        public TeamStaffEffectProfile Resolve(
            StaffCatalog catalog,
            IReadOnlyList<StaffContractState> contracts,
            TeamStaffAssignmentState assignment,
            StaffBalanceTable balance)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (contracts == null)
                throw new ArgumentNullException(nameof(contracts));
            if (assignment == null)
                throw new ArgumentNullException(nameof(assignment));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            var activeContracts = new Dictionary<string, StaffContractState>(StringComparer.Ordinal);
            for (int index = 0; index < contracts.Count; index++)
            {
                StaffContractState contract = contracts[index]
                    ?? throw new ArgumentException("null 스태프 계약이 있습니다.", nameof(contracts));
                if (!contract.IsActive || !string.Equals(contract.TeamSeasonKey, assignment.TeamSeasonKey, StringComparison.Ordinal))
                    continue;
                if (!activeContracts.TryAdd(contract.StaffId, contract))
                    throw new InvalidOperationException("한 스태프에 활성 계약이 둘 이상 존재합니다.");
            }

            double hitting = 1d;
            double pitching = 1d;
            double development = 1d;
            double recovery = 1d;
            double scouting = 0d;
            int roleCount = Enum.GetValues(typeof(StaffRole)).Length;
            for (int roleIndex = 0; roleIndex < roleCount; roleIndex++)
            {
                var role = (StaffRole)roleIndex;
                string staffId = assignment.GetAssignedStaffId(role);
                if (staffId == null)
                    continue;
                StaffDefinition definition = catalog.Get(staffId);
                if (definition.Role != role)
                    throw new InvalidOperationException("스태프 정의의 역할과 배치 슬롯이 일치하지 않습니다.");
                if (!activeContracts.ContainsKey(staffId))
                    throw new InvalidOperationException("활성 계약이 없는 스태프를 배치할 수 없습니다.");

                double bonus = CalculateEffectBonus(definition, balance);
                switch (role)
                {
                    case StaffRole.HittingCoach:
                        hitting = 1d + bonus;
                        break;
                    case StaffRole.PitchingCoach:
                        pitching = 1d + bonus;
                        break;
                    case StaffRole.DevelopmentCoach:
                        development = 1d + bonus;
                        break;
                    case StaffRole.ConditioningCoach:
                        recovery = 1d + bonus;
                        break;
                    case StaffRole.ScoutingDirector:
                        scouting = Math.Min(balance.MaximumScoutingConfidenceModifier, bonus);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(role));
                }
            }
            return new TeamStaffEffectProfile(hitting, pitching, development, recovery, scouting);
        }

        private static double CalculateEffectBonus(StaffDefinition definition, StaffBalanceTable balance)
        {
            double bonus = balance.GetQuality(definition.QualityTier).EffectBonus *
                balance.GetRole(definition.Role).EffectMultiplier;
            for (int index = 0; index < definition.Specialties.Count; index++)
            {
                StaffSpecialtyBalance specialty = balance.GetSpecialty(definition.Specialties[index]);
                if (specialty.Role != definition.Role)
                    throw new InvalidOperationException("스태프 역할과 Specialty가 일치하지 않습니다.");
                bonus += specialty.EffectBonus;
            }
            for (int index = 0; index < definition.Philosophies.Count; index++)
                bonus += balance.GetPhilosophy(definition.Philosophies[index]).GetEffectBonus(definition.Role);
            return Math.Min(balance.MaximumEffectBonus, bonus);
        }
    }

    /// <summary>훈련 Resolver가 Ceiling 판정과 능력치 변경 전에 소비할 효율 배율만 계산한다.</summary>
    public static class StaffTrainingEfficiencyResolver
    {
        public static StaffTrainingEfficiencyResult Resolve(
            TeamStaffEffectProfile profile,
            StaffTrainingEfficiencyContext context)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            double disciplineEfficiency = context.Discipline == StaffTrainingDiscipline.Hitting
                ? profile.HittingTrainingEfficiency
                : profile.PitchingTrainingEfficiency;
            double multiplier = context.IncludeDevelopmentCoach
                ? disciplineEfficiency * profile.DevelopmentPointEfficiency
                : disciplineEfficiency;
            return new StaffTrainingEfficiencyResult(multiplier);
        }
    }

    /// <summary>AI 구단에는 계약 경제 없이 리그·감독·Club DNA·Seed에서 효과 Profile만 제공한다.</summary>
    public sealed class AiStaffProfileResolver
    {
        private const ulong AiProfileStream = 0x5354414646414950UL;

        public TeamStaffEffectProfile Resolve(
            LeagueGrade leagueGrade,
            double managerQuality,
            TeamSeasonClubState clubState,
            ulong seed,
            StaffBalanceTable balance)
        {
            if (!Enum.IsDefined(typeof(LeagueGrade), leagueGrade))
                throw new ArgumentOutOfRangeException(nameof(leagueGrade));
            if (managerQuality < 0d || managerQuality > 100d || double.IsNaN(managerQuality))
                throw new ArgumentOutOfRangeException(nameof(managerQuality));
            if (clubState == null)
                throw new ArgumentNullException(nameof(clubState));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            ulong teamKey = StaffDeterministicKey.Hash(clubState.TeamSeasonKey);
            ulong profileSeed = DeterministicSeed.Derive(DeterministicSeed.Derive(seed, AiProfileStream), teamKey);
            var random = new Pcg32Random(profileSeed);
            double hitting = 1d + ResolveRoleBonus(StaffRole.HittingCoach, leagueGrade, managerQuality, clubState.Ratings, random, balance);
            double pitching = 1d + ResolveRoleBonus(StaffRole.PitchingCoach, leagueGrade, managerQuality, clubState.Ratings, random, balance);
            double development = 1d + ResolveRoleBonus(StaffRole.DevelopmentCoach, leagueGrade, managerQuality, clubState.Ratings, random, balance);
            double recovery = 1d + ResolveRoleBonus(StaffRole.ConditioningCoach, leagueGrade, managerQuality, clubState.Ratings, random, balance);
            double scouting = ResolveRoleBonus(StaffRole.ScoutingDirector, leagueGrade, managerQuality, clubState.Ratings, random, balance);
            scouting = Math.Min(balance.MaximumScoutingConfidenceModifier, scouting);
            return new TeamStaffEffectProfile(hitting, pitching, development, recovery, scouting);
        }

        private static double ResolveRoleBonus(
            StaffRole role,
            LeagueGrade leagueGrade,
            double managerQuality,
            ClubDnaRatings ratings,
            IRandomSource random,
            StaffBalanceTable balance)
        {
            StaffRoleBalance roleBalance = balance.GetRole(role);
            double[] values =
            {
                ratings.Contact,
                ratings.Power,
                ratings.Running,
                ratings.Defense,
                ratings.Rotation,
                ratings.Bullpen,
                ratings.Development,
                ratings.Experience
            };
            double weightedValue = 0d;
            double totalWeight = 0d;
            for (int index = 0; index < values.Length; index++)
            {
                double weight = roleBalance.GetAiClubDnaWeight(index);
                weightedValue += values[index] * weight;
                totalWeight += weight;
            }
            double clubDnaScore = totalWeight > 0d ? weightedValue / totalWeight / 100d : 0d;
            double centeredRoll = StaffCatalogGenerator.RequireUnitRandom(random.NextDouble()) * 2d - 1d;
            double bonus = balance.Ai.GetGradeEffectBonus(leagueGrade) +
                managerQuality / 100d * balance.Ai.ManagerQualityCoefficient +
                clubDnaScore * balance.Ai.ClubDnaCoefficient +
                centeredRoll * balance.Ai.SeedVariance;
            return Math.Max(0d, Math.Min(balance.MaximumEffectBonus, bonus * roleBalance.EffectMultiplier));
        }
    }

    /// <summary>계약·교체·급여·만료 결과와 Money 차감 명령을 원자적으로 계산한다.</summary>
    public sealed class StaffContractService
    {
        public StaffSigningResult TrySign(
            StaffSigningCommand command,
            StaffMarketOffer offer,
            StaffCatalog catalog,
            IReadOnlyList<StaffContractState> contracts,
            TeamStaffAssignmentState assignment,
            StaffBalanceTable balance)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (offer == null)
                throw new ArgumentNullException(nameof(offer));
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (contracts == null)
                throw new ArgumentNullException(nameof(contracts));
            if (assignment == null)
                throw new ArgumentNullException(nameof(assignment));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            if (!string.Equals(command.TargetTeamSeasonKey, offer.TargetTeamSeasonKey, StringComparison.Ordinal) ||
                !string.Equals(command.TargetTeamSeasonKey, assignment.TeamSeasonKey, StringComparison.Ordinal))
                return SigningFailure(StaffServiceStatus.InvalidState, contracts, assignment);

            StaffDefinition definition;
            if (!catalog.TryGet(offer.StaffId, out definition))
                return SigningFailure(StaffServiceStatus.InvalidState, contracts, assignment);
            if (offer.ContractYears < balance.Market.MinimumContractYears ||
                offer.ContractYears > balance.Market.MaximumContractYears ||
                IsAssignedToAnotherRole(assignment, definition.Role, offer.StaffId))
                return SigningFailure(StaffServiceStatus.InvalidState, contracts, assignment);

            StaffContractState replacement = null;
            var contractIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < contracts.Count; index++)
            {
                StaffContractState contract = contracts[index]
                    ?? throw new ArgumentException("null 스태프 계약이 있습니다.", nameof(contracts));
                if (!contractIds.Add(contract.ContractId))
                    return SigningFailure(StaffServiceStatus.InvalidState, contracts, assignment);
                if (string.Equals(contract.ContractId, command.ContractId, StringComparison.Ordinal))
                    return SigningFailure(StaffServiceStatus.InvalidState, contracts, assignment);
                if (contract.IsActive && string.Equals(contract.StaffId, offer.StaffId, StringComparison.Ordinal))
                    return SigningFailure(StaffServiceStatus.StaffUnavailable, contracts, assignment);
            }

            string assignedStaffId = assignment.GetAssignedStaffId(definition.Role);
            if (assignedStaffId != null)
            {
                replacement = FindActiveContract(contracts, assignedStaffId, assignment.TeamSeasonKey);
                if (replacement == null)
                    return SigningFailure(StaffServiceStatus.InvalidState, contracts, assignment);
            }

            long replacementPenalty = replacement == null
                ? 0L
                : StaffMarketResolver.RoundCurrency(
                    replacement.AnnualSalary * balance.Market.ReplacementPenaltyRate,
                    balance.Market.SalaryRoundingUnit);
            long immediateCost = checked(offer.SigningCost + replacementPenalty);
            if (command.AvailableMoney < immediateCost)
                return SigningFailure(StaffServiceStatus.InsufficientMoney, contracts, assignment);

            var signedContract = new StaffContractState(
                command.ContractId,
                offer.StaffId,
                command.TargetTeamSeasonKey,
                command.StartSeason,
                offer.ContractYears,
                offer.AnnualSalary);
            var updatedContracts = new StaffContractState[contracts.Count + 1];
            for (int index = 0; index < contracts.Count; index++)
            {
                StaffContractState contract = contracts[index];
                updatedContracts[index] = replacement != null &&
                    string.Equals(contract.ContractId, replacement.ContractId, StringComparison.Ordinal)
                    ? contract.WithRemainingSeasons(0)
                    : contract;
            }
            updatedContracts[contracts.Count] = signedContract;
            TeamStaffAssignmentState updatedAssignment = assignment.WithAssignment(definition.Role, offer.StaffId);
            StaffMoneyCommand moneyCommand = immediateCost > 0
                ? new StaffMoneyCommand(
                    command.TransactionId,
                    command.TargetTeamSeasonKey,
                    ResolveSigningReason(offer.SigningCost, replacementPenalty),
                    immediateCost)
                : null;
            return new StaffSigningResult(
                StaffServiceStatus.Succeeded,
                updatedContracts,
                updatedAssignment,
                signedContract,
                replacement?.ContractId,
                moneyCommand);
        }

        public StaffSalarySettlementResult SettleSalaries(
            StaffSalarySettlementCommand command,
            IReadOnlyList<StaffContractState> contracts)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (contracts == null)
                throw new ArgumentNullException(nameof(contracts));

            long totalSalary = 0L;
            bool hasUnsettledContract = false;
            for (int index = 0; index < contracts.Count; index++)
            {
                StaffContractState contract = contracts[index]
                    ?? throw new ArgumentException("null 스태프 계약이 있습니다.", nameof(contracts));
                if (!contract.IsActive ||
                    !string.Equals(contract.TeamSeasonKey, command.TeamSeasonKey, StringComparison.Ordinal) ||
                    contract.StartSeason > command.Season)
                    continue;
                if (contract.LastSalaryPaidSeason.HasValue && contract.LastSalaryPaidSeason.Value > command.Season)
                    return new StaffSalarySettlementResult(StaffServiceStatus.InvalidState, contracts, 0L, null);
                if (contract.LastSalaryPaidSeason == command.Season)
                    continue;
                totalSalary = checked(totalSalary + contract.AnnualSalary);
                hasUnsettledContract = true;
            }
            if (!hasUnsettledContract)
                return new StaffSalarySettlementResult(StaffServiceStatus.NoChange, contracts, 0L, null);
            if (command.AvailableMoney < totalSalary)
                return new StaffSalarySettlementResult(StaffServiceStatus.InsufficientMoney, contracts, totalSalary, null);

            var updatedContracts = new StaffContractState[contracts.Count];
            for (int index = 0; index < contracts.Count; index++)
            {
                StaffContractState contract = contracts[index];
                bool shouldPay = contract.IsActive &&
                    string.Equals(contract.TeamSeasonKey, command.TeamSeasonKey, StringComparison.Ordinal) &&
                    contract.StartSeason <= command.Season &&
                    contract.LastSalaryPaidSeason != command.Season;
                updatedContracts[index] = shouldPay ? contract.WithSalaryPaid(command.Season) : contract;
            }
            var moneyCommand = new StaffMoneyCommand(
                command.TransactionId,
                command.TeamSeasonKey,
                StaffMoneyReason.Salary,
                totalSalary);
            return new StaffSalarySettlementResult(
                StaffServiceStatus.Succeeded,
                updatedContracts,
                totalSalary,
                moneyCommand);
        }

        public StaffContractAdvanceResult AdvanceSeason(
            string teamSeasonKey,
            int completedSeason,
            IReadOnlyList<StaffContractState> contracts,
            TeamStaffAssignmentState assignment)
        {
            string teamKey = RequireId(teamSeasonKey, nameof(teamSeasonKey));
            if (completedSeason <= 0)
                throw new ArgumentOutOfRangeException(nameof(completedSeason));
            if (contracts == null)
                throw new ArgumentNullException(nameof(contracts));
            if (assignment == null)
                throw new ArgumentNullException(nameof(assignment));
            if (!string.Equals(teamKey, assignment.TeamSeasonKey, StringComparison.Ordinal))
                return new StaffContractAdvanceResult(
                    StaffServiceStatus.InvalidState,
                    contracts,
                    assignment,
                    Array.Empty<string>());

            bool hasContractToAdvance = false;
            for (int index = 0; index < contracts.Count; index++)
            {
                StaffContractState contract = contracts[index]
                    ?? throw new ArgumentException("null 스태프 계약이 있습니다.", nameof(contracts));
                if (!contract.IsActive ||
                    !string.Equals(contract.TeamSeasonKey, teamKey, StringComparison.Ordinal) ||
                    contract.StartSeason > completedSeason)
                    continue;
                hasContractToAdvance = true;
                if (!contract.LastSalaryPaidSeason.HasValue || contract.LastSalaryPaidSeason.Value < completedSeason)
                    return new StaffContractAdvanceResult(
                        StaffServiceStatus.SalaryNotSettled,
                        contracts,
                        assignment,
                        Array.Empty<string>());
            }
            if (!hasContractToAdvance)
                return new StaffContractAdvanceResult(
                    StaffServiceStatus.NoChange,
                    contracts,
                    assignment,
                    Array.Empty<string>());

            var updatedContracts = new StaffContractState[contracts.Count];
            var expiredContractIds = new List<string>();
            TeamStaffAssignmentState updatedAssignment = assignment;
            for (int index = 0; index < contracts.Count; index++)
            {
                StaffContractState contract = contracts[index];
                bool shouldAdvance = contract.IsActive &&
                    string.Equals(contract.TeamSeasonKey, teamKey, StringComparison.Ordinal) &&
                    contract.StartSeason <= completedSeason;
                if (!shouldAdvance)
                {
                    updatedContracts[index] = contract;
                    continue;
                }

                StaffContractState advanced = contract.WithRemainingSeasons(contract.RemainingSeasons - 1);
                updatedContracts[index] = advanced;
                if (advanced.IsActive)
                    continue;
                expiredContractIds.Add(advanced.ContractId);
                RemoveAssignmentIfMatching(ref updatedAssignment, advanced.StaffId);
            }
            return new StaffContractAdvanceResult(
                StaffServiceStatus.Succeeded,
                updatedContracts,
                updatedAssignment,
                expiredContractIds);
        }

        public static string CreateStableContractId(
            string teamSeasonKey,
            string staffId,
            int startSeason,
            int sequence)
        {
            string teamKey = RequireId(teamSeasonKey, nameof(teamSeasonKey));
            string stableStaffId = RequireId(staffId, nameof(staffId));
            if (startSeason <= 0 || sequence < 0)
                throw new ArgumentOutOfRangeException(nameof(startSeason));
            return $"staff-contract:{teamKey}:{stableStaffId}:{startSeason:D4}:{sequence:D2}";
        }

        private static StaffSigningResult SigningFailure(
            StaffServiceStatus status,
            IReadOnlyList<StaffContractState> contracts,
            TeamStaffAssignmentState assignment)
        {
            return new StaffSigningResult(status, contracts, assignment);
        }

        private static StaffContractState FindActiveContract(
            IReadOnlyList<StaffContractState> contracts,
            string staffId,
            string teamSeasonKey)
        {
            StaffContractState result = null;
            for (int index = 0; index < contracts.Count; index++)
            {
                StaffContractState contract = contracts[index];
                if (!contract.IsActive ||
                    !string.Equals(contract.StaffId, staffId, StringComparison.Ordinal) ||
                    !string.Equals(contract.TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal))
                    continue;
                if (result != null)
                    throw new InvalidOperationException("한 스태프에 활성 계약이 둘 이상 존재합니다.");
                result = contract;
            }
            return result;
        }

        private static StaffMoneyReason ResolveSigningReason(long signingCost, long replacementPenalty)
        {
            if (signingCost > 0 && replacementPenalty > 0)
                return StaffMoneyReason.SigningAndReplacement;
            return replacementPenalty > 0 ? StaffMoneyReason.ReplacementPenalty : StaffMoneyReason.Signing;
        }

        private static bool IsAssignedToAnotherRole(
            TeamStaffAssignmentState assignment,
            StaffRole targetRole,
            string staffId)
        {
            int roleCount = Enum.GetValues(typeof(StaffRole)).Length;
            for (int roleIndex = 0; roleIndex < roleCount; roleIndex++)
            {
                var role = (StaffRole)roleIndex;
                if (role != targetRole &&
                    string.Equals(assignment.GetAssignedStaffId(role), staffId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void RemoveAssignmentIfMatching(
            ref TeamStaffAssignmentState assignment,
            string staffId)
        {
            int roleCount = Enum.GetValues(typeof(StaffRole)).Length;
            for (int roleIndex = 0; roleIndex < roleCount; roleIndex++)
            {
                var role = (StaffRole)roleIndex;
                if (string.Equals(assignment.GetAssignedStaffId(role), staffId, StringComparison.Ordinal))
                {
                    assignment = assignment.WithoutAssignment(role);
                    return;
                }
            }
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Stable ID는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    internal static class StaffDeterministicKey
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        /// <summary>런타임별 string.GetHashCode 차이를 피하는 고정 UTF-16 FNV-1a 키다.</summary>
        public static ulong Hash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("결정론 키는 비어 있을 수 없습니다.", nameof(value));
            ulong hash = OffsetBasis;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                hash ^= (byte)character;
                hash *= Prime;
                hash ^= (byte)(character >> 8);
                hash *= Prime;
            }
            return hash;
        }
    }
}
