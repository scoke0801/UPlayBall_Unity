using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation.Career
{
    /// <summary>
    /// 감독 타순 AI의 역할별 선발과 결정론을 검증한다.
    /// </summary>
    public sealed class ManagerLineupAiTests
    {
        [Test]
        public void BuildLineup_출루형은1번_장타형은4번에배치한다()
        {
            LineupSlot[] candidates = CreateCandidates();
            candidates[0] = CreateSlot(101, PlayerPosition.Catcher, 90, 30, 95, 85);
            candidates[1] = CreateSlot(102, PlayerPosition.FirstBase, 70, 100, 30, 70);
            var ai = new ManagerLineupAi(ManagerLineupBalance.CreateDefault());

            Lineup lineup = ai.BuildLineup(candidates);

            Assert.That(lineup[0].Player.PlayerId, Is.EqualTo(101));
            Assert.That(lineup[3].Player.PlayerId, Is.EqualTo(102));
        }

        [Test]
        public void BuildLineup_입력순서가달라도같은타순을만든다()
        {
            LineupSlot[] forward = CreateCandidates();
            LineupSlot[] reverse = new LineupSlot[forward.Length];
            for (int index = 0; index < forward.Length; index++)
                reverse[index] = forward[forward.Length - index - 1];
            var ai = new ManagerLineupAi(ManagerLineupBalance.CreateDefault());

            Lineup first = ai.BuildLineup(forward);
            Lineup second = ai.BuildLineup(reverse);

            for (int index = 0; index < first.Count; index++)
            {
                Assert.That(second[index].Player.PlayerId, Is.EqualTo(first[index].Player.PlayerId));
                Assert.That(second[index].FieldingPosition, Is.EqualTo(first[index].FieldingPosition));
            }
        }

        [Test]
        public void BuildLineup_모든선수와수비위치를한번씩보존한다()
        {
            LineupSlot[] candidates = CreateCandidates();
            var ai = new ManagerLineupAi(ManagerLineupBalance.CreateDefault());

            Lineup lineup = ai.BuildLineup(candidates);

            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                int matchCount = 0;
                for (int lineupIndex = 0; lineupIndex < lineup.Count; lineupIndex++)
                {
                    if (lineup[lineupIndex].Player.PlayerId == candidates[candidateIndex].Player.PlayerId &&
                        lineup[lineupIndex].FieldingPosition == candidates[candidateIndex].FieldingPosition)
                    {
                        matchCount++;
                    }
                }
                Assert.That(matchCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void BuildLineup_3루수도능력치에따라4번이아닌타순에배치한다()
        {
            LineupSlot[] candidates = CreateCandidates();
            var ai = new ManagerLineupAi(ManagerLineupBalance.CreateDefault());

            Lineup lineup = ai.BuildLineup(candidates);

            int thirdBaseBattingOrder = 0;
            for (int index = 0; index < lineup.Count; index++)
            {
                if (lineup[index].FieldingPosition == PlayerPosition.ThirdBase)
                    thirdBaseBattingOrder = index + 1;
            }
            Assert.That(thirdBaseBattingOrder, Is.Not.EqualTo(4));
        }

        private static LineupSlot[] CreateCandidates()
        {
            var result = new LineupSlot[9];
            for (int index = 0; index < result.Length; index++)
            {
                int rating = 48 + index;
                result[index] = CreateSlot(
                    index + 1,
                    (PlayerPosition)(index + 1),
                    rating,
                    rating,
                    rating,
                    rating);
            }
            return result;
        }

        private static LineupSlot CreateSlot(
            int playerId,
            PlayerPosition position,
            int contact,
            int power,
            int speed,
            int mental)
        {
            var player = new Player(
                playerId,
                $"타순 테스트 {playerId}",
                position,
                Handedness.Right,
                Handedness.Right,
                new BatterAttributes(contact, power, speed, 50, 50, mental),
                new PitcherAttributes(20, 20, 20, 20, 20, 20));
            return new LineupSlot(player, position);
        }
    }
}
