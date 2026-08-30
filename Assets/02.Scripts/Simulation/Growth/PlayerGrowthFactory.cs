using System;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;

namespace Baseball.Simulation.Growth
{
    /// <summary>
    /// 캐릭터 생성 능력치에서 공정한 초기 Potential과 성장 상태를 만든다.
    /// </summary>
    public sealed class PlayerGrowthFactory
    {
        private readonly GrowthBalanceTable _balance;

        public PlayerGrowthFactory(GrowthBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public PlayerGrowthState Create(Player player, int age, int initialCondition)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            PlayerType playerType = IsPitcher(player.PrimaryPosition) ? PlayerType.Pitcher : PlayerType.Batter;
            AbilityRatings baseAbilities = CreateBaseAbilities(player);
            int[] potentials = CreatePotentials(baseAbilities, player.PrimaryPosition, playerType);

            // 생성 단계에는 숨은 RNG를 쓰지 않는다. 같은 포지션과 배분은 언제나 같은 Potential이다.
            return new PlayerGrowthState(
                player.PlayerId,
                age,
                playerType,
                baseAbilities,
                new AbilityRatings(potentials),
                WorkEthicGrade.Normal,
                initialCondition,
                fatigue: 0,
                durability: 70);
        }

        private static int[] CreatePotentials(
            AbilityRatings baseAbilities,
            PlayerPosition position,
            PlayerType playerType)
        {
            int[] result = baseAbilities.ToArray();
            for (int index = 0; index < result.Length; index++)
            {
                var ability = (PlayerAbility)index;
                int baseAbility = result[index];
                bool isRelevant = playerType == PlayerType.Pitcher
                    ? PlayerAbilityCatalog.IsPitcherAbility(ability)
                    : PlayerAbilityCatalog.IsBatterAbility(ability);
                if (!isRelevant)
                {
                    result[index] = Math.Min(AbilityRatings.Maximum, baseAbility + 5);
                    continue;
                }

                int importanceBonus = GetPositionImportanceBonus(position, ability);
                int partiallySeparated = 66 +
                    (int)Math.Round((baseAbility - 50) * 0.6d, MidpointRounding.AwayFromZero) +
                    importanceBonus;
                result[index] = Math.Min(88, Math.Max(baseAbility + 5, partiallySeparated));
            }
            return result;
        }

        private static int GetPositionImportanceBonus(PlayerPosition position, PlayerAbility ability)
        {
            if (position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher)
            {
                if (ability is PlayerAbility.Stuff or PlayerAbility.Control ||
                    position == PlayerPosition.StartingPitcher && ability == PlayerAbility.Stamina)
                    return 3;
                return ability is PlayerAbility.Velocity or PlayerAbility.Breaking or PlayerAbility.PitcherMental
                    ? 1
                    : 0;
            }

            if (ability == PlayerAbility.Contact)
                return 3;
            if (ability == PlayerAbility.BatterMental)
                return 1;
            bool powerPosition = position is PlayerPosition.FirstBase or PlayerPosition.ThirdBase or
                PlayerPosition.LeftField or PlayerPosition.RightField or PlayerPosition.DesignatedHitter;
            if (powerPosition && ability == PlayerAbility.Power)
                return 3;
            bool defensePosition = position is PlayerPosition.Catcher or PlayerPosition.SecondBase or
                PlayerPosition.Shortstop or PlayerPosition.CenterField;
            if (defensePosition && ability == PlayerAbility.Defense)
                return 3;
            return ability is PlayerAbility.Power or PlayerAbility.Speed or PlayerAbility.Arm or PlayerAbility.Defense
                ? 1
                : 0;
        }

        private static AbilityRatings CreateBaseAbilities(Player player)
        {
            return new AbilityRatings(new[]
            {
                Clamp(player.BatterAttributes.Contact),
                Clamp(player.BatterAttributes.Power),
                Clamp(player.BatterAttributes.Speed),
                Clamp(player.BatterAttributes.Arm),
                Clamp(player.BatterAttributes.Defense),
                Clamp(player.BatterAttributes.Mental),
                Clamp(player.PitcherAttributes.Stamina),
                Clamp(player.PitcherAttributes.Velocity),
                Clamp(player.PitcherAttributes.Stuff),
                Clamp(player.PitcherAttributes.Breaking),
                Clamp(player.PitcherAttributes.Control),
                Clamp(player.PitcherAttributes.Mental)
            });
        }

        private static bool IsPitcher(PlayerPosition position)
        {
            return position == PlayerPosition.StartingPitcher || position == PlayerPosition.ReliefPitcher;
        }

        private static int Clamp(int value)
        {
            if (value < AbilityRatings.Minimum) return AbilityRatings.Minimum;
            return value > AbilityRatings.Maximum ? AbilityRatings.Maximum : value;
        }
    }
}
