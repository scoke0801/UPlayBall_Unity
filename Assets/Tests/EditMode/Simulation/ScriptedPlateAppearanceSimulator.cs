using Baseball.Core.Players;
using Baseball.Simulation.PlateAppearance;

namespace Baseball.Tests.EditMode.Simulation
{
    internal sealed class ScriptedPlateAppearanceSimulator : IPreResolvedBallInPlaySimulator
    {
        private readonly PlateAppearanceResult[] _results;
        private readonly PlateAppearanceResult _defaultResult;
        private int _resultIndex;

        public ScriptedPlateAppearanceSimulator(
            PlateAppearanceResult defaultResult,
            params PlateAppearanceResult[] results)
        {
            _defaultResult = defaultResult;
            _results = results;
        }

        public PitchResult SimulatePitch(
            in PlateAppearanceMatchup matchup,
            int balls,
            int strikes,
            int pitchNumber,
            BattingApproach approach)
        {
            PlateAppearanceResult result = GetCurrentResult();
            if (result == PlateAppearanceResult.Walk)
            {
                if (balls == 3)
                    _resultIndex++;
                return PitchResult.Ball;
            }

            if (result == PlateAppearanceResult.HitByPitch)
            {
                _resultIndex++;
                return PitchResult.HitByPitch;
            }

            if (result == PlateAppearanceResult.Strikeout)
            {
                if (strikes == 2)
                    _resultIndex++;
                return PitchResult.CalledStrike;
            }

            return PitchResult.InPlay;
        }

        public PlateAppearanceResult ResolveBallInPlay(
            in PlateAppearanceMatchup matchup,
            BattingApproach approach)
        {
            PlateAppearanceResult result = GetCurrentResult();
            _resultIndex++;
            return result;
        }

        private PlateAppearanceResult GetCurrentResult()
        {
            return _resultIndex < _results.Length ? _results[_resultIndex] : _defaultResult;
        }
    }
}
