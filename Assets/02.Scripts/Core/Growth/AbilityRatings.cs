using System;
using Baseball.Core.Players;

namespace Baseball.Core.Growth
{
    /// <summary>
    /// 모든 타자·투수 능력치를 고정 순서 배열로 보관해 결정론적 순회를 보장한다.
    /// </summary>
    public sealed class AbilityRatings
    {
        public const int Minimum = 1;
        public const int Maximum = 99;
        private readonly int[] _values;

        public AbilityRatings(int defaultValue)
        {
            ValidateRating(defaultValue, nameof(defaultValue));
            _values = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < _values.Length; index++)
                _values[index] = defaultValue;
        }

        public AbilityRatings(int[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Length != PlayerAbilityCatalog.AbilityCount)
                throw new ArgumentException("모든 능력치 값을 순서대로 제공해야 합니다.", nameof(values));

            _values = new int[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                ValidateRating(values[index], nameof(values));
                _values[index] = values[index];
            }
        }

        public int Get(PlayerAbility ability)
        {
            ValidateAbility(ability);
            return _values[(int)ability];
        }

        /// <summary>
        /// 영구 성장·노쇠 결과를 1~99 범위에서 적용하고 실제 변화량을 반환한다.
        /// </summary>
        public int AddClamped(PlayerAbility ability, int delta, int minimum = Minimum, int maximum = Maximum)
        {
            ValidateAbility(ability);
            if (minimum < Minimum || maximum > Maximum || minimum > maximum)
                throw new ArgumentOutOfRangeException(nameof(minimum));

            int index = (int)ability;
            int before = _values[index];
            int after = Clamp(before + delta, minimum, maximum);
            _values[index] = after;
            return after - before;
        }

        public AbilityRatings Clone()
        {
            return new AbilityRatings(_values);
        }

        public int[] ToArray()
        {
            var copy = new int[_values.Length];
            Array.Copy(_values, copy, _values.Length);
            return copy;
        }

        public BatterAttributes ToBatterAttributes()
        {
            return new BatterAttributes(
                Get(PlayerAbility.Contact),
                Get(PlayerAbility.Power),
                Get(PlayerAbility.Speed),
                Get(PlayerAbility.Bunt),
                Get(PlayerAbility.Defense),
                Get(PlayerAbility.BatterMental));
        }

        public PitcherAttributes ToPitcherAttributes()
        {
            return new PitcherAttributes(
                Get(PlayerAbility.Stamina),
                Get(PlayerAbility.Velocity),
                Get(PlayerAbility.Stuff),
                Get(PlayerAbility.Breaking),
                Get(PlayerAbility.Control),
                Get(PlayerAbility.PitcherMental));
        }

        private static void ValidateAbility(PlayerAbility ability)
        {
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
        }

        private static void ValidateRating(int value, string parameterName)
        {
            if (value < Minimum || value > Maximum)
                throw new ArgumentOutOfRangeException(parameterName, value, "능력치는 1~99 범위여야 합니다.");
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
