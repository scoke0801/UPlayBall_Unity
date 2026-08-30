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
            int[] potentials = baseAbilities.ToArray();
            for (int index = 0; index < potentials.Length; index++)
                potentials[index] = Math.Min(AbilityRatings.Maximum, potentials[index] + _balance.DefaultPotentialGap);

            // 첫 구현은 동일 난이도 캐릭터의 총 Potential 공정성을 위해 무작위 총량 차이를 두지 않는다.
            // 아키타입별 시작 능력치 배분이 그대로 Potential 분포의 주된 차이가 된다.
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
