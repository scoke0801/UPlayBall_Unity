using System;
using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>스태프 계약·효과·시장·AI 경계를 구현 Agent와 독립적으로 공격 검증한다.</summary>
    public sealed class HistoricalStaffTests
    {
        private const string TeamKey = "COMETS_2026";

        [Test]
        public void StaffBalanceTable_CreateInitial은모든RoleSpecialty계약을만족한다()
        {
            Assert.That(() => StaffBalanceTable.CreateInitial(), Throws.Nothing);
        }

        [Test]
        public void StaffCatalog와Assignment_동일Staff중복을거부한다()
        {
            StaffDefinition staff = CreateStaff("staff-a", "강하늘", StaffRole.HittingCoach);
            StaffDefinition duplicateId = CreateStaff("staff-a", "문새벽", StaffRole.HittingCoach);

            Assert.That(
                () => new StaffCatalog(new[] { staff, duplicateId }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => new TeamStaffAssignmentState(
                    TeamKey,
                    hittingCoachStaffId: staff.StaffId,
                    pitchingCoachStaffId: staff.StaffId),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TeamStaffEffectResolver_역할불일치배치를거부한다()
        {
            StaffDefinition pitcher = CreateStaff("staff-p", "문새벽", StaffRole.PitchingCoach);
            var catalog = new StaffCatalog(new[] { pitcher });
            StaffContractState contract = CreateContract("contract-p", pitcher.StaffId, 2, 120000L);
            var assignment = new TeamStaffAssignmentState(TeamKey, hittingCoachStaffId: pitcher.StaffId);

            Assert.That(
                () => new TeamStaffEffectResolver().Resolve(
                    catalog,
                    new[] { contract },
                    assignment,
                    CreateValidBalance()),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void TeamStaffEffectResolver_한Staff의활성계약중복을거부한다()
        {
            StaffDefinition hitter = CreateStaff("staff-h", "강하늘", StaffRole.HittingCoach);
            var catalog = new StaffCatalog(new[] { hitter });
            var contracts = new[]
            {
                CreateContract("contract-h-a", hitter.StaffId, 2, 100000L),
                CreateContract("contract-h-b", hitter.StaffId, 1, 110000L)
            };
            var assignment = new TeamStaffAssignmentState(TeamKey, hittingCoachStaffId: hitter.StaffId);

            Assert.That(
                () => new TeamStaffEffectResolver().Resolve(
                    catalog,
                    contracts,
                    assignment,
                    CreateValidBalance()),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void TrySign_동일Contract재시도는거부하고TransactionId를그대로반환한다()
        {
            StaffDefinition hitter = CreateStaff("staff-h", "강하늘", StaffRole.HittingCoach);
            var catalog = new StaffCatalog(new[] { hitter });
            var assignment = new TeamStaffAssignmentState(TeamKey);
            StaffMarketOffer offer = CreateOffer(hitter.StaffId, 10000L);
            var command = new StaffSigningCommand("contract-h", "tx-sign-h", TeamKey, 2026, 10000L);
            var service = new StaffContractService();

            StaffSigningResult first = service.TrySign(
                command,
                offer,
                catalog,
                Array.Empty<StaffContractState>(),
                assignment,
                CreateValidBalance());
            StaffSigningResult retry = service.TrySign(
                command,
                offer,
                catalog,
                first.Contracts,
                first.Assignment,
                CreateValidBalance());

            Assert.That(first.Status, Is.EqualTo(StaffServiceStatus.Succeeded));
            Assert.That(first.MoneyCommand.TransactionId, Is.EqualTo(command.TransactionId));
            Assert.That(first.SignedContract.ContractId, Is.EqualTo(command.ContractId));
            Assert.That(retry.Status, Is.EqualTo(StaffServiceStatus.InvalidState));
            Assert.That(retry.MoneyCommand, Is.Null);
            Assert.That(retry.Contracts.Count, Is.EqualTo(first.Contracts.Count));
        }

        [Test]
        public void StaffSigningResult_동일ContractId상태를거부한다()
        {
            StaffContractState contract = CreateContract("contract-h", "staff-h", 2, 100000L);

            Assert.That(
                () => new StaffSigningResult(
                    StaffServiceStatus.InvalidState,
                    new[] { contract, contract },
                    new TeamStaffAssignmentState(TeamKey)),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TrySign_기존역할을종료하고교체비용을한MoneyCommand로반환한다()
        {
            StaffDefinition oldStaff = CreateStaff("staff-old", "강하늘", StaffRole.HittingCoach);
            StaffDefinition newStaff = CreateStaff("staff-new", "문새벽", StaffRole.HittingCoach);
            var catalog = new StaffCatalog(new[] { oldStaff, newStaff });
            StaffContractState oldContract = CreateContract("contract-old", oldStaff.StaffId, 2, 100000L);
            var assignment = new TeamStaffAssignmentState(TeamKey, hittingCoachStaffId: oldStaff.StaffId);
            var command = new StaffSigningCommand("contract-new", "tx-replace", TeamKey, 2026, 35000L);
            StaffMarketOffer offer = CreateOffer(newStaff.StaffId, 10000L);

            StaffSigningResult result = new StaffContractService().TrySign(
                command,
                offer,
                catalog,
                new[] { oldContract },
                assignment,
                CreateValidBalance());

            Assert.That(result.Status, Is.EqualTo(StaffServiceStatus.Succeeded));
            Assert.That(result.TerminatedContractId, Is.EqualTo(oldContract.ContractId));
            Assert.That(result.Contracts[0].RemainingSeasons, Is.Zero);
            Assert.That(result.Contracts[1].StaffId, Is.EqualTo(newStaff.StaffId));
            Assert.That(result.Assignment.HittingCoachStaffId, Is.EqualTo(newStaff.StaffId));
            Assert.That(result.MoneyCommand.Reason, Is.EqualTo(StaffMoneyReason.SigningAndReplacement));
            Assert.That(result.MoneyCommand.Amount, Is.EqualTo(35000L));
            Assert.That(result.MoneyCommand.TransactionId, Is.EqualTo("tx-replace"));
        }

        [Test]
        public void TrySign_Money부족은계약배치와MoneyCommand를만들지않는다()
        {
            StaffDefinition hitter = CreateStaff("staff-h", "강하늘", StaffRole.HittingCoach);
            var catalog = new StaffCatalog(new[] { hitter });
            var assignment = new TeamStaffAssignmentState(TeamKey);
            var command = new StaffSigningCommand("contract-h", "tx-sign-h", TeamKey, 2026, 9999L);

            StaffSigningResult result = new StaffContractService().TrySign(
                command,
                CreateOffer(hitter.StaffId, 10000L),
                catalog,
                Array.Empty<StaffContractState>(),
                assignment,
                CreateValidBalance());

            Assert.That(result.Status, Is.EqualTo(StaffServiceStatus.InsufficientMoney));
            Assert.That(result.Contracts, Is.Empty);
            Assert.That(result.Assignment.HittingCoachStaffId, Is.Null);
            Assert.That(result.SignedContract, Is.Null);
            Assert.That(result.MoneyCommand, Is.Null);
        }

        [Test]
        public void SettleSalaries_동일시즌재호출은급여를중복정산하지않는다()
        {
            StaffContractState contract = CreateContract("contract-h", "staff-h", 2, 100000L);
            var service = new StaffContractService();
            var command = new StaffSalarySettlementCommand("tx-salary-2026", TeamKey, 2026, 100000L);

            StaffSalarySettlementResult first = service.SettleSalaries(command, new[] { contract });
            StaffSalarySettlementResult second = service.SettleSalaries(command, first.Contracts);

            Assert.That(first.Status, Is.EqualTo(StaffServiceStatus.Succeeded));
            Assert.That(first.TotalSalary, Is.EqualTo(100000L));
            Assert.That(first.MoneyCommand.TransactionId, Is.EqualTo(command.TransactionId));
            Assert.That(first.Contracts[0].LastSalaryPaidSeason, Is.EqualTo(2026));
            Assert.That(second.Status, Is.EqualTo(StaffServiceStatus.NoChange));
            Assert.That(second.TotalSalary, Is.Zero);
            Assert.That(second.MoneyCommand, Is.Null);
            Assert.That(second.Contracts[0].LastSalaryPaidSeason, Is.EqualTo(2026));
        }

        [Test]
        public void SettleSalaries_Money부족은급여지급표시를변경하지않는다()
        {
            StaffContractState contract = CreateContract("contract-h", "staff-h", 2, 100000L);

            StaffSalarySettlementResult result = new StaffContractService().SettleSalaries(
                new StaffSalarySettlementCommand("tx-salary-2026", TeamKey, 2026, 99999L),
                new[] { contract });

            Assert.That(result.Status, Is.EqualTo(StaffServiceStatus.InsufficientMoney));
            Assert.That(result.TotalSalary, Is.EqualTo(100000L));
            Assert.That(result.MoneyCommand, Is.Null);
            Assert.That(result.Contracts[0].LastSalaryPaidSeason, Is.Null);
        }

        [Test]
        public void AdvanceSeason_미정산급여를거부하고정산후만료배치를제거한다()
        {
            StaffContractState unpaid = CreateContract("contract-h", "staff-h", 1, 100000L);
            var assignment = new TeamStaffAssignmentState(TeamKey, hittingCoachStaffId: unpaid.StaffId);
            var service = new StaffContractService();

            StaffContractAdvanceResult blocked = service.AdvanceSeason(
                TeamKey,
                2026,
                new[] { unpaid },
                assignment);
            StaffContractAdvanceResult advanced = service.AdvanceSeason(
                TeamKey,
                2026,
                new[] { unpaid.WithSalaryPaid(2026) },
                assignment);

            Assert.That(blocked.Status, Is.EqualTo(StaffServiceStatus.SalaryNotSettled));
            Assert.That(blocked.Contracts[0].RemainingSeasons, Is.EqualTo(1));
            Assert.That(blocked.Assignment.HittingCoachStaffId, Is.EqualTo(unpaid.StaffId));
            Assert.That(advanced.Status, Is.EqualTo(StaffServiceStatus.Succeeded));
            Assert.That(advanced.Contracts[0].RemainingSeasons, Is.Zero);
            Assert.That(advanced.ExpiredContractIds, Is.EqualTo(new[] { unpaid.ContractId }));
            Assert.That(advanced.Assignment.HittingCoachStaffId, Is.Null);
        }

        [Test]
        public void TeamStaffEffectResolver_모든효과를Balance상한내로제한한다()
        {
            StaffBalanceTable balance = CreateValidBalance();
            StaffDefinition[] staff = CreateMaximumEffectStaff();
            var catalog = new StaffCatalog(staff);
            var contracts = new StaffContractState[staff.Length];
            for (int index = 0; index < staff.Length; index++)
                contracts[index] = CreateContract($"contract-{index}", staff[index].StaffId, 2, 100000L);
            var assignment = new TeamStaffAssignmentState(
                TeamKey,
                staff[0].StaffId,
                staff[1].StaffId,
                staff[2].StaffId,
                staff[3].StaffId,
                staff[4].StaffId);

            TeamStaffEffectProfile profile = new TeamStaffEffectResolver().Resolve(
                catalog,
                contracts,
                assignment,
                balance);

            Assert.That(profile.HittingTrainingEfficiency, Is.InRange(1d, 1d + balance.MaximumEffectBonus));
            Assert.That(profile.PitchingTrainingEfficiency, Is.InRange(1d, 1d + balance.MaximumEffectBonus));
            Assert.That(profile.DevelopmentPointEfficiency, Is.InRange(1d, 1d + balance.MaximumEffectBonus));
            Assert.That(profile.ConditionRecoveryEfficiency, Is.InRange(1d, 1d + balance.MaximumEffectBonus));
            Assert.That(profile.ScoutingConfidenceModifier, Is.InRange(0d, balance.MaximumScoutingConfidenceModifier));
            Assert.That(profile.HittingTrainingEfficiency, Is.EqualTo(1d + balance.MaximumEffectBonus));
        }

        [Test]
        public void StaffTrainingEfficiencyResolver_효율만반환하고CardTraining을직접변경하지않는다()
        {
            var training = new CardTrainingState();
            training.AddBonus(PlayerAbility.Contact, 3);
            var ownedCard = new OwnedPlayerCardState("card-a", training: training);
            int before = ownedCard.Training.GetBonus(PlayerAbility.Contact);
            var profile = new TeamStaffEffectProfile(1.10d, 1.08d, 1.05d, 1.04d, 0.03d);

            StaffTrainingEfficiencyResult result = StaffTrainingEfficiencyResolver.Resolve(
                profile,
                new StaffTrainingEfficiencyContext(StaffTrainingDiscipline.Hitting, true));

            Assert.That(result.EfficiencyMultiplier, Is.EqualTo(1.155d).Within(0.0000001d));
            Assert.That(ownedCard.Training.GetBonus(PlayerAbility.Contact), Is.EqualTo(before));
            Assert.That(ownedCard.EnhancementLevel, Is.Zero);
            MethodInfo method = typeof(StaffTrainingEfficiencyResolver).GetMethod("Resolve");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(StaffTrainingEfficiencyResult)));
            Assert.That(method.GetParameters().Length, Is.EqualTo(2));
        }

        [Test]
        public void StaffPublicApi_ScoutOdds와CardTrainingCeiling을직접참조하지않는다()
        {
            Type[] staffTypes =
            {
                typeof(StaffCatalogGenerator),
                typeof(StaffMarketResolver),
                typeof(TeamStaffEffectResolver),
                typeof(StaffTrainingEfficiencyResolver),
                typeof(AiStaffProfileResolver),
                typeof(StaffContractService)
            };
            string[] forbiddenTypeNames =
            {
                nameof(ScoutRoller),
                nameof(ScoutPoolDefinition),
                nameof(WorldCardCatalog),
                nameof(PlayerSeasonDefinition),
                nameof(CardTrainingState),
                nameof(OwnedPlayerCardState)
            };

            for (int typeIndex = 0; typeIndex < staffTypes.Length; typeIndex++)
            {
                MethodInfo[] methods = staffTypes[typeIndex].GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
                {
                    AssertForbiddenType(methods[methodIndex].ReturnType, forbiddenTypeNames, methods[methodIndex].Name);
                    ParameterInfo[] parameters = methods[methodIndex].GetParameters();
                    for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                    {
                        AssertForbiddenType(
                            parameters[parameterIndex].ParameterType,
                            forbiddenTypeNames,
                            methods[methodIndex].Name);
                    }
                }
            }
        }

        [Test]
        public void Catalog와Market_같은Seed는입력순서와무관한동일제안을만든다()
        {
            StaffBalanceTable balance = CreateValidBalance();
            var names = new StaffNameCatalog(new[]
            {
                "강하늘", "문새벽", "윤가람", "서해솔", "진노을",
                "배이든", "한여름", "류푸름", "채마루", "오나래"
            });
            var generator = new StaffCatalogGenerator();

            StaffCatalog firstCatalog = generator.Generate(names, 2, 8123UL, balance);
            StaffCatalog secondCatalog = generator.Generate(names, 2, 8123UL, balance);
            AssertStaffCatalogsEqual(firstCatalog, secondCatalog);

            var market = new StaffMarketResolver();
            IReadOnlyList<StaffMarketOffer> firstOffers = market.CreateOffers(
                firstCatalog,
                Array.Empty<StaffContractState>(),
                TeamKey,
                "offseason-2026",
                StaffMarketKind.Offseason,
                LeagueGrade.Major,
                391UL,
                balance);
            IReadOnlyList<StaffMarketOffer> secondOffers = market.CreateOffers(
                secondCatalog,
                Array.Empty<StaffContractState>(),
                TeamKey,
                "offseason-2026",
                StaffMarketKind.Offseason,
                LeagueGrade.Major,
                391UL,
                balance);

            Assert.That(secondOffers.Count, Is.EqualTo(firstOffers.Count));
            for (int index = 0; index < firstOffers.Count; index++)
            {
                Assert.That(secondOffers[index].OfferId, Is.EqualTo(firstOffers[index].OfferId));
                Assert.That(secondOffers[index].StaffId, Is.EqualTo(firstOffers[index].StaffId));
                Assert.That(secondOffers[index].ContractYears, Is.EqualTo(firstOffers[index].ContractYears));
                Assert.That(secondOffers[index].AnnualSalary, Is.EqualTo(firstOffers[index].AnnualSalary));
                Assert.That(secondOffers[index].SigningCost, Is.EqualTo(firstOffers[index].SigningCost));
            }
        }

        [Test]
        public void AiStaffProfile_같은Seed는동일하고OwnedCard나경제를요구하지않는다()
        {
            StaffBalanceTable balance = CreateValidBalance();
            var clubState = new TeamSeasonClubState(
                TeamKey,
                new ClubDnaRatings(52d, 48d, 46d, 55d, 51d, 49d, 58d, 44d));
            var resolver = new AiStaffProfileResolver();

            TeamStaffEffectProfile first = resolver.Resolve(
                LeagueGrade.Major,
                63d,
                clubState,
                91027UL,
                balance);
            TeamStaffEffectProfile second = resolver.Resolve(
                LeagueGrade.Major,
                63d,
                clubState,
                91027UL,
                balance);

            AssertProfilesEqual(first, second);
            MethodInfo resolve = typeof(AiStaffProfileResolver).GetMethod("Resolve");
            ParameterInfo[] parameters = resolve.GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(5));
            for (int index = 0; index < parameters.Length; index++)
            {
                Assert.That(parameters[index].ParameterType, Is.Not.EqualTo(typeof(OwnedPlayerCardState)));
                Assert.That(parameters[index].ParameterType, Is.Not.EqualTo(typeof(ManagerEconomyState)));
                Assert.That(parameters[index].ParameterType, Is.Not.EqualTo(typeof(StaffContractState)));
            }
        }

        private static StaffDefinition CreateStaff(
            string staffId,
            string name,
            StaffRole role,
            int qualityTier = 3)
        {
            return new StaffDefinition(
                staffId,
                name,
                role,
                qualityTier,
                qualityTier >= 5 ? StaffSalaryBand.Elite : StaffSalaryBand.Standard,
                StaffContractPreference.Balanced,
                new[] { GetPrimarySpecialty(role) },
                new[] { StaffPhilosophyTag.Fundamentals });
        }

        private static StaffContractState CreateContract(
            string contractId,
            string staffId,
            int remainingSeasons,
            long annualSalary)
        {
            return new StaffContractState(
                contractId,
                staffId,
                TeamKey,
                2026,
                remainingSeasons,
                annualSalary);
        }

        private static StaffMarketOffer CreateOffer(string staffId, long signingCost)
        {
            return new StaffMarketOffer(
                StaffMarketOffer.CreateStableOfferId("midseason-2026", TeamKey, staffId),
                staffId,
                TeamKey,
                "midseason-2026",
                StaffMarketKind.MidseasonReplacement,
                2,
                200000L,
                signingCost);
        }

        private static StaffDefinition[] CreateMaximumEffectStaff()
        {
            StaffPhilosophyTag[] philosophies =
            {
                StaffPhilosophyTag.Fundamentals,
                StaffPhilosophyTag.AggressiveDevelopment,
                StaffPhilosophyTag.LongTermDevelopment,
                StaffPhilosophyTag.WorkloadManagement,
                StaffPhilosophyTag.EvidenceBased,
                StaffPhilosophyTag.PlayerCentered
            };
            return new[]
            {
                new StaffDefinition("staff-max-h", "강하늘", StaffRole.HittingCoach, 5,
                    StaffSalaryBand.Elite, StaffContractPreference.LongTerm,
                    new[] { StaffSpecialtyTag.ContactTraining, StaffSpecialtyTag.PowerTraining, StaffSpecialtyTag.PlateDiscipline },
                    philosophies),
                new StaffDefinition("staff-max-p", "문새벽", StaffRole.PitchingCoach, 5,
                    StaffSalaryBand.Elite, StaffContractPreference.LongTerm,
                    new[] { StaffSpecialtyTag.PitchCommand, StaffSpecialtyTag.PitchMovement, StaffSpecialtyTag.StarterDevelopment, StaffSpecialtyTag.BullpenDevelopment },
                    philosophies),
                new StaffDefinition("staff-max-d", "윤가람", StaffRole.DevelopmentCoach, 5,
                    StaffSalaryBand.Elite, StaffContractPreference.LongTerm,
                    new[] { StaffSpecialtyTag.ProspectDevelopment, StaffSpecialtyTag.VeteranManagement },
                    philosophies),
                new StaffDefinition("staff-max-c", "서해솔", StaffRole.ConditioningCoach, 5,
                    StaffSalaryBand.Elite, StaffContractPreference.LongTerm,
                    new[] { StaffSpecialtyTag.RecoveryPlanning, StaffSpecialtyTag.VeteranRecovery },
                    philosophies),
                new StaffDefinition("staff-max-s", "진노을", StaffRole.ScoutingDirector, 5,
                    StaffSalaryBand.Elite, StaffContractPreference.LongTerm,
                    new[] { StaffSpecialtyTag.DataAnalysis },
                    philosophies)
            };
        }

        private static StaffSpecialtyTag GetPrimarySpecialty(StaffRole role)
        {
            switch (role)
            {
                case StaffRole.HittingCoach: return StaffSpecialtyTag.ContactTraining;
                case StaffRole.PitchingCoach: return StaffSpecialtyTag.PitchCommand;
                case StaffRole.DevelopmentCoach: return StaffSpecialtyTag.ProspectDevelopment;
                case StaffRole.ConditioningCoach: return StaffSpecialtyTag.RecoveryPlanning;
                case StaffRole.ScoutingDirector: return StaffSpecialtyTag.DataAnalysis;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        private static StaffBalanceTable CreateValidBalance()
        {
            var qualities = new[]
            {
                new StaffQualityBalance(1, StaffSalaryBand.Budget, 0.02d, 80000L, 32d),
                new StaffQualityBalance(2, StaffSalaryBand.Standard, 0.04d, 120000L, 28d),
                new StaffQualityBalance(3, StaffSalaryBand.Standard, 0.06d, 180000L, 21d),
                new StaffQualityBalance(4, StaffSalaryBand.Premium, 0.08d, 270000L, 13d),
                new StaffQualityBalance(5, StaffSalaryBand.Elite, 0.10d, 400000L, 6d)
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
            return new StaffBalanceTable(
                qualities,
                salaryBands,
                roles,
                specialties,
                philosophies,
                new StaffMarketBalance(
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
                    1000L),
                new AiStaffBalance(
                    new[] { 0.010d, 0.015d, 0.020d, 0.025d, 0.030d, 0.035d, 0.040d, 0.045d, 0.050d, 0.055d },
                    0.030d,
                    0.035d,
                    0.005d),
                0.12d,
                0.10d);
        }

        private static void AssertForbiddenType(Type type, IReadOnlyList<string> forbiddenNames, string methodName)
        {
            string typeName = type.FullName ?? type.Name;
            for (int index = 0; index < forbiddenNames.Count; index++)
            {
                Assert.That(
                    typeName,
                    Does.Not.Contain(forbiddenNames[index]),
                    $"{methodName}이 금지된 API {forbiddenNames[index]}에 결합되었습니다.");
            }
        }

        private static void AssertStaffCatalogsEqual(StaffCatalog first, StaffCatalog second)
        {
            Assert.That(second.Staff.Count, Is.EqualTo(first.Staff.Count));
            for (int index = 0; index < first.Staff.Count; index++)
            {
                StaffDefinition left = first.Staff[index];
                StaffDefinition right = second.Staff[index];
                Assert.That(right.StaffId, Is.EqualTo(left.StaffId));
                Assert.That(right.FictionalName, Is.EqualTo(left.FictionalName));
                Assert.That(right.Role, Is.EqualTo(left.Role));
                Assert.That(right.QualityTier, Is.EqualTo(left.QualityTier));
                Assert.That(right.SalaryBand, Is.EqualTo(left.SalaryBand));
                Assert.That(right.ContractPreference, Is.EqualTo(left.ContractPreference));
                Assert.That(right.Specialties, Is.EqualTo(left.Specialties));
                Assert.That(right.Philosophies, Is.EqualTo(left.Philosophies));
            }
        }

        private static void AssertProfilesEqual(TeamStaffEffectProfile first, TeamStaffEffectProfile second)
        {
            Assert.That(second.HittingTrainingEfficiency, Is.EqualTo(first.HittingTrainingEfficiency));
            Assert.That(second.PitchingTrainingEfficiency, Is.EqualTo(first.PitchingTrainingEfficiency));
            Assert.That(second.DevelopmentPointEfficiency, Is.EqualTo(first.DevelopmentPointEfficiency));
            Assert.That(second.ConditionRecoveryEfficiency, Is.EqualTo(first.ConditionRecoveryEfficiency));
            Assert.That(second.ScoutingConfidenceModifier, Is.EqualTo(first.ScoutingConfidenceModifier));
        }
    }
}
