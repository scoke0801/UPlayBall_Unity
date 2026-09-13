using System;
using System.Reflection;
using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Game.Data;
using NUnit.Framework;
using UnityEngine;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>Production 새 게임이 09~12 저작 Config를 실제 BalanceTable에 주입하는지 검증한다.</summary>
    public sealed class OwnerExpansionBalanceConfigTests
    {
        [Test]
        public void ScoutEconomy_경기보상과두우승상여를실제JSON에서읽는다()
        {
            var economy = NewGameDefinition.LoadOwnerModeBalanceTable().ScoutEconomy;
            Assert.That(economy.ScoutingPointsPerCompletedGame, Is.EqualTo(35));
            Assert.That(economy.ScoutingPointsPerWin, Is.EqualTo(10));
            Assert.That(economy.PennantChampionshipScoutingPoints, Is.EqualTo(600));
            Assert.That(economy.PostseasonChampionshipScoutingPoints, Is.EqualTo(1200));
        }

        [Test]
        public void CommonMatch_공통경기JSON을구단주설정과캐시해시에반영한다()
        {
            var authored = MiniGameBalanceConfig.Load(out string hash, out var tactical);
            var defaults = Baseball.Core.Balance.MiniGameBalance.CreateDefault();
            var owner = NewGameDefinition.LoadOwnerModeBalanceTable();
            foreach (PropertyInfo property in typeof(Baseball.Core.Balance.MiniGameBalance).GetProperties())
            {
                Assert.That(property.GetValue(authored), Is.EqualTo(property.GetValue(defaults)), property.Name);
                Assert.That(property.GetValue(owner.MiniGame), Is.EqualTo(property.GetValue(authored)), property.Name);
            }
            Assert.That(owner.Match.Tactical.StealAttemptUtilityScale, Is.EqualTo(tactical.StealAttemptUtilityScale));
            Assert.That(owner.ContentHash, Does.Contain(hash));
            Assert.Throws<ArgumentException>(() => MiniGameBalanceConfig.Parse("{\"schemaVersion\":1}"));
        }

        [Test]
        public void PreferredOrder_저작값과보통하한을실제게임설정으로읽는다()
        {
            var balance = NewGameDefinition.LoadOwnerModeBalanceTable().ConditionChemistry;
            Assert.That(balance.Presentation.GetBand(balance.PreferredOrderConditionFloor).LabelKey, Is.EqualTo("condition.normal"));
            Assert.That(balance.MismatchedOrderDeclineProbability, Is.EqualTo(.65d));
            Assert.That(balance.MismatchedOrderConditionDecline, Is.EqualTo(3));
        }

        [Test]
        public void OwnerModeBalance_InjectsAllAuthoredOwnerExpansionBalances()
        {
            var balance = NewGameDefinition.LoadOwnerModeBalanceTable();

            Assert.That(balance.Version, Is.EqualTo(4));
            Assert.That(balance.ContentHash, Does.Contain(":"));

            Assert.That(balance.ConditionChemistry.Presentation.Bands.Count, Is.EqualTo(10));
            Assert.That(balance.ConditionChemistry.WeeklyBaseRecovery, Is.EqualTo(6));
            Assert.That(balance.ConditionChemistry.Presentation.GetBand(90).LabelKey,
                Is.EqualTo("condition.peak"));

            Assert.That(balance.ClubOperation.TicketPolicies.Count, Is.EqualTo(3));
            Assert.That(balance.ClubOperation.LeagueOperations.Count, Is.EqualTo(10));
            Assert.That(balance.ClubOperation.FacilityLevels.Count, Is.EqualTo(24));
            Assert.That(balance.ClubOperation.StadiumLevels.Count, Is.EqualTo(5));
            Assert.That(balance.ClubOperation.GetTicketPolicy(TicketPriceTier.Premium).DemandMultiplier,
                Is.EqualTo(0.72d));
            Assert.That(balance.ClubOperation.GetFacilityLevel(FacilityType.FanShop, 1).UpgradeMoneyCost,
                Is.EqualTo(800_000_000L));
            Assert.That(balance.ClubOperation.GetStadiumLevel(5).Capacity, Is.EqualTo(40_000));

            Assert.That(balance.Staff.GetQuality(5).BaseAnnualSalary,
                Is.EqualTo(400_000_000L));
            Assert.That(balance.Staff.Market.GetOfferCount(StaffMarketKind.Offseason), Is.EqualTo(10));
            Assert.That(balance.Staff.Ai.GetGradeEffectBonus(LeagueGrade.Galaxy), Is.EqualTo(0.055d));
            Assert.That(balance.Staff.GetRole(StaffRole.ScoutingDirector).Specialties,
                Is.EquivalentTo(new[] { StaffSpecialtyTag.DataAnalysis }));

            Assert.That(balance.ScoutingConfidence.LowConfidenceThreshold, Is.EqualTo(0.18d));
            Assert.That(balance.ScoutingConfidence.PublicRosterEvidenceQuality, Is.EqualTo(0.72d));
            Assert.That(balance.ScoutingConfidence.BullpenVeryTiredMinimumRecentPitches,
                Is.EqualTo(61));
        }

        [Test]
        public void ComposeOwnerModeBalanceTable_PreservesCommonOwnerAndHistoricalBalances()
        {
            var defaults = Baseball.Core.Balance.BalanceTable.CreateDefault();
            var common = new Baseball.Core.Balance.BalanceTable(
                version: 17,
                defaults.PlateDiscipline,
                defaults.BattedBall,
                defaults.BaseRunning,
                defaults.ContractOffer,
                defaults.TeamGeneration,
                defaults.PlayerEvaluation,
                defaults.CareerSeason,
                contentHash: "common-sentinel",
                ownerCardGrowth: defaults.OwnerCardGrowth,
                teamColor: defaults.TeamColor,
                ownerPlayerMarket: defaults.OwnerPlayerMarket,
                historicalPitcherUsage: defaults.HistoricalPitcherUsage);
            TextAsset config = Resources.Load<TextAsset>("NewGame/OwnerExpansionBalance");
            Assert.That(config, Is.Not.Null);
            Type configType = typeof(NewGameDefinition).Assembly.GetType(
                "Baseball.Game.Data.OwnerExpansionBalanceConfig",
                throwOnError: true);
            MethodInfo parse = configType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
            Assert.That(parse, Is.Not.Null);
            object ownerExpansion = parse.Invoke(null, new object[] { config.text });
            MethodInfo compose = typeof(NewGameDefinition).GetMethod(
                "ComposeOwnerModeBalanceTable",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(compose, Is.Not.Null);

            var composed = (Baseball.Core.Balance.BalanceTable)compose.Invoke(
                null,
                new[] { (object)common, ownerExpansion });

            Assert.That(composed.OwnerCardGrowth, Is.SameAs(common.OwnerCardGrowth));
            Assert.That(composed.TeamColor, Is.SameAs(common.TeamColor));
            Assert.That(composed.OwnerPlayerMarket, Is.SameAs(common.OwnerPlayerMarket));
            Assert.That(composed.HistoricalPitcherUsage, Is.SameAs(common.HistoricalPitcherUsage));
        }

        [Test]
        public void ToOwnerModeBalanceTable_MissingConfig_ThrowsWithoutFallback()
        {
            NewGameDefinition definition = ScriptableObject.CreateInstance<NewGameDefinition>();
            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => definition.ToOwnerModeBalanceTable());
                Assert.That(exception.Message, Does.Contain("OwnerExpansionBalance Config"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ToOwnerModeBalanceTable_IncompleteConfig_ThrowsWithoutFallback()
        {
            NewGameDefinition source = Resources.Load<NewGameDefinition>("NewGame/NewGameDefinition");
            Assert.That(source, Is.Not.Null);
            NewGameDefinition definition = UnityEngine.Object.Instantiate(source);
            var invalidConfig = new TextAsset("{\"schemaVersion\":1,\"contentId\":\"invalid\"}");
            try
            {
                FieldInfo field = typeof(NewGameDefinition).GetField(
                    "_ownerExpansionBalanceConfig",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                field.SetValue(definition, invalidConfig);

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => definition.ToOwnerModeBalanceTable());
                Assert.That(exception.Message, Does.Contain("09~12"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidConfig);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CareerConfiguration_IsIndependentFromOwnerConfigContentAndValidity()
        {
            NewGameDefinition source = Resources.Load<NewGameDefinition>("NewGame/NewGameDefinition");
            Assert.That(source, Is.Not.Null);
            NewGameConfiguration baseline = source.ToConfiguration();
            NewGameDefinition definition = UnityEngine.Object.Instantiate(source);
            var invalidConfig = new TextAsset("{\"schemaVersion\":999,\"contentId\":\"owner-change\"}");
            try
            {
                FieldInfo field = typeof(NewGameDefinition).GetField(
                    "_ownerExpansionBalanceConfig",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                field.SetValue(definition, invalidConfig);

                NewGameConfiguration career = definition.ToConfiguration();

                Assert.That(career.Balance.Version, Is.EqualTo(3));
                Assert.That(career.Balance.Version, Is.EqualTo(baseline.Balance.Version));
                Assert.That(career.Balance.ContentHash, Is.EqualTo(baseline.Balance.ContentHash));
                Assert.That(career.Balance.ContentHash, Does.Not.Contain(":"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidConfig);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ProductionDefinition_ReferencesDedicatedConfigTextAsset()
        {
            TextAsset config = Resources.Load<TextAsset>("NewGame/OwnerExpansionBalance");

            Assert.That(config, Is.Not.Null);
            Assert.That(config.text, Does.Contain("owner-expansion-09-12-v1"));
            Assert.That(config.text, Does.Contain("\"facilityLevels\""));
            Assert.That(config.text, Does.Contain("\"scoutingConfidence\""));
        }
    }
}
