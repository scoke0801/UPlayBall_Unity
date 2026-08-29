using System;

namespace Baseball.Core.Players
{
    /// <summary>
    /// 투수가 보유하거나 훈련으로 습득할 수 있는 구종을 정의한다.
    /// </summary>
    public enum PitchType
    {
        FourSeamFastball = 0,
        TwoSeamFastball = 1,
        Cutter = 2,
        Slider = 3,
        Curveball = 4,
        Changeup = 5,
        Splitter = 6,
        Sinker = 7
    }

    /// <summary>
    /// 생성 시 확정한 구종 하나와 초기 숙련도를 보관한다.
    /// </summary>
    public readonly struct PitchRepertoireEntry
    {
        public PitchRepertoireEntry(PitchType pitchType, int proficiency, bool isPrimary)
        {
            if (!Enum.IsDefined(typeof(PitchType), pitchType))
                throw new ArgumentOutOfRangeException(nameof(pitchType));
            PitchType = pitchType;
            Proficiency = AttributeRating.Validate(proficiency, nameof(proficiency));
            IsPrimary = isPrimary;
        }

        public PitchType PitchType { get; }
        public int Proficiency { get; }
        public bool IsPrimary { get; }
    }
}
