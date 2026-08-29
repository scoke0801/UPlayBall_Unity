using System;

namespace Baseball.Game.Career
{
    public enum SeasonReviewStep
    {
        RegularSeasonIntro,
        RegularSeasonResult,
        PostseasonEntry,
        PostseasonInProgress,
        PostseasonRecap,
        PostseasonResult,
        Awards,
        SeasonSummary,
        IncomeSettlement,
        Finished
    }

    /// <summary>시즌 결산 화면의 진행 순서를 세이브 가능한 상태로 보관한다.</summary>
    public sealed class SeasonReviewState
    {
        public SeasonReviewStep Step { get; private set; } = SeasonReviewStep.RegularSeasonIntro;
        public int RevealedPostseasonGameCount { get; private set; }
        public int RevealedAwardCount { get; private set; }

        /// <summary>현재 장면 안의 공개 항목을 모두 소비한 뒤 다음 결산 장면으로 이동한다.</summary>
        public void Advance(SeasonReviewSnapshot snapshot)
        {
            if (Step == SeasonReviewStep.Finished)
                throw new InvalidOperationException("시즌 결산이 이미 끝났습니다.");
            switch (Step)
            {
                case SeasonReviewStep.RegularSeasonIntro:
                    Step = SeasonReviewStep.RegularSeasonResult;
                    break;
                case SeasonReviewStep.RegularSeasonResult:
                    Step = SeasonReviewStep.PostseasonEntry;
                    break;
                case SeasonReviewStep.PostseasonEntry:
                    Step = SeasonReviewStep.PostseasonInProgress;
                    break;
                case SeasonReviewStep.PostseasonRecap:
                    if (snapshot != null &&
                        RevealedPostseasonGameCount < snapshot.PlayerTeamPostseasonGames.Count)
                    {
                        RevealedPostseasonGameCount++;
                    }
                    else
                    {
                        Step = SeasonReviewStep.PostseasonResult;
                    }
                    break;
                case SeasonReviewStep.PostseasonResult:
                    Step = SeasonReviewStep.Awards;
                    break;
                case SeasonReviewStep.Awards:
                    if (snapshot != null && RevealedAwardCount < snapshot.PlayerAwards.Count)
                        RevealedAwardCount++;
                    else
                        Step = SeasonReviewStep.SeasonSummary;
                    break;
                default:
                    throw new InvalidOperationException("현재 장면은 일반 다음 진행으로 넘길 수 없습니다.");
            }
        }

        /// <summary>우승 구단 확정 뒤 대진과 내 구단 경기 결과를 순서대로 공개할 준비를 한다.</summary>
        public void PreparePostseasonRecap()
        {
            if (Step == SeasonReviewStep.Finished)
                throw new InvalidOperationException("시즌 결산이 이미 끝났습니다.");
            RevealedPostseasonGameCount = 0;
            RevealedAwardCount = 0;
            Step = SeasonReviewStep.PostseasonRecap;
        }

        /// <summary>연출 건너뛰기 뒤에도 보상 전 최종 요약에서 한 번 멈춘다.</summary>
        public void SkipToSeasonSummary()
        {
            if (Step == SeasonReviewStep.Finished)
                throw new InvalidOperationException("시즌 결산이 이미 끝났습니다.");
            Step = SeasonReviewStep.SeasonSummary;
        }

        /// <summary>정규시즌 공개 연출만 건너뛰고 실제 포스트시즌 진행 화면으로 이동한다.</summary>
        public void SkipToPostseasonInProgress()
        {
            if (Step is not (SeasonReviewStep.RegularSeasonIntro or
                SeasonReviewStep.RegularSeasonResult or
                SeasonReviewStep.PostseasonEntry))
            {
                throw new InvalidOperationException("정규시즌 종료 연출 중에만 건너뛸 수 있습니다.");
            }
            Step = SeasonReviewStep.PostseasonInProgress;
        }

        /// <summary>성장·수입 적용 뒤 오프시즌을 열기 전 마지막 확인 장면으로 이동한다.</summary>
        public void MarkIncomeSettlementReady()
        {
            if (Step != SeasonReviewStep.SeasonSummary)
                throw new InvalidOperationException("최종 시즌 요약을 확인한 뒤에만 정산할 수 있습니다.");
            Step = SeasonReviewStep.IncomeSettlement;
        }

        public void Complete()
        {
            Step = SeasonReviewStep.Finished;
        }
    }
}
