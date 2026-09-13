using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Shop;
using Baseball.Game.Historical;
using Baseball.Game.Shop;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>구단주 모드 SP 경기 보상과 정밀 Scout 보장 영입 트랜잭션을 검증한다.</summary>
    public sealed class OwnerScoutEconomyTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void 페넌트상여는_플레이어1위에만_지급하고_재진입과저장복원으로_중복되지않는다(bool playerWins)
        {
            CreateRuntime(out var runtime, out _, out var adapter);
            var season = runtime.ManagerMode.LiveSeason;
            foreach (var game in season.Schedule.Games)
            {
                if (game.IsCompleted) continue;
                bool homeWins = game.HomeTeamId == season.PlayerTeamId ? playerWins :
                    game.AwayTeamId == season.PlayerTeamId ? !playerWins : true;
                game.Complete(homeWins ? 0 : 1, homeWins ? 1 : 0);
            }
            SetSingleGroupWorld(runtime);
            var balance = BalanceTable.CreateDefault();
            var service = new OwnerPostseasonService(balance);
            int before = runtime.Economy.ScoutingPoints;
            service.EnsureInitialized(runtime);
            int expected = before + (playerWins ? balance.ScoutEconomy.PennantChampionshipScoutingPoints : 0);
            Assert.That(runtime.Economy.ScoutingPoints, Is.EqualTo(expected));
            service.EnsureInitialized(runtime);
            Assert.That(runtime.Economy.ScoutingPoints, Is.EqualTo(expected));
            var restored = adapter.Restore(adapter.CreateSaveData(runtime));
            service.EnsureInitialized(restored);
            Assert.That(restored.Economy.ScoutingPoints, Is.EqualTo(expected));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void 포스트상여는_우승확정경기에서만_지급한다(bool playerChampion)
        {
            CreateRuntime(out var runtime, out var provider, out var adapter);
            var season = runtime.ManagerMode.LiveSeason;
            foreach (var game in season.Schedule.Games)
                if (!game.IsCompleted) game.Complete(game.AwayTeamId == season.PlayerTeamId ? 10 : 0,
                    game.HomeTeamId == season.PlayerTeamId ? 10 : 1);
            SetSingleGroupWorld(runtime);
            var balance = BalanceTable.CreateDefault();
            var service = new OwnerPostseasonService(balance);
            var matches = new ManagerModeMatchService(provider, balance);
            service.EnsureInitialized(runtime);
            int gameIndex = 0;
            while (!runtime.LeagueWorld.IsPostseasonCompleted)
            {
                int before = runtime.Economy.ScoutingPoints;
                var group = runtime.LeagueWorld.Groups[0];
                var post = group.Postseason;
                var series = post.EnsureCurrentSeries();
                var game = series.AppendNextGame(2_000_000 + ++gameIndex, (ulong)gameIndex);
                int winner = playerChampion || !series.IncludesTeam(season.PlayerTeamId)
                    ? series.HigherSeedTeamId : series.LowerSeedTeamId;
                game.Complete(game.AwayTeamId == winner ? 1 : 0, game.HomeTeamId == winner ? 1 : 0);
                var record = typeof(OwnerPostseasonService).GetMethod("RecordCompletedGame", BindingFlags.NonPublic | BindingFlags.Instance);
                record.Invoke(service, new object[] { runtime, group, series, game });
                int expected = post.IsCompleted && post.ChampionTeamId == season.PlayerTeamId
                        ? balance.ScoutEconomy.PostseasonChampionshipScoutingPoints : 0;
                Assert.That(runtime.Economy.ScoutingPoints - before, Is.EqualTo(expected));
                Assert.Throws<TargetInvocationException>(() => record.Invoke(service, new object[] { runtime, group, series, game }));
                Assert.That(runtime.Economy.ScoutingPoints - before, Is.EqualTo(expected));
            }
            int final = runtime.Economy.ScoutingPoints;
            Assert.That(runtime.LeagueWorld.Groups[0].Postseason.ChampionTeamId == season.PlayerTeamId,
                Is.EqualTo(playerChampion));
            Assert.That(service.Complete(runtime, matches), Is.EqualTo(0));
            var restored = adapter.Restore(adapter.CreateSaveData(runtime));
            Assert.That(service.Complete(restored, matches), Is.EqualTo(0));
            Assert.That(restored.Economy.ScoutingPoints, Is.EqualTo(final));
        }

        [Test]
        public void 플레이어경기를_마치면_승패에_맞는_SP를_한번만_받는다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            BalanceTable balance = BalanceTable.CreateDefault();
            var service = new ManagerModeMatchService(provider, balance);

            for (int game = 0; game < 3; game++)
            {
                int before = runtime.Economy.ScoutingPoints;
                ManagerModeMatchResult result = service.PlayNextGame(runtime);
                var season = runtime.ManagerMode.LiveSeason;
                var scheduled = season.Schedule.Games.Single(game => game.GameId == result.Match.Input.GameId);
                bool playerIsHome = scheduled.HomeTeamId == season.PlayerTeamId;
                int playerRuns = playerIsHome ? result.Match.HomeBoxScore.Runs : result.Match.AwayBoxScore.Runs;
                int opponentRuns = playerIsHome ? result.Match.AwayBoxScore.Runs : result.Match.HomeBoxScore.Runs;
                int expected = balance.ScoutEconomy.GetMatchReward(playerRuns > opponentRuns);

                Assert.That(result.ScoutingPointsEarned, Is.EqualTo(expected));
                Assert.That(runtime.Economy.ScoutingPoints - before, Is.EqualTo(expected));
            }
        }

        [Test]
        public void 정밀Scout는_쓴SP만큼_게이지를_채우고_보장영입은_게이지만_써서_미보유1군을_준다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out _);
            ShopService shop = CreateShop(runtime, out ScoutPoolDefinition precise, out ScoutPityBalanceTable pity);
            string guaranteedId = ShopCatalogBuilder.GetGuaranteedProductId(precise.ScoutPoolId);
            runtime.Economy.AddScoutingPoints(precise.PriceSp);
            int pityBefore = runtime.Economy.PityGauge;

            Assert.That(shop.Purchase("shop.player." + precise.ScoutPoolId).IsSuccess, Is.True);
            Assert.That(runtime.Economy.PityGauge - pityBefore, Is.EqualTo(precise.PriceSp));
            Assert.That(shop.TryGetQuote(guaranteedId, out ShopPurchaseQuote locked), Is.True);
            Assert.That(locked.CanPurchase, Is.EqualTo(runtime.Economy.PityGauge >= pity.Threshold));

            runtime.Economy.AddPityGauge(pity.Threshold, pity.Threshold);
            HashSet<string> ownedBefore = PlayerCardPackFulfillment.CollectOwnedSeasonIds(runtime);
            int scoutingPointsBefore = runtime.Economy.ScoutingPoints;
            ShopPurchaseResult guaranteed = shop.Purchase(guaranteedId);

            Assert.That(guaranteed.IsSuccess, Is.True);
            Assert.That(runtime.Economy.PityGauge, Is.Zero, "보장 영입은 게이지를 소비만 하고 다시 채우지 않는다.");
            Assert.That(runtime.Economy.ScoutingPoints, Is.EqualTo(scoutingPointsBefore));
            PlayerCardDefinition card = runtime.WorldCardCatalog.GetRequiredCard(guaranteed.Items[0].ItemId);
            PlayerSeasonDefinition season = runtime.WorldCardCatalog.GetPlayerSeason(card);
            Assert.That(ownedBefore.Contains(season.PlayerSeasonId), Is.False);
            Assert.That(ScoutRoller.IsCandidate(precise, runtime.WorldCardCatalog,
                OwnerScoutEconomyTestsPolicy(runtime), card), Is.True);
        }

        [Test]
        public void 보장영입상품은_정밀Scout에만_있고_게이지로만_산다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out _);
            ShopService shop = CreateShop(runtime, out _, out ScoutPityBalanceTable pity);

            ShopProductDefinition[] guaranteed = shop.Catalog.Products
                .Where(product => product.Currency == ShopCurrency.ScoutPity).ToArray();

            Assert.That(guaranteed, Is.Not.Empty);
            Assert.That(guaranteed.All(product => product.Kind == ShopProductKind.PlayerCardPack &&
                product.Price == pity.Threshold && product.TargetYear.HasValue &&
                product.TargetFranchiseId.Length > 0), Is.True);
        }

        private static ShopService CreateShop(
            ManagerHistoricalRuntimeState runtime,
            out ScoutPoolDefinition precise,
            out ScoutPityBalanceTable pity)
        {
            BalanceTable balance = BalanceTable.CreateDefault();
            pity = balance.ScoutEconomy.Pity;
            ScoutFeaturePolicy policy = OwnerScoutEconomyTestsPolicy(runtime);
            // 플레이어 구단은 이미 1군 전원을 보유하므로, 미보유 1군 선수가 있는 구단 연도를 목표로 삼는다.
            HashSet<string> owned = PlayerCardPackFulfillment.CollectOwnedSeasonIds(runtime);
            PlayerSeasonDefinition playerSeason = runtime.WorldCardCatalog.Cards
                .Select(card => runtime.WorldCardCatalog.GetPlayerSeason(card))
                .First(candidate => runtime.WorldCardCatalog.IsActiveRosterSeason(candidate.PlayerSeasonId) &&
                    !owned.Contains(candidate.PlayerSeasonId));
            var targets = new[] { new ScoutMarketTarget(playerSeason.OriginFranchiseId, playerSeason.OriginYear) };
            IReadOnlyList<ScoutPoolDefinition> pools = ShopDefaultPools.CreateScoutPools(policy, targets);
            precise = pools.Single(pool => pool.ScoutType == ScoutType.YearFranchise);
            ShopCatalog catalog = ShopCatalogBuilder.Build(
                balance.Growth.SkillGacha, pools, ShopDefaultPools.CreateTacticResearchPools(), scoutPity: pity);
            var wallet = new ManagerEconomyShopWallet(runtime.Economy);
            var history = new ShopPurchaseHistoryState();
            var fulfillment = new PlayerCardPackFulfillment(new ScoutRoller(), pools, policy, pity, wallet,
                () => runtime, () => new Pcg32Random((ulong)(history.TotalPurchaseCount + 1)));
            return new ShopService(catalog, ShopAvailabilityTable.AllUnlocked(), wallet,
                new IShopProductFulfillment[] { fulfillment }, history);
        }

        private static ScoutFeaturePolicy OwnerScoutEconomyTestsPolicy(ManagerHistoricalRuntimeState runtime)
        {
            return runtime.WorldCardCatalog.Cards.Any(card => card.Edition != PlayerCardEdition.Normal)
                ? ScoutFeaturePolicy.FullWorldAwards
                : ScoutFeaturePolicy.Phase4NormalOnly;
        }

        private static void SetSingleGroupWorld(ManagerHistoricalRuntimeState runtime)
        {
            typeof(ManagerHistoricalRuntimeState).GetMethod("SetLeagueWorld", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(runtime, new object[] { new OwnerLeagueWorldState(
                    new[] { new OwnerLeagueGroupState(runtime.League, runtime.ManagerMode.LiveSeason) }, runtime.Rosters) });
        }

        private static void CreateRuntime(
            out ManagerHistoricalRuntimeState runtime,
            out IHistoricalContentProvider provider)
            => CreateRuntime(out runtime, out provider, out _);

        private static void CreateRuntime(
            out ManagerHistoricalRuntimeState runtime,
            out IHistoricalContentProvider provider,
            out ManagerHistoricalSaveAdapter adapter)
        {
            Type fixtureType = typeof(ManagerHistoricalSaveTests).GetNestedType("Fixture", BindingFlags.NonPublic);
            MethodInfo create = fixtureType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            object fixture = create.Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
            Type fixtureDataType = fixture.GetType();
            var state = (ManagerHistoricalRuntimeState)fixtureDataType
                .GetProperty("State", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            adapter = (ManagerHistoricalSaveAdapter)fixtureDataType
                .GetMethod("CreateAdapter", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(fixture, null);
            provider = (IHistoricalContentProvider)fixtureDataType
                .GetProperty("Provider", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            runtime = adapter.Restore(adapter.CreateSaveData(state));
        }
    }
}
