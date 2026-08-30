using Baseball.Simulation.PlateAppearance;

namespace Baseball.Simulation.Match
{
    internal sealed partial class DetailedMatchEngine
    {
        private static void Emit(
            DetailedMatchState state,
            MatchEventType eventType,
            int inning,
            InningHalf half,
            int batterId = 0,
            int pitcherId = 0,
            int playerId = 0,
            PitchResult pitchResult = PitchResult.None,
            PlateAppearanceResult plateAppearanceResult = PlateAppearanceResult.None,
            int fromBase = 0,
            int toBase = 0,
            int balls = 0,
            int strikes = 0,
            int outs = 0,
            DecisionReasonCode reasonCode = DecisionReasonCode.None,
            PitchPlayData pitchPlayData = default)
        {
            state.EventSink.Record(new MatchEvent(
                state.NextEventSequence++,
                eventType,
                inning,
                half,
                batterId,
                pitcherId,
                playerId,
                pitchResult,
                plateAppearanceResult,
                fromBase,
                toBase,
                balls,
                strikes,
                outs,
                state.Away.BoxScore.Runs,
                state.Home.BoxScore.Runs,
                reasonCode,
                pitchPlayData));
        }
    }
}
