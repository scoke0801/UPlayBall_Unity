using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Match
{
    /// <summary>경기 상황을 순수 보정값으로 해석한다. RNG·UI·성장 블록 효과와 독립적이다.</summary>
    public static class CardTraitEffectResolver
    {
        public static double Contact(in CardTraitEffect effect, bool scoringPosition, bool emptyBases) =>
            effect.Get(CardTraitKind.Contact) + (scoringPosition ? effect.Get(CardTraitKind.Clutch) : 0)
            + (emptyBases ? effect.Get(CardTraitKind.Leadoff) : 0);

        public static double Control(in CardTraitEffect effect, int inning, bool scoringPosition, bool leading, PitcherRole role) =>
            (scoringPosition ? effect.Get(CardTraitKind.PitchClutch) : 0)
            + (inning <= 3 && role == PitcherRole.Starter ? effect.Get(CardTraitKind.EarlyStarter) : 0)
            + (inning >= 7 && inning <= 8 && role != PitcherRole.Starter ? effect.Get(CardTraitKind.Setup) : 0)
            + (inning >= 9 && leading && role == PitcherRole.Closer ? effect.Get(CardTraitKind.Closer) : 0);

        public static double Stuff(in CardTraitEffect effect, int strikes) => strikes == 2 ? effect.Get(CardTraitKind.Strikeout) : 0;
    }
}
