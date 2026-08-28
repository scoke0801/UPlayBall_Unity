using System;
using System.Collections.Generic;

namespace Baseball.Core.Players
{
    /// <summary>
    /// 경기 입력에 필요한 선수 신원과 현재 능력치를 표현한다.
    /// </summary>
    public sealed class Player
    {
        private const int EmergencyPositionProficiency = 35;
        private readonly PositionProficiency[] _secondaryPositions;

        /// <summary>
        /// 경기에서 사용할 선수 정보를 생성한다.
        /// </summary>
        public Player(
            int playerId,
            string name,
            PlayerPosition primaryPosition,
            Handedness battingHand,
            Handedness throwingHand,
            BatterAttributes batterAttributes,
            PitcherAttributes pitcherAttributes,
            IReadOnlyList<PositionProficiency> secondaryPositions = null,
            string nationality = "")
        {
            if (playerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerId), "PlayerId는 양수여야 합니다.");
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("선수 이름은 비어 있을 수 없습니다.", nameof(name));
            if (primaryPosition == PlayerPosition.Unknown)
                throw new ArgumentException("주 포지션을 지정해야 합니다.", nameof(primaryPosition));
            if (throwingHand == Handedness.Switch)
                throw new ArgumentException("투구 손은 Switch일 수 없습니다.", nameof(throwingHand));

            PlayerId = playerId;
            Name = name;
            Nationality = nationality?.Trim() ?? string.Empty;
            PrimaryPosition = primaryPosition;
            BattingHand = battingHand;
            ThrowingHand = throwingHand;
            BatterAttributes = batterAttributes;
            PitcherAttributes = pitcherAttributes;
            _secondaryPositions = CopySecondaryPositions(secondaryPositions, primaryPosition);
        }

        public int PlayerId { get; }
        public string Name { get; }
        public string Nationality { get; }
        public PlayerPosition PrimaryPosition { get; }
        public Handedness BattingHand { get; }
        public Handedness ThrowingHand { get; }
        public BatterAttributes BatterAttributes { get; }
        public PitcherAttributes PitcherAttributes { get; }
        public IReadOnlyList<PositionProficiency> SecondaryPositions => _secondaryPositions;

        /// <summary>
        /// 지정 포지션에서 발휘하는 0~100 적응도를 반환한다.
        /// </summary>
        public int GetPositionProficiency(PlayerPosition position)
        {
            if (position == PrimaryPosition)
                return 100;

            for (int index = 0; index < _secondaryPositions.Length; index++)
            {
                if (_secondaryPositions[index].Position == position)
                    return _secondaryPositions[index].Proficiency;
            }

            return EmergencyPositionProficiency;
        }

        private static PositionProficiency[] CopySecondaryPositions(
            IReadOnlyList<PositionProficiency> source,
            PlayerPosition primaryPosition)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<PositionProficiency>();

            var result = new PositionProficiency[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                PositionProficiency proficiency = source[index];
                if (proficiency.Position == PlayerPosition.Unknown || proficiency.Position == primaryPosition)
                    throw new ArgumentException("보조 포지션은 주 포지션과 달라야 합니다.", nameof(source));

                for (int previous = 0; previous < index; previous++)
                {
                    if (result[previous].Position == proficiency.Position)
                        throw new ArgumentException("보조 포지션은 중복될 수 없습니다.", nameof(source));
                }

                result[index] = proficiency;
            }

            return result;
        }
    }
}
