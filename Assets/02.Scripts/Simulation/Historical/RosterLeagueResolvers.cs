using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Historical
{
    /// <summary>ActiveRoster 구조 검증에서 발견한 문제 종류다.</summary>
    public enum RosterValidationIssueCode
    {
        TotalCount,
        HitterCount,
        StartingHitterCount,
        BenchHitterCount,
        PitcherCount,
        StartingPitcherCount,
        BullpenPitcherCount,
        SetupPitcherCount,
        CloserPitcherCount,
        ForeignPlayerCount,
        DuplicatePlayerPersonId,
        FixedRoleCount
    }

    /// <summary>ActiveRoster 구성 계약의 한 검증 실패를 설명한다.</summary>
    public readonly struct RosterValidationIssue
    {
        public RosterValidationIssue(
            RosterValidationIssueCode code,
            int expected,
            int actual,
            string context = "")
        {
            Code = code;
            Expected = expected;
            Actual = actual;
            Context = context ?? string.Empty;
        }

        public RosterValidationIssueCode Code { get; }
        public int Expected { get; }
        public int Actual { get; }
        public string Context { get; }
    }

    /// <summary>ActiveRoster 구성 검증 결과와 안정된 순서의 문제 목록이다.</summary>
    public sealed class RosterValidationResult
    {
        private readonly RosterValidationIssue[] _issues;

        public RosterValidationResult(IReadOnlyList<RosterValidationIssue> issues)
        {
            if (issues == null)
                throw new ArgumentNullException(nameof(issues));
            _issues = new RosterValidationIssue[issues.Count];
            for (int index = 0; index < issues.Count; index++)
                _issues[index] = issues[index];
        }

        public bool IsValid => _issues.Length == 0;
        public IReadOnlyList<RosterValidationIssue> Issues => _issues;
    }

    /// <summary>포지션 적합성과 무관하게 공통 25인 구성만 검증한다.</summary>
    public sealed class ActiveRosterValidator
    {
        private readonly ActiveRosterCompositionRule _rule;

        public ActiveRosterValidator(ActiveRosterCompositionRule rule = null)
        {
            _rule = rule ?? ActiveRosterCompositionRule.Standard;
        }

        /// <summary>25인·역할별 인원·외국인·PlayerPerson 중복 계약을 검증한다.</summary>
        public RosterValidationResult Validate(CurrentRosterState roster)
        {
            if (roster == null)
                throw new ArgumentNullException(nameof(roster));

            var issues = new List<RosterValidationIssue>();
            var roleCounts = new int[Enum.GetValues(typeof(ActiveRosterRole)).Length];
            var playerPersonIds = new HashSet<string>(StringComparer.Ordinal);
            int hitters = 0;
            int startingHitters = 0;
            int benchHitters = 0;
            int pitchers = 0;
            int startingPitchers = 0;
            int bullpenPitchers = 0;
            int setupPitchers = 0;
            int closerPitchers = 0;
            int foreignPlayers = 0;

            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                int roleIndex = (int)entry.Role;
                if (roleIndex >= 0 && roleIndex < roleCounts.Length)
                    roleCounts[roleIndex]++;

                if (_rule.IsHitterRole(entry.Role))
                    hitters++;
                if (_rule.IsStartingHitterRole(entry.Role))
                    startingHitters++;
                if (entry.Role == ActiveRosterRole.BenchHitter)
                    benchHitters++;
                if (_rule.IsPitcherRole(entry.Role))
                    pitchers++;
                if (_rule.IsStartingPitcherRole(entry.Role))
                    startingPitchers++;
                if (_rule.IsBullpenRole(entry.Role))
                    bullpenPitchers++;
                if (entry.Role == ActiveRosterRole.Setup)
                    setupPitchers++;
                if (entry.Role == ActiveRosterRole.Closer)
                    closerPitchers++;
                if (entry.RegistrationType == RegistrationType.Foreign)
                    foreignPlayers++;

                if (!playerPersonIds.Add(entry.PlayerPersonId))
                {
                    issues.Add(new RosterValidationIssue(
                        RosterValidationIssueCode.DuplicatePlayerPersonId,
                        1,
                        2,
                        entry.PlayerPersonId));
                }
            }

            AddCountIssue(issues, RosterValidationIssueCode.TotalCount,
                ActiveRosterCompositionRule.ActiveRosterSize, roster.Entries.Count);
            AddCountIssue(issues, RosterValidationIssueCode.HitterCount,
                ActiveRosterCompositionRule.HitterCount, hitters);
            AddCountIssue(issues, RosterValidationIssueCode.StartingHitterCount,
                ActiveRosterCompositionRule.StartingHitterCount, startingHitters);
            AddCountIssue(issues, RosterValidationIssueCode.BenchHitterCount,
                ActiveRosterCompositionRule.BenchHitterCount, benchHitters);
            AddCountIssue(issues, RosterValidationIssueCode.PitcherCount,
                ActiveRosterCompositionRule.PitcherCount, pitchers);
            AddCountIssue(issues, RosterValidationIssueCode.StartingPitcherCount,
                ActiveRosterCompositionRule.StartingPitcherCount, startingPitchers);
            AddCountIssue(issues, RosterValidationIssueCode.BullpenPitcherCount,
                ActiveRosterCompositionRule.BullpenPitcherCount, bullpenPitchers);
            AddCountIssue(issues, RosterValidationIssueCode.SetupPitcherCount,
                ActiveRosterCompositionRule.SetupPitcherCount, setupPitchers);
            AddCountIssue(issues, RosterValidationIssueCode.CloserPitcherCount,
                ActiveRosterCompositionRule.CloserPitcherCount, closerPitchers);

            if (foreignPlayers > ActiveRosterCompositionRule.MaxForeignPlayers)
            {
                issues.Add(new RosterValidationIssue(
                    RosterValidationIssueCode.ForeignPlayerCount,
                    ActiveRosterCompositionRule.MaxForeignPlayers,
                    foreignPlayers));
            }

            ValidateFixedRoleCounts(roleCounts, issues);
            return new RosterValidationResult(issues);
        }

        private static void ValidateFixedRoleCounts(
            IReadOnlyList<int> roleCounts,
            ICollection<RosterValidationIssue> issues)
        {
            for (int roleIndex = 0; roleIndex < roleCounts.Count; roleIndex++)
            {
                ActiveRosterRole role = (ActiveRosterRole)roleIndex;
                if (role == ActiveRosterRole.BenchHitter)
                    continue;
                AddCountIssue(
                    issues,
                    RosterValidationIssueCode.FixedRoleCount,
                    1,
                    roleCounts[roleIndex],
                    role.ToString());
            }
        }

        private static void AddCountIssue(
            ICollection<RosterValidationIssue> issues,
            RosterValidationIssueCode code,
            int expected,
            int actual,
            string context = "")
        {
            if (actual != expected)
                issues.Add(new RosterValidationIssue(code, expected, actual, context));
        }
    }

    /// <summary>허용된 야수 비주포지션과 투수 역할 불일치의 경기 비용을 계산한다.</summary>
    public sealed class PositionAssignmentPenaltyResolver
    {
        /// <summary>본래 야수 포지션과 실제 수비 슬롯을 비교하며 DH는 항상 무패널티 처리한다.</summary>
        public PositionAssignmentPenalty EvaluateHitter(
            PlayerPosition naturalPosition,
            PlayerPosition assignedPosition,
            PositionAssignmentRule rule)
        {
            if (!IsHitterPosition(naturalPosition))
                throw new ArgumentException("야수의 본래 포지션이 필요합니다.", nameof(naturalPosition));
            if (!IsHitterPosition(assignedPosition))
                throw new ArgumentException("야수 수비 슬롯 또는 DH가 필요합니다.", nameof(assignedPosition));
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            if (assignedPosition == PlayerPosition.DesignatedHitter || naturalPosition == assignedPosition)
                return PositionAssignmentPenalty.None;

            OffPositionPenaltyDefinition penalty = rule.OffPositionHitterPenalty;
            return new PositionAssignmentPenalty(
                true,
                penalty.ConditionPenalty,
                penalty.FieldingErrorProbabilityMultiplier);
        }

        /// <summary>기존 Player의 보조 포지션 자격까지 포함해 실제 수비 슬롯 비용을 계산한다.</summary>
        public PositionAssignmentPenalty EvaluateHitter(
            Player player,
            PlayerPosition assignedPosition,
            PositionAssignmentRule rule)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (assignedPosition == PlayerPosition.DesignatedHitter || player.PrimaryPosition == assignedPosition)
                return PositionAssignmentPenalty.None;

            for (int index = 0; index < player.SecondaryPositions.Count; index++)
            {
                if (player.SecondaryPositions[index].Position == assignedPosition)
                    return PositionAssignmentPenalty.None;
            }

            return EvaluateHitter(player.PrimaryPosition, assignedPosition, rule);
        }

        /// <summary>본래 투수 역할과 실제 기용 역할이 다르면 컨디션 비용을 반환한다.</summary>
        public PositionAssignmentPenalty EvaluatePitcher(
            PitcherRole naturalRole,
            PitcherRole assignedRole,
            PositionAssignmentRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));
            if (naturalRole == assignedRole)
                return PositionAssignmentPenalty.None;

            return new PositionAssignmentPenalty(
                true,
                rule.PitcherRoleMismatchPenalty.ConditionPenalty,
                1d);
        }

        private static bool IsHitterPosition(PlayerPosition position)
        {
            return position >= PlayerPosition.Catcher && position <= PlayerPosition.DesignatedHitter;
        }
    }

    /// <summary>Policy의 경기 구간과 역할 우선순위로 불펜 후보를 결정론적으로 선택한다.</summary>
    public sealed class BullpenUsageResolver
    {
        /// <summary>사용 가능한 후보 중 구간별 역할 우선순위가 가장 높은 선수를 반환한다.</summary>
        public BullpenCandidateState? SelectCandidate(
            BullpenUsagePolicy policy,
            BullpenSelectionContext context,
            IReadOnlyList<BullpenCandidateState> candidates)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            for (int bandIndex = 0; bandIndex < policy.Bands.Count; bandIndex++)
            {
                BullpenUsageBand band = policy.Bands[bandIndex];
                if (!band.Matches(context))
                    continue;

                for (int priorityIndex = 0; priorityIndex < band.RolePriority.Count; priorityIndex++)
                {
                    BullpenCandidateState? candidate = FindStableCandidate(
                        candidates,
                        band.RolePriority[priorityIndex],
                        band.MinimumCondition);
                    if (candidate.HasValue)
                        return candidate;
                }

                return null;
            }

            return null;
        }

        private static BullpenCandidateState? FindStableCandidate(
            IReadOnlyList<BullpenCandidateState> candidates,
            ActiveRosterRole role,
            int minimumCondition)
        {
            BullpenCandidateState? selected = null;
            for (int index = 0; index < candidates.Count; index++)
            {
                BullpenCandidateState candidate = candidates[index];
                if (candidate.BullpenRole != role || !candidate.IsAvailable || candidate.Condition < minimumCondition)
                    continue;
                if (!selected.HasValue ||
                    string.CompareOrdinal(candidate.PlayerSeasonId, selected.Value.PlayerSeasonId) < 0)
                {
                    selected = candidate;
                }
            }
            return selected;
        }
    }

    /// <summary>시즌 승률과 데이터화된 단계별 기준으로 승격·유지·강등을 판정한다.</summary>
    public sealed class LeaguePromotionResolver
    {
        /// <summary>현재 등급과 시즌 성적을 받아 다음 시즌 등급을 반환한다.</summary>
        public LeagueGrade ResolveNextGrade(
            LeagueGrade currentGrade,
            int wins,
            int losses,
            LeagueDefinition definition)
        {
            if (wins < 0 || losses < 0)
                throw new ArgumentOutOfRangeException(nameof(wins));
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            LeagueGradeRule rule = definition.GetRule(currentGrade);
            int games = wins + losses;
            if (games < rule.MinimumGames)
                return currentGrade;

            double winningPercentage = games == 0 ? 0d : (double)wins / games;
            if (rule.PromotionWinningPercentage.HasValue &&
                winningPercentage >= rule.PromotionWinningPercentage.Value)
            {
                return (LeagueGrade)((int)currentGrade + 1);
            }

            if (rule.RelegationWinningPercentage.HasValue &&
                winningPercentage <= rule.RelegationWinningPercentage.Value)
            {
                return (LeagueGrade)((int)currentGrade - 1);
            }

            return currentGrade;
        }
    }

    /// <summary>개별 TeamSeason의 Club DNA를 최대 ±5 범위에서 갱신한다.</summary>
    public sealed class TeamSeasonClubStateResolver
    {
        /// <summary>기존 DNA·최근 성적·감독 철학을 결합한 새 독립 상태를 반환한다.</summary>
        public TeamSeasonClubState ResolveNextSeason(
            TeamSeasonClubState current,
            ClubDnaRatings recentPerformance,
            ClubDnaRatings managerPhilosophy,
            ClubDnaUpdatePolicy policy)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));
            if (recentPerformance == null)
                throw new ArgumentNullException(nameof(recentPerformance));
            if (managerPhilosophy == null)
                throw new ArgumentNullException(nameof(managerPhilosophy));
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            ClubDnaRatings ratings = current.Ratings;
            var updated = new ClubDnaRatings(
                Blend(ratings.Contact, recentPerformance.Contact, managerPhilosophy.Contact, policy),
                Blend(ratings.Power, recentPerformance.Power, managerPhilosophy.Power, policy),
                Blend(ratings.Running, recentPerformance.Running, managerPhilosophy.Running, policy),
                Blend(ratings.Defense, recentPerformance.Defense, managerPhilosophy.Defense, policy),
                Blend(ratings.Rotation, recentPerformance.Rotation, managerPhilosophy.Rotation, policy),
                Blend(ratings.Bullpen, recentPerformance.Bullpen, managerPhilosophy.Bullpen, policy),
                Blend(ratings.Development, recentPerformance.Development, managerPhilosophy.Development, policy),
                Blend(ratings.Experience, recentPerformance.Experience, managerPhilosophy.Experience, policy));
            return new TeamSeasonClubState(current.TeamSeasonKey, updated);
        }

        private static double Blend(
            double current,
            double recentPerformance,
            double managerPhilosophy,
            ClubDnaUpdatePolicy policy)
        {
            double target = current * policy.CurrentWeight +
                            recentPerformance * policy.RecentPerformanceWeight +
                            managerPhilosophy * policy.ManagerPhilosophyWeight;
            double minimum = current - policy.MaximumSeasonChange;
            double maximum = current + policy.MaximumSeasonChange;
            if (target < minimum) return minimum;
            if (target > maximum) return maximum;
            return target;
        }
    }

    /// <summary>TeamSeason별 DNA를 안정된 키 순서로 평균해 Franchise 정체성을 만든다.</summary>
    public sealed class FranchiseIdentityResolver
    {
        /// <summary>입력 열거 순서와 무관하게 같은 Franchise 장기 평균을 반환한다.</summary>
        public FranchiseIdentityProfile Resolve(
            string franchiseId,
            IReadOnlyList<TeamSeasonClubState> teamSeasons)
        {
            if (string.IsNullOrWhiteSpace(franchiseId))
                throw new ArgumentException("FranchiseId는 비어 있을 수 없습니다.", nameof(franchiseId));
            if (teamSeasons == null || teamSeasons.Count == 0)
                throw new ArgumentException("하나 이상의 TeamSeason Club DNA가 필요합니다.", nameof(teamSeasons));

            var ordered = new TeamSeasonClubState[teamSeasons.Count];
            for (int index = 0; index < teamSeasons.Count; index++)
                ordered[index] = teamSeasons[index] ?? throw new ArgumentException("null Club DNA 상태가 있습니다.", nameof(teamSeasons));
            Array.Sort(ordered, CompareTeamSeasonKey);

            double contact = 0d;
            double power = 0d;
            double running = 0d;
            double defense = 0d;
            double rotation = 0d;
            double bullpen = 0d;
            double development = 0d;
            double experience = 0d;
            for (int index = 0; index < ordered.Length; index++)
            {
                ClubDnaRatings ratings = ordered[index].Ratings;
                contact += ratings.Contact;
                power += ratings.Power;
                running += ratings.Running;
                defense += ratings.Defense;
                rotation += ratings.Rotation;
                bullpen += ratings.Bullpen;
                development += ratings.Development;
                experience += ratings.Experience;
            }

            double count = ordered.Length;
            return new FranchiseIdentityProfile(
                franchiseId,
                new ClubDnaRatings(
                    contact / count,
                    power / count,
                    running / count,
                    defense / count,
                    rotation / count,
                    bullpen / count,
                    development / count,
                    experience / count));
        }

        private static int CompareTeamSeasonKey(TeamSeasonClubState left, TeamSeasonClubState right)
        {
            return string.CompareOrdinal(left.TeamSeasonKey, right.TeamSeasonKey);
        }
    }
}
