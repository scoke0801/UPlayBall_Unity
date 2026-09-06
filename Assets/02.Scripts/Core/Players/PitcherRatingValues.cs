using System;

namespace Baseball.Core.Players
{
    /// <summary>표시 BaseStat 상한과 독립적인 곡선 변환 전 실효 투수 입력을 보관한다.</summary>
    public readonly struct PitcherRatingValues
    {
        public PitcherRatingValues(double stamina, double velocity, double stuff, double breaking, double control, double mental)
        {
            Validate(stamina); Validate(velocity); Validate(stuff); Validate(breaking); Validate(control); Validate(mental);
            Stamina = stamina; Velocity = velocity; Stuff = stuff; Breaking = breaking; Control = control; Mental = mental;
        }
        public PitcherRatingValues(PitcherAttributes source)
            : this(source.Stamina, source.Velocity, source.Stuff, source.Breaking, source.Control, source.Mental) { }
        public double Stamina { get; }
        public double Velocity { get; }
        public double Stuff { get; }
        public double Breaking { get; }
        public double Control { get; }
        public double Mental { get; }
        private static void Validate(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
