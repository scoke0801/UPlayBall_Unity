using System;
using Baseball.Core.Growth;

namespace Baseball.Core.Historical
{
    /// <summary>슬로건의 대상 조건과 여섯 단계 효과를 저작한다.</summary>
    [Serializable]
    public sealed class OwnerSloganDefinition
    {
        public string id;
        public string name;
        public OwnerSupportTarget target;
        public bool reliefOnly;
        public PlayerAbility requiredAbility;
        public int minimumAbility;
        public int[] minimumAbilityByLevel;
        // JsonUtility를 거친 선택적 배열은 null 대신 빈 배열이 될 수 있다.
        public int GetMinimumAbility(int level) => minimumAbilityByLevel == null || minimumAbilityByLevel.Length == 0
            ? minimumAbility : minimumAbilityByLevel[level - 1];
        public int[] cardsRequired;
        public PlayerAbility bonusAbility;
        public int[] bonusByLevel;
        public PlayerAbility penaltyAbility;
        public int[] penaltyByLevel;
        /// <summary>저장과 명령 복사본이 원본 배열을 공유하지 않도록 복제한다.</summary>
        public OwnerSloganDefinition Copy() => new OwnerSloganDefinition {
            id = id, name = name, target = target, reliefOnly = reliefOnly,
            requiredAbility = requiredAbility, minimumAbility = minimumAbility,
            minimumAbilityByLevel = minimumAbilityByLevel == null ? null : (int[])minimumAbilityByLevel.Clone(),
            cardsRequired = (int[])cardsRequired.Clone(), bonusAbility = bonusAbility,
            bonusByLevel = (int[])bonusByLevel.Clone(), penaltyAbility = penaltyAbility,
            penaltyByLevel = (int[])penaltyByLevel.Clone() };
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || cardsRequired?.Length != 6
                || bonusByLevel?.Length != 6 || penaltyByLevel?.Length != 6 || minimumAbility < 0
                || minimumAbilityByLevel != null && minimumAbilityByLevel.Length != 0 && minimumAbilityByLevel.Length != 6)
                throw new ArgumentException("슬로건 정의가 올바르지 않습니다.");
            for (int i = 0; i < 6; i++)
                if (cardsRequired[i] <= 0 || i > 0 && cardsRequired[i] <= cardsRequired[i - 1] || bonusByLevel[i] < 0 || penaltyByLevel[i] > 0)
                    throw new ArgumentException("슬로건 단계 조건이 올바르지 않습니다.");
        }
    }
    /// <summary>시즌 전에 확정한 슬로건과 효과 수준을 보존한다.</summary>
    public sealed class OwnerSloganState
    {
        public OwnerSloganState(OwnerSloganDefinition definition, int level, int revision)
        {
            definition.Validate();
            if (level < 1 || level > 6 || revision < 1) throw new ArgumentOutOfRangeException(nameof(level));
            Definition = definition.Copy(); Level = level; Revision = revision;
        }
        public OwnerSloganDefinition Definition { get; }
        public int Level { get; }
        public int Revision { get; }
    }
}
