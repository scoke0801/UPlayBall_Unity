using System;
using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Rules;
using Baseball.Core.Shop;
using Baseball.Game.Shop;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class ManagerModeMatchServiceTests
    {
        [TestCase(60)]
        [TestCase(100)]
        public void Condition_능력치보정을강한타구확률단위로변환한다(int condition)
        {
            CreateRuntime(out var runtime, out var provider);
            foreach (var roster in runtime.Rosters)
                foreach (var player in runtime.ManagerMode.GetPlayerStatus(roster.TeamSeasonKey).Players)
                    player.SetCondition(condition);
            var balance = BalanceTable.CreateDefault();
            var input = new ManagerModeMatchService(provider, balance).PlayNextGame(runtime).Match.Input;
            var recorder = new ConditionMatchupRecorder();
            new MatchSimulator(balance, new Baseball.Simulation.Random.Pcg32Random(input.RandomSeed), recorder).Simulate(input);
            Assert.That(input.AwayRoster.TryGetEffectiveCondition(recorder.First.Batter.PlayerId, out var effective), Is.True);
            int rating = new Baseball.Simulation.Historical.MatchConditionRatingResolver(balance.ConditionChemistry)
                .ResolveRatingModifier(effective.Value);
            Assert.That(rating, Is.Not.Zero);
            Assert.That(recorder.First.HardHitAdjustment, Is.EqualTo(rating * balance.BattedBall.PowerHomeRunWeight).Within(1e-12));
            Assert.That(recorder.First.BatterContactAdjustment, Is.EqualTo(rating));
        }

        private sealed class ConditionMatchupRecorder : IPlateAppearanceSimulator
        {
            private bool _hasFirst;
            public PlateAppearanceMatchup First { get; private set; }

            public PitchResult SimulatePitch(in PlateAppearanceMatchup matchup, int balls, int strikes, int pitchNumber,
                Baseball.Core.Players.BattingApproach approach)
            {
                if (!_hasFirst) { First = matchup; _hasFirst = true; }
                return PitchResult.CalledStrike;
            }

            public PlateAppearanceResult ResolveBallInPlay(in PlateAppearanceMatchup matchup,
                Baseball.Core.Players.BattingApproach approach) => throw new InvalidOperationException("인플레이 없는 단위 검증이다.");
        }

        [Test]
        public void HeadCoach_선수단경기컨디션을높이고저장원본에는중복가산하지않는다()
        {
            CreateRuntime(out var runtime, out var provider);
            var balance = BalanceTable.CreateDefault();
            var state = runtime.ManagerMode.Dugout;
            var catalog = DugoutStaffCatalog.CreateDefault();
            HeadCoachDefinition coach = null;
            foreach (var candidate in catalog.HeadCoaches)
                if (candidate.HasConditionSupport) coach = candidate;
            Assert.That(coach, Is.Not.Null);
            state.Configure(state.ManagerId, coach.HeadCoachId, state.Policy, catalog);
            var match = new ManagerModeMatchService(provider, balance).PlayNextGame(runtime).Match;
            var roster = match.Input.HomeRoster.TeamId == runtime.ManagerMode.LiveSeason.PlayerTeamId
                ? match.Input.HomeRoster : match.Input.AwayRoster;
            for (int index = 0; index < roster.StartingLineup.Count; index++)
            {
                var slot = roster.StartingLineup[index];
                Assert.That(roster.TryGetEffectiveCondition(slot.Player.PlayerId, out var condition), Is.True);
                Assert.That(condition.StoredBaseCondition, Is.EqualTo(balance.ConditionChemistry.NeutralMatchCondition));
                Assert.That(condition.TemporaryModifier, Is.EqualTo(balance.ConditionChemistry.HeadCoachConditionBonus));
            }
            Assert.That(roster.StartingPitcher.Condition,
                Is.EqualTo(balance.ConditionChemistry.NeutralMatchCondition + balance.ConditionChemistry.HeadCoachConditionBonus));
            foreach (var player in runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey).Players)
                Assert.That(player.StoredBaseCondition, Is.InRange(77, 83));
        }

        [TestCase(80, true)]
        [TestCase(97, true)]
        [TestCase(100, false)]
        public void ConditionItem_전원에게즉시적용하고결제와저장을일치시킨다(int initialCondition, bool succeeds)
        {
            CreateRuntime(out var runtime, out var provider);
            var balance = BalanceTable.CreateDefault();
            foreach (var player in runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey).Players)
                player.SetCondition(initialCondition);
            var wallet = new ManagerEconomyShopWallet(runtime.Economy);
            var catalog = ShopCatalogBuilder.Build(balance.Growth.SkillGacha, null, null, conditionBalance: balance.ConditionChemistry);
            int featuredCount = 0;
            foreach (var product in catalog.GetProducts(ShopTab.Featured))
                if (product.Kind == ShopProductKind.ConditionItem) featuredCount++;
            Assert.That(featuredCount, Is.EqualTo(1));
            var shop = new ShopService(catalog, ShopAvailabilityTable.AllUnlocked(), wallet,
                new IShopProductFulfillment[] { new ConditionItemFulfillment(wallet, () => runtime, balance.ConditionChemistry) },
                runtime.ShopPurchaseHistory);
            long before = runtime.Economy.Money;
            Assert.That(shop.Purchase(ConditionItemFulfillment.ProductId).IsSuccess, Is.EqualTo(succeeds));
            Assert.That(runtime.Economy.Money, Is.EqualTo(before - (succeeds ? balance.ConditionChemistry.ConditionItemPrice : 0)));
            var adapter = new ManagerHistoricalSaveAdapter(provider, CardEditionBalanceTable.CreateInitial());
            var restored = adapter.Restore(adapter.CreateSaveData(runtime));
            foreach (var player in restored.ManagerMode.GetPlayerStatus(restored.PlayerTeamSeasonKey).Players)
                Assert.That(player.StoredBaseCondition, Is.EqualTo(Math.Min(100, initialCondition + (succeeds ? balance.ConditionChemistry.ConditionItemBoost : 0))));
            Assert.That(restored.ShopPurchaseHistory.GetPurchaseCount(ConditionItemFulfillment.ProductId), Is.EqualTo(succeeds ? 1 : 0));
        }

        [Test]
        public void ConditionItem_잔액부족이면컨디션과구매횟수를보존한다()
        {
            CreateRuntime(out var runtime, out _);
            Assert.That(runtime.Economy.TrySpendMoney(runtime.Economy.Money), Is.True);
            var balance = BalanceTable.CreateDefault();
            var wallet = new ManagerEconomyShopWallet(runtime.Economy);
            var shop = new ShopService(
                ShopCatalogBuilder.Build(balance.Growth.SkillGacha, null, null, conditionBalance: balance.ConditionChemistry),
                ShopAvailabilityTable.AllUnlocked(), wallet,
                new IShopProductFulfillment[] { new ConditionItemFulfillment(wallet, () => runtime, balance.ConditionChemistry) },
                runtime.ShopPurchaseHistory);
            Assert.That(shop.Purchase(ConditionItemFulfillment.ProductId).IsSuccess, Is.False);
            Assert.That(runtime.ShopPurchaseHistory.GetPurchaseCount(ConditionItemFulfillment.ProductId), Is.Zero);
            foreach (var player in runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey).Players)
                Assert.That(player.StoredBaseCondition, Is.EqualTo(balance.ConditionChemistry.NeutralMatchCondition));
        }

        [TestCase(LeagueGrade.Champion, 0)]
        [TestCase(LeagueGrade.Master, 1)]
        public void AiTactics_리그경계가경기입력에반영되고플레이어카드는유지된다(LeagueGrade grade, int aiCards)
        {
            CreateRuntime(out var runtime, out var provider);
            var adapter = new ManagerHistoricalSaveAdapter(provider, CardEditionBalanceTable.CreateInitial());
            var save = adapter.CreateSaveData(runtime);
            save.league.grade = (int)grade;
            runtime = adapter.Restore(save);
            var card = new TacticCardDefinition("AUDIT_CONTACT", "컨택 작전", TacticCardCategory.Common,
                TacticTier.Normal, "테스트", "테스트", Array.Empty<TacticTriggerCondition>(),
                TacticTargetRule.BattingTeam,
                new[] { new TacticStatModifier(Baseball.Core.Growth.PlayerAbility.Contact, 1) },
                Array.Empty<TacticBehaviorModifier>(), TacticDurationRule.UntilInningEnd,
                Array.Empty<string>(), false);
            runtime.TacticCollection.Acquire(card.CardId);
            runtime.ManagerMode.LiveSeason.NextPlayerGame.PlanTactics(new[] { card.CardId });
            var match = new ManagerModeMatchService(provider, BalanceTable.CreateDefault(),
                tacticCards: new[] { card }).PlayNextGame(runtime).Match;
            bool playerIsHome = match.Input.HomeRoster.TeamId == runtime.ManagerMode.LiveSeason.PlayerTeamId;
            var configuration = match.Input.HistoricalConfiguration;
            Assert.That((playerIsHome ? configuration.HomeTacticLoadout : configuration.AwayTacticLoadout).Cards.Count,
                Is.EqualTo(1));
            Assert.That((playerIsHome ? configuration.AwayTacticLoadout : configuration.HomeTacticLoadout).Cards.Count,
                Is.EqualTo(aiCards));
            Assert.That(runtime.TacticCollection.Contains(card.CardId), Is.False);
        }

        [Test]
        public void PitchingWorkload_AI대AI를포함한모든휴식선발이하루씩갱신된다()
        {
            CreateRuntime(out var runtime, out var provider);
            foreach (var roster in runtime.Rosters)
                foreach (var entry in roster.Entries)
                    if (entry.Role == ActiveRosterRole.StartingPitcher2)
                    {
                        var player = runtime.ManagerMode.GetPlayerStatus(roster.TeamSeasonKey).GetRequiredPlayer(entry.PlayerPersonId);
                        player.AdvancePitchingWorkload(30);
                        player.AdvancePitchingWorkload(60);
                        player.AdvancePitchingWorkload(90);
                    }
            new ManagerModeMatchService(provider, BalanceTable.CreateDefault()).PlayNextGame(runtime);
            int checkedTeams = 0;
            foreach (var roster in runtime.Rosters)
                foreach (var entry in roster.Entries)
                    if (entry.Role == ActiveRosterRole.StartingPitcher2)
                    {
                        var load = runtime.ManagerMode.GetPlayerStatus(roster.TeamSeasonKey).GetRequiredPlayer(entry.PlayerPersonId).PitchingWorkload;
                        Assert.That(load.PreviousDayPitches, Is.Zero, roster.TeamSeasonKey);
                        Assert.That(load.TwoDaysAgoPitches, Is.EqualTo(90), roster.TeamSeasonKey);
                        Assert.That(load.ThreeDaysAgoPitches, Is.EqualTo(60), roster.TeamSeasonKey);
                        checkedTeams++;
                    }
            Assert.That(checkedTeams, Is.EqualTo(10));
        }

        [TestCase(0), TestCase(6), Explicit("회복 없음과 6경기마다 주간 회복을 비교해 각각 10,000경기 이상을 검증한다.")]
        public void PitchingWorkload_대량시즌통계(int recoveryInterval)
        {
            long games = 0, runs = 0, hits = 0, atBats = 0, homeRuns = 0, walks = 0, strikeouts = 0, earned = 0, outs = 0;
            int seasons = 0;
            while (games < 10000)
            {
                CreateRuntime(out var runtime, out var provider);
                var adapter = new ManagerHistoricalSaveAdapter(provider, CardEditionBalanceTable.CreateInitial());
                var save = adapter.CreateSaveData(runtime);
                foreach (var game in save.managerMode.liveSeason.games)
                    game.randomSeed = (ulong)(71000000 + seasons * 10000 + game.gameId);
                runtime = adapter.Restore(save);
                var coordinator = new ManagerModeCoordinator(BalanceTable.CreateDefault());
                int completedPlayerGames = 0;
                var result = new ManagerModeMatchService(provider, BalanceTable.CreateDefault()).CompleteRegularSeason(runtime, _ =>
                {
                    completedPlayerGames++;
                    if (recoveryInterval > 0 && completedPlayerGames % recoveryInterval == 0)
                        Assert.That(coordinator.AdvanceWeek(runtime).Status, Is.EqualTo(ManagerModeTransactionStatus.Applied));
                });
                Assert.That(result.IsCompleted, Is.True);
                games += result.LeagueGamesSimulated;
                foreach (var player in runtime.ManagerMode.LiveSeason.Statistics.RegularSeason.Players.Values)
                {
                    var batting = player.Batting;
                    var pitching = player.Pitching;
                    runs += batting.Runs; hits += batting.Hits; atBats += batting.AtBats;
                    homeRuns += batting.HomeRuns; walks += batting.Walks; strikeouts += batting.Strikeouts;
                    earned += pitching.EarnedRuns; outs += pitching.OutsRecorded;
                }
                seasons++;
            }
            Assert.That(outs, Is.GreaterThan(0));
            Assert.That(hits / (double)atBats, Is.InRange(0.22d, 0.33d), "컨디션 등락이 장기 타율을 확률 상한·하한으로 밀면 안 된다.");
            Assert.That(earned * 27d / outs, Is.InRange(2d, 6d));
            TestContext.WriteLine($"주간 회복 간격={recoveryInterval}, 피로 누적 {seasons}시즌 {games}경기: AVG={hits / (double)atBats:F3}, ERA={earned * 27d / outs:F3}, " +
                $"팀 경기당 득점={runs / (2d * games):F3}, HR={homeRuns / (2d * games):F3}, BB/K={walks / (double)strikeouts:F3}");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        public void PitchingWorkload_휴식선발과빈라운드를반영하고불러와도같다(int roundSpacing)
        {
            CreateRuntime(out var runtime, out var provider);
            var adapter = new ManagerHistoricalSaveAdapter(provider, CardEditionBalanceTable.CreateInitial());
            var save = adapter.CreateSaveData(runtime);
            foreach (var game in save.managerMode.liveSeason.games)
                game.round = (game.round - 1) * roundSpacing + 1;
            runtime = adapter.Restore(save);
            var service = new ManagerModeMatchService(provider, BalanceTable.CreateDefault());
            string teamKey = runtime.PlayerTeamSeasonKey;
            var firstRoster = runtime.GetRoster(teamKey);
            ActiveRosterEntry firstStarter = null;
            foreach (var entry in firstRoster.Entries)
                if (entry.Role == ActiveRosterRole.StartingPitcher1) firstStarter = entry;
            Assert.That(firstStarter, Is.Not.Null);
            int firstPitches = 0;
            for (int gameIndex = 0; gameIndex < 6; gameIndex++)
            {
                var replay = adapter.Restore(adapter.CreateSaveData(runtime));
                var match = service.PlayNextGame(runtime).Match;
                var repeated = service.PlayNextGame(replay).Match;
                Assert.That(repeated.AwayBoxScore.Runs, Is.EqualTo(match.AwayBoxScore.Runs));
                Assert.That(repeated.HomeBoxScore.Runs, Is.EqualTo(match.HomeBoxScore.Runs));
                var roster = match.Input.AwayRoster.TeamId == runtime.ManagerMode.LiveSeason.PlayerTeamId
                    ? match.Input.AwayRoster : match.Input.HomeRoster;
                if (gameIndex == 0)
                {
                    foreach (var usage in match.PitcherUsage)
                        if (usage.PlayerId == roster.StartingPitcher.Player.PlayerId) firstPitches = usage.PitchCount;
                    Assert.That(firstPitches, Is.GreaterThan(0));
                }
                if (gameIndex == 1 && roundSpacing == 1)
                {
                    var load = runtime.ManagerMode.GetPlayerStatus(teamKey)
                        .GetRequiredPlayer(firstStarter.PlayerPersonId).PitchingWorkload;
                    Assert.That(load.PreviousDayPitches, Is.Zero);
                    Assert.That(load.TwoDaysAgoPitches, Is.EqualTo(firstPitches));
                }
                // 연속 일정의 두 번째 로테이션, 또는 3일 이상 쉬고 재등판하는 입력을 직접 확인한다.
                if (gameIndex == 5 && roundSpacing == 1 || gameIndex > 0 && roundSpacing == 5)
                {
                    Assert.That(roster.StartingPitcher.RecentWorkload.PreviousDayPitches, Is.Zero);
                    Assert.That(roster.StartingPitcher.RecentWorkload.TwoDaysAgoPitches, Is.Zero);
                    Assert.That(roster.StartingPitcher.RecentWorkload.ThreeDaysAgoPitches, Is.Zero);
                }
                foreach (var entry in runtime.GetRoster(teamKey).Entries)
                {
                    var actual = runtime.ManagerMode.GetPlayerStatus(teamKey).GetRequiredPlayer(entry.PlayerPersonId).PitchingWorkload;
                    var restored = replay.ManagerMode.GetPlayerStatus(teamKey).GetRequiredPlayer(entry.PlayerPersonId).PitchingWorkload;
                    Assert.That(restored, Is.EqualTo(actual));
                }
            }
        }

        [Test]
        public void AiTeamColor_공개선택이실제타자경기능력치에반영된다()
        {
            CreateRuntime(out var runtime, out var provider);
            var balance = BalanceTable.CreateDefault();
            var match = new ManagerModeMatchService(provider, balance).PlayNextGame(runtime).Match;
            var opponent = match.Input.AwayRoster.TeamId == runtime.ManagerMode.LiveSeason.PlayerTeamId
                ? match.Input.HomeRoster : match.Input.AwayRoster;
            var roster = runtime.GetRoster(runtime.ManagerMode.LiveSeason.GetTeamSeasonKey(opponent.TeamId));
            var bonuses = ManagerModeMatchService.ResolveAiTeamColorBonuses(
                roster, runtime.WorldCardCatalog, balance.TeamColor, out var selected);
            Assert.That(selected[0], Is.Not.Null);
            var plan = ManagerModeMatchService.CreateRosterRolePlan(roster);
            int positiveBonuses = 0;
            for (int index = 0; index < plan.BattingOrderCardIds.Count; index++)
            {
                string id = plan.BattingOrderCardIds[index];
                Assert.That(runtime.WorldCardCatalog.TryGetCard(id, out var card), Is.True);
                var season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                int bonus = bonuses.Get(id, Baseball.Core.Growth.PlayerAbility.Contact);
                if (bonus > 0) positiveBonuses++;
                int raw = new Baseball.Simulation.Historical.OwnerCardAbilityResolver(balance.Growth)
                    .ResolveRawPermanent(season, card, null, Baseball.Core.Growth.PlayerAbility.Contact);
                Assert.That(opponent.StartingLineup[index].Player.BatterAttributes.Contact,
                    Is.EqualTo(MatchRatingCurve.ResolveMatchInput(raw + bonus, balance.MatchRatingCurve)));
            }
            Assert.That(positiveBonuses, Is.GreaterThan(0));
            var replay = new MatchSimulator(balance,
                Baseball.Simulation.Random.MatchRandomStreams.Create(match.Input.RandomSeed)).Simulate(match.Input);
            Assert.That(replay.AwayBoxScore.Runs, Is.EqualTo(match.AwayBoxScore.Runs));
            Assert.That(replay.HomeBoxScore.Runs, Is.EqualTo(match.HomeBoxScore.Runs));
        }

        [Test, Explicit("구단주 서비스가 만든 AI 팀컬러 적용 입력으로 10,000경기를 검증한다.")]
        public void AiTeamColor_대량경기통계()
        {
            CreateRuntime(out var runtime, out var provider);
            var balance = BalanceTable.CreateDefault();
            var template = new ManagerModeMatchService(provider, balance).PlayNextGame(runtime).Match.Input;
            long hits = 0, atBats = 0, runs = 0, earned = 0, outs = 0, homeRuns = 0, walks = 0, strikeouts = 0;
            const int games = 10000;
            for (int index = 0; index < games; index++)
            {
                var input = new MatchInput(template.SeasonId, index + 1, (ulong)(620000 + index),
                    template.AwayRoster, template.HomeRoster, template.Rules,
                    historicalConfiguration: template.HistoricalConfiguration);
                var result = new MatchSimulator(balance,
                    Baseball.Simulation.Random.MatchRandomStreams.Create(input.RandomSeed)).Simulate(input);
                foreach (var box in new[] { result.AwayBoxScore, result.HomeBoxScore })
                {
                    runs += box.Runs;
                    foreach (var line in box.BattingLines)
                    {
                        hits += line.Hits; atBats += line.AtBats; homeRuns += line.HomeRuns;
                        walks += line.Walks; strikeouts += line.Strikeouts;
                    }
                    foreach (var line in box.PitchingLines) { earned += line.EarnedRuns; outs += line.OutsRecorded; }
                }
            }
            Assert.That(outs, Is.GreaterThan(0));
            TestContext.WriteLine($"AI 팀컬러 {games}경기: AVG={hits / (double)atBats:F3}, ERA={earned * 27d / outs:F3}, " +
                $"팀 경기당 득점={runs / (2d * games):F3}, HR={homeRuns / (2d * games):F3}, BB/K={walks / (double)strikeouts:F3}");
        }

        [TestCase(0, -10)]
        [TestCase(4, 10)]
        public void Dugout_ExtremePoliciesReachDetailedMatchAtInitialTrust(int level, int modifier)
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            var state = runtime.ManagerMode.Dugout;
            state.Configure(state.ManagerId, state.HeadCoachId,
                new DugoutPolicySettings(level, level, level, level, level, level),
                DugoutStaffCatalog.CreateDefault());
            var result = new ManagerModeMatchService(provider, BalanceTable.CreateDefault()).PlayNextGame(runtime);
            var input = result.Match.Input;
            var profile = (input.AwayRoster.TeamId == runtime.ManagerMode.LiveSeason.PlayerTeamId
                ? input.AwayRoster : input.HomeRoster).ManagerProfile;
            Assert.That(profile.BattingApproach, Is.EqualTo(40 + modifier));
            Assert.That(profile.RunningAggression, Is.EqualTo(50 + modifier));
            Assert.That(profile.SmallBallPreference, Is.EqualTo(50 + modifier));
            Assert.That(profile.PinchHitAggression, Is.EqualTo(53 + modifier));
            Assert.That(profile.HookSpeed, Is.EqualTo(50 + modifier));
            Assert.That(profile.BullpenAggression, Is.EqualTo(50 + modifier));
        }

        [Test, Explicit("동일한 구단주 경기 로스터로 방침별 1,000경기를 비교한다.")]
        public void Dugout_PolicyBatchChangesMatchEventsAndRemainsDeterministic()
        {
            const int games = 1000;
            var eventTotals = new long[3];
            for (int variant = 0; variant < 3; variant++)
            {
                int level = variant * 2;
                CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
                var state = runtime.ManagerMode.Dugout;
                state.Configure(state.ManagerId, state.HeadCoachId,
                    new DugoutPolicySettings(level, level, level, level, level, level),
                    DugoutStaffCatalog.CreateDefault());
                var balance = BalanceTable.CreateDefault();
                MatchInput template = new ManagerModeMatchService(provider, balance).PlayNextGame(runtime).Match.Input;
                long runs = 0, hits = 0, atBats = 0, homeRuns = 0, walks = 0, strikeouts = 0;
                long steals = 0, bunts = 0, substitutions = 0, earnedRuns = 0, outsRecorded = 0;
                for (int game = 0; game < games; game++)
                {
                    var input = new MatchInput(template.SeasonId, game + 1, (ulong)(910000 + game),
                        template.AwayRoster, template.HomeRoster, template.Rules,
                        historicalConfiguration: template.HistoricalConfiguration);
                    var result = new MatchSimulator(balance,
                        Baseball.Simulation.Random.MatchRandomStreams.Create(input.RandomSeed)).Simulate(input);
                    if (game == 0)
                    {
                        var replay = new MatchSimulator(balance,
                            Baseball.Simulation.Random.MatchRandomStreams.Create(input.RandomSeed)).Simulate(input);
                        Assert.That(replay.Events, Is.EqualTo(result.Events), "같은 방침과 Seed는 이벤트까지 같아야 한다.");
                    }
                    foreach (var box in new[] { result.AwayBoxScore, result.HomeBoxScore })
                    {
                        runs += box.Runs;
                        foreach (var line in box.PitchingLines)
                        {
                            earnedRuns += line.EarnedRuns;
                            outsRecorded += line.OutsRecorded;
                        }
                        foreach (var line in box.BattingLines)
                        {
                            hits += line.Hits; atBats += line.AtBats; homeRuns += line.HomeRuns;
                            walks += line.Walks; strikeouts += line.Strikeouts;
                        }
                    }
                    foreach (var item in result.Events)
                    {
                        if (item.EventType == MatchEventType.StealAttempted) steals++;
                        if (item.EventType == MatchEventType.BuntAttempted) bunts++;
                        if (item.EventType == MatchEventType.PitcherRemoved) substitutions++;
                    }
                    eventTotals[variant] += result.Events.Count;
                }
                TestContext.WriteLine($"방침 {level - 2}: {games}경기 AVG={hits / (double)atBats:F3}, " +
                    $"ERA={earnedRuns * 27d / outsRecorded:F3}, 득점/경기={runs / (double)games:F3}, HR/경기={homeRuns / (double)games:F3}, " +
                    $"BB/K={walks / (double)strikeouts:F3}, 도루시도={steals}, 번트시도={bunts}, 투수교체={substitutions}");
            }
            Assert.That(eventTotals[0], Is.Not.EqualTo(eventTotals[1]));
            Assert.That(eventTotals[2], Is.Not.EqualTo(eventTotals[1]));
        }

        [Test]
        public void PlayNextGame_UsesDetailedPathAndUpdatesConditionFamiliarityAndSchedule()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            TeamSeasonPlayerStatus firstHitter = runtime.ManagerMode
                .GetPlayerStatus(runtime.PlayerTeamSeasonKey)
                .Players[0];
            int conditionBefore = firstHitter.StoredBaseCondition;
            ScheduledGameState scheduled = runtime.ManagerMode.LiveSeason.NextPlayerGame;
            var service = new ManagerModeMatchService(provider, BalanceTable.CreateDefault());

            ManagerModeMatchResult result = service.PlayNextGame(runtime);

            Assert.That(result.Match.Input.RulesVersion, Is.EqualTo(SimulationRulesVersion.DetailedV2));
            Assert.That(result.Match.Input.GameId, Is.EqualTo(scheduled.GameId));
            Assert.That(scheduled.IsCompleted, Is.True);
            Assert.That(Math.Abs(firstHitter.StoredBaseCondition - conditionBefore),
                Is.LessThanOrEqualTo(BalanceTable.CreateDefault().ConditionChemistry.ConditionFluctuation));
            Assert.That(
                runtime.ManagerMode.GetFamiliarity(runtime.PlayerTeamSeasonKey).Entries.Count,
                Is.GreaterThan(0));
            Assert.That(result.Match.BatteryUsage.Count, Is.GreaterThan(0));
            Assert.That(
                SumBatteryFamiliarity(runtime.ManagerMode.GetFamiliarity(runtime.PlayerTeamSeasonKey)),
                Is.GreaterThan(0));
            Assert.That(result.PlayerPlan.ScheduledGameId, Is.EqualTo(scheduled.GameId));
        }

        private static int SumBatteryFamiliarity(TeamChemistryFamiliarityState state)
        {
            int total = 0;
            for (int index = 0; index < state.Entries.Count; index++)
                total += state.Entries[index].BatteryFamiliarity;
            return total;
        }

        [Test]
        public void SameSaveAndSeed_ReproducesMatchAndAttendance()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState first, out IHistoricalContentProvider firstProvider);
            CreateRuntime(out ManagerHistoricalRuntimeState second, out IHistoricalContentProvider secondProvider);

            ManagerModeMatchResult firstResult = new ManagerModeMatchService(
                firstProvider,
                BalanceTable.CreateDefault()).PlayNextGame(first);
            ManagerModeMatchResult secondResult = new ManagerModeMatchService(
                secondProvider,
                BalanceTable.CreateDefault()).PlayNextGame(second);

            Assert.That(firstResult.Match.AwayBoxScore.Runs, Is.EqualTo(secondResult.Match.AwayBoxScore.Runs));
            Assert.That(firstResult.Match.HomeBoxScore.Runs, Is.EqualTo(secondResult.Match.HomeBoxScore.Runs));
            Assert.That(firstResult.Match.PitcherUsage.Count, Is.EqualTo(secondResult.Match.PitcherUsage.Count));
            Assert.That(firstResult.HomeFinance.Status, Is.EqualTo(secondResult.HomeFinance.Status));
            Assert.That(firstResult.HomeFinance.Attendance, Is.EqualTo(secondResult.HomeFinance.Attendance));
        }

        [Test]
        public void SelectedPreset_IsRevalidatedImmediatelyBeforeMatch()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            LineupPresetState original = runtime.ManagerMode.GetSelectedLineupPreset();
            var invalidBattingOrder = new string[original.BattingOrderCardIds.Count];
            for (int index = 0; index < invalidBattingOrder.Length; index++)
                invalidBattingOrder[index] = original.BattingOrderCardIds[index];
            invalidBattingOrder[1] = invalidBattingOrder[0];
            var invalid = new LineupPresetState(
                "preset:invalid",
                "잘못된 프리셋",
                original.StartingLineupSlots,
                invalidBattingOrder,
                original.BenchPriorityCardIds,
                original.StarterRotationCardIds,
                original.BullpenAssignmentCardIds,
                original.SetupPitcherCardId,
                original.CloserPitcherCardId,
                original.TeamColorIds,
                original.DefaultTacticCardIds);
            runtime.ManagerMode.UpsertLineupPreset(invalid);
            runtime.ManagerMode.SelectLineupPreset(invalid.PresetId);
            ScheduledGameState scheduled = runtime.ManagerMode.LiveSeason.NextPlayerGame;

            Assert.Throws<InvalidOperationException>(() =>
                new ManagerModeMatchService(provider, BalanceTable.CreateDefault()).PlayNextGame(runtime));
            Assert.That(scheduled.IsCompleted, Is.False);
        }

        [Test]
        public void PitcherAndCatcherBatteryLookup_UsesCurrentPairWithoutAccumulation()
        {
            var balance = BalanceTable.CreateDefault();
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);

            ManagerModeMatchResult result = new ManagerModeMatchService(provider, balance).PlayNextGame(runtime);
            MatchRosterSnapshot roster = result.Match.Input.AwayRoster.TeamId ==
                                         runtime.ManagerMode.LiveSeason.PlayerTeamId
                ? result.Match.Input.AwayRoster
                : result.Match.Input.HomeRoster;
            int catcherId = roster.StartingLineup[0].Player.PlayerId;
            Assert.That(
                roster.TryGetBatteryConditionModifier(
                    roster.StartingPitcher.Player.PlayerId,
                    catcherId,
                    out int starterModifier),
                Is.True);
            Assert.That(
                roster.TryGetBatteryConditionModifier(
                    roster.Bullpen[0].Player.PlayerId,
                    catcherId,
                    out int reliefModifier),
                Is.True);
            Assert.That(starterModifier, Is.InRange(-balance.ConditionChemistry.ConditionLevelStep,
                balance.ConditionChemistry.ConditionLevelStep));
            Assert.That(reliefModifier, Is.InRange(-balance.ConditionChemistry.ConditionLevelStep,
                balance.ConditionChemistry.ConditionLevelStep));
        }

        [Test]
        public void HomeGame_AppliesAttendanceRevenueAndFanChangeOnlyOnPlayerHomeGame()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            var service = new ManagerModeMatchService(provider, BalanceTable.CreateDefault());
            ScheduledGameState next = runtime.ManagerMode.LiveSeason.NextPlayerGame;
            while (next.HomeTeamId != runtime.ManagerMode.LiveSeason.PlayerTeamId)
            {
                ManagerModeMatchResult away = service.PlayNextGame(runtime);
                Assert.That(away.HomeFinance.Status, Is.EqualTo(HomeGameFinanceStatus.NotHomeGame));
                next = runtime.ManagerMode.LiveSeason.NextPlayerGame;
            }

            long moneyBefore = runtime.Economy.Money;
            double fanBaseBefore = runtime.ManagerMode.ClubOperation.FanBase;
            ManagerModeMatchResult home = service.PlayNextGame(runtime);

            Assert.That(home.HomeFinanceStatus, Is.EqualTo(ManagerModeTransactionStatus.Applied));
            Assert.That(home.HomeFinance.Attendance, Is.InRange(0, home.HomeFinance.Capacity));
            Assert.That(runtime.Economy.Money, Is.EqualTo(moneyBefore + home.HomeFinance.NetGameIncome));
            Assert.That(runtime.ManagerMode.ClubOperation.CurrentSeason.HomeGames, Is.EqualTo(1));
            Assert.That(runtime.ManagerMode.ClubOperation.FanBase, Is.Not.EqualTo(fanBaseBefore));
        }

        [Test]
        public void PreviewNextHomeAttendance_실제경기와같은Context와Seed를사용한다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            var service = new ManagerModeMatchService(provider, BalanceTable.CreateDefault());
            while (runtime.ManagerMode.LiveSeason.NextPlayerGame.HomeTeamId !=
                   runtime.ManagerMode.LiveSeason.PlayerTeamId)
                service.PlayNextGame(runtime);

            AttendanceResult? preview = service.PreviewNextHomeAttendance(runtime);
            ManagerModeMatchResult result = service.PlayNextGame(runtime);

            Assert.That(preview.HasValue, Is.True);
            Assert.That(preview.Value.Attendance, Is.EqualTo(result.HomeFinance.Attendance));
            Assert.That(preview.Value.Attendance, Is.LessThanOrEqualTo(preview.Value.Capacity));
        }

        [Test]
        public void PlayNextGame_같은라운드의AI구단대진도함께확정한다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            ManagerLiveSeasonState season = runtime.ManagerMode.LiveSeason;
            int playerRound = season.NextPlayerGame.Round;

            new ManagerModeMatchService(provider, BalanceTable.CreateDefault()).PlayNextGame(runtime);

            int completedAiGames = 0;
            IReadOnlyList<ScheduledGameState> games = season.Schedule.Games;
            for (int index = 0; index < games.Count; index++)
            {
                ScheduledGameState game = games[index];
                if (game.Round > playerRound)
                {
                    Assert.That(game.IsCompleted, Is.False, "이후 라운드를 미리 진행하면 안 된다.");
                    continue;
                }
                Assert.That(game.IsCompleted, Is.True, $"라운드 {game.Round}의 대진이 남아 있다.");
                if (!game.IncludesTeam(season.PlayerTeamId)) completedAiGames++;
            }
            Assert.That(completedAiGames, Is.GreaterThan(0));
        }

        [Test]
        public void 정규시즌세션은한Step만진행하고안전지점에서중단한다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            ManagerLiveSeasonState season = runtime.ManagerMode.LiveSeason;
            int firstRound = season.NextPlayerGame.Round;
            var session = new ManagerRegularSeasonSimulationSession(
                runtime,
                new ManagerModeMatchService(provider, BalanceTable.CreateDefault()));

            ManagerRegularSeasonSimulationProgress initial = session.CreateProgressSnapshot();
            Assert.That(initial.Status, Is.EqualTo(ManagerRegularSeasonSimulationStatus.Ready));
            Assert.That(initial.PlayerGamesSimulated, Is.Zero);
            Assert.That(initial.LeagueGamesSimulated, Is.Zero);
            Assert.That(initial.NextRound, Is.EqualTo(firstRound));

            ManagerRegularSeasonSimulationStepResult step = session.AdvanceNextStep();

            Assert.That(step.MatchResult, Is.Not.Null);
            Assert.That(step.Progress.PlayerGamesSimulated, Is.EqualTo(1));
            Assert.That(step.Progress.LeagueGamesSimulated, Is.GreaterThan(1));
            Assert.That(step.Progress.LastCompletedRound, Is.EqualTo(firstRound));
            Assert.That(season.NextPlayerGame.Round, Is.GreaterThan(firstRound));
            ManagerRegularSeasonSimulationProgress stopped = session.StopByUser();
            Assert.That(stopped.Status, Is.EqualTo(ManagerRegularSeasonSimulationStatus.StoppedByUser));
            Assert.That(stopped.PlayerGamesSimulated, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => session.AdvanceNextStep());
        }

        [Test]
        public void 정규시즌을끝까지진행하면모든구단이같은경기수를치른다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            ManagerLiveSeasonState season = runtime.ManagerMode.LiveSeason;
            var service = new ManagerModeMatchService(provider, BalanceTable.CreateDefault());

            ManagerRegularSeasonCompletionResult result = service.CompleteRegularSeason(runtime);

            var gamesByTeam = new Dictionary<int, int>();
            for (int index = 0; index < season.Teams.Count; index++) gamesByTeam.Add(season.Teams[index].TeamId, 0);
            IReadOnlyList<ScheduledGameState> games = season.Schedule.Games;
            for (int index = 0; index < games.Count; index++)
            {
                ScheduledGameState game = games[index];
                Assert.That(game.IsCompleted, Is.True, "시즌 종료 시 미완료 대진이 남아 있다.");
                gamesByTeam[game.AwayTeamId]++;
                gamesByTeam[game.HomeTeamId]++;
            }

            int playerGames = gamesByTeam[season.PlayerTeamId];
            Assert.That(playerGames, Is.GreaterThan(0));
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.PlayerGamesSimulated, Is.EqualTo(playerGames));
            Assert.That(result.LeagueGamesSimulated, Is.EqualTo(games.Count));
            Assert.That(result.SeasonWins + result.SeasonDraws + result.SeasonLosses, Is.EqualTo(playerGames));
            for (int index = 0; index < season.Teams.Count; index++)
            {
                int teamId = season.Teams[index].TeamId;
                Assert.That(gamesByTeam[teamId], Is.EqualTo(playerGames), $"TeamId {teamId}의 경기 수가 다르다.");
            }

            ManagerRegularSeasonCompletionResult repeated = service.CompleteRegularSeason(runtime);
            Assert.That(repeated.IsCompleted, Is.True);
            Assert.That(repeated.PlayerGamesSimulated, Is.Zero);
            Assert.That(repeated.LeagueGamesSimulated, Is.Zero);
        }

        [Test]
        public void 시즌일괄진행은단일경기반복과같은일정결과를낸다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState batchRuntime, out IHistoricalContentProvider batchProvider);
            CreateRuntime(out ManagerHistoricalRuntimeState stepRuntime, out IHistoricalContentProvider stepProvider);

            new ManagerModeMatchService(batchProvider, BalanceTable.CreateDefault())
                .CompleteRegularSeason(batchRuntime);
            var stepService = new ManagerModeMatchService(stepProvider, BalanceTable.CreateDefault());
            while (stepRuntime.ManagerMode.LiveSeason.NextPlayerGame != null)
                stepService.PlayNextGame(stepRuntime);

            IReadOnlyList<ScheduledGameState> batchGames = batchRuntime.ManagerMode.LiveSeason.Schedule.Games;
            IReadOnlyList<ScheduledGameState> stepGames = stepRuntime.ManagerMode.LiveSeason.Schedule.Games;
            Assert.That(stepGames.Count, Is.EqualTo(batchGames.Count));
            for (int index = 0; index < batchGames.Count; index++)
            {
                Assert.That(stepGames[index].GameId, Is.EqualTo(batchGames[index].GameId));
                Assert.That(stepGames[index].RandomSeed, Is.EqualTo(batchGames[index].RandomSeed));
                Assert.That(stepGames[index].AwayRuns, Is.EqualTo(batchGames[index].AwayRuns));
                Assert.That(stepGames[index].HomeRuns, Is.EqualTo(batchGames[index].HomeRuns));
            }
        }

        [Test]
        public void AI구단대진도같은Seed에서같은결과를낸다()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState first, out IHistoricalContentProvider firstProvider);
            CreateRuntime(out ManagerHistoricalRuntimeState second, out IHistoricalContentProvider secondProvider);

            for (int round = 0; round < 3; round++)
            {
                new ManagerModeMatchService(firstProvider, BalanceTable.CreateDefault()).PlayNextGame(first);
                new ManagerModeMatchService(secondProvider, BalanceTable.CreateDefault()).PlayNextGame(second);
            }

            IReadOnlyList<ScheduledGameState> firstGames = first.ManagerMode.LiveSeason.Schedule.Games;
            IReadOnlyList<ScheduledGameState> secondGames = second.ManagerMode.LiveSeason.Schedule.Games;
            for (int index = 0; index < firstGames.Count; index++)
            {
                Assert.That(secondGames[index].IsCompleted, Is.EqualTo(firstGames[index].IsCompleted));
                Assert.That(secondGames[index].AwayRuns, Is.EqualTo(firstGames[index].AwayRuns));
                Assert.That(secondGames[index].HomeRuns, Is.EqualTo(firstGames[index].HomeRuns));
            }
        }

        private static void CreateRuntime(
            out ManagerHistoricalRuntimeState runtime,
            out IHistoricalContentProvider provider)
        {
            Type fixtureType = typeof(ManagerHistoricalSaveTests).GetNestedType(
                "Fixture",
                BindingFlags.NonPublic);
            MethodInfo create = fixtureType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            object fixture = create.Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
            Type fixtureDataType = fixture.GetType();
            var state = (ManagerHistoricalRuntimeState)fixtureDataType
                .GetProperty("State", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            var adapter = (ManagerHistoricalSaveAdapter)fixtureDataType
                .GetMethod("CreateAdapter", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(fixture, null);
            object rawProvider = fixtureDataType
                .GetProperty("Provider", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            provider = (IHistoricalContentProvider)rawProvider;
            runtime = adapter.Restore(adapter.CreateSaveData(state));
        }
    }
}
