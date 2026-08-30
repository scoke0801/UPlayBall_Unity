using System;
using Baseball.Core.Balance;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 정규시즌 일정일이 끝난 뒤 관심→루머→협상 단계를 갱신하고 조건이 맞으면 한 번만 이동시킨다.
    /// </summary>
    public sealed class TradeMarketService
    {
        private const ulong TradeEvaluationStream = 0x54524144454D4B54UL;

        private readonly CareerState _career;
        private readonly BalanceTable _balance;
        private readonly TradeValuationAi _valuationAi;
        private readonly SkillBoardService _skillBoardService;

        public TradeMarketService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _valuationAi = new TradeValuationAi(balance.TradeMarket);
            _skillBoardService = new SkillBoardService(balance.Growth.SkillBoard, balance.Growth.SkillBlocks);
        }

        /// <summary>
        /// 평가일이 아니거나 거래가 없으면 null, 트레이드가 확정되면 이동 결과를 반환한다.
        /// </summary>
        public TradeExecutionResult? ProcessAfterScheduleDate()
        {
            SeasonState season = _career.CurrentLeague.CurrentSeason;
            if (season?.Phase != SeasonPhase.RegularSeason)
                return null;

            PlayerTradeState tradeState = _career.TradeState;
            TradeMarketBalance market = _balance.TradeMarket;
            tradeState.BeginSeason(season.SeasonId, market.TradeDeadlineGame);
            int gameIndex = GetCurrentGameIndex();
            if (gameIndex > market.TradeDeadlineGame)
            {
                tradeState.CloseUnresolvedInterests(gameIndex);
                return null;
            }
            if (!CanEvaluate(gameIndex, tradeState, market))
                return null;

            int currentTeamId = _career.MyPlayer.CurrentTeamId;
            TeamState currentTeam = GetTeam(currentTeamId);
            int playerValue = new PlayerValueEvaluator(_balance.PlayerEvaluation)
                .CalculatePositionValue(_career.MyPlayer.ToRosterPlayer(_skillBoardService));
            int currentRank = CalculateRank(currentTeamId);
            TradeCandidate? bestCandidate = null;
            bool hasSellerInterest = false;
            for (int index = 0; index < _career.CurrentLeague.Teams.Count; index++)
            {
                TeamState targetTeam = _career.CurrentLeague.Teams[index];
                if (targetTeam.TeamId == currentTeamId)
                    continue;

                TradeValuationResult evaluation = _valuationAi.Evaluate(BuildInput(
                    currentTeam,
                    targetTeam,
                    playerValue,
                    currentRank));
                if (evaluation.SellerInterest >= market.SellerInterestThreshold)
                    hasSellerInterest = true;
                if (evaluation.BuyerInterest < market.TeamInterestThreshold ||
                    evaluation.SellerInterest < market.SellerInterestThreshold)
                {
                    continue;
                }

                TradeInterestRecord? previous = tradeState.FindInterest(targetTeam.TeamId);
                TradeInterestStage stage = AdvanceStage(previous?.Stage, tradeState.Preference);
                var interest = previous.HasValue
                    ? previous.Value.Advance(evaluation, stage, gameIndex)
                    : new TradeInterestRecord(
                        targetTeam.TeamId,
                        (int)Math.Round(evaluation.BuyerInterest),
                        (int)Math.Round(evaluation.SellerInterest),
                        evaluation.ProjectedRole,
                        evaluation.ProjectedPlayingTime,
                        stage,
                        gameIndex);
                tradeState.UpsertInterest(interest);

                if (stage != TradeInterestStage.Negotiating || !DoesTradeComplete(
                    targetTeam.TeamId,
                    gameIndex,
                    evaluation.CompletionProbability))
                {
                    continue;
                }

                var candidate = new TradeCandidate(targetTeam.TeamId, evaluation);
                if (!bestCandidate.HasValue || candidate.Score > bestCandidate.Value.Score ||
                    Math.Abs(candidate.Score - bestCandidate.Value.Score) < 0.000001d &&
                    candidate.TeamId < bestCandidate.Value.TeamId)
                {
                    bestCandidate = candidate;
                }
            }

            tradeState.SetTradeBlock(hasSellerInterest);
            if (bestCandidate.HasValue)
            {
                TradeCandidate selected = bestCandidate.Value;
                return new PlayerMovementService(_career, _balance).ExecuteTrade(
                    selected.TeamId,
                    selected.Evaluation.ProjectedRole,
                    gameIndex);
            }

            if (gameIndex >= market.TradeDeadlineGame)
                tradeState.CloseUnresolvedInterests(gameIndex);
            return null;
        }

        private bool CanEvaluate(
            int gameIndex,
            PlayerTradeState tradeState,
            TradeMarketBalance market)
        {
            if (gameIndex < market.EvaluationStartGame || gameIndex < market.RookieProtectionGames)
                return false;
            if (tradeState.TradesThisSeason >= market.MaximumTradesPerSeason)
                return false;
            if (tradeState.LastTradeGameIndex >= 0 &&
                gameIndex - tradeState.LastTradeGameIndex < market.PostTradeProtectionGames)
            {
                return false;
            }
            return (gameIndex - market.EvaluationStartGame) % market.EvaluationIntervalGames == 0 ||
                   gameIndex == market.TradeDeadlineGame;
        }

        private TradeValuationInput BuildInput(
            TeamState currentTeam,
            TeamState targetTeam,
            int playerValue,
            int currentRank)
        {
            int teamCount = _career.CurrentLeague.Teams.Count;
            int targetRank = CalculateRank(targetTeam.TeamId);
            int currentCompetitor = currentTeam.GetStrongestCompetitorOverall(_career.MyPlayer.PrimaryPosition);
            int targetCompetitor = targetTeam.GetStrongestCompetitorOverall(_career.MyPlayer.PrimaryPosition);
            double rebuildingPressure = teamCount <= 1
                ? 0d
                : (currentRank - 1d) / (teamCount - 1d) * 100d;
            double currentContention = currentRank <= 4 ? 100d - (currentRank - 1d) * 15d : 20d;
            double targetUrgency = targetRank <= 4 ? 95d - (targetRank - 1d) * 12d : 35d;
            int remainingSeasons = _career.CurrentContract.GetRemainingSeasonsAfter(
                _career.CurrentLeague.CurrentSeason.Year);
            double expiryRisk = remainingSeasons <= 0 ? 100d : remainingSeasons == 1 ? 60d : 20d;
            double salaryRatio = _career.CurrentContract.AnnualSalary /
                                 (double)Math.Max(1L, _balance.ContractOffer.BaseSalary);
            double salaryBurden = Clamp((salaryRatio - 0.5d) * 65d, 0d, 100d);
            double contractValue = Clamp(105d - salaryRatio * 35d + remainingSeasons * 5d, 0d, 100d);
            // 계약 당시 약속 역할이 아니라 현재 전력·감독 신뢰를 쓴다. 약속만 남은 벤치 선수를
            // 핵심 전력으로 오판하면 트레이드라는 두 번째 기회 경로가 사실상 막히기 때문이다.
            double roleImportance = Clamp(
                50d + (playerValue - currentCompetitor) * 2d +
                (_career.MyPlayer.ManagerEvaluation - 50d) * 0.5d,
                15d,
                90d);
            return new TradeValuationInput(
                playerValue,
                targetTeam.GetPositionNeed(_career.MyPlayer.PrimaryPosition),
                Clamp(50d + (playerValue - targetCompetitor) * 2.5d, 0d, 100d),
                Clamp(targetUrgency, 0d, 100d),
                contractValue,
                Clamp(50d + (currentCompetitor - playerValue) * 2.5d, 0d, 100d),
                expiryRisk,
                Clamp(rebuildingPressure, 0d, 100d),
                salaryBurden,
                roleImportance,
                Clamp(currentContention, 0d, 100d),
                _career.TradeState.Preference);
        }

        private bool DoesTradeComplete(int teamId, int gameIndex, double probability)
        {
            ulong stream = TradeEvaluationStream ^
                           ((ulong)(uint)_career.CurrentLeague.CurrentSeason.SeasonId << 32) ^
                           ((ulong)(uint)gameIndex << 16) ^
                           (uint)teamId;
            ulong seed = DeterministicSeed.Derive(_career.CurrentLeague.RandomSeed, stream);
            return new Pcg32Random(seed).NextDouble() < probability;
        }

        private static TradeInterestStage AdvanceStage(
            TradeInterestStage? current,
            TradePreference preference)
        {
            if (!current.HasValue)
                return preference == TradePreference.RequestTrade
                    ? TradeInterestStage.Rumor
                    : TradeInterestStage.Interest;
            return current.Value switch
            {
                TradeInterestStage.Interest => TradeInterestStage.Rumor,
                TradeInterestStage.Rumor => TradeInterestStage.Negotiating,
                TradeInterestStage.Negotiating => TradeInterestStage.Negotiating,
                _ => TradeInterestStage.Interest
            };
        }

        private int GetCurrentGameIndex()
        {
            TeamSeasonRecordState record = _career.CurrentLeague.CurrentSeason.GetTeamRecord(
                _career.MyPlayer.CurrentTeamId);
            return record?.GamesPlayed ?? 0;
        }

        private int CalculateRank(int teamId)
        {
            TeamSeasonRecordState record = _career.CurrentLeague.CurrentSeason.GetTeamRecord(teamId);
            int rank = 1;
            for (int index = 0; index < _career.CurrentLeague.CurrentSeason.TeamRecords.Count; index++)
            {
                TeamSeasonRecordState other = _career.CurrentLeague.CurrentSeason.TeamRecords[index];
                if (other.TeamId == teamId)
                    continue;
                if (other.WinningPercentage > record.WinningPercentage ||
                    Math.Abs(other.WinningPercentage - record.WinningPercentage) < 0.000001d &&
                    other.Wins > record.Wins)
                {
                    rank++;
                }
            }
            return rank;
        }

        private TeamState GetTeam(int teamId)
        {
            for (int index = 0; index < _career.CurrentLeague.Teams.Count; index++)
            {
                if (_career.CurrentLeague.Teams[index].TeamId == teamId)
                    return _career.CurrentLeague.Teams[index];
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }

        private readonly struct TradeCandidate
        {
            public TradeCandidate(int teamId, TradeValuationResult evaluation)
            {
                TeamId = teamId;
                Evaluation = evaluation;
            }

            public int TeamId { get; }
            public TradeValuationResult Evaluation { get; }
            public double Score => Evaluation.BuyerInterest + Evaluation.SellerInterest;
        }
    }
}
