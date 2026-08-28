using System;

namespace Baseball.Game.Career
{
    public enum SeasonReviewStep
    {
        PostseasonResult,
        Awards,
        PlayerSeasonSummary,
        IncomeSettlement,
        ContractResult,
        Finished
    }

    /// <summary>시즌 결산 화면의 진행 순서를 세이브 가능한 상태로 보관한다.</summary>
    public sealed class SeasonReviewState
    {
        public SeasonReviewStep Step { get; private set; } = SeasonReviewStep.PostseasonResult;

        public void Advance()
        {
            if (Step == SeasonReviewStep.Finished)
                throw new InvalidOperationException("시즌 결산이 이미 끝났습니다.");
            Step++;
        }

        public void Complete()
        {
            Step = SeasonReviewStep.Finished;
        }
    }
}
