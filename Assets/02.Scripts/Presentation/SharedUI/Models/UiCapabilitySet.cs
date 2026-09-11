using System;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// 공용 UI가 모드 이름을 해석하지 않고 허용된 조작만 노출하도록 하는 기능 단위다.
    /// </summary>
    [Flags]
    public enum UiCapability : ulong
    {
        None = 0,
        CanEditActiveRoster = 1UL << 0,
        CanEditLineup = 1UL << 1,
        CanEquipTeamColor = 1UL << 2,
        CanEquipTacticCards = 1UL << 3,
        CanUseScout = 1UL << 4,
        CanTrainOwnedCards = 1UL << 5,
        CanManageFinance = 1UL << 6,
        CanViewCareerPlayerGrowth = 1UL << 7,
        CanPlayPlayerMiniGame = 1UL << 8,
        CanViewManagerDecisionReason = 1UL << 9,
        CanViewLeagueInformation = 1UL << 10,
        CanViewSeasonRecords = 1UL << 11,
        CanManagePlayerContracts = 1UL << 12
    }

    /// <summary>
    /// 한 모드가 제공하는 UI 기능을 불변 값으로 전달한다.
    /// </summary>
    public readonly struct UiCapabilitySet : IEquatable<UiCapabilitySet>
    {
        private readonly UiCapability _values;

        /// <summary>
        /// 아무 조작 권한도 없는 기능 집합이다.
        /// </summary>
        public static UiCapabilitySet None => new UiCapabilitySet(UiCapability.None);

        /// <summary>
        /// 포함된 기능 비트 값을 반환한다.
        /// </summary>
        public UiCapability Values => _values;

        /// <summary>
        /// 지정한 기능 비트로 기능 집합을 만든다.
        /// </summary>
        public UiCapabilitySet(UiCapability values)
        {
            _values = values;
        }

        /// <summary>
        /// 필요한 모든 기능이 현재 집합에 포함되는지 확인한다.
        /// </summary>
        public bool Has(UiCapability required)
        {
            return required == UiCapability.None || (_values & required) == required;
        }

        /// <summary>
        /// 기능을 추가한 새 집합을 반환한다.
        /// </summary>
        public UiCapabilitySet With(UiCapability capability)
        {
            return new UiCapabilitySet(_values | capability);
        }

        /// <summary>
        /// 기능을 제거한 새 집합을 반환한다.
        /// </summary>
        public UiCapabilitySet Without(UiCapability capability)
        {
            return new UiCapabilitySet(_values & ~capability);
        }

        /// <summary>
        /// 두 기능 집합이 같은 비트를 가지는지 확인한다.
        /// </summary>
        public bool Equals(UiCapabilitySet other)
        {
            return _values == other._values;
        }

        /// <summary>
        /// 다른 객체와 기능 집합 값이 같은지 확인한다.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is UiCapabilitySet other && Equals(other);
        }

        /// <summary>
        /// 기능 집합의 해시 값을 반환한다.
        /// </summary>
        public override int GetHashCode()
        {
            return (int)_values;
        }
    }
}
