using System;
using Baseball.Core.Balance;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Career
{
    public enum TradePreference
    {
        PreferToStay,
        Neutral,
        OpenToTrade,
        RequestTrade
    }

    public enum TradeInterestStage
    {
        Interest,
        Rumor,
        Negotiating,
        Completed,
        Failed
    }

    /// <summary>
    /// 영입 구단과 현재 구단의 트레이드 이유를 0~100 지표로 전달한다.
    /// </summary>
    public readonly struct TradeValuationInput
    {
        public TradeValuationInput(
            double playerValue,
            double targetPositionNeed,
            double targetUpgrade,
            double targetContentionUrgency,
            double contractValue,
            double positionDuplication,
            double expiryRisk,
            double rebuildingPressure,
            double salaryBurden,
            double currentRoleImportance,
            double currentTeamContention,
            TradePreference preference)
        {
            PlayerValue = Validate(playerValue, nameof(playerValue));
            TargetPositionNeed = Validate(targetPositionNeed, nameof(targetPositionNeed));
            TargetUpgrade = Validate(targetUpgrade, nameof(targetUpgrade));
            TargetContentionUrgency = Validate(targetContentionUrgency, nameof(targetContentionUrgency));
            ContractValue = Validate(contractValue, nameof(contractValue));
            PositionDuplication = Validate(positionDuplication, nameof(positionDuplication));
            ExpiryRisk = Validate(expiryRisk, nameof(expiryRisk));
            RebuildingPressure = Validate(rebuildingPressure, nameof(rebuildingPressure));
            SalaryBurden = Validate(salaryBurden, nameof(salaryBurden));
            CurrentRoleImportance = Validate(currentRoleImportance, nameof(currentRoleImportance));
            CurrentTeamContention = Validate(currentTeamContention, nameof(currentTeamContention));
            Preference = preference;
        }

        public double PlayerValue { get; }
        public double TargetPositionNeed { get; }
        public double TargetUpgrade { get; }
        public double TargetContentionUrgency { get; }
        public double ContractValue { get; }
        public double PositionDuplication { get; }
        public double ExpiryRisk { get; }
        public double RebuildingPressure { get; }
        public double SalaryBurden { get; }
        public double CurrentRoleImportance { get; }
        public double CurrentTeamContention { get; }
        public TradePreference Preference { get; }

        private static double Validate(double value, string name)
        {
            if (value < 0d || value > 100d)
                throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }

    public readonly struct TradeValuationResult
    {
        public TradeValuationResult(
            double buyerInterest,
            double sellerInterest,
            ExpectedRole projectedRole,
            double projectedPlayingTime,
            double completionProbability)
        {
            BuyerInterest = buyerInterest;
            SellerInterest = sellerInterest;
            ProjectedRole = projectedRole;
            ProjectedPlayingTime = projectedPlayingTime;
            CompletionProbability = completionProbability;
        }

        public double BuyerInterest { get; }
        public double SellerInterest { get; }
        public ExpectedRole ProjectedRole { get; }
        public double ProjectedPlayingTime { get; }
        public double CompletionProbability { get; }
    }

    /// <summary>
    /// 영입 관심과 매각 의향을 따로 계산해 한쪽 이유만으로 무작위 이동이 생기지 않게 한다.
    /// </summary>
    public sealed class TradeValuationAi
    {
        private readonly TradeMarketBalance _balance;

        public TradeValuationAi(TradeMarketBalance balance)
        {
            _balance = balance;
        }

        public TradeValuationResult Evaluate(TradeValuationInput input)
        {
            double buyerInterest = input.PlayerValue * 0.25d +
                                   input.TargetPositionNeed * 0.25d +
                                   input.TargetUpgrade * 0.20d +
                                   input.TargetContentionUrgency * 0.15d +
                                   input.ContractValue * 0.15d;
            // 매각 사유의 합을 90점, 잔류 사유의 합을 20점으로 둔다. 양쪽을 같은 100점
            // 합으로 두면 평범한 벤치 선수도 매각 기준에 거의 도달하지 못해 이동 경로가 사라진다.
            double sellerInterest = input.PositionDuplication * 0.30d +
                                    input.ExpiryRisk * 0.25d +
                                    input.RebuildingPressure * 0.20d +
                                    input.SalaryBurden * 0.15d +
                                    GetPreferenceSellerBonus(input.Preference) -
                                    input.CurrentRoleImportance * 0.15d -
                                    input.CurrentTeamContention * 0.05d;
            buyerInterest = Clamp(buyerInterest, 0d, 100d);
            sellerInterest = Clamp(sellerInterest, 0d, 100d);

            double margin = input.TargetUpgrade - 50d;
            ExpectedRole projectedRole = margin >= 12d
                ? ExpectedRole.StartingCompetition
                : margin >= -3d
                    ? ExpectedRole.RosterCompetition
                    : ExpectedRole.BenchCompetition;
            double projectedPlayingTime = projectedRole switch
            {
                ExpectedRole.StartingCompetition => 0.68d,
                ExpectedRole.RosterCompetition => 0.46d,
                _ => 0.22d
            };
            projectedPlayingTime = Clamp(
                projectedPlayingTime + (input.TargetPositionNeed - 50d) / 300d,
                0.10d,
                0.85d);

            double thresholdSurplus = Math.Max(0d, buyerInterest - _balance.TeamInterestThreshold) +
                                      Math.Max(0d, sellerInterest - _balance.SellerInterestThreshold);
            double completionProbability = _balance.BaseCompletionProbability + thresholdSurplus / 300d +
                                           GetPreferenceProbabilityBonus(input.Preference);
            return new TradeValuationResult(
                buyerInterest,
                sellerInterest,
                projectedRole,
                projectedPlayingTime,
                Clamp(completionProbability, 0d, 0.45d));
        }

        private static double GetPreferenceSellerBonus(TradePreference preference)
        {
            return preference switch
            {
                TradePreference.PreferToStay => -15d,
                TradePreference.OpenToTrade => 10d,
                TradePreference.RequestTrade => 25d,
                _ => 0d
            };
        }

        private static double GetPreferenceProbabilityBonus(TradePreference preference)
        {
            return preference switch
            {
                TradePreference.PreferToStay => -0.05d,
                TradePreference.OpenToTrade => 0.03d,
                TradePreference.RequestTrade => 0.08d,
                _ => 0d
            };
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
