using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed partial class ManagerHistoricalSaveTests
    {
        [Test]
        public void SpecialCards_RecruitReservesConsumesOnceAndRestoresReceipt()
        {
            var state = CreateSpecialCardRuntime(out string target, out string[] materials);
            state.ReserveSpecialRecruit("recruit-1", target, materials);
            Assert.That(state.IsCardReserved(materials[0]), Is.True);
            Assert.Throws<InvalidOperationException>(() => state.ReserveSpecialRecruit("recruit-2", target, materials));
            var pending = state.CreateSpecialCardTransactionsSave();
            var restored = CreateSpecialCardRuntime(out _, out _);
            restored.RestoreSpecialCardTransactions(pending);
            Assert.That(restored.IsCardReserved(materials[0]), Is.True);
            Assert.That(restored.CommitSpecialRecruit("recruit-1"), Is.EqualTo(target));
            Assert.That(restored.CommitSpecialRecruit("recruit-1"), Is.EqualTo(target));
            Assert.That(restored.TryGetOwnedCard(target, out var acquired), Is.True);
            Assert.That(acquired.DuplicateCount, Is.Zero);
            Assert.That(acquired.IsLocked, Is.True);
            foreach (string id in materials) Assert.That(restored.TryGetOwnedCard(id, out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => restored.ReserveSpecialRecruit("recruit-3", target, materials));
        }

        [Test]
        public void SpecialCards_DuplicateYearAndProtectedMaterialAreRejectedWithoutConsumption()
        {
            var state = CreateSpecialCardRuntime(out string target, out string[] materials);
            var duplicate = (string[])materials.Clone();
            duplicate[7] = duplicate[0];
            Assert.Throws<ArgumentException>(() => state.ReserveSpecialRecruit("duplicate", target, duplicate));
            state.TryGetOwnedCard(materials[0], out var locked);
            locked.IsLocked = true;
            Assert.Throws<InvalidOperationException>(() => state.ReserveSpecialRecruit("locked", target, materials));
            locked.IsLocked = false;
            state.Wishlist.Add(materials[0]);
            Assert.Throws<InvalidOperationException>(() => state.ReserveSpecialRecruit("wishlist", target, materials));
            foreach (string id in materials) Assert.That(state.TryGetOwnedCard(id, out _), Is.True);
        }

        [Test]
        public void SpecialCards_CombinationIsDeterministicAndCannotUseRecruitOrRosterMaterials()
        {
            var state = CreateSpecialCardRuntime(out _, out string[] materials);
            var second = CreateSpecialCardRuntime(out _, out _);
            var five = new[] { materials[0], materials[1], materials[2], materials[3], materials[4] };
            var outcomes = new[] { new CardCombinationOutcome(5, 10, PlayerCardEdition.Ex, 1) };
            string result = state.CombineCards("combine-1", five, outcomes, new Pcg32Random(42));
            Assert.That(second.CombineCards("combine-1", five, outcomes, new Pcg32Random(42)), Is.EqualTo(result));
            Assert.That(state.CombineCards("combine-1", five, outcomes, null), Is.EqualTo(result));
            Assert.That(state.TryGetOwnedCard(result, out var owned), Is.True);
            Assert.That(owned.DuplicateCount, Is.Zero);
            Assert.Throws<ArgumentException>(() => state.AcquireCardWithResult(result));
            var rosterId = second.GetRoster(second.PlayerTeamSeasonKey).Entries[0].CardId;
            Assert.Throws<InvalidOperationException>(() => second.CombineCards("roster", new[] {
                rosterId, rosterId, rosterId, rosterId, rosterId }, outcomes, new Pcg32Random(1)));
        }

        [Test]
        public void SpecialCards_GeneralCombinationReceiptSurvivesFullSaveLoad()
        {
            var fixture = Fixture.Create(WorldRecordMode.SimulatedHistory);
            var state = fixture.State;
            var materials = new List<string>();
            foreach (var entry in state.Rosters[1].Entries)
            {
                state.AcquireCardWithResult(entry.CardId);
                materials.Add(entry.CardId);
                if (materials.Count == 5) break;
            }
            state.WorldCardCatalog.TryGetCard(materials[0], out var card);
            int cost = state.WorldCardCatalog.GetPlayerSeason(card).Cost;
            var outcomes = new[] { new CardCombinationOutcome(5, cost, PlayerCardEdition.Normal, 1) };
            string result = state.CombineCards("save-combination", materials, outcomes, new Pcg32Random(3));
            var adapter = fixture.CreateAdapter();
            var restored = adapter.Restore(adapter.CreateSaveData(state));
            Assert.That(restored.CombineCards("save-combination", materials, outcomes, null), Is.EqualTo(result));
        }

        [Test]
        public void SpecialCards_LegendRequiresItsGroupsAndCancellationReleasesMaterials()
        {
            var state = CreateSpecialCardRuntime(out string target, out string[] materials, true);
            var wrongGroups = (string[])materials.Clone();
            wrongGroups[0] = materials[1];
            wrongGroups[1] = materials[0];
            Assert.Throws<ArgumentException>(() => state.ReserveSpecialRecruit("wrong", target, wrongGroups));
            state.ReserveSpecialRecruit("cancel", target, materials);
            Assert.That(state.CancelSpecialRecruit("cancel"), Is.True);
            Assert.That(state.IsCardReserved(materials[0]), Is.False);
            state.ReserveSpecialRecruit("legend", target, materials);
            Assert.That(state.CommitSpecialRecruit("legend"), Is.EqualTo(target));
            Assert.That(state.CancelSpecialRecruit("legend"), Is.False);
            Assert.That(state.TryGetOwnedCard(target, out var card), Is.True);
            Assert.That(card.IsLocked, Is.True);
        }

        private static ManagerHistoricalRuntimeState CreateSpecialCardRuntime(out string target, out string[] materials, bool isLegend = false)
        {
            var original = Fixture.Create(WorldRecordMode.SimulatedHistory).State;
            var cards = new List<PlayerCardDefinition>(original.WorldCardCatalog.Cards);
            var seasonsById = new Dictionary<string, PlayerSeasonDefinition>();
            foreach (var card in cards) seasonsById[card.PlayerSeasonId] = original.WorldCardCatalog.GetPlayerSeason(card);
            var owned = new List<OwnedPlayerCardState>(original.OwnedCards);
            materials = new string[8];
            var zeros = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < 8; index++)
            {
                string sid = "SPECIAL-" + index;
                var season = new PlayerSeasonDefinition(sid, "PP-000", 2000 + index,
                    "FRANCHISE-00", "TEAM-00", PlayerPosition.Catcher, PitcherRole.Starter,
                    PlayerType.Batter, RegistrationType.Domestic, new AbilityRatings(50), 10, new AbilityRatings(70));
                seasonsById.Add(sid, season);
                materials[index] = sid + ":Normal";
                cards.Add(new PlayerCardDefinition(materials[index], sid, PlayerCardEdition.Normal, zeros));
                owned.Add(new OwnedPlayerCardState(materials[index]));
            }
            var edition = isLegend ? PlayerCardEdition.Legend : PlayerCardEdition.CareerHigh;
            target = "SPECIAL-0:" + edition + ":lineage-a";
            cards.Add(new PlayerCardDefinition(target, "SPECIAL-0", edition, zeros,
                teamColorLineageId: "lineage-a"));
            cards.Add(new PlayerCardDefinition("SPECIAL-0:Ex", "SPECIAL-0", PlayerCardEdition.Ex, zeros));
            var groups = new SpecialRecruitMaterialGroup[8];
            for (int index = 0; index < 8; index++) groups[index] = new SpecialRecruitMaterialGroup("slot-" + index,
                isLegend ? new[] { materials[index] } : materials);
            var lineages = new TeamColorLineageMap(new Dictionary<string, string> { ["FRANCHISE-00"] = "lineage-a" });
            var catalog = new WorldCardCatalog(new List<PlayerSeasonDefinition>(seasonsById.Values), cards,
                teamColorLineages: lineages, specialRecruitRecipes: new[] { new SpecialRecruitRecipe(target, groups) });
            return new ManagerHistoricalRuntimeState(original.PlayerTeamSeasonKey, original.ContentReference,
                original.IdentityRegistry, original.WorldHistory, catalog, original.League,
                original.Rosters, owned, original.Economy, original.ManagerMode, playerGrowth: original.PlayerGrowth);
        }
    }
}
