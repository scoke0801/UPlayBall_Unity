using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Historical;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    /// <summary>화면 왕복의 재사용과 Runtime 변경 이후의 조회 무효화를 검증한다.</summary>
    public sealed partial class OwnerNavigationRefreshTests
    {
        private GameObject _root;
        private SharedGameShellView _shell;
        private OwnerModeShellCoordinator _coordinator;
        private OwnerModeManager _manager;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("OwnerNavigationRefreshTests", typeof(RectTransform));
            _shell = SharedGameShellView.CreateRuntime(_root.transform);
            _manager = _root.AddComponent<OwnerModeManager>();
            _coordinator = _root.AddComponent<OwnerModeShellCoordinator>();
            BalanceTable balance = BalanceTable.CreateDefault();
            ManagerHistoricalRuntimeState runtime = CreateRuntime(out IHistoricalContentProvider provider);
            typeof(OwnerModeManager).GetProperty("Runtime").SetValue(_manager, runtime);
            SetField(_manager, "_balance", balance);
            SetField(_manager, "_contentProvider", provider);
            SetField(_manager, "_coordinator", new ManagerModeCoordinator(balance));
            SetField(_manager, "_pregameService", new ManagerPregameService(balance, provider));
            SetField(_manager, "_matchService", new ManagerModeMatchService(provider, balance));
            SetField(_manager, "_staffMarketResolver", new StaffMarketResolver());
            SetField(_coordinator, "_shell", _shell);
            SetField(_coordinator, "_manager", _manager);
            Invoke(_coordinator, "EnsureExpansionWorkspace");
            Invoke(_coordinator, "EnsureSharedInformationWorkspace");
            UiGameModeSession.Select(UiGameMode.OwnerCareer);
            _coordinator.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            UiGameModeSession.Clear();
        }

        [Test]
        public void PlayerCards_양구단카드와선수기록표가현재시즌기록을공유한다()
        {
            var factory = new OwnerModeRuntimeSnapshotFactory();
            ManagerHistoricalRuntimeState runtime = _manager.Runtime;
            foreach (ManagerTeamReference team in runtime.ManagerMode.LiveSeason.Teams)
            {
                OwnerTeamLineupSnapshot before = factory.CreateTeamLineup(_manager, team.TeamSeasonKey);
                foreach (OwnerCollectionCardSnapshot card in before.PitcherDetails)
                    if (card != null) Assert.That(card.SeasonRecord, Is.Empty);
            }
            ((ManagerModeMatchService)GetField(_manager, "_matchService")).PlayNextGame(runtime);
            int checkedPitchers = 0;
            foreach (ManagerTeamReference team in runtime.ManagerMode.LiveSeason.Teams)
            {
                OwnerTeamLineupSnapshot snapshot = factory.CreateTeamLineup(_manager, team.TeamSeasonKey);
                foreach (OwnerCollectionCardSnapshot card in snapshot.PitcherDetails)
                {
                    if (card == null) continue;
                    PlayerCompetitionStatisticsState record = OwnerSeasonRecordsService.GetCurrentPlayerRecord(
                        runtime, team.TeamSeasonKey, card.PlayerSeasonId);
                    if (record == null || record.Pitching.Appearances == 0)
                    {
                        Assert.That(card.SeasonRecord, Is.Empty);
                        continue;
                    }
                    checkedPitchers++;
                    var metrics = new[] { CareerRecordMetric.OutsRecorded, CareerRecordMetric.EarnedRunAverage,
                        CareerRecordMetric.PitchingStrikeouts };
                    for (int index = 0; index < metrics.Length; index++)
                        Assert.That(card.SeasonRecord[index].Value, Is.EqualTo(
                            Baseball.Presentation.SharedScreens.CareerSharedSnapshotFormatters.FormatMetricValue(
                                metrics[index], LeagueLeaderboardService.GetMetricValue(record, metrics[index]))));
                }
            }
            Assert.That(checkedPitchers, Is.GreaterThan(1));
        }

        [Test]
        public void Study_직접진입과상태갱신은선택선수의상세와과정만조회한다()
        {
            var watch = Stopwatch.StartNew();
            Navigate(OwnerNavigationRoutes.PowerUpStudy);
            watch.Stop();
            TestContext.WriteLine($"유학 직접 최초 진입: {watch.Elapsed.TotalMilliseconds:F2} ms");
            object expansion = GetField(_coordinator, "_expansionWorkspace");
            object view = GetField(expansion, "_growthView");
            var snapshot = (OwnerGrowthSnapshot)GetField(view, "_snapshot");
            Assert.That(snapshot.Cards[0].Card.IsActiveRoster, Is.True);
            int studyQueries = 0, detailQueries = 0;
            foreach (OwnerGrowthCardSnapshot card in snapshot.Cards)
            {
                if (((Lazy<OwnerStudyOption[]>)GetField(card, "_studies")).IsValueCreated) studyQueries++;
                if (((Lazy<OwnerCollectionCardSnapshot>)GetField(card, "_detailCard")).IsValueCreated) detailQueries++;
            }
            Assert.That(studyQueries, Is.EqualTo(1));
            Assert.That(detailQueries, Is.EqualTo(1));
            Assert.That(snapshot.Cards[0].Studies[0].CanStart, Is.False);
            Navigate(OwnerNavigationRoutes.Home);
            Navigate(OwnerNavigationRoutes.PowerUpStudy);
            Assert.That(GetField(view, "_snapshot"), Is.SameAs(snapshot));
            Invoke(_coordinator, "HandleRuntimeChanged");
            Assert.That(GetField(view, "_snapshot"), Is.Not.SameAs(snapshot));
        }

        [Test]
        public void ClubSummary_상세조회없이구단집계값을보존한다()
        {
            var factory = new OwnerModeRuntimeSnapshotFactory();
            var watch = Stopwatch.StartNew();
            OwnerCollectionSnapshot full = factory.CreateCollection(_manager);
            watch.Stop();
            double fullMilliseconds = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            OwnerCollectionSnapshot summary = factory.CreateCollectionSummary(_manager);
            watch.Stop();
            TestContext.WriteLine($"구단 카드 {full.Cards.Count}장: 상세 {fullMilliseconds:F2} ms / 요약 {watch.Elapsed.TotalMilliseconds:F2} ms");
            Assert.That(summary.Cards.Count, Is.EqualTo(full.Cards.Count));
            for (int index = 0; index < full.Cards.Count; index++)
            {
                Assert.That(summary.Cards[index].CardId, Is.EqualTo(full.Cards[index].CardId));
                Assert.That(summary.Cards[index].Edition, Is.EqualTo(full.Cards[index].Edition));
                Assert.That(summary.Cards[index].GetAbility(PlayerAbility.Contact), Is.Null);
            }
        }

        [Test]
        public void PowerUp_진입시선택상품의확률만조회하고왕복시재사용한다()
        {
            var watch = Stopwatch.StartNew();
            Navigate(OwnerNavigationRoutes.PowerUpScout);
            watch.Stop();
            object shop = GetField(_coordinator, "_shopService");
            object expansion = GetField(_coordinator, "_expansionWorkspace");
            object view = GetField(expansion, "_powerUpView");
            Assert.That(((RectTransform)GetField(view, "_trainingCardList")).childCount, Is.Zero);
            Assert.That(((RectTransform)GetField(view, "_enhancementCardList")).childCount, Is.Zero);
            var details = (System.Collections.IDictionary)GetField(shop, "_detailsByProductId");
            Assert.That(details.Count, Is.EqualTo(1));
            TestContext.WriteLine($"전력보강 최초 진입: {watch.Elapsed.TotalMilliseconds:F2} ms");
            Navigate(OwnerNavigationRoutes.Home);
            Navigate(OwnerNavigationRoutes.PowerUpScout);
            Assert.That(GetField(_coordinator, "_shopService"), Is.SameAs(shop));
            Assert.That(details.Count, Is.EqualTo(1));
            Invoke(_coordinator, "HandleRuntimeChanged");
            object refreshed = GetField(_coordinator, "_shopService");
            Assert.That(refreshed, Is.Not.SameAs(shop));
            Assert.That(((System.Collections.IDictionary)GetField(refreshed, "_detailsByProductId")).Count,
                Is.EqualTo(1));
        }

        [Test]
        public void Home_미방문화면을생성하지않고왕복시기존화면을재사용한다()
        {
            object expansion = GetField(_coordinator, "_expansionWorkspace");
            bool wasRosterCreatedAtHome = GetField(expansion, "_rosterLineupView") != null;
            Navigate(OwnerNavigationRoutes.RosterLineup);
            var view = (UI_Scene_OwnerRosterLineup)GetField(expansion, "_rosterLineupView");
            Assert.That(view, Is.Not.Null);
            var workspace = (RectTransform)GetField(view, "_workspaceRoot");
            Transform[] before = workspace.GetComponentsInChildren<Transform>(true);
            var watch = Stopwatch.StartNew();
            for (int index = 0; index < 10; index++)
            {
                Navigate(OwnerNavigationRoutes.Home);
                Navigate(OwnerNavigationRoutes.RosterLineup);
            }
            watch.Stop();
            TestContext.WriteLine($"홈↔선수단 10회 왕복: {watch.Elapsed.TotalMilliseconds:F2} ms");
            Assert.That(wasRosterCreatedAtHome, Is.False);
            Assert.That(GetField(expansion, "_rosterPitchingView"), Is.Null);
            CollectionAssert.AreEqual(before, workspace.GetComponentsInChildren<Transform>(true));
            Assert.That(GetField(expansion, "_growthView"), Is.Null);
        }

        [Test]
        public void RuntimeChanged_현재화면만갱신하고다른화면은다음진입시갱신한다()
        {
            Navigate(OwnerNavigationRoutes.RosterLineup);
            object expansion = GetField(_coordinator, "_expansionWorkspace");
            object before = GetField(expansion, "_rosterLineupModel");
            Navigate(OwnerNavigationRoutes.Home);
            Invoke(_coordinator, "HandleRuntimeChanged");
            Assert.That(GetField(expansion, "_rosterLineupModel"), Is.SameAs(before));
            Navigate(OwnerNavigationRoutes.RosterLineup);
            Assert.That(GetField(expansion, "_rosterLineupModel"), Is.Not.SameAs(before));
            before = GetField(expansion, "_rosterLineupModel");
            Invoke(_coordinator, "HandleRuntimeChanged");
            Assert.That(GetField(expansion, "_rosterLineupModel"), Is.Not.SameAs(before));
        }

        [Test]
        public void Navigation_전체업무Route를열고뒤로가기와재갱신을유지한다()
        {
            string[] routes =
            {
                OwnerNavigationRoutes.RosterLineup, OwnerNavigationRoutes.RosterTeamColor,
                OwnerNavigationRoutes.RosterTacticCards, OwnerNavigationRoutes.RosterSupportCards,
                OwnerNavigationRoutes.PowerUpScout, OwnerNavigationRoutes.PowerUpTraining,
                OwnerNavigationRoutes.PowerUpEnhancementSale, OwnerNavigationRoutes.PowerUpSkills,
                OwnerNavigationRoutes.PowerUpStudy, OwnerNavigationRoutes.DugoutLineupNotes,
                OwnerNavigationRoutes.DugoutManagerPolicy, OwnerManagementRoutes.ClubFinance,
                OwnerManagementRoutes.ClubFacility, OwnerNavigationRoutes.ClubOwner,
                OwnerNavigationRoutes.ClubInformation, OwnerNavigationRoutes.ClubContract,
                OwnerNavigationRoutes.LeagueStandings,
                OwnerNavigationRoutes.LeagueTeamResults, OwnerNavigationRoutes.LeagueMatchups,
                OwnerNavigationRoutes.LeagueRankHistory,
                OwnerSharedInformationWorkspaceCoordinator.ScheduleRouteId,
                OwnerSharedInformationWorkspaceCoordinator.SeasonRecordsRouteId,
                OwnerSharedInformationWorkspaceCoordinator.RecordsRouteId,
                OwnerExpansionWorkspaceCoordinator.StaffOfficeRouteId,
                OwnerNavigationRoutes.Shop, OwnerNavigationRoutes.MatchCenterAnalysis,
                OwnerNavigationRoutes.MatchCenterOpponentLineup
            };
            foreach (string route in routes)
            {
                var watch = Stopwatch.StartNew();
                Navigate(route);
                watch.Stop();
                Assert.That(GetField(_coordinator, "_navigationState") is GameModeNavigationState state
                    ? state.ActiveRouteId : null, Is.EqualTo(route), route);
                TestContext.WriteLine($"{route}: {watch.Elapsed.TotalMilliseconds:F2} ms");
            }
            _coordinator.Refresh();
            Invoke(_coordinator, "HandleBackRequested");
        }

        [Test]
        public void OpponentLineup_자동선택한TeamColor를상세능력치에표시한다()
        {
            var factory = new OwnerModeRuntimeSnapshotFactory();
            OwnerTeamLineupSnapshot snapshot = factory.CreateTeamLineup(_manager, "TEAM-01");

            Assert.That(snapshot.TeamColors[0], Is.Not.EqualTo("팀컬러 적용 없음"));
            OwnerCollectionCardSnapshot card = snapshot.HitterDetails[0];
            int totalTeamColorBonus = 0;
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
            {
                var ability = (PlayerAbility)index;
                OwnerAbilityBreakdownSnapshot breakdown = card.GetAbilityBreakdown(ability).Value;
                totalTeamColorBonus += breakdown.TeamColor;
                Assert.That(card.GetEffectiveAbility(ability),
                    Is.EqualTo(Math.Min(card.AbilityGraphMaximum, breakdown.Total)));
            }

            Assert.That(totalTeamColorBonus, Is.GreaterThan(0));
        }

        [Test]
        public void OwnTeamLineup_장착한TeamColor의카드정보를전용Skin에전달한다()
        {
            var provider = (IHistoricalContentProvider)GetField(_manager, "_contentProvider");
            Invoke(
                _manager,
                "ConfigureTeamColors",
                provider.Load(),
                _manager.Runtime.PlayerTeamSeasonKey);
            IReadOnlyList<TeamColorDefinition> available = _manager.GetAvailableTeamColors();
            Assert.That(available, Is.Not.Empty);
            _manager.ConfigureSelectedPresetTeamColors(new[] { available[0].TeamColorId, null });

            var factory = new OwnerModeRuntimeSnapshotFactory();
            OwnerTeamLineupSnapshot snapshot = factory.CreateTeamLineup(
                _manager,
                _manager.Runtime.PlayerTeamSeasonKey);

            Assert.That(snapshot.TeamColorCards[0], Is.Not.Null);
            Assert.That(snapshot.TeamColorCards[0].Id, Is.EqualTo(available[0].TeamColorId));
            Assert.That(snapshot.TeamColors[0], Is.EqualTo(snapshot.TeamColorCards[0].Name));
            Assert.That(snapshot.TeamColorCards[0].Grade, Is.Not.Empty);
            Assert.That(snapshot.TeamColorCards[0].EligibleCount,
                Is.GreaterThanOrEqualTo(snapshot.TeamColorCards[0].Definition.RequiredCount));
        }

        [Test]
        public void Contract_연장직후목록과상세에증가한잔여기간을표시한다()
        {
            Navigate(OwnerNavigationRoutes.ClubContract);
            object expansion = GetField(_coordinator, "_expansionWorkspace");
            var before = (OwnerContractSnapshot)GetField(expansion, "_contractSnapshot");
            string cardId = before.SelectedCardId;
            int remaining = _manager.Runtime.ManagerMode.GetPlayerContract(cardId).RemainingSeasons;

            Invoke(_coordinator, "HandleContractRenewalRequested", cardId, 1);

            var after = (OwnerContractSnapshot)GetField(expansion, "_contractSnapshot");
            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after.SelectedCardId, Is.EqualTo(cardId));
            var row = new List<OwnerContractPlayerRow>(after.Players).Find(player => player.CardId == cardId);
            Assert.That(row.RemainingSeasons, Is.EqualTo(remaining + 1));
            var view = (UI_Scene_OwnerPlayerMarket)GetField(expansion, "_playerMarketView");
            var labels = (List<UnityEngine.UI.Text>)GetField(view, "_leftLabels");
            Assert.That(labels.Exists(label => label.text.Contains($"잔여 {remaining + 1}년")), Is.True);
        }

        private void Navigate(string route) => Invoke(_coordinator, "HandleNavigationRequested", route);
        private static object GetField(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void SetField(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private static ManagerHistoricalRuntimeState CreateRuntime(out IHistoricalContentProvider provider)
        {
            var persons = new List<PlayerPersonDefinition>();
            var seasons = new List<PlayerSeasonDefinition>();
            var cards = new List<PlayerCardDefinition>();
            var teams = new List<TeamSeasonDefinition>();
            var rosters = new List<CurrentRosterState>();
            var owned = new List<OwnedPlayerCardState>();
            var teamKeys = new string[10];
            for (int teamIndex = 0; teamIndex < teamKeys.Length; teamIndex++)
            {
                string teamKey = teamKeys[teamIndex] = $"TEAM-{teamIndex:00}";
                var entries = new List<ActiveRosterEntry>();
                var teamCards = new string[25];
                for (int index = 0; index < 25; index++)
                {
                    string id = $"{teamIndex:00}-{index:00}";
                    ActiveRosterRole role = index < 9 ? (ActiveRosterRole)index
                        : index < 14 ? ActiveRosterRole.BenchHitter : (ActiveRosterRole)(index - 4);
                    var rule = ActiveRosterCompositionRule.Standard;
                    bool isPitcher = rule.IsPitcherRole(role);
                    PlayerPosition position = rule.IsStartingHitterRole(role) ? rule.GetAssignedPosition(role)
                        : !isPitcher ? PlayerPosition.Catcher : rule.IsStartingPitcherRole(role)
                            ? PlayerPosition.StartingPitcher : PlayerPosition.ReliefPitcher;
                    persons.Add(new PlayerPersonDefinition(id, 1998, Handedness.Right, Handedness.Right,
                        position, RegistrationType.Domestic, 2020, 2035,
                        new PersonPotentialTrait(new int[PlayerAbilityCatalog.AbilityCount])));
                    seasons.Add(new PlayerSeasonDefinition(id, id, 2024, $"FRANCHISE-{teamIndex:00}", teamKey,
                        position, isPitcher ? rule.GetAssignedPitcherRole(role) : PitcherRole.Starter,
                        isPitcher ? PlayerType.Pitcher : PlayerType.Batter, RegistrationType.Domestic,
                        new AbilityRatings(50), 5, new AbilityRatings(60)));
                    string cardId = teamCards[index] = PlayerCardDefinition.CreateStableCardId(id, PlayerCardEdition.Normal);
                    cards.Add(new PlayerCardDefinition(cardId, id, PlayerCardEdition.Normal,
                        new int[PlayerAbilityCatalog.AbilityCount]));
                    entries.Add(new ActiveRosterEntry(cardId, id, id, RegistrationType.Domestic, role));
                    if (teamIndex == 0) owned.Add(new OwnedPlayerCardState(cardId));
                }
                teams.Add(new TeamSeasonDefinition(teamKey, $"FRANCHISE-{teamIndex:00}", 2024, teamCards, teamCards, 50d));
                rosters.Add(new CurrentRosterState(teamKey, entries));
            }
            var manifest = new HistoricalContentManifest(1, 1, "ui-test",
                new HistoricalSourceContentManifest("ui-test", "ui-test", "ui-test", 77123UL, "ui-test"));
            var year = new HistoricalYearContentDefinition(2024, seasons, cards, teams,
                Array.Empty<OriginalSeasonRecordDefinition>(), Array.Empty<OriginalAwardRecordDefinition>());
            var content = new HistoricalBakedContent(manifest, persons, new[] { year });
            content = new HistoricalBakedContent(manifest, persons, new[] { year }, new WorldIdentityNameCatalog(
                content.IdentityNameCatalog.DomesticPlayerNames, content.IdentityNameCatalog.ForeignPlayerNames,
                content.IdentityNameCatalog.FranchiseNames));
            provider = new ContentProvider(content);
            var awards = new WorldAwardRecord(Array.Empty<WorldAwardEntry>());
            var history = new WorldHistorySnapshot(WorldRecordMode.SimulatedHistory, 77123UL,
                Array.Empty<SeasonStatistics>(), awards);
            var identities = new WorldIdentityGenerator().Generate(persons, teams, content.IdentityNameCatalog, 77123UL);
            var catalog = WorldCardCatalogBuilder.Build(seasons, awards, CardEditionBalanceTable.CreateInitial());
            var starterCards = new List<string>();
            foreach (var card in owned) starterCards.Add(card.CardId);
            var runtime = new ManagerHistoricalRuntimeState(teamKeys[0], HistoricalContentReference.FromManifest(manifest),
                identities, history, catalog, new LeagueInstance("TEST", LeagueGrade.Rookie, teamKeys,
                    Array.Empty<SpecialCompositeTeamRegistration>()), rosters, owned, new ManagerEconomyState(100000000L, 10000, 3000),
                newGameReceipt: new OwnerNewGameReceipt(starterCards, Array.Empty<string>(), 0, 77123UL));
            var adapter = new ManagerHistoricalSaveAdapter(provider, CardEditionBalanceTable.CreateInitial());
            return adapter.Restore(adapter.CreateSaveData(runtime));
        }

        private sealed class ContentProvider : IHistoricalContentProvider
        {
            private readonly HistoricalBakedContent _content;
            public ContentProvider(HistoricalBakedContent content) => _content = content;
            public HistoricalBakedContent Load() => _content;
        }
    }
}
