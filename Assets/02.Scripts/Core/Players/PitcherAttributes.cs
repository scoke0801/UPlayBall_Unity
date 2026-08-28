namespace Baseball.Core.Players
{
    /// <summary>
    /// 투구 결과와 피로 확장에 사용하는 투수 능력치를 불변 값으로 보관한다.
    /// </summary>
    public readonly struct PitcherAttributes
    {
        /// <summary>
        /// 0~100 범위의 투수 능력치를 생성한다.
        /// </summary>
        public PitcherAttributes(int stamina, int velocity, int stuff, int breaking, int control, int mental)
        {
            Stamina = AttributeRating.Validate(stamina, nameof(stamina));
            Velocity = AttributeRating.Validate(velocity, nameof(velocity));
            Stuff = AttributeRating.Validate(stuff, nameof(stuff));
            Breaking = AttributeRating.Validate(breaking, nameof(breaking));
            Control = AttributeRating.Validate(control, nameof(control));
            Mental = AttributeRating.Validate(mental, nameof(mental));
        }

        public int Stamina { get; }
        public int Velocity { get; }
        public int Stuff { get; }
        public int Breaking { get; }
        public int Control { get; }
        public int Mental { get; }
    }
}
