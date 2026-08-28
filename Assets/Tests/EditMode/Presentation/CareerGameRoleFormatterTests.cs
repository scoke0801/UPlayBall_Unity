using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Presentation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation
{
    /// <summary>
    /// 투수 비등판일이 야수 벤치와 구분되어 표시되는지 검증한다.
    /// </summary>
    public sealed class CareerGameRoleFormatterTests
    {
        [TestCase(PlayerGameRole.PitcherRest, PlayerPosition.StartingPitcher, "로테이션 휴식")]
        [TestCase(PlayerGameRole.PitcherRest, PlayerPosition.ReliefPitcher, "불펜 휴식")]
        [TestCase(PlayerGameRole.Bench, PlayerPosition.StartingPitcher, "로테이션 휴식")]
        public void PitcherRest_투수보직에맞는휴식문구를반환한다(
            PlayerGameRole role,
            PlayerPosition position,
            string expectedLabel)
        {
            Assert.That(CareerGameRoleFormatter.IsPitcherRest(role, position), Is.True);
            Assert.That(CareerGameRoleFormatter.GetPitcherRestLabel(position), Is.EqualTo(expectedLabel));
        }

        [Test]
        public void Bench_야수는투수휴식으로판정하지않는다()
        {
            Assert.That(
                CareerGameRoleFormatter.IsPitcherRest(PlayerGameRole.Bench, PlayerPosition.Shortstop),
                Is.False);
        }
    }
}
