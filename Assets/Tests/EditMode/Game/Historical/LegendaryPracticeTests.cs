using System;
using System.Linq;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class LegendaryPracticeTests
    {
        public static LegendaryPracticeCatalog Catalog()
        {
            var result = new LegendaryPracticeCatalog { simulationVersion = "test-v1", contentHash = "test",
                gamesPerCandidate = 1000, candidateCount = 100, teams = new LegendaryPracticeTeam[100] };
            for (int i = 0; i < 100; i++) result.teams[i] = new LegendaryPracticeTeam { rank = i + 1,
                challengeTeamId = "team" + (i + 1).ToString("D3"), teamSeasonKey = "season" + i, year = 1982 + i % 44,
                wins = 500, losses = 500, rosterHash = "test", rewardMoney = 10000, rewardDevelopment = 100, rewardScouting = 10 };
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
