using System;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Core
{
    /// <summary>
    /// Player·Lineup·Team 모델이 유효한 경기 입력만 허용하는지 검증한다.
    /// </summary>
    public sealed class PlayerTeamModelTests
    {
        [Test]
        public void Lineup_아홉포지션과선수를중복없이보관한다()
        {
            Lineup lineup = CreateLineup(100);

            Assert.That(lineup.Count, Is.EqualTo(9));
            Assert.That(lineup[0].FieldingPosition, Is.EqualTo(PlayerPosition.Catcher));
            Assert.That(lineup[8].FieldingPosition, Is.EqualTo(PlayerPosition.DesignatedHitter));
        }

        [Test]
        public void Lineup_중복수비포지션을거부한다()
        {
            LineupSlot[] slots = CreateSlots(100);
            slots[1] = new LineupSlot(slots[1].Player, PlayerPosition.Catcher);

            Assert.Throws<ArgumentException>(() => new Lineup(slots));
        }

        [Test]
        public void Lineup_중복선수를거부한다()
        {
            LineupSlot[] slots = CreateSlots(100);
            slots[1] = new LineupSlot(slots[0].Player, PlayerPosition.FirstBase);

            Assert.Throws<ArgumentException>(() => new Lineup(slots));
        }

        [Test]
        public void Team_선발투수와타순선수의중복을거부한다()
        {
            Lineup lineup = CreateLineup(100);

            Assert.Throws<ArgumentException>(() => new Team(1, "테스트", lineup, lineup[0].Player));
        }

        [Test]
        public void CalculateDefenseRating_포지션적응도를반영한다()
        {
            LineupSlot[] slots = CreateSlots(200);
            Player original = slots[0].Player;
            var emergencyCatcher = new Player(
                999,
                "비상 포수",
                PlayerPosition.FirstBase,
                Handedness.Right,
                Handedness.Right,
                original.BatterAttributes,
                original.PitcherAttributes);
            slots[0] = new LineupSlot(emergencyCatcher, PlayerPosition.Catcher);

            var lineup = new Lineup(slots);

            Assert.That(lineup.CalculateDefenseRating(), Is.LessThan(50d));
        }

        private static Lineup CreateLineup(int playerIdBase)
        {
            return new Lineup(CreateSlots(playerIdBase));
        }

        private static LineupSlot[] CreateSlots(int playerIdBase)
        {
            var slots = new LineupSlot[9];
            var batterAttributes = new BatterAttributes(50, 50, 50, 50, 50, 50);
            var pitcherAttributes = new PitcherAttributes(20, 20, 20, 20, 20, 20);

            for (int index = 0; index < slots.Length; index++)
            {
                PlayerPosition position = (PlayerPosition)(index + 1);
                var player = new Player(
                    playerIdBase + index,
                    $"선수 {index + 1}",
                    position,
                    Handedness.Right,
                    Handedness.Right,
                    batterAttributes,
                    pitcherAttributes);
                slots[index] = new LineupSlot(player, position);
            }

            return slots;
        }
    }
}
