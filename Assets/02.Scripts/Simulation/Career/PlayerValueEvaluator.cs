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
            AttributeWeightProfile weights = GetBatterWeights(_balance, position);

            return WeightedAverage(
                value.Contact, weights.First,
                value.Power, weights.Second,
                value.Speed, weights.Third,
                value.Arm, weights.Fourth,
                value.Defense, weights.Fifth,
                value.Mental, weights.Sixth);
        }

        private int CalculatePitcherValue(PitcherAttributes value, PlayerPosition position)
        {
            AttributeWeightProfile weights = GetPitcherWeights(_balance, position);

            return WeightedAverage(
                value.Stamina, weights.First,
                value.Velocity, weights.Second,
                value.Stuff, weights.Third,
                value.Breaking, weights.Fourth,
                value.Control, weights.Fifth,
                value.Mental, weights.Sixth);
        }

        internal static AttributeWeightProfile GetBatterWeights(
            PlayerEvaluationBalance balance,
            PlayerPosition position)
        {
            double contact = balance.GeneralAttributeWeight;
            double power = balance.GeneralAttributeWeight;
            double speed = balance.GeneralAttributeWeight;
            double arm = balance.GeneralAttributeWeight;
            double defense = balance.GeneralAttributeWeight;
            double mental = balance.GeneralAttributeWeight;

            switch (position)
            {
                case PlayerPosition.Catcher:
                    contact = balance.SupportingAttributeWeight;
                    arm = balance.KeyAttributeWeight;
                    defense = balance.KeyAttributeWeight;
                    mental = balance.KeyAttributeWeight;
                    break;
                case PlayerPosition.FirstBase:
                case PlayerPosition.DesignatedHitter:
                    contact = balance.SupportingAttributeWeight;
                    power = balance.KeyAttributeWeight;
                    mental = balance.SupportingAttributeWeight;
                    break;
                case PlayerPosition.SecondBase:
                    contact = balance.SupportingAttributeWeight;
                    speed = balance.SupportingAttributeWeight;
                    defense = balance.KeyAttributeWeight;
                    mental = balance.SupportingAttributeWeight;
                    break;
                case PlayerPosition.ThirdBase:
                    contact = balance.SupportingAttributeWeight;
                    power = balance.KeyAttributeWeight;
                    arm = balance.KeyAttributeWeight;
                    defense = balance.SupportingAttributeWeight;
                    break;
                case PlayerPosition.Shortstop:
                    contact = balance.SupportingAttributeWeight;
                    speed = balance.SupportingAttributeWeight;
                    arm = balance.KeyAttributeWeight;
                    defense = balance.KeyAttributeWeight;
                    mental = balance.SupportingAttributeWeight;
                    break;
                case PlayerPosition.LeftField:
                case PlayerPosition.RightField:
                    contact = balance.SupportingAttributeWeight;
                    power = balance.KeyAttributeWeight;
                    arm = balance.KeyAttributeWeight;
                    defense = balance.SupportingAttributeWeight;
                    break;
                case PlayerPosition.CenterField:
                    contact = balance.SupportingAttributeWeight;
                    speed = balance.KeyAttributeWeight;
                    arm = balance.SupportingAttributeWeight;
                    defense = balance.KeyAttributeWeight;
                    break;
            }

            return new AttributeWeightProfile(contact, power, speed, arm, defense, mental);
        }

        internal static AttributeWeightProfile GetPitcherWeights(
            PlayerEvaluationBalance balance,
            PlayerPosition position)
        {
            double stamina = position == PlayerPosition.StartingPitcher
                ? balance.KeyAttributeWeight
                : balance.GeneralAttributeWeight;
            double velocity = position == PlayerPosition.ReliefPitcher
                ? balance.KeyAttributeWeight
                : balance.SupportingAttributeWeight;
            double stuff = position == PlayerPosition.ReliefPitcher
                ? balance.KeyAttributeWeight
                : balance.SupportingAttributeWeight;
            double breaking = balance.SupportingAttributeWeight;
            double control = position == PlayerPosition.StartingPitcher
                ? balance.KeyAttributeWeight
                : balance.SupportingAttributeWeight;
            double mental = balance.SupportingAttributeWeight;
            return new AttributeWeightProfile(stamina, velocity, stuff, breaking, control, mental);
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

        internal readonly struct AttributeWeightProfile
        {
            public AttributeWeightProfile(
                double first,
                double second,
                double third,
                double fourth,
                double fifth,
                double sixth)
            {
                First = first;
                Second = second;
                Third = third;
                Fourth = fourth;
                Fifth = fifth;
                Sixth = sixth;
            }

            public double First { get; }
            public double Second { get; }
            public double Third { get; }
            public double Fourth { get; }
            public double Fifth { get; }
            public double Sixth { get; }
            public double Total => First + Second + Third + Fourth + Fifth + Sixth;

            public double Get(int index)
            {
                return index switch
                {
                    0 => First,
                    1 => Second,
                    2 => Third,
                    3 => Fourth,
                    4 => Fifth,
                    5 => Sixth,
                    _ => throw new ArgumentOutOfRangeException(nameof(index))
                };
            }
        }
    }
}
