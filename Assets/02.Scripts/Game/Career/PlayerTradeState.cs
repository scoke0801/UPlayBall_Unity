using System;
using System.Collections.Generic;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 한 구단의 관심 단계와 이동 시 기대 역할을 세이브 가능한 값으로 보관한다.
    /// </summary>
    public readonly struct TradeInterestRecord
    {
        public TradeInterestRecord(
            int interestedTeamId,
            int buyerInterestScore,
            int sellerInterestScore,
            ExpectedRole projectedRole,
            double projectedPlayingTime,
            TradeInterestStage stage,
            int lastEvaluatedGameIndex)
        {
            InterestedTeamId = interestedTeamId;
            BuyerInterestScore = buyerInterestScore;
            SellerInterestScore = sellerInterestScore;
            ProjectedRole = projectedRole;
            ProjectedPlayingTime = projectedPlayingTime;
            Stage = stage;
            LastEvaluatedGameIndex = lastEvaluatedGameIndex;
        }

        public int InterestedTeamId { get; }
        public int BuyerInterestScore { get; }
        public int SellerInterestScore { get; }
        public ExpectedRole ProjectedRole { get; }
        public double ProjectedPlayingTime { get; }
        public TradeInterestStage Stage { get; }
        public int LastEvaluatedGameIndex { get; }

        public TradeInterestRecord Advance(
            TradeValuationResult evaluation,
            TradeInterestStage stage,
            int gameIndex)
        {
            return new TradeInterestRecord(
                InterestedTeamId,
                (int)Math.Round(evaluation.BuyerInterest),
                (int)Math.Round(evaluation.SellerInterest),
                evaluation.ProjectedRole,
                evaluation.ProjectedPlayingTime,
                stage,
                gameIndex);
        }
    }

    /// <summary>
    /// 커리어 연표와 팀별 시즌 분할 기록을 연결하는 확정 트레이드 이력이다.
    /// </summary>
    public readonly struct TradeHistoryRecord
    {
        public TradeHistoryRecord(
            int seasonId,
            int year,
            int gameIndex,
            int previousTeamId,
            int newTeamId,
            ExpectedRole previousRole,
            ExpectedRole projectedRole,
            int exchangedPlayerId)
        {
            SeasonId = seasonId;
            Year = year;
            GameIndex = gameIndex;
            PreviousTeamId = previousTeamId;
            NewTeamId = newTeamId;
            PreviousRole = previousRole;
            ProjectedRole = projectedRole;
            ExchangedPlayerId = exchangedPlayerId;
        }

        public int SeasonId { get; }
        public int Year { get; }
        public int GameIndex { get; }
        public int PreviousTeamId { get; }
        public int NewTeamId { get; }
        public ExpectedRole PreviousRole { get; }
        public ExpectedRole ProjectedRole { get; }
        public int ExchangedPlayerId { get; }
    }

    /// <summary>
    /// 트레이드 태도, 현재 관심 구단, 시즌별 보호 규칙과 전체 이동 이력을 소유한다.
    /// </summary>
    public sealed class PlayerTradeState
    {
        private readonly List<TradeInterestRecord> _interests = new();
        private readonly List<TradeHistoryRecord> _history = new();

        public TradePreference Preference { get; private set; } = TradePreference.Neutral;
        public bool IsOnTradeBlock { get; private set; }
        public int SeasonId { get; private set; }
        public int TradeDeadlineGameIndex { get; private set; }
        public int TradesThisSeason { get; private set; }
        public int LastTradeGameIndex { get; private set; } = -1;
        public ExpectedRole? CurrentTeamRole { get; private set; }
        public IReadOnlyList<TradeInterestRecord> Interests => _interests;
        public IReadOnlyList<TradeHistoryRecord> History => _history;

        public void BeginSeason(int seasonId, int tradeDeadlineGameIndex)
        {
            if (seasonId <= 0 || tradeDeadlineGameIndex <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonId));
            if (SeasonId == seasonId)
                return;

            SeasonId = seasonId;
            TradeDeadlineGameIndex = tradeDeadlineGameIndex;
            TradesThisSeason = 0;
            LastTradeGameIndex = -1;
            CurrentTeamRole = null;
            IsOnTradeBlock = false;
            _interests.Clear();
        }

        public void SetPreference(TradePreference preference)
        {
            Preference = preference;
            IsOnTradeBlock = preference == TradePreference.RequestTrade;
        }

        public void SetTradeBlock(bool isOnTradeBlock)
        {
            IsOnTradeBlock = isOnTradeBlock || Preference == TradePreference.RequestTrade;
        }

        public TradeInterestRecord? FindInterest(int teamId)
        {
            for (int index = 0; index < _interests.Count; index++)
            {
                if (_interests[index].InterestedTeamId == teamId)
                    return _interests[index];
            }
            return null;
        }

        public void UpsertInterest(TradeInterestRecord interest)
        {
            for (int index = 0; index < _interests.Count; index++)
            {
                if (_interests[index].InterestedTeamId != interest.InterestedTeamId)
                    continue;
                _interests[index] = interest;
                SortInterests();
                return;
            }
            _interests.Add(interest);
            SortInterests();
        }

        private void SortInterests()
        {
            _interests.Sort((left, right) =>
            {
                int score = right.BuyerInterestScore.CompareTo(left.BuyerInterestScore);
                return score != 0 ? score : left.InterestedTeamId.CompareTo(right.InterestedTeamId);
            });
        }

        public void RecordTrade(TradeHistoryRecord history)
        {
            _history.Add(history);
            TradesThisSeason++;
            LastTradeGameIndex = history.GameIndex;
            CurrentTeamRole = history.ProjectedRole;
            IsOnTradeBlock = false;
            _interests.Clear();
        }

        public void CloseUnresolvedInterests(int gameIndex)
        {
            for (int index = 0; index < _interests.Count; index++)
            {
                TradeInterestRecord interest = _interests[index];
                if (interest.Stage is TradeInterestStage.Completed or TradeInterestStage.Failed)
                    continue;
                _interests[index] = new TradeInterestRecord(
                    interest.InterestedTeamId,
                    interest.BuyerInterestScore,
                    interest.SellerInterestScore,
                    interest.ProjectedRole,
                    interest.ProjectedPlayingTime,
                    TradeInterestStage.Failed,
                    gameIndex);
            }
        }
    }
}
