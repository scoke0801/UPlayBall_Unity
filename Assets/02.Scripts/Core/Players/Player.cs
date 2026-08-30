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
        private readonly PitchRepertoireEntry[] _pitchRepertoire;

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
            string nationality = "",
            IReadOnlyList<PitchRepertoireEntry> pitchRepertoire = null)
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
            _pitchRepertoire = CopyPitchRepertoire(pitchRepertoire);
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
        public IReadOnlyList<PitchRepertoireEntry> PitchRepertoire => _pitchRepertoire;

        /// <summary>같은 경기 능력치에 커리어에서 확정한 구종 목록을 결합한다.</summary>
        public Player WithPitchRepertoire(IReadOnlyList<PitchRepertoireEntry> pitchRepertoire)
        {
            return new Player(
                PlayerId,
                Name,
                PrimaryPosition,
                BattingHand,
                ThrowingHand,
                BatterAttributes,
                PitcherAttributes,
                _secondaryPositions,
                Nationality,
                pitchRepertoire);
        }

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

        private static PitchRepertoireEntry[] CopyPitchRepertoire(
            IReadOnlyList<PitchRepertoireEntry> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<PitchRepertoireEntry>();

            var result = new PitchRepertoireEntry[source.Count];
            bool hasPrimary = false;
            for (int index = 0; index < source.Count; index++)
            {
                PitchRepertoireEntry entry = source[index];
                for (int previous = 0; previous < index; previous++)
                {
                    if (result[previous].PitchType == entry.PitchType)
                        throw new ArgumentException("구종 목록은 중복될 수 없습니다.", nameof(source));
                }

                if (entry.IsPrimary)
                {
                    if (hasPrimary)
                        throw new ArgumentException("주력 구종은 하나만 지정할 수 있습니다.", nameof(source));
                    hasPrimary = true;
                }
                result[index] = entry;
            }

            return result;
        }
    }
}
