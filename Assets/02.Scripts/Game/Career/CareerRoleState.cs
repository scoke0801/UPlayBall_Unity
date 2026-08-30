using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>감독 역할 평가가 발생한 시점을 구분한다.</summary>
    public enum CareerRoleEvaluationTrigger
    {
        SpringCamp,
        InjuryReturn,
        RegularSeason20,
        RegularSeason40,
        TeamChange
    }

    /// <summary>한 번의 역할 권고와 실제 적용 결과를 설명 데이터와 함께 보존한다.</summary>
    public sealed class CareerRoleEvaluationRecord
    {
        public CareerRoleEvaluationRecord(
            int seasonId,
            int round,
            CareerRoleEvaluationTrigger trigger,
            ExpectedRole previousRole,
            ExpectedRole recommendedRole,
            ExpectedRole appliedRole,
            double playerScore,
            double competitorScore,
            bool wasCooldownProtected,
            bool wasInjuryReturnProtected,
            DecisionExplanation explanation)
        {
            SeasonId = seasonId;
            Round = round;
            Trigger = trigger;
            PreviousRole = previousRole;
            RecommendedRole = recommendedRole;
            AppliedRole = appliedRole;
            PlayerScore = playerScore;
            CompetitorScore = competitorScore;
            WasCooldownProtected = wasCooldownProtected;
            WasInjuryReturnProtected = wasInjuryReturnProtected;
            Explanation = explanation;
        }

        public int SeasonId { get; }
        public int Round { get; }
        public CareerRoleEvaluationTrigger Trigger { get; }
        public ExpectedRole PreviousRole { get; }
        public ExpectedRole RecommendedRole { get; }
        public ExpectedRole AppliedRole { get; }
        public double PlayerScore { get; }
        public double CompetitorScore { get; }
        public double Margin => PlayerScore - CompetitorScore;
        public bool WasCooldownProtected { get; }
        public bool WasInjuryReturnProtected { get; }
        public DecisionExplanation Explanation { get; }
    }

    /// <summary>시즌 역할, 재평가 시점, 변경 쿨다운과 부상 복귀 보호를 세이브 상태로 소유한다.</summary>
    public sealed class CareerRoleState
    {
        public const int RoleChangeCooldownRounds = 10;
        public const int InjuryReturnObservationRound = 10;
        public const int FirstRegularSeasonEvaluationRound = 20;
        public const int SecondRegularSeasonEvaluationRound = 40;

        private readonly List<CareerRoleEvaluationRecord> _history = new();

        public int SeasonId { get; private set; }
        public ExpectedRole? ActiveRole { get; private set; }
        public int LastRoleChangeRound { get; private set; } = -RoleChangeCooldownRounds;
        public bool IsInjuryReturnObservationActive { get; private set; }
        public IReadOnlyList<CareerRoleEvaluationRecord> History => _history;
        public CareerRoleEvaluationRecord LatestEvaluation =>
            _history.Count == 0 ? null : _history[^1];

        /// <summary>계약 역할을 기준으로 새 시즌 역할 평가 상태를 연다.</summary>
        public void BeginSeason(
            int seasonId,
            ExpectedRole contractedRole,
            bool requiresInjuryReturnObservation)
        {
            if (seasonId <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonId));
            SeasonId = seasonId;
            ActiveRole = contractedRole;
            LastRoleChangeRound = -RoleChangeCooldownRounds;
            IsInjuryReturnObservationActive = requiresInjuryReturnObservation;
        }

        public bool ShouldEvaluateAfterRound(int seasonId, int round)
        {
            if (seasonId != SeasonId || round <= 0)
                return false;
            if (IsInjuryReturnObservationActive && round == InjuryReturnObservationRound)
                return true;
            return round is FirstRegularSeasonEvaluationRound or SecondRegularSeasonEvaluationRound;
        }

        public CareerRoleEvaluationTrigger ResolveTrigger(int round)
        {
            if (IsInjuryReturnObservationActive && round == InjuryReturnObservationRound)
                return CareerRoleEvaluationTrigger.InjuryReturn;
            return round == FirstRegularSeasonEvaluationRound
                ? CareerRoleEvaluationTrigger.RegularSeason20
                : CareerRoleEvaluationTrigger.RegularSeason40;
        }

        /// <summary>권고 역할을 쿨다운·복귀 보호와 함께 적용하고 계산 근거를 남긴다.</summary>
        public CareerRoleEvaluationRecord ApplyEvaluation(
            int seasonId,
            int round,
            CareerRoleEvaluationTrigger trigger,
            ExpectedRole recommendedRole,
            double playerScore,
            double competitorScore,
            DecisionExplanation explanation)
        {
            if (seasonId != SeasonId || !ActiveRole.HasValue)
                throw new InvalidOperationException("현재 시즌 역할 상태가 열려 있지 않습니다.");

            ExpectedRole previous = ActiveRole.Value;
            bool injuryProtection = IsInjuryReturnObservationActive &&
                                    (trigger is CareerRoleEvaluationTrigger.SpringCamp or
                                        CareerRoleEvaluationTrigger.InjuryReturn) &&
                                    (int)recommendedRole < (int)previous;
            bool cooldownProtection = round > 0 &&
                                      recommendedRole != previous &&
                                      round - LastRoleChangeRound < RoleChangeCooldownRounds;
            ExpectedRole applied = injuryProtection || cooldownProtection
                ? previous
                : recommendedRole;
            if (applied != previous)
                LastRoleChangeRound = round;
            ActiveRole = applied;
            if (trigger == CareerRoleEvaluationTrigger.InjuryReturn)
                IsInjuryReturnObservationActive = false;

            var record = new CareerRoleEvaluationRecord(
                seasonId,
                round,
                trigger,
                previous,
                recommendedRole,
                applied,
                playerScore,
                competitorScore,
                cooldownProtection,
                injuryProtection,
                explanation);
            _history.Add(record);
            return record;
        }

        /// <summary>트레이드 직후 새 구단의 제시 역할로 상태를 재설정한다.</summary>
        public void ApplyTeamChange(int seasonId, int round, ExpectedRole projectedRole)
        {
            SeasonId = seasonId;
            ExpectedRole previous = ActiveRole ?? projectedRole;
            ActiveRole = projectedRole;
            LastRoleChangeRound = round;
            IsInjuryReturnObservationActive = false;
            _history.Add(new CareerRoleEvaluationRecord(
                seasonId,
                round,
                CareerRoleEvaluationTrigger.TeamChange,
                previous,
                projectedRole,
                projectedRole,
                0d,
                0d,
                false,
                false,
                explanation: null));
        }

        /// <summary>구버전 세이브는 현재 계약 역할을 다음 재평가까지 안전한 기준값으로 사용한다.</summary>
        public void MigrateLegacySeason(int seasonId, ExpectedRole currentRole)
        {
            if (ActiveRole.HasValue)
                return;
            BeginSeason(seasonId, currentRole, requiresInjuryReturnObservation: false);
        }
    }
}
