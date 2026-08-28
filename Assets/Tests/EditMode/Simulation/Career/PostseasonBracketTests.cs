using Baseball.Simulation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Career
{
    /// <summary>
    /// 정규 시즌 순위에서 포스트시즌 시드를 뽑는 규칙이 완전 순서를 이루는지 검증한다.
    /// </summary>
    public sealed class PostseasonBracketTests
    {
        [Test]
        public void SelectSeeds_승률순으로상위4팀을고른다()
        {
            var standings = new[]
            {
                new TeamStandingEntry(1, 40, 40, 400, 400),
                new TeamStandingEntry(2, 60, 20, 500, 300),
                new TeamStandingEntry(3, 50, 30, 450, 380),
                new TeamStandingEntry(4, 55, 25, 470, 350),
                new TeamStandingEntry(5, 20, 60, 300, 500),
                new TeamStandingEntry(6, 45, 35, 420, 400)
            };

            int[] seeds = PostseasonBracket.SelectSeeds(standings, 4);

            Assert.That(seeds, Is.EqualTo(new[] { 2, 4, 3, 6 }));
        }

        [Test]
        public void SelectSeeds_승률이같으면득실차로가르고최후에TeamId로끊는다()
        {
            var sameRecord = new[]
            {
                new TeamStandingEntry(7, 40, 40, 400, 300),
                new TeamStandingEntry(3, 40, 40, 400, 300),
                new TeamStandingEntry(5, 40, 40, 400, 450),
                new TeamStandingEntry(9, 40, 40, 400, 350)
            };

            int[] seeds = PostseasonBracket.SelectSeeds(sameRecord, 4);

            // 득실차 +100 두 팀이 TeamId 오름차순으로 앞서고, 그다음 +50, 마지막이 -50이다.
            Assert.That(seeds, Is.EqualTo(new[] { 3, 7, 9, 5 }));
        }

        [Test]
        public void GetWinsRequired_홀수시리즈의과반승수를반환한다()
        {
            Assert.That(PostseasonBracket.GetWinsRequired(3), Is.EqualTo(2));
            Assert.That(PostseasonBracket.GetWinsRequired(5), Is.EqualTo(3));
            Assert.That(PostseasonBracket.GetWinsRequired(7), Is.EqualTo(4));
        }

        [Test]
        public void IsHigherSeedHome_상위시드가홀수경기를홈에서치른다()
        {
            Assert.That(PostseasonBracket.IsHigherSeedHome(1), Is.True);
            Assert.That(PostseasonBracket.IsHigherSeedHome(2), Is.False);
            Assert.That(PostseasonBracket.IsHigherSeedHome(3), Is.True);
        }

        [Test]
        public void GetHigherSeedIndex_계단식대진에서기다리던시드를반환한다()
        {
            Assert.That(PostseasonBracket.GetHigherSeedIndex(PostseasonRound.WildCard), Is.EqualTo(2));
            Assert.That(PostseasonBracket.GetHigherSeedIndex(PostseasonRound.Playoff), Is.EqualTo(1));
            Assert.That(
                PostseasonBracket.GetHigherSeedIndex(PostseasonRound.ChampionshipSeries),
                Is.EqualTo(0));
        }
    }
}
