using System;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>역사 우선 선정과 시뮬레이션 보조 평가의 콘텐츠 계약을 검증한다.</summary>
    public sealed class LegendaryPracticeRankingTests
    {
        [Test]
        public void 역사승률을우선하면서비슷한역사팀은시뮬레이션으로구분한다()
        {
            var catalog = LegendaryPracticeTests.Catalog();
            var historic = new LegendaryPracticeTeam { historicalWins = 70, historicalLosses = 30, wins = 500, losses = 500 };
            var simulated = new LegendaryPracticeTeam { historicalWins = 50, historicalLosses = 50, wins = 800, losses = 200 };
            Assert.That(catalog.Compare(historic, simulated), Is.LessThan(0));
            simulated.historicalWins = 69; simulated.historicalLosses = 31;
            Assert.That(catalog.Compare(historic, simulated), Is.GreaterThan(0));
            historic.draws = 200;
            Assert.That(historic.WinRate, Is.EqualTo(0.5));
        }

        [TestCase(0.5)]
        [TestCase(1.0)]
        [TestCase(double.NaN)]
        public void 역사우선과시뮬레이션반영을위반한가중치는거부한다(double weight)
        {
            var catalog = LegendaryPracticeTests.Catalog(); catalog.historicalWinRateWeight = weight;
            catalog.dataHash = catalog.CalculateHash();
            Assert.Throws<InvalidOperationException>(() => catalog.Validate());
        }

        [Test]
        public void 역사원기록변경도콘텐츠해시로검출한다()
        {
            var catalog = LegendaryPracticeTests.Catalog(); catalog.teams[0].historicalWins++;
            Assert.Throws<InvalidOperationException>(() => catalog.Validate());
        }
    }
}
