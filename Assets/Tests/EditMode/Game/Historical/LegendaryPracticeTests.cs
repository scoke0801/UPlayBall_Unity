using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class LegendaryPracticeTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void 부포지션백업포수와역사투구용량을모든선발편성에반영한다(bool hasHistoricalUsage)
        {
            var persons = new List<PlayerPersonDefinition>();
            var seasons = new List<PlayerSeasonDefinition>();
            var cards = new List<PlayerCardDefinition>();
            for (int i = 0; i < 25; i++)
            {
                var position = i < 9 ? (PlayerPosition)(i + 1) : i < 14 ? PlayerPosition.DesignatedHitter :
                    i < 19 ? PlayerPosition.StartingPitcher : PlayerPosition.ReliefPitcher;
                var season = CreateSeason(i, position, i == 9, hasHistoricalUsage && i >= 14);
                seasons.Add(season);
                persons.Add(new PlayerPersonDefinition(season.PlayerPersonId, 1980, Handedness.Right, Handedness.Right,
                    position, RegistrationType.Domestic, 2000, 2000, new PersonPotentialTrait(new int[12])));
                cards.Add(new PlayerCardDefinition(season.PlayerSeasonId + ":Normal", season.PlayerSeasonId,
                    PlayerCardEdition.Normal, new int[12]));
            }
            var ids = cards.Select(c => c.CardId).ToArray();
            var team = new TeamSeasonDefinition("team", "franchise", 2000, ids, ids, 50);
            var manifest = new HistoricalContentManifest(1, 1, "archive",
                new HistoricalSourceContentManifest("reference", "generator", "balance", 1, "content"));
            var year = new HistoricalYearContentDefinition(2000, seasons, cards, new[] { team },
                Array.Empty<OriginalSeasonRecordDefinition>(), Array.Empty<OriginalAwardRecordDefinition>());
            var content = new HistoricalBakedContent(manifest, persons, new[] { year });
            var identities = new WorldIdentityRegistry("test", 1,
                persons.Select((p, i) => new WorldPlayerIdentity(p.PlayerPersonId, "선수" + (char)('가' + i))).ToArray(),
                new[] { new WorldFranchiseIdentity("franchise", "검증구단") });
            var builder = new LegendaryPracticeRosterBuilder(content, BalanceTable.CreateDefault());
            var snapshots = builder.Build(team, identities, 1, 100, out _);
            Assert.That(snapshots.Length, Is.EqualTo(5));
            foreach (var snapshot in snapshots)
            {
                Assert.That(snapshot.Bench.Any(p => p.GetPositionProficiency(PlayerPosition.Catcher) == 100), Is.True);
                Assert.That(snapshot.StartingPitcher.CapacityMultiplier, Is.EqualTo(hasHistoricalUsage ? 1.5d : 1d));
                Assert.That(snapshot.StartingPitcher.RecoveryMultiplier, Is.EqualTo(hasHistoricalUsage ? 1.25d : 1d));
                foreach (var pitcher in snapshot.Bullpen)
                {
                    Assert.That(pitcher.CapacityMultiplier, Is.EqualTo(hasHistoricalUsage ? 2.5d : 1d));
                    Assert.That(pitcher.RecoveryMultiplier, Is.EqualTo(hasHistoricalUsage ? 2.5d : 1d));
                }
            }
            CollectionAssert.AreEqual(ids, builder.SelectCards(team).Select(c => c.CardId));
        }

        [Test]
        public void 특수카드보강은주포지션이같아도백업포수자격을잃지않는다()
        {
            var method = typeof(LegendaryPracticeRosterBuilder).GetMethod("IsSlotCompatible", BindingFlags.Static | BindingFlags.NonPublic);
            var catcher = CreateSeason(1, PlayerPosition.DesignatedHitter, true);
            var hitter = CreateSeason(2, PlayerPosition.DesignatedHitter, false);
            Assert.That(method.Invoke(null, new object[] { 9, catcher, hitter }), Is.False);
            Assert.That(method.Invoke(null, new object[] { 9, catcher, CreateSeason(3, PlayerPosition.DesignatedHitter, true) }), Is.True);
        }

        [Test]
        public void 백위밖역사팀누락도정본대조에서거부한다()
        {
            var catalog = Catalog();
            var cards = Enumerable.Range(0, 25).Select(i => "card" + i).ToArray();
            var expected = catalog.teams.Select(t => new TeamSeasonDefinition(t.teamSeasonKey, "franchise", t.year, cards, cards, 50)).ToList();
            Assert.DoesNotThrow(() => catalog.ValidateCoverage(expected));
            expected.Add(new TeamSeasonDefinition("missing", "franchise", 2000, cards, cards, 50));
            Assert.Throws<InvalidOperationException>(() => catalog.ValidateCoverage(expected));
            catalog.candidateTeamSeasonKeys = catalog.candidateTeamSeasonKeys.Concat(new[] { "unexpected" }).ToArray();
            catalog.candidateCount++;
            catalog.dataHash = catalog.CalculateHash();
            Assert.Throws<InvalidOperationException>(() => catalog.ValidateCoverage(expected));
        }

        private static PlayerSeasonDefinition CreateSeason(int index, PlayerPosition position, bool hasSecondaryCatcher,
            bool hasHistoricalUsage = false) =>
            new PlayerSeasonDefinition("season" + index, "person" + index, 2000, "franchise", "team", position,
                index < 19 ? PitcherRole.Starter : PitcherRole.MiddleRelief,
                position >= PlayerPosition.StartingPitcher ? PlayerType.Pitcher : PlayerType.Batter,
                RegistrationType.Domestic, new AbilityRatings(50), 5, new AbilityRatings(70),
                secondaryPositions: hasSecondaryCatcher ? new[] { new PositionProficiency(PlayerPosition.Catcher, 100) } : null,
                historicalPitchingAppearances: hasHistoricalUsage ? 20 : 0,
                historicalPitchingOuts: hasHistoricalUsage ? 540 : 0,
                historicalTeamGames: hasHistoricalUsage ? 120 : 0);

        public static LegendaryPracticeCatalog Catalog()
        {
            var result = new LegendaryPracticeCatalog { simulationVersion = "test-v1", contentHash = "test",
                historicalWinRateWeight = 0.75, gamesPerCandidate = 1000, candidateCount = 100, teams = new LegendaryPracticeTeam[100] };
            for (int i = 0; i < 100; i++) result.teams[i] = new LegendaryPracticeTeam { rank = i + 1,
                challengeTeamId = "team" + (i + 1).ToString("D3"), teamSeasonKey = "season" + i, year = 1982 + i % 44,
                historicalWins = 50, historicalLosses = 50,
                wins = 500, losses = 500, rosterHash = "test", rewardMoney = 10000, rewardDevelopment = 100, rewardScouting = 10 };
            result.candidateTeamSeasonKeys = result.teams.Select(t => t.teamSeasonKey).ToArray();
            result.dataHash = result.CalculateHash(); result.Validate(); return result;
        }

        [Test]
        public void 누적300승완주는저장복원과보상재시도에도중복지급하지않는다()
        {
            var catalog = Catalog(); var state = new LegendaryPracticeState(); var wallet = new ManagerEconomyState();
            for (int rank = 100; rank >= 1; rank--)
            {
                var team = catalog.teams[rank - 1]; Assert.That(state.CanPlay(catalog, team), Is.True);
                for (int attempt = 1; attempt <= 5; attempt++)
                {
                    bool win = attempt > 2;
                    Assert.That(state.Commit(catalog, team.challengeTeamId, attempt, win ? 4 : 0, attempt == 1 ? 2 : 0, 1), Is.True);
                    Assert.That(state.Commit(catalog, team.challengeTeamId, attempt, 99, 0, 1), Is.False);
                    state = LegendaryPracticeState.Restore(state.Capture());
                    Assert.That(state.Get(team.challengeTeamId).NextStarterIndex, Is.EqualTo(attempt % 5));
                }
                Assert.That(state.Get(team.challengeTeamId).wins, Is.EqualTo(3));
                Assert.That(state.Claim(catalog, team.challengeTeamId, wallet), Is.True);
                state = LegendaryPracticeState.Restore(state.Capture());
                Assert.That(state.Claim(catalog, team.challengeTeamId, wallet), Is.False);
                state.Commit(catalog, team.challengeTeamId, 6, 1, 0, 1);
                Assert.That(state.Get(team.challengeTeamId).wins, Is.EqualTo(3));
                Assert.That(state.Claim(catalog, team.challengeTeamId, wallet), Is.False);
            }
            Assert.That(wallet.Money, Is.EqualTo(1000000));
            Assert.That(wallet.DevelopmentPoints, Is.EqualTo(10000)); Assert.That(wallet.ScoutingPoints, Is.EqualTo(1000));
            Assert.That(state.Capture().Sum(p => p.wins), Is.EqualTo(300));
        }

        [Test]
        public void 잠금과잘못된시도번호및오염된저장은거부한다()
        {
            var catalog = Catalog(); var state = new LegendaryPracticeState();
            Assert.Throws<InvalidOperationException>(() => state.Commit(catalog, "team001", 1, 1, 0, 1));
            Assert.Throws<InvalidOperationException>(() => state.Commit(catalog, "team100", 2, 1, 0, 1));
            Assert.Throws<ArgumentException>(() => LegendaryPracticeState.Restore(new[] {
                new LegendaryPracticeProgress { teamId = "team100", wins = 2, attempts = 2, rewardClaimed = true } }));
            catalog.teams[0].rewardMoney++;
            Assert.Throws<InvalidOperationException>(() => catalog.Validate());
        }

        [Test]
        public void 지급한도를초과하면원장과모든재화를변경하지않는다()
        {
            var catalog = Catalog(); var state = new LegendaryPracticeState();
            for (int i = 1; i <= 3; i++) state.Commit(catalog, "team100", i, 1, 0, 1);
            var wallet = new ManagerEconomyState(0, int.MaxValue, 0);
            Assert.Throws<OverflowException>(() => state.Claim(catalog, "team100", wallet));
            Assert.That(wallet.Money, Is.Zero); Assert.That(wallet.DevelopmentPoints, Is.Zero);
            Assert.That(state.Get("team100").rewardClaimed, Is.False);
        }
    }
}
