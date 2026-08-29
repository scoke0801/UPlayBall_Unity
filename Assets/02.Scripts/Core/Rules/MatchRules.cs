using System;

namespace Baseball.Core.Rules
{
    /// <summary>
    /// 저장 데이터와 진단 결과가 사용하는 경기 시뮬레이션 규칙 세대를 표시한다.
    /// </summary>
    public enum SimulationRulesVersion
    {
        DetailedV2 = 2
    }

    /// <summary>
    /// 정규 이닝 종료 뒤 동점 경기의 처리 방식을 정의한다.
    /// </summary>
    public enum ExtraInningPolicy
    {
        DrawAtLimit = 0,
        ContinueUntilWinner = 1,
        AutomaticRunnerUntilWinner = 2
    }

    /// <summary>
    /// 리그와 포스트시즌이 경기 시뮬레이터에 전달하는 순수 C# 규칙 묶음이다.
    /// </summary>
    public sealed class MatchRules
    {
        public MatchRules(
            int regulationInnings,
            int maximumRegulationExtraInnings,
            ExtraInningPolicy extraInningPolicy,
            int automaticRunnerStartInning,
            bool usesDesignatedHitter,
            int intentionalWalkPitchCount)
        {
            if (regulationInnings <= 0)
                throw new ArgumentOutOfRangeException(nameof(regulationInnings));
            if (maximumRegulationExtraInnings < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumRegulationExtraInnings));
            if (automaticRunnerStartInning <= regulationInnings)
                throw new ArgumentOutOfRangeException(nameof(automaticRunnerStartInning));
            if (intentionalWalkPitchCount < 0)
                throw new ArgumentOutOfRangeException(nameof(intentionalWalkPitchCount));

            RegulationInnings = regulationInnings;
            MaximumRegulationExtraInnings = maximumRegulationExtraInnings;
            ExtraInningPolicy = extraInningPolicy;
            AutomaticRunnerStartInning = automaticRunnerStartInning;
            UsesDesignatedHitter = usesDesignatedHitter;
            IntentionalWalkPitchCount = intentionalWalkPitchCount;
        }

        public int RegulationInnings { get; }
        public int MaximumRegulationExtraInnings { get; }
        public ExtraInningPolicy ExtraInningPolicy { get; }
        public int AutomaticRunnerStartInning { get; }
        public bool UsesDesignatedHitter { get; }
        public int IntentionalWalkPitchCount { get; }
        public int DrawInningLimit => RegulationInnings + MaximumRegulationExtraInnings;

        /// <summary>
        /// 정규시즌은 12회 동점 무승부, 포스트시즌은 승자가 날 때까지 진행하는 기본 규칙을 만든다.
        /// </summary>
        public static MatchRules CreateDefault(bool requiresWinner)
        {
            return new MatchRules(
                BaseballRules.RegulationInnings,
                maximumRegulationExtraInnings: 3,
                requiresWinner ? ExtraInningPolicy.ContinueUntilWinner : ExtraInningPolicy.DrawAtLimit,
                automaticRunnerStartInning: 13,
                usesDesignatedHitter: true,
                intentionalWalkPitchCount: 0);
        }
    }
}
