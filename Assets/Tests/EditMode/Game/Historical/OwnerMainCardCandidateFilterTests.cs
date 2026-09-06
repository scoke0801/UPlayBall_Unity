using Baseball.Core.Players;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>구단주 새 게임 카드 Browser의 복합 필터를 검증한다.</summary>
    public sealed class OwnerMainCardCandidateFilterTests
    {
        [Test]
        public void Matches_연도포지션비용이름을모두적용한다()
        {
            var card = new OwnerNewGameCardView(
                "CARD", "PERSON", "김가람", 2024, 7,
                PlayerType.Batter, PlayerPosition.Shortstop, false);
            var matching = new OwnerMainCardCandidateFilter(2024, PlayerPosition.Shortstop, 7, "가람");

            Assert.That(matching.Matches(card), Is.True);
            Assert.That(new OwnerMainCardCandidateFilter(originYear: 2023).Matches(card), Is.False);
            Assert.That(new OwnerMainCardCandidateFilter(position: PlayerPosition.Catcher).Matches(card), Is.False);
            Assert.That(new OwnerMainCardCandidateFilter(cost: 6).Matches(card), Is.False);
            Assert.That(new OwnerMainCardCandidateFilter(playerName: "다른 이름").Matches(card), Is.False);
        }
    }
}
