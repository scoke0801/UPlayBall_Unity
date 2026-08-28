using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Random;

namespace Baseball.Tests.EditMode.Simulation
{
    internal static class SimulationTestFactory
    {
        public static Team CreateTeam(int teamId, int batterRating, int pitcherRating, int defenseRating = -1)
        {
            if (defenseRating < 0)
                defenseRating = batterRating;

            var slots = new LineupSlot[9];
            for (int index = 0; index < slots.Length; index++)
            {
                PlayerPosition position = (PlayerPosition)(index + 1);
                int handednessIndex = index % 3;
                Handedness battingHand = handednessIndex == 0
                    ? Handedness.Left
                    : handednessIndex == 1
                        ? Handedness.Right
                        : Handedness.Switch;
                var batter = new Player(
                    teamId * 100 + index + 1,
                    $"{teamId}팀 타자 {index + 1}",
                    position,
                    battingHand,
                    Handedness.Right,
                    new BatterAttributes(
                        batterRating,
                        batterRating,
                        batterRating,
                        batterRating,
                        defenseRating,
                        batterRating),
                    new PitcherAttributes(20, 20, 20, 20, 20, 20));
                slots[index] = new LineupSlot(batter, position);
            }

            var pitcher = new Player(
                teamId * 100 + 99,
                $"{teamId}팀 선발",
                PlayerPosition.StartingPitcher,
                Handedness.Right,
                teamId % 2 == 0 ? Handedness.Left : Handedness.Right,
                new BatterAttributes(20, 20, 20, 20, 30, 20),
                new PitcherAttributes(
                    pitcherRating,
                    pitcherRating,
                    pitcherRating,
                    pitcherRating,
                    pitcherRating,
                    pitcherRating));
            return new Team(teamId, $"테스트 {teamId}팀", new Lineup(slots), pitcher);
        }
    }

    internal sealed class SequenceRandom : IRandomSource
    {
        private readonly double[] _values;
        private int _index;

        public SequenceRandom(params double[] values)
        {
            _values = values;
        }

        public double NextDouble()
        {
            double value = _values[_index % _values.Length];
            _index++;
            return value;
        }
    }
}
