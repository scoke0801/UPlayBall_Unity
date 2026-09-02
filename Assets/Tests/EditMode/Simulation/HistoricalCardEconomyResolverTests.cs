using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    public sealed class HistoricalCardEconomyResolverTests
    {
        [Test]
        public void WorldCardCatalog_Normal은_항상_생성되고_특수Edition은_Award가_있을_때만_생성된다()
        {
            PlayerSeasonDefinition awarded = CreateSeason("season-awarded", 4, 2011, "COMETS");
            PlayerSeasonDefinition normalOnly = CreateSeason("season-normal", 9, 2012, "WOLVES");
            var awards = new WorldAwardRecord(new[]
            {
                new WorldAwardEntry(2011, WorldAwardType.AllStar, awarded.PlayerSeasonId, PlayerPosition.Catcher),
                new WorldAwardEntry(2011, WorldAwardType.RegularSeasonMvp, awarded.PlayerSeasonId, PlayerPosition.Catcher)
            });

            WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                new[] { normalOnly, awarded }, awards, CardEditionBalanceTable.CreateInitial());

            Assert.That(catalog.Cards.Count, Is.EqualTo(4));
            Assert.That(FindCard(catalog, awarded.PlayerSeasonId, PlayerCardEdition.Normal), Is.Not.Null);
            Assert.That(FindCard(catalog, awarded.PlayerSeasonId, PlayerCardEdition.AllStar), Is.Not.Null);
            Assert.That(FindCard(catalog, awarded.PlayerSeasonId, PlayerCardEdition.Mvp), Is.Not.Null);
            Assert.That(FindCard(catalog, awarded.PlayerSeasonId, PlayerCardEdition.GoldenGlove), Is.Null);
            Assert.That(FindCard(catalog, normalOnly.PlayerSeasonId, PlayerCardEdition.Normal), Is.Not.Null);
            Assert.That(FindCard(catalog, normalOnly.PlayerSeasonId, PlayerCardEdition.AllStar), Is.Null);
        }

        [Test]
        public void WorldCardCatalog_모든_Edition은_StableId와_동일한_Cost_Origin을_공유한다()
        {
            PlayerSeasonDefinition season = CreateSeason("season-shared", 7, 2011, "COMETS");
            var awards = new WorldAwardRecord(new[]
            {
                new WorldAwardEntry(2011, WorldAwardType.AllStar, season.PlayerSeasonId, PlayerPosition.Catcher),
                new WorldAwardEntry(2011, WorldAwardType.GoldenGlove, season.PlayerSeasonId, PlayerPosition.Catcher),
                new WorldAwardEntry(2011, WorldAwardType.PostseasonMvp, season.PlayerSeasonId, PlayerPosition.Catcher)
            });
            WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                new[] { season }, awards, CardEditionBalanceTable.CreateInitial());

            foreach (PlayerCardDefinition card in catalog.Cards)
            {
                Assert.That(card.CardId, Is.EqualTo(PlayerCardDefinition.CreateStableCardId(season.PlayerSeasonId, card.Edition)));
                PlayerSeasonDefinition origin = catalog.GetPlayerSeason(card);
                Assert.That(origin.Cost, Is.EqualTo(7));
                Assert.That(origin.OriginYear, Is.EqualTo(2011));
                Assert.That(origin.OriginFranchiseId, Is.EqualTo("COMETS"));
                Assert.That(origin.OriginTeamSeasonKey, Is.EqualTo("COMETS_2011"));
            }
        }

        [Test]
        public void EditionModifier는_Cost와_PlayerRole에_맞는_능력치에만_적용된다()
        {
            PlayerSeasonDefinition hitter = CreateSeason("hitter", 3, playerType: PlayerType.Batter);
            PlayerSeasonDefinition pitcher = CreateSeason("pitcher", 9, playerType: PlayerType.Pitcher);
            var awards = new WorldAwardRecord(new[]
            {
                new WorldAwardEntry(2011, WorldAwardType.AllStar, hitter.PlayerSeasonId, PlayerPosition.Catcher),
                new WorldAwardEntry(2011, WorldAwardType.AllStar, pitcher.PlayerSeasonId, PlayerPosition.StartingPitcher),
                new WorldAwardEntry(2011, WorldAwardType.GoldenGlove, hitter.PlayerSeasonId, PlayerPosition.Catcher),
                new WorldAwardEntry(2011, WorldAwardType.RegularSeasonMvp, pitcher.PlayerSeasonId, PlayerPosition.StartingPitcher)
            });
            WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                new[] { hitter, pitcher }, awards, CardEditionBalanceTable.CreateInitial());

            PlayerCardDefinition hitterAllStar = FindCard(catalog, hitter.PlayerSeasonId, PlayerCardEdition.AllStar);
            PlayerCardDefinition hitterGoldenGlove = FindCard(catalog, hitter.PlayerSeasonId, PlayerCardEdition.GoldenGlove);
            PlayerCardDefinition pitcherAllStar = FindCard(catalog, pitcher.PlayerSeasonId, PlayerCardEdition.AllStar);
            PlayerCardDefinition pitcherMvp = FindCard(catalog, pitcher.PlayerSeasonId, PlayerCardEdition.Mvp);

            Assert.That(hitterAllStar.GetModifier(PlayerAbility.Contact), Is.EqualTo(5));
            Assert.That(hitterAllStar.GetModifier(PlayerAbility.Speed), Is.EqualTo(5));
            Assert.That(hitterAllStar.GetModifier(PlayerAbility.Power), Is.Zero);
            Assert.That(hitterGoldenGlove.GetModifier(PlayerAbility.Power), Is.EqualTo(2));
            Assert.That(hitterGoldenGlove.GetModifier(PlayerAbility.Defense), Is.EqualTo(2));
            Assert.That(pitcherAllStar.GetModifier(PlayerAbility.Velocity), Is.EqualTo(2));
            Assert.That(pitcherAllStar.GetModifier(PlayerAbility.Control), Is.EqualTo(2));
            Assert.That(pitcherMvp.GetModifier(PlayerAbility.Stamina), Is.EqualTo(3));
            Assert.That(pitcherMvp.GetModifier(PlayerAbility.PitcherMental), Is.EqualTo(3));
            Assert.That(pitcherMvp.GetModifier(PlayerAbility.Contact), Is.Zero);
        }

        [Test]
        public void ScoutRoller_Phase4는_특수Edition을_제외하고_AwardScout를_거부한다()
        {
            WorldCardCatalog catalog = CreateAwardedCatalog();
            var roller = new ScoutRoller();
            var general = new ScoutPoolDefinition(
                "general", ScoutType.General,
                ScoutPoolDefinition.CreateInitialCostWeights(),
                ScoutPoolDefinition.CreateStandardEditionWeights(), 100);
            var award = new ScoutPoolDefinition(
                "award", ScoutType.Award,
                ScoutPoolDefinition.CreateInitialCostWeights(),
                new[] { 0d, 100d, 0d, 0d }, 500,
                editionFilter: PlayerCardEdition.AllStar);

            for (int index = 0; index < 100; index++)
                Assert.That(roller.Roll(general, catalog, ScoutFeaturePolicy.Phase4NormalOnly, new Pcg32Random((ulong)index)).Edition,
                    Is.EqualTo(PlayerCardEdition.Normal));
            Assert.Throws<InvalidOperationException>(() =>
                roller.Roll(award, catalog, ScoutFeaturePolicy.Phase4NormalOnly, new Pcg32Random(1UL)));
        }

        [Test]
        public void ScoutRoller_빈_Bucket을_제거하고_남은_가중치를_재정규화한다()
        {
            WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                new[] { CreateSeason("cost-1", 1), CreateSeason("cost-10", 10) },
                null,
                CardEditionBalanceTable.CreateInitial());
            var costWeights = new double[11];
            costWeights[1] = 1d;
            costWeights[5] = 100d;
            costWeights[10] = 3d;
            var pool = new ScoutPoolDefinition(
                "empty-bucket", ScoutType.General, costWeights,
                ScoutPoolDefinition.CreateNormalOnlyEditionWeights(), 100);

            IReadOnlyList<ScoutBucketProbability> probabilities = new ScoutRoller().GetProbabilities(
                pool, catalog, ScoutFeaturePolicy.Phase4NormalOnly);

            Assert.That(probabilities.Count, Is.EqualTo(2));
            Assert.That(probabilities.Single(value => value.Cost == 1).Probability, Is.EqualTo(0.25d).Within(0.000001d));
            Assert.That(probabilities.Single(value => value.Cost == 10).Probability, Is.EqualTo(0.75d).Within(0.000001d));
            Assert.That(probabilities.Sum(value => value.Probability), Is.EqualTo(1d).Within(0.000001d));
        }

        [Test]
        public void ScoutRoller_같은_Seed에서_같은_CardId_수열을_만든다()
        {
            WorldCardCatalog catalog = CreateCostCatalog(3);
            var pool = new ScoutPoolDefinition(
                "general", ScoutType.General,
                ScoutPoolDefinition.CreateInitialCostWeights(),
                ScoutPoolDefinition.CreateNormalOnlyEditionWeights(), 100);
            var leftRandom = new Pcg32Random(9921UL);
            var rightRandom = new Pcg32Random(9921UL);
            var roller = new ScoutRoller();

            for (int index = 0; index < 200; index++)
            {
                string left = roller.Roll(pool, catalog, ScoutFeaturePolicy.Phase4NormalOnly, leftRandom).CardId;
                string right = roller.Roll(pool, catalog, ScoutFeaturePolicy.Phase4NormalOnly, rightRandom).CardId;
                Assert.That(left, Is.EqualTo(right));
            }
        }

        [Test]
        public void ScoutRoller_대량_추첨이_JointBucket_가중치에_수렴한다()
        {
            WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                new[] { CreateSeason("cost-1", 1), CreateSeason("cost-10", 10) },
                null,
                CardEditionBalanceTable.CreateInitial());
            var costWeights = new double[11];
            costWeights[1] = 1d;
            costWeights[10] = 3d;
            var pool = new ScoutPoolDefinition(
                "statistical", ScoutType.General, costWeights,
                ScoutPoolDefinition.CreateNormalOnlyEditionWeights(), 100);
            var random = new Pcg32Random(8282UL);
            var roller = new ScoutRoller();
            const int sampleCount = 100_000;
            int costTenCount = 0;

            for (int index = 0; index < sampleCount; index++)
            {
                PlayerCardDefinition card = roller.Roll(pool, catalog, ScoutFeaturePolicy.Phase4NormalOnly, random);
                if (catalog.GetPlayerSeason(card).Cost == 10)
                    costTenCount++;
            }

            double measuredProbability = (double)costTenCount / sampleCount;
            TestContext.WriteLine($"Scout Cost10: {costTenCount}/{sampleCount} = {measuredProbability:P3}");
            Assert.That(measuredProbability, Is.EqualTo(0.75d).Within(0.01d));
        }

        [Test]
        public void ScoutRoller_SP를_소비하고_Pity100에서_Cost7이상을_보장한다()
        {
            WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                new[] { CreateSeason("low", 2), CreateSeason("high", 8) },
                null,
                CardEditionBalanceTable.CreateInitial());
            var pool = new ScoutPoolDefinition(
                "general", ScoutType.General,
                ScoutPoolDefinition.CreateInitialCostWeights(),
                ScoutPoolDefinition.CreateNormalOnlyEditionWeights(), 100);
            var economy = new ManagerEconomyState(scoutingPoints: 1000, pityGauge: 90);
            var pity = ScoutPityBalanceTable.CreateInitial();
            var roller = new ScoutRoller();

            roller.RollAndSpend(pool, catalog, ScoutFeaturePolicy.Phase4NormalOnly, pity, economy, new Pcg32Random(3UL));
            PlayerCardDefinition focused = roller.RollFocused(
                "COMETS", 2011, catalog, ScoutFeaturePolicy.Phase4NormalOnly, pity, economy, new Pcg32Random(5UL));

            Assert.That(economy.ScoutingPoints, Is.EqualTo(900));
            Assert.That(economy.PityGauge, Is.Zero);
            Assert.That(catalog.GetPlayerSeason(focused).Cost, Is.GreaterThanOrEqualTo(7));
        }

        [Test]
        public void Enhancement는_중복을_소비해_실패없이_최대_5까지_증가한다()
        {
            var owned = new OwnedPlayerCardState("season:Normal", duplicateCount: 7);

            for (int expected = 1; expected <= 5; expected++)
            {
                Assert.That(CardEnhancementResolver.Enhance(owned), Is.EqualTo(CardEnhancementResult.Enhanced));
                Assert.That(owned.EnhancementLevel, Is.EqualTo(expected));
            }

            Assert.That(CardEnhancementResolver.Enhance(owned), Is.EqualTo(CardEnhancementResult.MaximumLevel));
            Assert.That(owned.EnhancementLevel, Is.EqualTo(5));
            Assert.That(owned.DuplicateCount, Is.EqualTo(2));
        }

        [Test]
        public void CardSale는_Cost와_Edition배율로_SP를_지급하고_MAX추가중복만_자동판매한다()
        {
            PlayerSeasonDefinition season = CreateSeason("sale", 10);
            var card = new PlayerCardDefinition(
                PlayerCardDefinition.CreateStableCardId(season.PlayerSeasonId, PlayerCardEdition.Mvp),
                season.PlayerSeasonId,
                PlayerCardEdition.Mvp,
                new int[PlayerAbilityCatalog.AbilityCount]);
            var owned = new OwnedPlayerCardState(card.CardId, enhancementLevel: 5, duplicateCount: 2);
            var economy = new ManagerEconomyState();

            int saleSp = CardSaleResolver.AutoSellIfApplicable(
                DuplicateAutoSalePolicy.MaxDuplicateOnly,
                owned,
                card,
                season,
                CardSaleBalanceTable.CreateInitial(),
                economy);

            Assert.That(saleSp, Is.EqualTo(198));
            Assert.That(economy.ScoutingPoints, Is.EqualTo(198));
            Assert.That(owned.DuplicateCount, Is.Zero);
        }

        [Test]
        public void CardTraining은_나이없이_TrainingCeiling까지만_DP를_소비한다()
        {
            PlayerSeasonDefinition season = CreateSeason("training", 3, baseValue: 50, ceilingValue: 52);
            var owned = new OwnedPlayerCardState(PlayerCardDefinition.CreateStableCardId(season.PlayerSeasonId, PlayerCardEdition.Normal));
            var economy = new ManagerEconomyState(developmentPoints: 100);
            var program = new CardTrainingProgramDefinition("contact", PlayerAbility.Contact, 10, 5);

            CardTrainingResult first = CardTrainingResolver.Train(owned, season, program, economy);
            CardTrainingResult second = CardTrainingResolver.Train(owned, season, program, economy);

            Assert.That(first.GainedPoints, Is.EqualTo(2));
            Assert.That(first.SpentDp, Is.EqualTo(20));
            Assert.That(second.GainedPoints, Is.Zero);
            Assert.That(owned.Training.GetBonus(PlayerAbility.Contact), Is.EqualTo(2));
            Assert.That(economy.DevelopmentPoints, Is.EqualTo(80));
        }

        [Test]
        public void TeamColor_자유2슬롯은_각_조건을_만족한_카드에만_합산된다()
        {
            List<TeamColorRosterCard> roster = CreateMixedFranchiseRoster();
            var definitions = new List<TeamColorDefinition>();
            definitions.AddRange(InitialTeamColorDefinitionFactory.CreateYearFranchise(2011, "COMETS"));
            definitions.AddRange(InitialTeamColorDefinitionFactory.CreateFranchise("COMETS"));
            TeamColorDefinition yearFranchise20 = definitions.Single(value => value.TeamColorId == "YearFranchise:2011:COMETS:20");
            TeamColorDefinition franchise25 = definitions.Single(value => value.TeamColorId == "Franchise:COMETS:25");

            PerCardBonusMap bonuses = new TeamColorResolver().ApplyEquipped(
                roster, definitions, yearFranchise20, franchise25);

            Assert.That(bonuses.Get("card-0", PlayerAbility.Contact), Is.EqualTo(13));
            Assert.That(bonuses.Get("card-24", PlayerAbility.Contact), Is.EqualTo(6));
        }

        [Test]
        public void GoldenGlove_정확히8명에서_발동하고_10명에서는_HighestOnly_상위단계만_남는다()
        {
            IReadOnlyList<TeamColorDefinition> definitions = InitialTeamColorDefinitionFactory.CreateGoldenGlove(2011);
            var resolver = new TeamColorResolver();

            IReadOnlyList<TeamColorCandidate> eight = resolver.Resolve(CreateHonorRoster(8, PlayerCardEdition.GoldenGlove), definitions);
            IReadOnlyList<TeamColorCandidate> ten = resolver.Resolve(CreateHonorRoster(10, PlayerCardEdition.GoldenGlove), definitions);

            Assert.That(eight.Count, Is.EqualTo(1));
            Assert.That(eight[0].Definition.TeamColorId, Is.EqualTo("GoldenGlove:2011:8"));
            Assert.That(ten.Count, Is.EqualTo(1));
            Assert.That(ten[0].Definition.TeamColorId, Is.EqualTo("GoldenGlove:2011:10"));
        }

        [Test]
        public void AllStar_SamePool은_동일연도20명_후보_하나만_노출한다()
        {
            IReadOnlyList<TeamColorCandidate> candidates = new TeamColorResolver().Resolve(
                CreateHonorRoster(20, PlayerCardEdition.AllStar),
                InitialTeamColorDefinitionFactory.CreateAllStar(2011));

            Assert.That(candidates.Count, Is.EqualTo(1));
            Assert.That(candidates[0].Definition.TeamColorId, Is.EqualTo("AllStar:2011:20"));
        }

        [Test]
        public void YearFranchise와_Mvp의_단계는_의도대로_Stackable이다()
        {
            var resolver = new TeamColorResolver();
            List<TeamColorRosterCard> yearRoster = CreateUniformRoster(PlayerCardEdition.Normal);
            IReadOnlyList<TeamColorDefinition> yearDefinitions = InitialTeamColorDefinitionFactory.CreateYearFranchise(2011, "COMETS");
            TeamColorDefinition year20 = yearDefinitions.Single(value => value.RequiredCount == 20);
            TeamColorDefinition year25 = yearDefinitions.Single(value => value.RequiredCount == 25);
            PerCardBonusMap yearBonuses = resolver.ApplyEquipped(yearRoster, yearDefinitions, year20, year25);

            List<TeamColorRosterCard> mvpRoster = CreateUniformRoster(PlayerCardEdition.Mvp);
            IReadOnlyList<TeamColorDefinition> mvpDefinitions = InitialTeamColorDefinitionFactory.CreateMvp();
            PerCardBonusMap mvpBonuses = resolver.ApplyEquipped(mvpRoster, mvpDefinitions, mvpDefinitions[0], mvpDefinitions[1]);

            Assert.That(yearBonuses.Get("card-0", PlayerAbility.Contact), Is.EqualTo(17));
            Assert.That(mvpBonuses.Get("card-0", PlayerAbility.Contact), Is.EqualTo(5));
        }

        [Test]
        public void EffectiveRating은_BaseStat을_변조하지_않고_Hard140과_Soft120곡선을_적용한다()
        {
            const int baseStat = 99;
            EffectiveRatingResult result = EffectiveRatingResolver.Resolve(
                baseStat, 5, 10, 5, 17, 4, 3, EffectiveRatingCapTable.CreateInitial());

            Assert.That(baseStat, Is.EqualTo(99));
            Assert.That(result.Rating, Is.EqualTo(140));
            Assert.That(result.CurveRating, Is.EqualTo(130d).Within(0.000001d));
        }

        [Test]
        public void AiEditionUnlockPolicy_API는_OwnedEconomy를_입력받지_않는다()
        {
            MethodInfo method = typeof(AiEditionUnlockPolicy).GetMethod(nameof(AiEditionUnlockPolicy.GetAvailableCards));

            Assert.That(method, Is.Not.Null);
            Assert.That(method.GetParameters().Any(parameter => parameter.ParameterType == typeof(OwnedPlayerCardState)), Is.False);
            Assert.That(method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ManagerEconomyState)), Is.False);
        }

        private static WorldCardCatalog CreateAwardedCatalog()
        {
            PlayerSeasonDefinition season = CreateSeason("awarded", 5);
            var awards = new WorldAwardRecord(new[]
            {
                new WorldAwardEntry(2011, WorldAwardType.AllStar, season.PlayerSeasonId, PlayerPosition.Catcher)
            });
            return WorldCardCatalogBuilder.Build(new[] { season }, awards, CardEditionBalanceTable.CreateInitial());
        }

        private static WorldCardCatalog CreateCostCatalog(int perCost)
        {
            var seasons = new List<PlayerSeasonDefinition>();
            for (int cost = 1; cost <= 10; cost++)
                for (int index = 0; index < perCost; index++)
                    seasons.Add(CreateSeason("cost-" + cost + "-" + index, cost));
            return WorldCardCatalogBuilder.Build(seasons, null, CardEditionBalanceTable.CreateInitial());
        }

        private static PlayerCardDefinition FindCard(
            WorldCardCatalog catalog,
            string playerSeasonId,
            PlayerCardEdition edition)
        {
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = catalog.Cards[index];
                if (card.PlayerSeasonId == playerSeasonId && card.Edition == edition)
                    return card;
            }
            return null;
        }

        private static PlayerSeasonDefinition CreateSeason(
            string id,
            int cost,
            int year = 2011,
            string franchise = "COMETS",
            PlayerType playerType = PlayerType.Batter,
            int baseValue = 50,
            int ceilingValue = 60)
        {
            PlayerPosition position = playerType == PlayerType.Batter
                ? PlayerPosition.Catcher
                : PlayerPosition.StartingPitcher;
            return new PlayerSeasonDefinition(
                id,
                "person-" + id,
                year,
                franchise,
                franchise + "_" + year,
                position,
                playerType == PlayerType.Pitcher ? PitcherRole.Starter : PitcherRole.MiddleRelief,
                playerType,
                RegistrationType.Domestic,
                new AbilityRatings(baseValue),
                cost,
                new AbilityRatings(ceilingValue));
        }

        private static List<TeamColorRosterCard> CreateMixedFranchiseRoster()
        {
            var roster = new List<TeamColorRosterCard>();
            for (int index = 0; index < 25; index++)
            {
                int year = index < 20 ? 2011 : 2012;
                roster.Add(new TeamColorRosterCard(
                    "card-" + index,
                    new TeamColorEligibilityKey(year, "COMETS", "COMETS_" + year, PlayerCardEdition.Normal),
                    PlayerRole.Hitter));
            }
            return roster;
        }

        private static List<TeamColorRosterCard> CreateHonorRoster(int honorCount, PlayerCardEdition edition)
        {
            var roster = new List<TeamColorRosterCard>();
            for (int index = 0; index < 25; index++)
            {
                PlayerCardEdition cardEdition = index < honorCount ? edition : PlayerCardEdition.Normal;
                roster.Add(new TeamColorRosterCard(
                    "card-" + index,
                    new TeamColorEligibilityKey(2011, "COMETS", "COMETS_2011", cardEdition),
                    PlayerRole.Hitter));
            }
            return roster;
        }

        private static List<TeamColorRosterCard> CreateUniformRoster(PlayerCardEdition edition)
        {
            return CreateHonorRoster(25, edition);
        }
    }
}
