using Baseball.Core.Balance;
using Baseball.Core.Players;

namespace Baseball.Simulation.Match
{
    /// <summary>감독의 상대 맞춤 선호를 실제 타석의 좌우 컨택 유불리에 반영한다.</summary>
    public static class ManagerMatchupAi
    {
        public static double EvaluateContactAdjustment(Handedness battingHand, Handedness throwingHand,
            int matchupPreference, PlateDisciplineBalance discipline)
        {
            double contact = battingHand == Handedness.Switch || battingHand != throwingHand
                ? discipline.OppositeHandedContactBonus : -discipline.SameHandedContactPenalty;
            return contact * matchupPreference / 100d;
        }
    }
}
