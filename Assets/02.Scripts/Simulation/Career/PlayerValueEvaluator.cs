using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 같은 능력치 총합도 포지션과 구단 철학에 따라 다르게 평가되도록 선수 가치를 계산한다.
    /// </summary>
    public sealed class PlayerValueEvaluator
    {
        private readonly PlayerEvaluationBalance _balance;

        public PlayerValueEvaluator(PlayerEvaluationBalance balance)
        {
            _balance = balance;
        }

        /// <summary>
        /// 주 포지션의 핵심 능력치에 가중치를 둔 0~100 가치를 반환한다.
        /// </summary>
        public int CalculatePositionValue(Player player)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            return player.PrimaryPosition is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher
                ? CalculatePitcherValue(player.PitcherAttributes, player.PrimaryPosition)
                : CalculateBatterValue(player.BatterAttributes, player.PrimaryPosition);
        }

        /// <summary>
        /// 구단 철학과 선수 빌드의 궁합을 제한된 배율로 반환한다.
        /// </summary>
        public double CalculateTeamPreferenceFactor(Player player, TeamArchetype archetype)
        {
            double fit = archetype switch
            {
                TeamArchetype.Development => GetMental(player),
                TeamArchetype.OffenseFocused => GetOffenseFit(player),
                TeamArchetype.PitchingFocused => GetPitchingFit(player),
                _ => CalculatePositionValue(player)
            };

            return 1d + ((fit - 50d) / 50d) * _balance.TeamPreferenceInfluence;
        }

        private int CalculateBatterValue(BatterAttributes value, PlayerPosition position)
        {
            double contact = _balance.GeneralAttributeWeight;
            double power = _balance.GeneralAttributeWeight;
            double speed = _balance.GeneralAttributeWeight;
            double bunt = _balance.GeneralAttributeWeight;
            double defense = _balance.GeneralAttributeWeight;
            double mental = _balance.GeneralAttributeWeight;

            switch (position)
            {
                case PlayerPosition.Catcher:
                    contact = _balance.SupportingAttributeWeight;
                    defense = _balance.KeyAttributeWeight;
                    mental = _balance.KeyAttributeWeight;
                    break;
                case PlayerPosition.FirstBase:
                case PlayerPosition.DesignatedHitter:
                    contact = _balance.SupportingAttributeWeight;
                    power = _balance.KeyAttributeWeight;
                    mental = _balance.SupportingAttributeWeight;
                    break;
                case PlayerPosition.SecondBase:
                    contact = _balance.SupportingAttributeWeight;
                    speed = _balance.SupportingAttributeWeight;
                    defense = _balance.KeyAttributeWeight;
                    mental = _balance.SupportingAttributeWeight;
                    break;
                case PlayerPosition.Shortstop:
                    contact = _balance.SupportingAttributeWeight;
                    speed = _balance.SupportingAttributeWeight;
                    defense = _balance.KeyAttributeWeight;
                    mental = _balance.KeyAttributeWeight;
                    break;
                case PlayerPosition.CenterField:
                    contact = _balance.SupportingAttributeWeight;
                    speed = _balance.KeyAttributeWeight;
                    defense = _balance.KeyAttributeWeight;
                    break;
                default:
                    contact = _balance.SupportingAttributeWeight;
                    power = _balance.KeyAttributeWeight;
                    defense = _balance.SupportingAttributeWeight;
                    break;
            }

            return WeightedAverage(
                value.Contact, contact,
                value.Power, power,
                value.Speed, speed,
                value.Bunt, bunt,
                value.Defense, defense,
                value.Mental, mental);
        }

        private int CalculatePitcherValue(PitcherAttributes value, PlayerPosition position)
        {
            double stamina = position == PlayerPosition.StartingPitcher
                ? _balance.KeyAttributeWeight
                : _balance.GeneralAttributeWeight;
            double velocity = position == PlayerPosition.ReliefPitcher
                ? _balance.KeyAttributeWeight
                : _balance.SupportingAttributeWeight;
            double stuff = position == PlayerPosition.ReliefPitcher
                ? _balance.KeyAttributeWeight
                : _balance.SupportingAttributeWeight;
            double breaking = _balance.SupportingAttributeWeight;
            double control = position == PlayerPosition.StartingPitcher
                ? _balance.KeyAttributeWeight
                : _balance.SupportingAttributeWeight;
            double mental = _balance.SupportingAttributeWeight;

            return WeightedAverage(
                value.Stamina, stamina,
                value.Velocity, velocity,
                value.Stuff, stuff,
                value.Breaking, breaking,
                value.Control, control,
                value.Mental, mental);
        }

        private static int WeightedAverage(
            int first, double firstWeight,
            int second, double secondWeight,
            int third, double thirdWeight,
            int fourth, double fourthWeight,
            int fifth, double fifthWeight,
            int sixth, double sixthWeight)
        {
            double totalWeight = firstWeight + secondWeight + thirdWeight +
                                 fourthWeight + fifthWeight + sixthWeight;
            double total = first * firstWeight + second * secondWeight + third * thirdWeight +
                           fourth * fourthWeight + fifth * fifthWeight + sixth * sixthWeight;
            return (int)Math.Round(total / totalWeight, MidpointRounding.AwayFromZero);
        }

        private static int GetMental(Player player)
        {
            return player.PrimaryPosition is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher
                ? player.PitcherAttributes.Mental
                : player.BatterAttributes.Mental;
        }

        private static double GetOffenseFit(Player player)
        {
            if (player.PrimaryPosition is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher)
                return 35d;

            BatterAttributes value = player.BatterAttributes;
            return (value.Contact * 2d + value.Power * 2d + value.Speed) / 5d;
        }

        private static double GetPitchingFit(Player player)
        {
            if (player.PrimaryPosition is not (PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher))
                return 35d;

            PitcherAttributes value = player.PitcherAttributes;
            return (value.Velocity + value.Stuff + value.Breaking + value.Control) / 4d;
        }
    }
}
