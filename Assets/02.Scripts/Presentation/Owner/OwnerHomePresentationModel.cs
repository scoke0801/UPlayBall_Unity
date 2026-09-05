using System;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>Game 레이어가 구단주 Runtime State와 Resolver 결과에서 준비하는 홈 화면 Snapshot이다.</summary>
    public sealed class OwnerHomeSnapshot
    {
        /// <summary>공용 셸과 구단 현황에 필요한 실제 진행 값을 묶는다.</summary>
        public OwnerHomeSnapshot(
            string seasonText,
            string dateText,
            string leagueText,
            string teamName,
            string rankText,
            string nextMatchText,
            long money,
            int scoutingPoints,
            int developmentPoints,
            int pityGauge,
            int activeRosterCount,
            int activeRosterCapacity,
            int hitterCount,
            int requiredHitterCount,
            int pitcherCount,
            int requiredPitcherCount,
            int foreignPlayerCount,
            int foreignPlayerLimit,
            int ownedCardCount,
            bool isRosterValid,
            string rosterValidationMessage,
            RosterStrengthBreakdown rosterStrength = null,
            RosterCostBreakdown? rosterCost = null,
            string opponentStrengthText = null)
        {
            if (money < 0)
                throw new ArgumentOutOfRangeException(nameof(money));
            if (scoutingPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(scoutingPoints));
            if (developmentPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(developmentPoints));
            if (pityGauge < 0)
                throw new ArgumentOutOfRangeException(nameof(pityGauge));
            if (activeRosterCount < 0)
                throw new ArgumentOutOfRangeException(nameof(activeRosterCount));
            if (activeRosterCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(activeRosterCapacity));
            if (hitterCount < 0)
                throw new ArgumentOutOfRangeException(nameof(hitterCount));
            if (requiredHitterCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredHitterCount));
            if (pitcherCount < 0)
                throw new ArgumentOutOfRangeException(nameof(pitcherCount));
            if (requiredPitcherCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredPitcherCount));
            if (foreignPlayerCount < 0)
                throw new ArgumentOutOfRangeException(nameof(foreignPlayerCount));
            if (foreignPlayerLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(foreignPlayerLimit));
            if (ownedCardCount < 0)
                throw new ArgumentOutOfRangeException(nameof(ownedCardCount));
            if (!isRosterValid && string.IsNullOrWhiteSpace(rosterValidationMessage))
                throw new ArgumentException("유효하지 않은 선수단에는 검증 사유가 필요합니다.", nameof(rosterValidationMessage));

            SeasonText = seasonText ?? string.Empty;
            RosterStrength = rosterStrength;
            RosterCost = rosterCost;
            OpponentStrengthText = opponentStrengthText ?? string.Empty;
            DateText = dateText ?? string.Empty;
            LeagueText = leagueText ?? string.Empty;
            TeamName = teamName ?? string.Empty;
            RankText = rankText ?? string.Empty;
            NextMatchText = nextMatchText ?? string.Empty;
            Money = money;
            ScoutingPoints = scoutingPoints;
            DevelopmentPoints = developmentPoints;
            PityGauge = pityGauge;
            ActiveRosterCount = activeRosterCount;
            ActiveRosterCapacity = activeRosterCapacity;
            HitterCount = hitterCount;
            RequiredHitterCount = requiredHitterCount;
            PitcherCount = pitcherCount;
            RequiredPitcherCount = requiredPitcherCount;
            ForeignPlayerCount = foreignPlayerCount;
            ForeignPlayerLimit = foreignPlayerLimit;
            OwnedCardCount = ownedCardCount;
            IsRosterValid = isRosterValid;
            RosterValidationMessage = rosterValidationMessage ?? string.Empty;
        }

        public RosterStrengthBreakdown RosterStrength { get; }
        public RosterCostBreakdown? RosterCost { get; }
        public string OpponentStrengthText { get; }
        public string SeasonText { get; }
        public string DateText { get; }
        public string LeagueText { get; }
        public string TeamName { get; }
        public string RankText { get; }
        public string NextMatchText { get; }
        public long Money { get; }
        public int ScoutingPoints { get; }
        public int DevelopmentPoints { get; }
        public int PityGauge { get; }
        public int ActiveRosterCount { get; }
        public int ActiveRosterCapacity { get; }
        public int HitterCount { get; }
        public int RequiredHitterCount { get; }
        public int PitcherCount { get; }
        public int RequiredPitcherCount { get; }
        public int ForeignPlayerCount { get; }
        public int ForeignPlayerLimit { get; }
        public int OwnedCardCount { get; }
        public bool IsRosterValid { get; }
        public string RosterValidationMessage { get; }
    }

    /// <summary>구단주 홈과 SharedGameShell이 그대로 표시할 읽기 전용 Presentation Model이다.</summary>
    public sealed class OwnerHomePresentationModel
    {
        internal OwnerHomePresentationModel(OwnerHomeSnapshot snapshot, ShellStatusModel shellStatus)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            ShellStatus = shellStatus ?? throw new ArgumentNullException(nameof(shellStatus));
        }

        public OwnerHomeSnapshot Snapshot { get; }
        public ShellStatusModel ShellStatus { get; }
        public string StrengthText => OwnerRosterEvaluationFormatter.FormatStrength(Snapshot.RosterStrength);
        public string CostText => OwnerRosterEvaluationFormatter.FormatCost(Snapshot.RosterCost);
        public string RosterCountText =>
            $"현재 1군 {Snapshot.ActiveRosterCount}/{Snapshot.ActiveRosterCapacity}";
        public string RosterCompositionText =>
            $"야수 {Snapshot.HitterCount}/{Snapshot.RequiredHitterCount} · " +
            $"투수 {Snapshot.PitcherCount}/{Snapshot.RequiredPitcherCount}";
        public string ForeignPlayerText =>
            $"외국인 {Snapshot.ForeignPlayerCount}/{Snapshot.ForeignPlayerLimit}";
        public string OwnedCardText => $"보유 선수 {Snapshot.OwnedCardCount}";
        public ShellStatusEmphasis RosterEmphasis =>
            Snapshot.IsRosterValid ? ShellStatusEmphasis.Positive : ShellStatusEmphasis.Critical;
    }

    /// <summary>Game 레이어 Snapshot을 구단주 홈과 공용 Header가 소비할 모델로 변환한다.</summary>
    public static class OwnerHomePresentationBuilder
    {
        /// <summary>Money/SP/DP와 로스터 상태를 모드 전용 Header 슬롯에만 배치한다.</summary>
        public static OwnerHomePresentationModel Build(OwnerHomeSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var slots = new[]
            {
                new ShellStatusSlotModel("Money", "자금", OwnerMoneyFormatter.Format(snapshot.Money)),
                new ShellStatusSlotModel("SP", "스카우트 포인트", snapshot.ScoutingPoints.ToString("N0")),
                new ShellStatusSlotModel("DP", "육성 포인트", snapshot.DevelopmentPoints.ToString("N0")),
                new ShellStatusSlotModel(
                    "Roster",
                    "1군",
                    $"{snapshot.ActiveRosterCount}/{snapshot.ActiveRosterCapacity}",
                    snapshot.IsRosterValid ? ShellStatusEmphasis.Positive : ShellStatusEmphasis.Critical,
                    snapshot.IsRosterValid ? "등록 규칙 충족" : snapshot.RosterValidationMessage)
            };
            var shellStatus = new ShellStatusModel(
                snapshot.SeasonText,
                snapshot.DateText,
                snapshot.LeagueText,
                snapshot.TeamName,
                snapshot.RankText,
                snapshot.NextMatchText,
                slots);
            return new OwnerHomePresentationModel(snapshot, shellStatus);
        }
    }

    /// <summary>구단주 모드에 생성된 장식 배경의 프로젝트 자산 경로다.</summary>
    public static class OwnerUiAssetIds
    {
        public const string HomeBackgroundAssetPath =
            "Assets/Resources/UI/Generated/bg_owner_container_office_v2.png";
        public const string HomeBackgroundResourcePath =
            "UI/Generated/bg_owner_container_office_v2";
    }
}
