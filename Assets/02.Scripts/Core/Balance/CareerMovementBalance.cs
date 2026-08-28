using System;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 기존 구단 재계약 의향, 계약 기간, 오퍼 보류 위험을 조정하는 계수다.
    /// </summary>
    public readonly struct ContractRenewalBalance
    {
        public ContractRenewalBalance(
            double minimumInterestScore,
            double normalOfferScore,
            double coreOfferScore,
            double holdWithdrawalProbability,
            int extensionStartGame,
            int extensionEndGame,
            double extensionMarketValueRatio)
        {
            if (minimumInterestScore < 0d || coreOfferScore > 100d ||
                normalOfferScore < minimumInterestScore || coreOfferScore < normalOfferScore)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumInterestScore));
            }
            if (holdWithdrawalProbability < 0d || holdWithdrawalProbability > 1d)
                throw new ArgumentOutOfRangeException(nameof(holdWithdrawalProbability));
            if (extensionStartGame <= 0 || extensionEndGame < extensionStartGame)
                throw new ArgumentOutOfRangeException(nameof(extensionStartGame));
            if (extensionMarketValueRatio <= 1d)
                throw new ArgumentOutOfRangeException(nameof(extensionMarketValueRatio));

            MinimumInterestScore = minimumInterestScore;
            NormalOfferScore = normalOfferScore;
            CoreOfferScore = coreOfferScore;
            HoldWithdrawalProbability = holdWithdrawalProbability;
            ExtensionStartGame = extensionStartGame;
            ExtensionEndGame = extensionEndGame;
            ExtensionMarketValueRatio = extensionMarketValueRatio;
        }

        public double MinimumInterestScore { get; }
        public double NormalOfferScore { get; }
        public double CoreOfferScore { get; }
        public double HoldWithdrawalProbability { get; }
        public int ExtensionStartGame { get; }
        public int ExtensionEndGame { get; }
        public double ExtensionMarketValueRatio { get; }

        public static ContractRenewalBalance CreateDefault()
        {
            return new ContractRenewalBalance(
                minimumInterestScore: 35d,
                normalOfferScore: 65d,
                coreOfferScore: 80d,
                holdWithdrawalProbability: 0.20d,
                extensionStartGame: 21,
                extensionEndGame: 40,
                extensionMarketValueRatio: 1.20d);
        }
    }

    /// <summary>
    /// 동일 리그 트레이드의 평가 구간, 보호 기간, 성사 희소성을 조정하는 계수다.
    /// </summary>
    public readonly struct TradeMarketBalance
    {
        public TradeMarketBalance(
            int evaluationStartGame,
            int tradeDeadlineGame,
            int evaluationIntervalGames,
            int rookieProtectionGames,
            int postTradeProtectionGames,
            int maximumTradesPerSeason,
            double teamInterestThreshold,
            double sellerInterestThreshold,
            double baseCompletionProbability,
            int arrivalManagerEvaluation)
        {
            if (evaluationStartGame <= 0 || tradeDeadlineGame < evaluationStartGame)
                throw new ArgumentOutOfRangeException(nameof(evaluationStartGame));
            if (evaluationIntervalGames <= 0 || rookieProtectionGames < 0 || postTradeProtectionGames < 0)
                throw new ArgumentOutOfRangeException(nameof(evaluationIntervalGames));
            if (maximumTradesPerSeason < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumTradesPerSeason));
            if (teamInterestThreshold < 0d || teamInterestThreshold > 100d ||
                sellerInterestThreshold < 0d || sellerInterestThreshold > 100d)
            {
                throw new ArgumentOutOfRangeException(nameof(teamInterestThreshold));
            }
            if (baseCompletionProbability < 0d || baseCompletionProbability > 1d)
                throw new ArgumentOutOfRangeException(nameof(baseCompletionProbability));
            if (arrivalManagerEvaluation < 0 || arrivalManagerEvaluation > 100)
                throw new ArgumentOutOfRangeException(nameof(arrivalManagerEvaluation));

            EvaluationStartGame = evaluationStartGame;
            TradeDeadlineGame = tradeDeadlineGame;
            EvaluationIntervalGames = evaluationIntervalGames;
            RookieProtectionGames = rookieProtectionGames;
            PostTradeProtectionGames = postTradeProtectionGames;
            MaximumTradesPerSeason = maximumTradesPerSeason;
            TeamInterestThreshold = teamInterestThreshold;
            SellerInterestThreshold = sellerInterestThreshold;
            BaseCompletionProbability = baseCompletionProbability;
            ArrivalManagerEvaluation = arrivalManagerEvaluation;
        }

        public int EvaluationStartGame { get; }
        public int TradeDeadlineGame { get; }
        public int EvaluationIntervalGames { get; }
        public int RookieProtectionGames { get; }
        public int PostTradeProtectionGames { get; }
        public int MaximumTradesPerSeason { get; }
        public double TeamInterestThreshold { get; }
        public double SellerInterestThreshold { get; }
        public double BaseCompletionProbability { get; }
        public int ArrivalManagerEvaluation { get; }

        public static TradeMarketBalance CreateDefault()
        {
            return new TradeMarketBalance(
                evaluationStartGame: 41,
                tradeDeadlineGame: 56,
                evaluationIntervalGames: 4,
                rookieProtectionGames: 20,
                postTradeProtectionGames: 25,
                maximumTradesPerSeason: 1,
                teamInterestThreshold: 64d,
                sellerInterestThreshold: 35d,
                baseCompletionProbability: 0.10d,
                arrivalManagerEvaluation: 50);
        }
    }
}
