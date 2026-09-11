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
        public BatterAttributes(int contact, int power, int speed, int arm, int defense, int mental)
        {
            Contact = AttributeRating.Validate(contact, nameof(contact));
            Power = AttributeRating.Validate(power, nameof(power));
            Speed = AttributeRating.Validate(speed, nameof(speed));
            Arm = AttributeRating.Validate(arm, nameof(arm));
            Defense = AttributeRating.Validate(defense, nameof(defense));
            Mental = AttributeRating.Validate(mental, nameof(mental));
        }

        public int Contact { get; }
        public int Power { get; }
        public int Speed { get; }
        public int Arm { get; }
        public int Defense { get; }
        public int Mental { get; }

        /// <summary>
        /// 번트는 별도 성장축으로 두지 않고 배트 컨트롤과 상황 판단에서 파생한다.
        /// </summary>
        public int Bunt => CalculateBunt(Contact, Mental);

        /// <summary>경기 능력치와 100 초과 카드 표시가 같은 번트 파생식을 사용한다.</summary>
        public static int CalculateBunt(int contact, int mental) => (contact * 3 + mental * 2 + 2) / 5;
    }
}
