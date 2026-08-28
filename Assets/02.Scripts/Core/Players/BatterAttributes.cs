namespace Baseball.Core.Players
{
    /// <summary>
    /// 타격·주루·수비에 사용하는 선수 능력치를 불변 값으로 보관한다.
    /// </summary>
    public readonly struct BatterAttributes
    {
        /// <summary>
        /// 0~100 범위의 타자 능력치를 생성한다.
        /// </summary>
        public BatterAttributes(int contact, int power, int speed, int bunt, int defense, int mental)
        {
            Contact = AttributeRating.Validate(contact, nameof(contact));
            Power = AttributeRating.Validate(power, nameof(power));
            Speed = AttributeRating.Validate(speed, nameof(speed));
            Bunt = AttributeRating.Validate(bunt, nameof(bunt));
            Defense = AttributeRating.Validate(defense, nameof(defense));
            Mental = AttributeRating.Validate(mental, nameof(mental));
        }

        public int Contact { get; }
        public int Power { get; }
        public int Speed { get; }
        public int Bunt { get; }
        public int Defense { get; }
        public int Mental { get; }
    }
}
