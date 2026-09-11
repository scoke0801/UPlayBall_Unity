using System;
using System.Collections.Generic;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 선택 구단 관점에서 확정된 경기 장소를 구분한다.
    /// </summary>
    public enum ScheduleFocusSide
    {
        None = 0,
        Away = 1,
        Home = 2
    }

    /// <summary>
    /// 선택 구단 관점에서 Adapter가 확정한 경기 결과를 구분한다.
    /// </summary>
    public enum ScheduleFocusOutcome
    {
        Pending = 0,
        Win = 1,
        Loss = 2,
        Tie = 3
    }

    /// <summary>
    /// 일정 화면에서 홈·원정 구단을 같은 계약으로 표시한다.
    /// </summary>
    public sealed class ScheduleTeamSnapshot
    {
        /// <summary>
        /// 구단 ID, 표시 이름, Emblem과 제한적인 Accent 정보를 만든다.
        /// </summary>
        public ScheduleTeamSnapshot(string teamId, string displayName, string emblemAssetKey = null, string accentHex = null)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("일정 구단 ID는 비어 있을 수 없습니다.", nameof(teamId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("일정 구단 이름은 비어 있을 수 없습니다.", nameof(displayName));

            TeamId = teamId;
            DisplayName = displayName;
            EmblemAssetKey = emblemAssetKey ?? string.Empty;
            AccentHex = accentHex ?? string.Empty;
        }

        public string TeamId { get; }
        public string DisplayName { get; }
        public string EmblemAssetKey { get; }
        public string AccentHex { get; }
    }

    /// <summary>
    /// Player나 Owner 모드 이름 없이 한 경기의 확정된 일정 표시 값을 전달한다.
    /// </summary>
    public sealed class ScheduleGameSnapshot
    {
        /// <summary>
        /// 경기 식별 정보, 양 팀, 점수와 선택 구단 관점 결과를 만든다.
        /// </summary>
        public ScheduleGameSnapshot(
            string gameId,
            int round,
            DateTime date,
            ScheduleTeamSnapshot awayTeam,
            ScheduleTeamSnapshot homeTeam,
            bool isCompleted,
            int awayRuns,
            int homeRuns,
            ScheduleFocusSide focusSide = ScheduleFocusSide.None,
            ScheduleFocusOutcome focusOutcome = ScheduleFocusOutcome.Pending)
            : this(
                gameId,
                round,
                date,
                hasCalendarDate: true,
                periodLabel: string.Empty,
                awayTeam,
                homeTeam,
                isCompleted,
                awayRuns,
                homeRuns,
                focusSide,
                focusOutcome)
        {
        }

        /// <summary>
        /// 달력 날짜가 없는 Runtime 일정의 확정 Round 라벨과 경기 결과를 만든다.
        /// </summary>
        public ScheduleGameSnapshot(
            string gameId,
            int round,
            string periodLabel,
            ScheduleTeamSnapshot awayTeam,
            ScheduleTeamSnapshot homeTeam,
            bool isCompleted,
            int awayRuns,
            int homeRuns,
            ScheduleFocusSide focusSide = ScheduleFocusSide.None,
            ScheduleFocusOutcome focusOutcome = ScheduleFocusOutcome.Pending)
            : this(
                gameId,
                round,
                default,
                hasCalendarDate: false,
                periodLabel,
                awayTeam,
                homeTeam,
                isCompleted,
                awayRuns,
                homeRuns,
                focusSide,
                focusOutcome)
        {
        }

        private ScheduleGameSnapshot(
            string gameId,
            int round,
            DateTime date,
            bool hasCalendarDate,
            string periodLabel,
            ScheduleTeamSnapshot awayTeam,
            ScheduleTeamSnapshot homeTeam,
            bool isCompleted,
            int awayRuns,
            int homeRuns,
            ScheduleFocusSide focusSide,
            ScheduleFocusOutcome focusOutcome)
        {
            if (string.IsNullOrWhiteSpace(gameId))
                throw new ArgumentException("경기 ID는 비어 있을 수 없습니다.", nameof(gameId));
            if (!hasCalendarDate && string.IsNullOrWhiteSpace(periodLabel))
                throw new ArgumentException("달력 날짜가 없는 경기에는 Round 라벨이 필요합니다.", nameof(periodLabel));
            if (!isCompleted && (awayRuns != 0 || homeRuns != 0))
                throw new ArgumentException("미완료 경기에는 점수를 표시할 수 없습니다.");

            GameId = gameId;
            Round = round;
            Date = date;
            HasCalendarDate = hasCalendarDate;
            PeriodLabel = periodLabel ?? string.Empty;
            AwayTeam = awayTeam ?? throw new ArgumentNullException(nameof(awayTeam));
            HomeTeam = homeTeam ?? throw new ArgumentNullException(nameof(homeTeam));
            IsCompleted = isCompleted;
            AwayRuns = awayRuns;
            HomeRuns = homeRuns;
            FocusSide = focusSide;
            FocusOutcome = focusOutcome;
        }

        public string GameId { get; }
        public int Round { get; }
        public DateTime Date { get; }
        public bool HasCalendarDate { get; }
        public string PeriodLabel { get; }
        public ScheduleTeamSnapshot AwayTeam { get; }
        public ScheduleTeamSnapshot HomeTeam { get; }
        public bool IsCompleted { get; }
        public int AwayRuns { get; }
        public int HomeRuns { get; }
        public ScheduleFocusSide FocusSide { get; }
        public ScheduleFocusOutcome FocusOutcome { get; }
    }

    /// <summary>
    /// 공용 일정 화면이 달력과 목록을 구성할 수 있는 시즌 Snapshot이다.
    /// </summary>
    public sealed class ScheduleScreenSnapshot
    {
        private readonly ScheduleGameSnapshot[] _games;

        /// <summary>
        /// 시즌 맥락과 정렬이 확정된 전체 경기 목록을 복사한다.
        /// </summary>
        public ScheduleScreenSnapshot(
            string seasonLabel,
            string leagueLabel,
            DateTime currentDate,
            string focusTeamId,
            IReadOnlyList<ScheduleGameSnapshot> games)
            : this(
                seasonLabel,
                leagueLabel,
                currentDate,
                hasCalendarDate: true,
                currentPeriodLabel: string.Empty,
                focusTeamId,
                games)
        {
        }

        /// <summary>
        /// 달력 날짜가 없는 Runtime 일정의 현재 진행 라벨과 전체 경기를 복사한다.
        /// </summary>
        public ScheduleScreenSnapshot(
            string seasonLabel,
            string leagueLabel,
            string currentPeriodLabel,
            string focusTeamId,
            IReadOnlyList<ScheduleGameSnapshot> games)
            : this(
                seasonLabel,
                leagueLabel,
                default,
                hasCalendarDate: false,
                currentPeriodLabel,
                focusTeamId,
                games)
        {
        }

        private ScheduleScreenSnapshot(
            string seasonLabel,
            string leagueLabel,
            DateTime currentDate,
            bool hasCalendarDate,
            string currentPeriodLabel,
            string focusTeamId,
            IReadOnlyList<ScheduleGameSnapshot> games)
        {
            if (!hasCalendarDate && string.IsNullOrWhiteSpace(currentPeriodLabel))
                throw new ArgumentException("달력 날짜가 없는 일정에는 현재 진행 라벨이 필요합니다.", nameof(currentPeriodLabel));
            SeasonLabel = seasonLabel ?? string.Empty;
            LeagueLabel = leagueLabel ?? string.Empty;
            CurrentDate = currentDate.Date;
            HasCalendarDate = hasCalendarDate;
            CurrentPeriodLabel = currentPeriodLabel ?? string.Empty;
            FocusTeamId = focusTeamId ?? string.Empty;
            _games = CopyItems(games, nameof(games));
        }

        public string SeasonLabel { get; }
        public string LeagueLabel { get; }
        public DateTime CurrentDate { get; }
        public bool HasCalendarDate { get; }
        public string CurrentPeriodLabel { get; }
        public string FocusTeamId { get; }
        public IReadOnlyList<ScheduleGameSnapshot> Games => _games;

        private static ScheduleGameSnapshot[] CopyItems(IReadOnlyList<ScheduleGameSnapshot> items, string parameterName)
        {
            if (items == null || items.Count == 0)
                return Array.Empty<ScheduleGameSnapshot>();
            var copy = new ScheduleGameSnapshot[items.Count];
            for (int i = 0; i < items.Count; i++)
                copy[i] = items[i] ?? throw new ArgumentException("일정 경기는 null일 수 없습니다.", parameterName);
            return copy;
        }
    }

    /// <summary>
    /// 리그 순위·최근 결과·다음 대진 표를 한 화면 Snapshot으로 묶는다.
    /// </summary>
    public sealed class LeagueScreenSnapshot
    {
        /// <summary>
        /// 리그 진행 맥락과 세 개의 공용 기록표를 만든다.
        /// </summary>
        public LeagueScreenSnapshot(
            string seasonLabel,
            string leagueLabel,
            string progressText,
            string focusTeamId,
            RecordTableModel standings,
            RecordTableModel recentResults = null,
            RecordTableModel nextRoundGames = null,
            RecordTableModel battingLeaders = null,
            RecordTableModel pitchingLeaders = null,
            RecordTableModel teamMetrics = null)
        {
            SeasonLabel = seasonLabel ?? string.Empty;
            LeagueLabel = leagueLabel ?? string.Empty;
            ProgressText = progressText ?? string.Empty;
            FocusTeamId = focusTeamId ?? string.Empty;
            Standings = standings ?? throw new ArgumentNullException(nameof(standings));
            RecentResults = recentResults;
            NextRoundGames = nextRoundGames;
            BattingLeaders = battingLeaders;
            PitchingLeaders = pitchingLeaders;
            TeamMetrics = teamMetrics;
        }

        public string SeasonLabel { get; }
        public string LeagueLabel { get; }
        public string ProgressText { get; }
        public string FocusTeamId { get; }
        public RecordTableModel Standings { get; }
        public RecordTableModel RecentResults { get; }
        public RecordTableModel NextRoundGames { get; }
        public RecordTableModel BattingLeaders { get; }
        public RecordTableModel PitchingLeaders { get; }
        public RecordTableModel TeamMetrics { get; }
    }

    /// <summary>
    /// 선수·구단 기록표와 규정 자격 설명을 공용 기록 화면에 전달한다.
    /// </summary>
    public sealed class RecordsScreenSnapshot
    {
        /// <summary>
        /// 범위·부문과 이미 형식화된 기록표를 묶는다.
        /// </summary>
        public RecordsScreenSnapshot(
            string seasonLabel,
            string leagueLabel,
            string scopeLabel,
            string categoryLabel,
            RecordTableModel table,
            string qualificationText = null,
            string focusedRowId = null)
        {
            SeasonLabel = seasonLabel ?? string.Empty;
            LeagueLabel = leagueLabel ?? string.Empty;
            ScopeLabel = scopeLabel ?? string.Empty;
            CategoryLabel = categoryLabel ?? string.Empty;
            Table = table ?? throw new ArgumentNullException(nameof(table));
            QualificationText = qualificationText ?? string.Empty;
            FocusedRowId = focusedRowId ?? string.Empty;
        }

        public string SeasonLabel { get; }
        public string LeagueLabel { get; }
        public string ScopeLabel { get; }
        public string CategoryLabel { get; }
        public RecordTableModel Table { get; }
        public string QualificationText { get; }
        public string FocusedRowId { get; }
    }

    /// <summary>
    /// 구단 요약과 읽기 전용 Roster를 Owner/Player 공용 화면에 전달한다.
    /// </summary>
    public sealed class TeamOverviewSnapshot
    {
        /// <summary>
        /// 구단 식별 정보, 순위 요약, 전력 지표와 Roster Snapshot을 만든다.
        /// </summary>
        public TeamOverviewSnapshot(
            string teamId,
            string teamName,
            string seasonLabel,
            string leagueLabel,
            string recordText,
            string rankText,
            string accentHex,
            string emblemAssetKey,
            RecordTableModel strengthTable,
            ReadOnlyRosterModel roster)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("구단 ID는 비어 있을 수 없습니다.", nameof(teamId));
            if (string.IsNullOrWhiteSpace(teamName))
                throw new ArgumentException("구단 이름은 비어 있을 수 없습니다.", nameof(teamName));

            TeamId = teamId;
            TeamName = teamName;
            SeasonLabel = seasonLabel ?? string.Empty;
            LeagueLabel = leagueLabel ?? string.Empty;
            RecordText = recordText ?? string.Empty;
            RankText = rankText ?? string.Empty;
            AccentHex = accentHex ?? string.Empty;
            EmblemAssetKey = emblemAssetKey ?? string.Empty;
            StrengthTable = strengthTable;
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));
        }

        public string TeamId { get; }
        public string TeamName { get; }
        public string SeasonLabel { get; }
        public string LeagueLabel { get; }
        public string RecordText { get; }
        public string RankText { get; }
        public string AccentHex { get; }
        public string EmblemAssetKey { get; }
        public RecordTableModel StrengthTable { get; }
        public ReadOnlyRosterModel Roster { get; }
    }

    /// <summary>
    /// 선수 상세에서 이름 있는 값을 재사용 가능한 한 줄로 전달한다.
    /// </summary>
    public sealed class DetailValueModel
    {
        /// <summary>
        /// 값 ID, 라벨, 형식화된 표시 값을 만든다.
        /// </summary>
        public DetailValueModel(string valueId, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(valueId))
                throw new ArgumentException("상세 값 ID는 비어 있을 수 없습니다.", nameof(valueId));
            ValueId = valueId;
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string ValueId { get; }
        public string Label { get; }
        public string Value { get; }
    }

    /// <summary>
    /// Career 성장 State나 Owner 소유 State 없이 공통 선수 정보와 기록만 전달한다.
    /// </summary>
    public sealed class PlayerDetailSnapshot
    {
        private readonly DetailValueModel[] _summaryValues;

        /// <summary>
        /// 선수 식별·소속과 Adapter가 확정한 공통 상세 값 및 기록표를 만든다.
        /// </summary>
        public PlayerDetailSnapshot(
            string playerId,
            string displayName,
            string teamId,
            string teamName,
            string seasonLabel,
            string positionLabel,
            string portraitAssetKey,
            string accentHex,
            IReadOnlyList<DetailValueModel> summaryValues,
            RecordTableModel abilityTable = null,
            RecordTableModel seasonRecordTable = null,
            RecordTableModel careerRecordTable = null)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("선수 ID는 비어 있을 수 없습니다.", nameof(playerId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("선수 이름은 비어 있을 수 없습니다.", nameof(displayName));

            PlayerId = playerId;
            DisplayName = displayName;
            TeamId = teamId ?? string.Empty;
            TeamName = teamName ?? string.Empty;
            SeasonLabel = seasonLabel ?? string.Empty;
            PositionLabel = positionLabel ?? string.Empty;
            PortraitAssetKey = portraitAssetKey ?? string.Empty;
            AccentHex = accentHex ?? string.Empty;
            _summaryValues = CopyValues(summaryValues);
            AbilityTable = abilityTable;
            SeasonRecordTable = seasonRecordTable;
            CareerRecordTable = careerRecordTable;
        }

        public string PlayerId { get; }
        public string DisplayName { get; }
        public string TeamId { get; }
        public string TeamName { get; }
        public string SeasonLabel { get; }
        public string PositionLabel { get; }
        public string PortraitAssetKey { get; }
        public string AccentHex { get; }
        public IReadOnlyList<DetailValueModel> SummaryValues => _summaryValues;
        public RecordTableModel AbilityTable { get; }
        public RecordTableModel SeasonRecordTable { get; }
        public RecordTableModel CareerRecordTable { get; }

        private static DetailValueModel[] CopyValues(IReadOnlyList<DetailValueModel> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<DetailValueModel>();
            var copy = new DetailValueModel[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i] ?? throw new ArgumentException("상세 값은 null일 수 없습니다.", nameof(values));
            return copy;
        }
    }
}
