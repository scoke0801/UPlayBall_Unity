namespace Baseball.Core.Players
{
    /// <summary>
    /// 한 수비 포지션에 대한 선수의 적응도를 보관한다.
    /// </summary>
    public readonly struct PositionProficiency
    {
        /// <summary>
        /// 포지션과 0~100 범위의 적응도를 생성한다.
        /// </summary>
        public PositionProficiency(PlayerPosition position, int proficiency)
        {
            Position = position;
            Proficiency = AttributeRating.Validate(proficiency, nameof(proficiency));
        }

        public PlayerPosition Position { get; }
        public int Proficiency { get; }
    }
}
