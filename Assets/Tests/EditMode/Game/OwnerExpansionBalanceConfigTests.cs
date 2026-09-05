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
