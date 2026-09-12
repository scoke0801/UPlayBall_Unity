#if UNITY_EDITOR
using System.Linq;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using NUnit.Framework;
using UnityEditor;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>실제 배포 TextAsset의 네 등급과 레시피·Scout·선호 타순을 검증한다.</summary>
    public sealed class ProductionSpecialCardTests
    {
        [TestCase(HistoricalContentVerificationMode.Fast)]
        [TestCase(HistoricalContentVerificationMode.Full)]
        public void SpecialCards_LoadProductionAndValidateAllReferences(HistoricalContentVerificationMode mode)
        {
            var asset = AssetDatabase.LoadAssetAtPath<HistoricalRuntimeContentCatalog>(
                "Assets/10.Datas/HistoricalSimulation/HistoricalRuntimeContentCatalog.asset");
            Assert.That(asset.SpecialCards, Is.Not.Null);
            var content = new UnityHistoricalContentProvider(asset, mode).Load();
            var special = content.SpecialCards;
            Assert.That(special.Cards.Count(c => c.Edition == PlayerCardEdition.Ex), Is.EqualTo(73));
            Assert.That(special.Cards.Count(c => c.Edition == PlayerCardEdition.Rare), Is.EqualTo(363));
            Assert.That(special.Cards.Count(c => c.Edition == PlayerCardEdition.CareerHigh), Is.EqualTo(116));
            Assert.That(special.Cards.Count(c => c.Edition == PlayerCardEdition.Legend), Is.EqualTo(105));
            Assert.That(special.Recipes.Count, Is.EqualTo(special.Cards.Count(c => c.IsUniqueOwnedCard)));
            var catalog = WorldCardCatalogBuilder.Build(content.PlayerSeasons, null, CardEditionBalanceTable.CreateInitial(),
                content.PlayerPersons, content.TeamSeasons, special);
            foreach (var card in special.Cards)
            {
                var issued = catalog.GetRequiredCard(card.CardId);
                var normal = catalog.GetRequiredCard(card.PlayerSeasonId + ":Normal");
                Assert.That(issued.PreferredBattingOrder, Is.EqualTo(normal.PreferredBattingOrder));
                if (issued.IsUniqueOwnedCard)
                    Assert.That(catalog.SpecialCards.GetRequiredRecipe(card.CardId).MaterialGroups.Count, Is.EqualTo(8));
            }
            var pool = new ScoutPoolDefinition("production", ScoutType.General,
                ScoutPoolDefinition.CreateInitialCostWeights(), ScoutPoolDefinition.CreateStandardEditionWeights(), 0);
            var buckets = new ScoutRoller().GetProbabilities(pool, catalog, ScoutFeaturePolicy.FullWorldAwards);
            Assert.That(buckets.Where(b => b.Edition == PlayerCardEdition.Rare).Sum(b => b.Probability), Is.GreaterThan(0));
            Assert.That(buckets.All(b => b.Edition == PlayerCardEdition.Normal || b.Edition == PlayerCardEdition.Rare), Is.True);
        }
    }
}
#endif
