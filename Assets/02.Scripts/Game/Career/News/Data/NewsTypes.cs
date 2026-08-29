using System;

namespace Baseball.Game.Career.News
{
    /// <summary>기사의 내부 분류를 나타낸다.</summary>
    public enum NewsCategory
    {
        Game,
        MyPlayer,
        Club,
        League,
        Injury,
        TransferContract,
        Postseason,
        RecordsAwards,
        Offseason
    }

    /// <summary>뉴스 피드에서 플레이어가 선택할 수 있는 압축 필터다.</summary>
    public enum NewsFeedCategory
    {
        Latest,
        MyCareer,
        Club,
        League,
        TransferContract,
        RecordsAwards,
        CareerTimeline
    }

    /// <summary>확정된 도메인 사건을 기사 후보로 변환할 때 사용하는 사건 종류다.</summary>
    public enum NewsEventType
    {
        GameCompleted,
        PostseasonGameCompleted,
        PlayerGamePerformance,
        TeamStreakReached,
        LeagueBriefing,
        CareerMilestoneReached,
        PlayerRoleChanged,
        PlayerInjuryConfirmed,
        InjuryRecoveryStageReached,
        PlayerReturnedFromInjury,
        TeamRosterChanged,
        ContractSigned,
        PostseasonBerthClinched,
        PostseasonEliminated,
        PostseasonSeriesCompleted,
        ChampionshipWon,
        SeasonAwardGranted,
        OffseasonActivityCompleted,
        PlayerFormChanged,
        WeeklyReport,
        MonthlyReport,
        RoleCompetitionChanged,
        CareerMilestoneApproaching,
        ContractNegotiationReported,
        ContractNegotiationDeclined,
        TradeInterestReported,
        TradeRumorReported,
        TradeNegotiationReported,
        PlayerTraded
    }

    /// <summary>점수에서 파생되는 기사 노출 등급이다.</summary>
    public enum NewsImportance
    {
        D,
        C,
        B,
        A,
        S
    }

    /// <summary>기사가 전달되는 세계관 내부 출처다.</summary>
    public enum NewsSourceType
    {
        LeagueOfficial,
        LeagueSportsMedia,
        ClubNews,
        RegionalSports,
        NationalSports
    }

    /// <summary>한 기사 묶음 안에서 제목·리드·본문이 공유하는 서술 태도다.</summary>
    public enum NewsTone
    {
        Neutral,
        Positive,
        Analytical,
        Dramatic,
        Critical
    }

    /// <summary>결과 화면보다 기사가 먼저 공개되지 않도록 막는 공개 관문이다.</summary>
    public enum NewsReleaseGate
    {
        EndOfScheduleDate,
        AfterGameResult,
        AfterSeriesResult,
        AfterPostseasonReveal,
        AfterAwardReveal,
        AfterContractConfirmation
    }

    /// <summary>기사의 대표 대상 또는 관련 대상을 구분한다.</summary>
    public enum NewsSubjectType
    {
        Player,
        Team,
        Game,
        League,
        Contract,
        Award,
        OffseasonActivity
    }

    /// <summary>한 선수의 커리어 사건을 여러 기사에 걸쳐 연결하는 이야기 종류다.</summary>
    public enum NewsStorylineType
    {
        RisingForm,
        Slump,
        RosterCompetition,
        RookieBreakout,
        InjuryReturn,
        ContractSeason,
        RecordChase,
        PostseasonRun,
        RoleChange,
        TradeRumor
    }

    /// <summary>종료된 스토리라인이 어떤 결말에 도달했는지 기록한다.</summary>
    public enum NewsStorylineResolution
    {
        None,
        Succeeded,
        Stabilized,
        LostCompetition,
        Transferred,
        Recovered,
        Eliminated,
        Champion,
        SeasonEnded,
        Declined
    }

    /// <summary>단신·일반·주요 기사의 본문 밀도를 구분한다.</summary>
    public enum NewsArticleLength
    {
        Brief,
        Standard,
        Feature
    }

    /// <summary>기사 템플릿과 화면이 공유하는 구조화된 사실 키다.</summary>
    public enum NewsFactKey
    {
        PlayerName,
        TeamName,
        OpponentName,
        LeagueName,
        GameId,
        TeamRuns,
        OpponentRuns,
        DidWin,
        DidLose,
        DidTie,
        DidAppear,
        IsPitcher,
        GameAtBats,
        GameHits,
        GameHomeRuns,
        GameRbi,
        GameRuns,
        GameStrikeouts,
        GameInningsOuts,
        GameEarnedRuns,
        GamePerformanceSummary,
        GameStatLine,
        HasNotablePerformance,
        SeasonBattingAverage,
        SeasonHomeRuns,
        SeasonHits,
        SeasonRbi,
        SeasonEra,
        SeasonStrikeouts,
        TeamWinningStreak,
        TeamLosingStreak,
        TeamRank,
        TeamWins,
        TeamLosses,
        TeamRecordSummary,
        RoundGames,
        CareerMilestone,
        ExpectedAbsenceGames,
        RecoveryStage,
        PreviousRole,
        NewRole,
        ContractYears,
        ContractSalary,
        PostseasonRound,
        PostseasonSeriesScore,
        AwardName,
        OffseasonActivityName,
        CoverageReason,
        GamePlateAppearances,
        GameDoubles,
        GameTriples,
        GameWalks,
        GameHitByPitches,
        IsOneRunGame,
        ScoreMargin,
        HitStreak,
        HitlessStreak,
        PreviousHitlessStreak,
        GamesSinceLastHit,
        RecentFiveGames,
        RecentFiveAtBats,
        RecentFiveHits,
        ManagerTrustBefore,
        ManagerTrustAfter,
        ManagerTrustChange,
        PlayerRole,
        FormSlump,
        FormRebound,
        PreviousTeamRank,
        RoundScoreSummary,
        StandingChangeSummary,
        PlayerGameSummary,
        LeaderChanged,
        ReportLabel,
        ReportGames,
        ReportAtBats,
        ReportHits,
        ReportHomeRuns,
        ReportRbi,
        ReportTeamWins,
        ReportTeamLosses,
        ReportTrend,
        FormHot,
        FormCooled,
        RoleCompetitionStarted,
        RoleCompetitionResolved,
        MilestoneTarget,
        MilestoneName,
        InterestedTeamName,
        PreviousTeamName,
        NewTeamName,
        TradeStage,
        ProjectedRole,
        ManagerComment,
        ManagerStyle
    }

    /// <summary>숫자와 텍스트를 손실 없이 보관하는 Fact 값 종류다.</summary>
    public enum NewsFactValueType
    {
        Integer,
        Decimal,
        Text,
        Boolean
    }

    /// <summary>한글 조사 선택에서 받침의 특수 규칙까지 구분한다.</summary>
    internal enum KoreanFinalConsonantType
    {
        None,
        Rieul,
        Other
    }

    /// <summary>시즌 단계와 주기 번호를 합쳐 뉴스 발행 단위를 결정론적으로 식별한다.</summary>
    public readonly struct NewsCycleKey : IEquatable<NewsCycleKey>
    {
        public NewsCycleKey(int seasonId, SeasonPhase phase, int cycleIndex)
        {
            if (seasonId <= 0) throw new ArgumentOutOfRangeException(nameof(seasonId));
            if (cycleIndex < 0) throw new ArgumentOutOfRangeException(nameof(cycleIndex));
            SeasonId = seasonId;
            Phase = phase;
            CycleIndex = cycleIndex;
        }

        public int SeasonId { get; }
        public SeasonPhase Phase { get; }
        public int CycleIndex { get; }

        /// <summary>쿨다운 비교에 사용하는 시즌 내 고정 순번을 반환한다.</summary>
        public int ToOrdinal()
        {
            return checked(SeasonId * 100_000 + (int)Phase * 10_000 + CycleIndex);
        }

        public bool Equals(NewsCycleKey other)
        {
            return SeasonId == other.SeasonId && Phase == other.Phase && CycleIndex == other.CycleIndex;
        }

        public override bool Equals(object obj) => obj is NewsCycleKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SeasonId;
                hash = hash * 397 ^ (int)Phase;
                hash = hash * 397 ^ CycleIndex;
                return hash;
            }
        }

        public override string ToString() => $"{SeasonId}:{Phase}:{CycleIndex}";
    }

    /// <summary>실제 달력 날짜와 시즌 내 발행 위치를 함께 저장한다.</summary>
    public readonly struct CareerDate : IComparable<CareerDate>, IEquatable<CareerDate>
    {
        public CareerDate(int seasonId, int year, int month, int day, SeasonPhase phase, int cycleIndex)
        {
            CalendarDate = new DateTime(year, month, day);
            Cycle = new NewsCycleKey(seasonId, phase, cycleIndex);
        }

        public CareerDate(NewsCycleKey cycle, DateTime calendarDate)
        {
            Cycle = cycle;
            CalendarDate = calendarDate.Date;
        }

        public NewsCycleKey Cycle { get; }
        public DateTime CalendarDate { get; }
        public int Year => CalendarDate.Year;
        public int Month => CalendarDate.Month;
        public int Day => CalendarDate.Day;

        public int CompareTo(CareerDate other)
        {
            int date = CalendarDate.CompareTo(other.CalendarDate);
            return date != 0 ? date : Cycle.ToOrdinal().CompareTo(other.Cycle.ToOrdinal());
        }

        public bool Equals(CareerDate other)
        {
            return Cycle.Equals(other.Cycle) && CalendarDate.Equals(other.CalendarDate);
        }

        public override bool Equals(object obj) => obj is CareerDate other && Equals(other);
        public override int GetHashCode() => Cycle.GetHashCode() * 397 ^ CalendarDate.GetHashCode();
        public override string ToString() => $"{CalendarDate:yyyy-MM-dd} ({Cycle})";
    }
}
