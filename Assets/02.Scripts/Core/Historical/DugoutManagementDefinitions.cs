using System;
using System.Collections.Generic;
using Baseball.Core.Teams;

namespace Baseball.Core.Historical
{
    /// <summary>구단주가 감독에게 요청할 수 있는 여섯 경기 운영 축이다.</summary>
    public enum DugoutPolicyAxis
    {
        BattingApproach = 0,
        RunningAggression = 1,
        SmallBallPreference = 2,
        PinchHitAggression = 3,
        HookSpeed = 4,
        BullpenAggression = 5
    }

    /// <summary>각 방침을 -2~+2 단계로 저장하는 불변 구단주 지시다.</summary>
    public readonly struct DugoutPolicySettings
    {
        public const int MinimumLevel = 0;
        public const int NeutralLevel = 2;
        public const int MaximumLevel = 4;

        public DugoutPolicySettings(
            int battingApproach,
            int runningAggression,
            int smallBallPreference,
            int pinchHitAggression,
            int hookSpeed,
            int bullpenAggression)
        {
            BattingApproach = Validate(battingApproach, nameof(battingApproach));
            RunningAggression = Validate(runningAggression, nameof(runningAggression));
            SmallBallPreference = Validate(smallBallPreference, nameof(smallBallPreference));
            PinchHitAggression = Validate(pinchHitAggression, nameof(pinchHitAggression));
            HookSpeed = Validate(hookSpeed, nameof(hookSpeed));
            BullpenAggression = Validate(bullpenAggression, nameof(bullpenAggression));
        }

        public int BattingApproach { get; }
        public int RunningAggression { get; }
        public int SmallBallPreference { get; }
        public int PinchHitAggression { get; }
        public int HookSpeed { get; }
        public int BullpenAggression { get; }

        public static DugoutPolicySettings Neutral => new DugoutPolicySettings(2, 2, 2, 2, 2, 2);

        public int GetLevel(DugoutPolicyAxis axis)
        {
            return axis switch
            {
                DugoutPolicyAxis.BattingApproach => BattingApproach,
                DugoutPolicyAxis.RunningAggression => RunningAggression,
                DugoutPolicyAxis.SmallBallPreference => SmallBallPreference,
                DugoutPolicyAxis.PinchHitAggression => PinchHitAggression,
                DugoutPolicyAxis.HookSpeed => HookSpeed,
                DugoutPolicyAxis.BullpenAggression => BullpenAggression,
                _ => throw new ArgumentOutOfRangeException(nameof(axis))
            };
        }

        private static int Validate(int value, string parameterName)
        {
            if (value < MinimumLevel || value > MaximumLevel)
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    /// <summary>감독 카드의 고정 전술 성향과 플레이어가 이해할 수 있는 선택 근거를 정의한다.</summary>
    public sealed class ManagerDefinition
    {
        public ManagerDefinition(
            string managerId,
            string displayName,
            string styleName,
            string description,
            string traitDescription,
            ManagerTacticalProfile baseProfile)
        {
            ManagerId = Require(managerId, nameof(managerId));
            DisplayName = Require(displayName, nameof(displayName));
            StyleName = Require(styleName, nameof(styleName));
            Description = Require(description, nameof(description));
            TraitDescription = Require(traitDescription, nameof(traitDescription));
            BaseProfile = baseProfile;
        }

        public string ManagerId { get; }
        public string DisplayName { get; }
        public string StyleName { get; }
        public string Description { get; }
        public string TraitDescription { get; }
        public ManagerTacticalProfile BaseProfile { get; }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("값이 필요합니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>수석코치가 강화할 판단 축과 보정 방향을 정의한다.</summary>
    public sealed class HeadCoachDefinition
    {
        public HeadCoachDefinition(
            string headCoachId,
            string displayName,
            string specialtyName,
            string description,
            DugoutPolicyAxis primaryAxis,
            int primaryModifier,
            DugoutPolicyAxis secondaryAxis,
            int secondaryModifier)
        {
            if (string.IsNullOrWhiteSpace(headCoachId))
                throw new ArgumentException("HeadCoachId가 필요합니다.", nameof(headCoachId));
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(specialtyName) ||
                string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("수석코치 표시 정보가 필요합니다.");
            if (primaryModifier < -20 || primaryModifier > 20 || secondaryModifier < -20 || secondaryModifier > 20)
                throw new ArgumentOutOfRangeException(nameof(primaryModifier));
            HeadCoachId = headCoachId.Trim();
            DisplayName = displayName.Trim();
            SpecialtyName = specialtyName.Trim();
            Description = description.Trim();
            PrimaryAxis = primaryAxis;
            PrimaryModifier = primaryModifier;
            SecondaryAxis = secondaryAxis;
            SecondaryModifier = secondaryModifier;
        }

        public string HeadCoachId { get; }
        public string DisplayName { get; }
        public string SpecialtyName { get; }
        public string Description { get; }
        public DugoutPolicyAxis PrimaryAxis { get; }
        public int PrimaryModifier { get; }
        public DugoutPolicyAxis SecondaryAxis { get; }
        public int SecondaryModifier { get; }

        public int GetModifier(DugoutPolicyAxis axis)
        {
            int modifier = axis == PrimaryAxis ? PrimaryModifier : 0;
            if (axis == SecondaryAxis) modifier += SecondaryModifier;
            return modifier;
        }
    }

    /// <summary>실존 인물과 분리된 기본 감독·수석코치 카드 카탈로그다.</summary>
    public sealed class DugoutStaffCatalog
    {
        private readonly ManagerDefinition[] _managers;
        private readonly HeadCoachDefinition[] _headCoaches;

        public DugoutStaffCatalog(
            IReadOnlyList<ManagerDefinition> managers,
            IReadOnlyList<HeadCoachDefinition> headCoaches)
        {
            _managers = CopyUnique(managers, item => item.ManagerId, nameof(managers));
            _headCoaches = CopyUnique(headCoaches, item => item.HeadCoachId, nameof(headCoaches));
        }

        public IReadOnlyList<ManagerDefinition> Managers => _managers;
        public IReadOnlyList<HeadCoachDefinition> HeadCoaches => _headCoaches;

        public ManagerDefinition GetManager(string managerId)
        {
            for (int index = 0; index < _managers.Length; index++)
                if (string.Equals(_managers[index].ManagerId, managerId, StringComparison.Ordinal)) return _managers[index];
            throw new KeyNotFoundException($"ManagerId {managerId}가 카탈로그에 없습니다.");
        }

        public HeadCoachDefinition GetHeadCoach(string headCoachId)
        {
            for (int index = 0; index < _headCoaches.Length; index++)
                if (string.Equals(_headCoaches[index].HeadCoachId, headCoachId, StringComparison.Ordinal)) return _headCoaches[index];
            throw new KeyNotFoundException($"HeadCoachId {headCoachId}가 카탈로그에 없습니다.");
        }

        public static DugoutStaffCatalog CreateDefault()
        {
            var managers = new[]
            {
                Manager("MGR-BALANCED", "윤도현", "균형 운영", "전력과 경기 흐름을 함께 보며 무리하지 않습니다.",
                    "접전에서는 기대값이 높은 정석 선택을 우선합니다.", 50, 50, 55, 50, 50, 55, 52, 55, 50, 50),
                Manager("MGR-ATTACK", "강태욱", "공격 야구", "출루 뒤 추가 진루와 빠른 승부를 선호합니다.",
                    "동점과 한 점 차에서 공격적 선택 기준이 빨라집니다.", 62, 55, 42, 44, 68, 45, 45, 42, 62, 58),
                Manager("MGR-SMALLBALL", "서민재", "세밀한 야구", "한 베이스를 쌓아 득점권 기회를 만듭니다.",
                    "후반 접전에서 번트와 대주자 판단을 우선합니다.", 42, 48, 58, 72, 61, 55, 52, 52, 45, 46),
                Manager("MGR-PITCHING", "문재혁", "투수 중심", "선발의 상태와 불펜 역할을 엄격하게 관리합니다.",
                    "투수의 피로와 역할 적합도를 교체 판단에 강하게 반영합니다.", 48, 67, 72, 44, 43, 62, 58, 63, 47, 68),
                Manager("MGR-ANALYTIC", "한지성", "상대 맞춤", "상대와 상황의 작은 차이를 적극 활용합니다.",
                    "플래툰과 수비 가치를 동일 능력의 동률 해소 기준으로 사용합니다.", 54, 60, 40, 38, 52, 75, 70, 38, 56, 61)
            };
            var coaches = new[]
            {
                Coach("HC-CONTACT", "오세진", "컨택 플랜", "강한 스윙보다 인플레이 타구가 필요한 상황을 선명하게 만듭니다.", DugoutPolicyAxis.BattingApproach, -10, DugoutPolicyAxis.PinchHitAggression, 3),
                Coach("HC-RUNNING", "배준호", "주루 코디네이터", "접전에서 도루와 대주자 판단을 한 단계 빠르게 합니다.", DugoutPolicyAxis.RunningAggression, 10, DugoutPolicyAxis.PinchHitAggression, 2),
                Coach("HC-SMALLBALL", "노경민", "스몰볼 코디네이터", "후반 한 점 승부에서 희생번트 선택을 보강합니다.", DugoutPolicyAxis.SmallBallPreference, 10, DugoutPolicyAxis.RunningAggression, 3),
                Coach("HC-BENCH", "임수현", "벤치 코디네이터", "대타 후보의 우위를 더 일찍 포착합니다.", DugoutPolicyAxis.PinchHitAggression, 10, DugoutPolicyAxis.BattingApproach, -3),
                Coach("HC-STARTER", "최도윤", "선발 코디네이터", "선발에게 위기를 넘길 여지를 주되 한계는 명확히 합니다.", DugoutPolicyAxis.HookSpeed, -10, DugoutPolicyAxis.BullpenAggression, -3),
                Coach("HC-BULLPEN", "정해원", "불펜 코디네이터", "고레버리지에서 불펜 가동 시점을 앞당깁니다.", DugoutPolicyAxis.BullpenAggression, 10, DugoutPolicyAxis.HookSpeed, 5)
            };
            return new DugoutStaffCatalog(managers, coaches);
        }

        private static ManagerDefinition Manager(
            string id, string name, string style, string description, string trait,
            int hook, int bullpen, int rigidity, int smallBall, int running, int matchup,
            int defense, int trust, int batting, int pinchHit)
        {
            return new ManagerDefinition(id, name, style, description, trait,
                new ManagerTacticalProfile(hook, bullpen, rigidity, smallBall, running, matchup, defense, trust,
                    batting, pinchHit));
        }

        private static HeadCoachDefinition Coach(
            string id, string name, string specialty, string description,
            DugoutPolicyAxis primary, int primaryModifier, DugoutPolicyAxis secondary, int secondaryModifier)
        {
            return new HeadCoachDefinition(id, name, specialty, description,
                primary, primaryModifier, secondary, secondaryModifier);
        }

        private static T[] CopyUnique<T>(IReadOnlyList<T> source, Func<T, string> getId, string parameterName)
            where T : class
        {
            if (source == null || source.Count == 0) throw new ArgumentException("하나 이상의 정의가 필요합니다.", parameterName);
            var result = new T[source.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < result.Length; index++)
            {
                T item = source[index] ?? throw new ArgumentException("null 정의가 있습니다.", parameterName);
                if (!ids.Add(getId(item))) throw new ArgumentException("정의 ID는 중복될 수 없습니다.", parameterName);
                result[index] = item;
            }
            return result;
        }
    }

    /// <summary>선택한 감독·수석코치와 신뢰도, 구단주 방침을 시즌을 넘어 보관한다.</summary>
    public sealed class DugoutManagementState
    {
        public const int InitialTrust = 20;

        public DugoutManagementState(
            string managerId,
            string headCoachId,
            DugoutPolicySettings policy,
            int managerTrust = InitialTrust)
        {
            if (string.IsNullOrWhiteSpace(managerId) || string.IsNullOrWhiteSpace(headCoachId))
                throw new ArgumentException("감독과 수석코치 ID가 필요합니다.");
            if (managerTrust < 0 || managerTrust > 100) throw new ArgumentOutOfRangeException(nameof(managerTrust));
            ManagerId = managerId.Trim();
            HeadCoachId = headCoachId.Trim();
            Policy = policy;
            ManagerTrust = managerTrust;
        }

        public string ManagerId { get; private set; }
        public string HeadCoachId { get; private set; }
        public DugoutPolicySettings Policy { get; private set; }
        public int ManagerTrust { get; private set; }
        public int AllowedPolicyOffset => ManagerTrust >= 60 ? 2 : 1;

        public static DugoutManagementState CreateDefault()
        {
            return new DugoutManagementState("MGR-BALANCED", "HC-CONTACT", DugoutPolicySettings.Neutral);
        }

        public void Configure(
            string managerId,
            string headCoachId,
            DugoutPolicySettings policy,
            DugoutStaffCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            ManagerDefinition manager = catalog.GetManager(managerId);
            HeadCoachDefinition headCoach = catalog.GetHeadCoach(headCoachId);
            bool changedManager = !string.Equals(ManagerId, manager.ManagerId, StringComparison.Ordinal);
            ValidatePolicy(policy, changedManager ? 1 : AllowedPolicyOffset);
            ManagerId = manager.ManagerId;
            HeadCoachId = headCoach.HeadCoachId;
            Policy = policy;
            if (changedManager) ManagerTrust = InitialTrust;
        }

        public void RecordMatchCompleted()
        {
            if (ManagerTrust < 100) ManagerTrust++;
        }

        private static void ValidatePolicy(DugoutPolicySettings policy, int allowedOffset)
        {
            foreach (DugoutPolicyAxis axis in Enum.GetValues(typeof(DugoutPolicyAxis)))
            {
                if (Math.Abs(policy.GetLevel(axis) - DugoutPolicySettings.NeutralLevel) > allowedOffset)
                    throw new InvalidOperationException("현재 감독 신뢰도로는 ±2 단계 방침을 요청할 수 없습니다.");
            }
        }
    }
}
