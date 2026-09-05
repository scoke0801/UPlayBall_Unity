using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Core.Historical
{
    /// <summary>공통 ActiveRoster에서 한 선수가 점유하는 고정 슬롯을 구분한다.</summary>
    public enum ActiveRosterRole
    {
        StartingCatcher,
        StartingFirstBase,
        StartingSecondBase,
        StartingThirdBase,
        StartingShortstop,
        StartingLeftField,
        StartingCenterField,
        StartingRightField,
        StartingDesignatedHitter,
        BenchHitter,
        StartingPitcher1,
        StartingPitcher2,
        StartingPitcher3,
        StartingPitcher4,
        StartingPitcher5,
        Bullpen1,
        Bullpen2,
        Bullpen3,
        Bullpen4,
        Setup,
        Closer
    }

    /// <summary>선수 커리어와 구단주 모드가 공유하는 25인 1군 구성 계약이다.</summary>
    public sealed class ActiveRosterCompositionRule
    {
        public const int ActiveRosterSize = 25;
        public const int HitterCount = 14;
        public const int StartingHitterCount = 9;
        public const int BenchHitterCount = 5;
        public const int PitcherCount = 11;
        public const int StartingPitcherCount = 5;
        public const int BullpenPitcherCount = 4;
        public const int SetupPitcherCount = 1;
        public const int CloserPitcherCount = 1;
        public const int MaxForeignPlayers = 3;

        private ActiveRosterCompositionRule()
        {
        }

        public static ActiveRosterCompositionRule Standard { get; } = new ActiveRosterCompositionRule();

        /// <summary>주어진 슬롯이 야수 14인 영역에 속하는지 반환한다.</summary>
        public bool IsHitterRole(ActiveRosterRole role)
        {
            return IsStartingHitterRole(role) || role == ActiveRosterRole.BenchHitter;
        }

        /// <summary>주어진 슬롯이 주전 야수 9인 영역에 속하는지 반환한다.</summary>
        public bool IsStartingHitterRole(ActiveRosterRole role)
        {
            return role >= ActiveRosterRole.StartingCatcher &&
                   role <= ActiveRosterRole.StartingDesignatedHitter;
        }

        /// <summary>주어진 슬롯이 투수 11인 영역에 속하는지 반환한다.</summary>
        public bool IsPitcherRole(ActiveRosterRole role)
        {
            return role >= ActiveRosterRole.StartingPitcher1 && role <= ActiveRosterRole.Closer;
        }

        /// <summary>주어진 슬롯이 선발투수 5인 영역에 속하는지 반환한다.</summary>
        public bool IsStartingPitcherRole(ActiveRosterRole role)
        {
            return role >= ActiveRosterRole.StartingPitcher1 && role <= ActiveRosterRole.StartingPitcher5;
        }

        /// <summary>주어진 슬롯이 일반 불펜 1~4 영역에 속하는지 반환한다.</summary>
        public bool IsBullpenRole(ActiveRosterRole role)
        {
            return role >= ActiveRosterRole.Bullpen1 && role <= ActiveRosterRole.Bullpen4;
        }

        /// <summary>주전 야수 슬롯의 실제 수비 포지션을 반환한다.</summary>
        public PlayerPosition GetAssignedPosition(ActiveRosterRole role)
        {
            switch (role)
            {
                case ActiveRosterRole.StartingCatcher: return PlayerPosition.Catcher;
                case ActiveRosterRole.StartingFirstBase: return PlayerPosition.FirstBase;
                case ActiveRosterRole.StartingSecondBase: return PlayerPosition.SecondBase;
                case ActiveRosterRole.StartingThirdBase: return PlayerPosition.ThirdBase;
                case ActiveRosterRole.StartingShortstop: return PlayerPosition.Shortstop;
                case ActiveRosterRole.StartingLeftField: return PlayerPosition.LeftField;
                case ActiveRosterRole.StartingCenterField: return PlayerPosition.CenterField;
                case ActiveRosterRole.StartingRightField: return PlayerPosition.RightField;
                case ActiveRosterRole.StartingDesignatedHitter: return PlayerPosition.DesignatedHitter;
                default: throw new ArgumentException("주전 야수 슬롯만 수비 포지션을 가집니다.", nameof(role));
            }
        }

        /// <summary>투수 슬롯이 경기에서 맡는 투수 역할을 반환한다.</summary>
        public PitcherRole GetAssignedPitcherRole(ActiveRosterRole role)
        {
            if (IsStartingPitcherRole(role))
                return PitcherRole.Starter;
            if (IsBullpenRole(role))
                return PitcherRole.MiddleRelief;
            if (role == ActiveRosterRole.Setup)
                return PitcherRole.Setup;
            if (role == ActiveRosterRole.Closer)
                return PitcherRole.Closer;
            throw new ArgumentException("투수 슬롯만 투수 역할을 가집니다.", nameof(role));
        }
    }

    /// <summary>현재 1군의 카드·선수 인물·등록 슬롯을 연결하는 저장 가능 항목이다.</summary>
    public sealed class ActiveRosterEntry
    {
        public ActiveRosterEntry(
            string cardId,
            string playerSeasonId,
            string playerPersonId,
            RegistrationType registrationType,
            ActiveRosterRole role)
        {
            CardId = RequireId(cardId, nameof(cardId));
            PlayerSeasonId = RequireId(playerSeasonId, nameof(playerSeasonId));
            PlayerPersonId = RequireId(playerPersonId, nameof(playerPersonId));
            if (!Enum.IsDefined(typeof(ActiveRosterRole), role))
                throw new ArgumentOutOfRangeException(nameof(role));
            RegistrationType = registrationType;
            Role = role;
        }

        public string CardId { get; }
        public string PlayerSeasonId { get; }
        public string PlayerPersonId { get; }
        public RegistrationType RegistrationType { get; }
        public ActiveRosterRole Role { get; }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>게임 진행 중 변하는 구단별 1군 카드 배치를 보관한다.</summary>
    public sealed class CurrentRosterState
    {
        private readonly ActiveRosterEntry[] _entries;

        public CurrentRosterState(string teamSeasonKey, IReadOnlyList<ActiveRosterEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            TeamSeasonKey = teamSeasonKey.Trim();
            _entries = new ActiveRosterEntry[entries.Count];
            for (int index = 0; index < entries.Count; index++)
                _entries[index] = entries[index] ?? throw new ArgumentException("null 로스터 항목이 있습니다.", nameof(entries));
        }

        public string TeamSeasonKey { get; }
        public IReadOnlyList<ActiveRosterEntry> Entries => _entries;
    }

    /// <summary>야수의 비주포지션 기용에 적용할 데이터 기반 경기 비용이다.</summary>
    public sealed class OffPositionPenaltyDefinition
    {
        public OffPositionPenaltyDefinition(int conditionPenalty, double fieldingErrorProbabilityMultiplier)
        {
            if (conditionPenalty < 0)
                throw new ArgumentOutOfRangeException(nameof(conditionPenalty));
            if (fieldingErrorProbabilityMultiplier < 1d || double.IsNaN(fieldingErrorProbabilityMultiplier))
                throw new ArgumentOutOfRangeException(nameof(fieldingErrorProbabilityMultiplier));

            ConditionPenalty = conditionPenalty;
            FieldingErrorProbabilityMultiplier = fieldingErrorProbabilityMultiplier;
        }

        public int ConditionPenalty { get; }
        public double FieldingErrorProbabilityMultiplier { get; }
    }

    /// <summary>투수의 본래 역할과 실제 역할이 다를 때 적용할 데이터 기반 비용이다.</summary>
    public sealed class PitcherRoleMismatchPenaltyDefinition
    {
        public PitcherRoleMismatchPenaltyDefinition(
            int conditionPenalty,
            double mediumConfidenceMultiplier = 0.65d,
            double lowConfidenceMultiplier = 0.25d)
        {
            if (conditionPenalty < 0)
                throw new ArgumentOutOfRangeException(nameof(conditionPenalty));
            if (mediumConfidenceMultiplier < 0d || mediumConfidenceMultiplier > 1d ||
                double.IsNaN(mediumConfidenceMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(mediumConfidenceMultiplier));
            }
            if (lowConfidenceMultiplier < 0d || lowConfidenceMultiplier > mediumConfidenceMultiplier ||
                double.IsNaN(lowConfidenceMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(lowConfidenceMultiplier));
            }
            ConditionPenalty = conditionPenalty;
            MediumConfidenceMultiplier = mediumConfidenceMultiplier;
            LowConfidenceMultiplier = lowConfidenceMultiplier;
        }

        public int ConditionPenalty { get; }
        public double MediumConfidenceMultiplier { get; }
        public double LowConfidenceMultiplier { get; }

        /// <summary>역할 근거가 불완전할수록 비본래 역할 비용을 완화하고 가장 가까운 정수로 반올림한다.</summary>
        public int GetConditionPenalty(PitcherRoleConfidence confidence)
        {
            double multiplier = confidence switch
            {
                PitcherRoleConfidence.Low => LowConfidenceMultiplier,
                PitcherRoleConfidence.Medium => MediumConfidenceMultiplier,
                _ => 1d
            };
            return (int)Math.Round(ConditionPenalty * multiplier, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>포지션 불일치를 거부하지 않고 Simulation 비용으로 바꾸는 공통 규칙이다.</summary>
    public sealed class PositionAssignmentRule
    {
        public PositionAssignmentRule(
            OffPositionPenaltyDefinition offPositionHitterPenalty,
            PitcherRoleMismatchPenaltyDefinition pitcherRoleMismatchPenalty)
        {
            OffPositionHitterPenalty = offPositionHitterPenalty ??
                throw new ArgumentNullException(nameof(offPositionHitterPenalty));
            PitcherRoleMismatchPenalty = pitcherRoleMismatchPenalty ??
                throw new ArgumentNullException(nameof(pitcherRoleMismatchPenalty));
        }

        public OffPositionPenaltyDefinition OffPositionHitterPenalty { get; }
        public PitcherRoleMismatchPenaltyDefinition PitcherRoleMismatchPenalty { get; }
    }

    /// <summary>허용된 포지션 배치가 경기 판정에 더할 컨디션·수비 비용이다.</summary>
    public readonly struct PositionAssignmentPenalty
    {
        public PositionAssignmentPenalty(
            bool isOffPosition,
            int conditionPenalty,
            double fieldingErrorProbabilityMultiplier)
        {
            if (conditionPenalty < 0)
                throw new ArgumentOutOfRangeException(nameof(conditionPenalty));
            if (fieldingErrorProbabilityMultiplier < 1d || double.IsNaN(fieldingErrorProbabilityMultiplier))
                throw new ArgumentOutOfRangeException(nameof(fieldingErrorProbabilityMultiplier));
            IsOffPosition = isOffPosition;
            ConditionPenalty = conditionPenalty;
            FieldingErrorProbabilityMultiplier = fieldingErrorProbabilityMultiplier;
        }

        public bool IsAllowed => true;
        public bool IsOffPosition { get; }
        public int ConditionPenalty { get; }
        public double FieldingErrorProbabilityMultiplier { get; }

        public static PositionAssignmentPenalty None => new PositionAssignmentPenalty(false, 0, 1d);
    }

    /// <summary>불펜 후보를 평가할 현재 경기 상황이다.</summary>
    public readonly struct BullpenSelectionContext
    {
        public BullpenSelectionContext(int inning, int runDifferential, double leverageIndex)
        {
            if (inning <= 0)
                throw new ArgumentOutOfRangeException(nameof(inning));
            if (leverageIndex < 0d || double.IsNaN(leverageIndex))
                throw new ArgumentOutOfRangeException(nameof(leverageIndex));
            Inning = inning;
            RunDifferential = runDifferential;
            LeverageIndex = leverageIndex;
        }

        public int Inning { get; }
        public int RunDifferential { get; }
        public double LeverageIndex { get; }
    }

    /// <summary>불펜 한 명의 실제 사용 가능 여부와 현재 컨디션이다.</summary>
    public readonly struct BullpenCandidateState
    {
        public BullpenCandidateState(
            string playerSeasonId,
            ActiveRosterRole bullpenRole,
            int condition,
            bool isAvailable)
        {
            if (string.IsNullOrWhiteSpace(playerSeasonId))
                throw new ArgumentException("PlayerSeasonId는 비어 있을 수 없습니다.", nameof(playerSeasonId));
            if (!ActiveRosterCompositionRule.Standard.IsBullpenRole(bullpenRole))
                throw new ArgumentException("일반 불펜 1~4 슬롯만 후보가 될 수 있습니다.", nameof(bullpenRole));
            if (condition < 0 || condition > 100)
                throw new ArgumentOutOfRangeException(nameof(condition));

            PlayerSeasonId = playerSeasonId.Trim();
            BullpenRole = bullpenRole;
            Condition = condition;
            IsAvailable = isAvailable;
        }

        public string PlayerSeasonId { get; }
        public ActiveRosterRole BullpenRole { get; }
        public int Condition { get; }
        public bool IsAvailable { get; }
    }

    /// <summary>특정 경기 구간에서 적용할 최소 컨디션과 불펜 역할 우선순위다.</summary>
    public sealed class BullpenUsageBand
    {
        private readonly ActiveRosterRole[] _rolePriority;

        public BullpenUsageBand(
            int minimumInning,
            int maximumInning,
            int minimumRunDifferential,
            int maximumRunDifferential,
            double minimumLeverageIndex,
            double maximumLeverageIndex,
            int minimumCondition,
            IReadOnlyList<ActiveRosterRole> rolePriority)
        {
            if (minimumInning <= 0 || maximumInning < minimumInning)
                throw new ArgumentOutOfRangeException(nameof(maximumInning));
            if (maximumRunDifferential < minimumRunDifferential)
                throw new ArgumentOutOfRangeException(nameof(maximumRunDifferential));
            if (minimumLeverageIndex < 0d || maximumLeverageIndex < minimumLeverageIndex ||
                double.IsNaN(minimumLeverageIndex) || double.IsNaN(maximumLeverageIndex))
                throw new ArgumentOutOfRangeException(nameof(maximumLeverageIndex));
            if (minimumCondition < 0 || minimumCondition > 100)
                throw new ArgumentOutOfRangeException(nameof(minimumCondition));
            if (rolePriority == null || rolePriority.Count != ActiveRosterCompositionRule.BullpenPitcherCount)
                throw new ArgumentException("불펜 1~4의 전체 우선순위가 필요합니다.", nameof(rolePriority));

            _rolePriority = new ActiveRosterRole[rolePriority.Count];
            for (int index = 0; index < rolePriority.Count; index++)
            {
                ActiveRosterRole role = rolePriority[index];
                if (!ActiveRosterCompositionRule.Standard.IsBullpenRole(role))
                    throw new ArgumentException("일반 불펜 슬롯만 우선순위에 포함할 수 있습니다.", nameof(rolePriority));
                for (int previous = 0; previous < index; previous++)
                    if (_rolePriority[previous] == role)
                        throw new ArgumentException("불펜 역할 우선순위는 중복될 수 없습니다.", nameof(rolePriority));
                _rolePriority[index] = role;
            }

            MinimumInning = minimumInning;
            MaximumInning = maximumInning;
            MinimumRunDifferential = minimumRunDifferential;
            MaximumRunDifferential = maximumRunDifferential;
            MinimumLeverageIndex = minimumLeverageIndex;
            MaximumLeverageIndex = maximumLeverageIndex;
            MinimumCondition = minimumCondition;
        }

        public int MinimumInning { get; }
        public int MaximumInning { get; }
        public int MinimumRunDifferential { get; }
        public int MaximumRunDifferential { get; }
        public double MinimumLeverageIndex { get; }
        public double MaximumLeverageIndex { get; }
        public int MinimumCondition { get; }
        public IReadOnlyList<ActiveRosterRole> RolePriority => _rolePriority;

        /// <summary>이 구간이 현재 경기 상황을 담당하는지 반환한다.</summary>
        public bool Matches(BullpenSelectionContext context)
        {
            return context.Inning >= MinimumInning && context.Inning <= MaximumInning &&
                   context.RunDifferential >= MinimumRunDifferential &&
                   context.RunDifferential <= MaximumRunDifferential &&
                   context.LeverageIndex >= MinimumLeverageIndex &&
                   context.LeverageIndex <= MaximumLeverageIndex;
        }
    }

    /// <summary>순서가 고정된 경기 구간별 불펜 사용 Balance 데이터다.</summary>
    public sealed class BullpenUsagePolicy
    {
        private readonly BullpenUsageBand[] _bands;

        public BullpenUsagePolicy(IReadOnlyList<BullpenUsageBand> bands)
        {
            if (bands == null || bands.Count == 0)
                throw new ArgumentException("하나 이상의 불펜 사용 구간이 필요합니다.", nameof(bands));
            _bands = new BullpenUsageBand[bands.Count];
            for (int index = 0; index < bands.Count; index++)
                _bands[index] = bands[index] ?? throw new ArgumentException("null 불펜 사용 구간이 있습니다.", nameof(bands));
        }

        public IReadOnlyList<BullpenUsageBand> Bands => _bands;
    }

    /// <summary>가상 TeamSeason이 오르는 열 단계 리그를 낮은 순서부터 정의한다.</summary>
    public enum LeagueGrade
    {
        Rookie,
        Minor,
        Major,
        World,
        AllStar,
        Classic,
        Winners,
        Champion,
        Master,
        Galaxy
    }

    /// <summary>리그 한 단계의 경기 수와 승강 승률 기준을 데이터로 보관한다.</summary>
    public sealed class LeagueGradeRule
    {
        public LeagueGradeRule(
            LeagueGrade grade,
            int minimumGames,
            double? promotionWinningPercentage,
            double? relegationWinningPercentage)
        {
            if (minimumGames <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumGames));
            ValidatePercentage(promotionWinningPercentage, nameof(promotionWinningPercentage));
            ValidatePercentage(relegationWinningPercentage, nameof(relegationWinningPercentage));
            if (promotionWinningPercentage.HasValue && relegationWinningPercentage.HasValue &&
                relegationWinningPercentage.Value >= promotionWinningPercentage.Value)
            {
                throw new ArgumentException("강등 기준은 승격 기준보다 낮아야 합니다.");
            }

            Grade = grade;
            MinimumGames = minimumGames;
            PromotionWinningPercentage = promotionWinningPercentage;
            RelegationWinningPercentage = relegationWinningPercentage;
        }

        public LeagueGrade Grade { get; }
        public int MinimumGames { get; }
        public double? PromotionWinningPercentage { get; }
        public double? RelegationWinningPercentage { get; }

        private static void ValidatePercentage(double? value, string parameterName)
        {
            if (value.HasValue &&
                (value.Value < 0d || value.Value > 1d || double.IsNaN(value.Value)))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>Rookie부터 Galaxy까지 모든 단계의 승강 기준을 보관한다.</summary>
    public sealed class LeagueDefinition
    {
        private readonly LeagueGradeRule[] _rules;

        public LeagueDefinition(IReadOnlyList<LeagueGradeRule> rules)
        {
            int gradeCount = Enum.GetValues(typeof(LeagueGrade)).Length;
            if (rules == null || rules.Count != gradeCount)
                throw new ArgumentException("모든 LeagueGrade 규칙이 필요합니다.", nameof(rules));

            _rules = new LeagueGradeRule[gradeCount];
            var assigned = new bool[gradeCount];
            for (int index = 0; index < rules.Count; index++)
            {
                LeagueGradeRule rule = rules[index] ?? throw new ArgumentException("null 리그 규칙이 있습니다.", nameof(rules));
                int gradeIndex = (int)rule.Grade;
                if (gradeIndex < 0 || gradeIndex >= gradeCount || assigned[gradeIndex])
                    throw new ArgumentException("LeagueGrade 규칙은 중복될 수 없습니다.", nameof(rules));
                assigned[gradeIndex] = true;
                _rules[gradeIndex] = rule;
            }

            if (_rules[(int)LeagueGrade.Rookie].RelegationWinningPercentage.HasValue)
                throw new ArgumentException("Rookie는 더 낮은 리그로 강등될 수 없습니다.", nameof(rules));
            if (_rules[(int)LeagueGrade.Galaxy].PromotionWinningPercentage.HasValue)
                throw new ArgumentException("Galaxy는 더 높은 리그로 승격할 수 없습니다.", nameof(rules));
        }

        /// <summary>지정 등급의 승강 규칙을 반환한다.</summary>
        public LeagueGradeRule GetRule(LeagueGrade grade)
        {
            int index = (int)grade;
            if (index < 0 || index >= _rules.Length)
                throw new ArgumentOutOfRangeException(nameof(grade));
            return _rules[index];
        }
    }

    /// <summary>특수 합성 참가팀의 세 가지 생성 목적을 구분한다.</summary>
    public enum SpecialCompositeTeamType
    {
        AllStarComposite,
        GoldenGloveComposite,
        YearSelectComposite
    }

    /// <summary>수상 확정 뒤 해당 연도 정규 구단과 별도로 리그에 추가할 합성 참가팀이다.</summary>
    public sealed class SpecialCompositeTeamRegistration
    {
        public SpecialCompositeTeamRegistration(
            string teamSeasonKey,
            int originYear,
            SpecialCompositeTeamType teamType)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            if (originYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));
            if (!Enum.IsDefined(typeof(SpecialCompositeTeamType), teamType))
                throw new ArgumentOutOfRangeException(nameof(teamType));
            TeamSeasonKey = teamSeasonKey.Trim();
            OriginYear = originYear;
            TeamType = teamType;
        }

        public string TeamSeasonKey { get; }
        public int OriginYear { get; }
        public SpecialCompositeTeamType TeamType { get; }
    }

    /// <summary>연도별 정규 Franchise 6~10구단과 별도 특수 합성 참가팀을 구분해 보관한다.</summary>
    public sealed class LeagueInstance
    {
        public const int MinimumRegularFranchiseTeamCount = 6;
        public const int MaximumRegularFranchiseTeamCount = 10;
        private readonly string[] _regularTeamSeasonKeys;
        private readonly SpecialCompositeTeamRegistration[] _specialCompositeTeams;

        public LeagueInstance(
            string leagueInstanceId,
            LeagueGrade grade,
            IReadOnlyList<string> regularTeamSeasonKeys,
            IReadOnlyList<SpecialCompositeTeamRegistration> specialCompositeTeams = null)
        {
            if (string.IsNullOrWhiteSpace(leagueInstanceId))
                throw new ArgumentException("LeagueInstanceId는 비어 있을 수 없습니다.", nameof(leagueInstanceId));
            if (!Enum.IsDefined(typeof(LeagueGrade), grade))
                throw new ArgumentOutOfRangeException(nameof(grade));
            if (regularTeamSeasonKeys == null || !IsSupportedRegularFranchiseTeamCount(regularTeamSeasonKeys.Count))
                throw new ArgumentException("정규 Franchise 구단은 6~10개여야 합니다.", nameof(regularTeamSeasonKeys));

            LeagueInstanceId = leagueInstanceId.Trim();
            Grade = grade;
            _regularTeamSeasonKeys = CopyRegularTeams(regularTeamSeasonKeys);
            _specialCompositeTeams = CopySpecialTeams(specialCompositeTeams, _regularTeamSeasonKeys);
        }

        public string LeagueInstanceId { get; }
        public LeagueGrade Grade { get; }
        public int RegularFranchiseTeamCount => _regularTeamSeasonKeys.Length;
        public IReadOnlyList<string> RegularTeamSeasonKeys => _regularTeamSeasonKeys;
        public IReadOnlyList<SpecialCompositeTeamRegistration> SpecialCompositeTeams => _specialCompositeTeams;
        public int ParticipantTeamCount => _regularTeamSeasonKeys.Length + _specialCompositeTeams.Length;

        public static bool IsSupportedRegularFranchiseTeamCount(int teamCount)
        {
            return teamCount >= MinimumRegularFranchiseTeamCount &&
                   teamCount <= MaximumRegularFranchiseTeamCount;
        }

        private static string[] CopyRegularTeams(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                string key = source[index]?.Trim();
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(source));
                if (!unique.Add(key))
                    throw new ArgumentException("정규 TeamSeasonKey는 중복될 수 없습니다.", nameof(source));
                result[index] = key;
            }
            return result;
        }

        private static SpecialCompositeTeamRegistration[] CopySpecialTeams(
            IReadOnlyList<SpecialCompositeTeamRegistration> source,
            IReadOnlyList<string> regularTeams)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<SpecialCompositeTeamRegistration>();
            int specialTeamTypeCount = Enum.GetValues(typeof(SpecialCompositeTeamType)).Length;
            if (source.Count != specialTeamTypeCount)
                throw new ArgumentException("특수 합성팀은 세 종류를 한 번에 등록해야 합니다.", nameof(source));

            var result = new SpecialCompositeTeamRegistration[source.Count];
            var types = new HashSet<SpecialCompositeTeamType>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            int originYear = source[0]?.OriginYear ?? 0;
            for (int index = 0; index < source.Count; index++)
            {
                SpecialCompositeTeamRegistration registration = source[index] ??
                    throw new ArgumentException("null 특수 합성팀이 있습니다.", nameof(source));
                if (!types.Add(registration.TeamType) || !keys.Add(registration.TeamSeasonKey))
                    throw new ArgumentException("특수 합성팀 종류와 TeamSeasonKey는 중복될 수 없습니다.", nameof(source));
                if (registration.OriginYear != originYear)
                    throw new ArgumentException("세 특수 합성팀은 같은 OriginYear여야 합니다.", nameof(source));
                for (int regularIndex = 0; regularIndex < regularTeams.Count; regularIndex++)
                    if (string.Equals(regularTeams[regularIndex], registration.TeamSeasonKey, StringComparison.Ordinal))
                        throw new ArgumentException("특수 합성팀은 정규 Franchise 슬롯을 점유할 수 없습니다.", nameof(source));
                result[index] = registration;
            }
            return result;
        }
    }

    /// <summary>새 TeamSeason의 리그 진행 상태이며 최초 등급은 항상 Rookie다.</summary>
    public sealed class TeamSeasonLeagueState
    {
        public TeamSeasonLeagueState(string teamSeasonKey)
            : this(teamSeasonKey, LeagueGrade.Rookie)
        {
        }

        private TeamSeasonLeagueState(string teamSeasonKey, LeagueGrade grade)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            if (!Enum.IsDefined(typeof(LeagueGrade), grade))
                throw new ArgumentOutOfRangeException(nameof(grade));
            TeamSeasonKey = teamSeasonKey.Trim();
            Grade = grade;
        }

        public string TeamSeasonKey { get; }
        public LeagueGrade Grade { get; }

        /// <summary>승강 판정 결과를 반영한 새 상태를 반환한다.</summary>
        public TeamSeasonLeagueState MoveTo(LeagueGrade grade)
        {
            return new TeamSeasonLeagueState(TeamSeasonKey, grade);
        }
    }

    /// <summary>Club DNA의 여덟 운영 성향을 0~100 범위로 보관한다.</summary>
    public sealed class ClubDnaRatings
    {
        public ClubDnaRatings(
            double contact,
            double power,
            double running,
            double defense,
            double rotation,
            double bullpen,
            double development,
            double experience)
        {
            Contact = Validate(contact, nameof(contact));
            Power = Validate(power, nameof(power));
            Running = Validate(running, nameof(running));
            Defense = Validate(defense, nameof(defense));
            Rotation = Validate(rotation, nameof(rotation));
            Bullpen = Validate(bullpen, nameof(bullpen));
            Development = Validate(development, nameof(development));
            Experience = Validate(experience, nameof(experience));
        }

        public double Contact { get; }
        public double Power { get; }
        public double Running { get; }
        public double Defense { get; }
        public double Rotation { get; }
        public double Bullpen { get; }
        public double Development { get; }
        public double Experience { get; }

        private static double Validate(double value, string parameterName)
        {
            if (value < 0d || value > 100d || double.IsNaN(value))
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    /// <summary>개별 TeamSeason이 독립적으로 소유하고 매 시즌 갱신하는 Club DNA 상태다.</summary>
    public sealed class TeamSeasonClubState
    {
        public TeamSeasonClubState(string teamSeasonKey, ClubDnaRatings ratings)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            TeamSeasonKey = teamSeasonKey.Trim();
            Ratings = ratings ?? throw new ArgumentNullException(nameof(ratings));
        }

        public string TeamSeasonKey { get; }
        public ClubDnaRatings Ratings { get; }
    }

    /// <summary>여러 TeamSeason Club DNA의 장기 평균으로 표현하는 Franchise 정체성이다.</summary>
    public sealed class FranchiseIdentityProfile
    {
        public FranchiseIdentityProfile(string franchiseId, ClubDnaRatings ratings)
        {
            if (string.IsNullOrWhiteSpace(franchiseId))
                throw new ArgumentException("FranchiseId는 비어 있을 수 없습니다.", nameof(franchiseId));
            FranchiseId = franchiseId.Trim();
            Ratings = ratings ?? throw new ArgumentNullException(nameof(ratings));
        }

        public string FranchiseId { get; }
        public ClubDnaRatings Ratings { get; }
    }

    /// <summary>기존 DNA·최근 성적·감독 철학의 반영 비율과 시즌 변화 상한을 보관한다.</summary>
    public sealed class ClubDnaUpdatePolicy
    {
        public ClubDnaUpdatePolicy(
            double currentWeight,
            double recentPerformanceWeight,
            double managerPhilosophyWeight,
            double maximumSeasonChange)
        {
            if (currentWeight < 0d || recentPerformanceWeight < 0d || managerPhilosophyWeight < 0d ||
                double.IsNaN(currentWeight) || double.IsNaN(recentPerformanceWeight) ||
                double.IsNaN(managerPhilosophyWeight))
                throw new ArgumentOutOfRangeException(nameof(currentWeight));
            double totalWeight = currentWeight + recentPerformanceWeight + managerPhilosophyWeight;
            if (Math.Abs(totalWeight - 1d) > 0.0000001d)
                throw new ArgumentException("Club DNA 갱신 가중치 합은 1이어야 합니다.");
            if (maximumSeasonChange < 0d || maximumSeasonChange > 5d || double.IsNaN(maximumSeasonChange))
                throw new ArgumentOutOfRangeException(nameof(maximumSeasonChange), "시즌 변화폭 상한은 5를 넘을 수 없습니다.");

            CurrentWeight = currentWeight;
            RecentPerformanceWeight = recentPerformanceWeight;
            ManagerPhilosophyWeight = managerPhilosophyWeight;
            MaximumSeasonChange = maximumSeasonChange;
        }

        public double CurrentWeight { get; }
        public double RecentPerformanceWeight { get; }
        public double ManagerPhilosophyWeight { get; }
        public double MaximumSeasonChange { get; }
    }
}
