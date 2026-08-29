using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Game.Career.Narrative;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 선수 커리어가 진행되는 리그 단계를 구분한다.
    /// </summary>
    public enum LeagueLevel
    {
        Rookie,
        Minor,
        Major
    }

    /// <summary>
    /// 생성 직후부터 정규 시즌 진입까지의 시즌 상태를 구분한다.
    /// </summary>
    public enum SeasonPhase
    {
        Preseason,
        RegularSeason,
        Postseason,
        SeasonReview,
        Offseason,
        Completed
    }

    /// <summary>
    /// 여러 시즌 누적을 전제로 현재 시즌의 식별자와 진행 상태를 보관한다.
    /// </summary>
    public sealed class SeasonState
    {
        private readonly Dictionary<int, bool> _rookieEligibilitySnapshot = new();
        private readonly List<MatchNarrativeSnapshot> _matchNarrativeSnapshots = new();

        public SeasonState(int saveVersion, int seasonId, int year, LeagueLevel leagueLevel)
        {
            if (seasonId <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonId));

            SaveVersion = saveVersion;
            SeasonId = seasonId;
            Year = year;
            LeagueLevel = leagueLevel;
            Phase = SeasonPhase.Preseason;
            LeagueStatistics = new LeagueSeasonStatisticsState();
            Settlement = new SeasonSettlementState();
        }

        public int SaveVersion { get; }
        public int SeasonId { get; }
        public int Year { get; }
        public LeagueLevel LeagueLevel { get; }
        public SeasonPhase Phase { get; private set; }
        public SeasonScheduleState Schedule { get; private set; }
        public IReadOnlyList<TeamSeasonRecordState> TeamRecords { get; private set; }
        public PlayerSeasonStatisticsState PlayerStatistics { get; private set; }
        public LeagueSeasonStatisticsState LeagueStatistics { get; }
        public PostseasonState Postseason { get; private set; }
        public SeasonAwardsState Awards { get; private set; }
        public SeasonReviewState Review { get; private set; }
        public SeasonReviewSnapshot ReviewSnapshot { get; private set; }
        public SeasonSettlementState Settlement { get; }
        public IReadOnlyList<MatchNarrativeSnapshot> MatchNarrativeSnapshots => _matchNarrativeSnapshots;

        /// <summary>
        /// 포스트시즌 기록은 정규 시즌 기록과 절대 합산하지 않으므로 별도 누적기로 보관한다.
        /// 포스트시즌에 진입하기 전에는 null이다.
        /// </summary>
        public PlayerSeasonStatisticsState PostseasonPlayerStatistics { get; private set; }

        /// <summary>경기 종료 당시의 사실과 선택 문장을 GameId별로 한 번만 보관한다.</summary>
        public void RecordMatchNarrative(MatchNarrativeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.SeasonId != SeasonId)
                throw new InvalidOperationException("현재 시즌과 경기 내러티브의 SeasonId가 다릅니다.");
            if (FindMatchNarrative(snapshot.GameId) != null)
                throw new InvalidOperationException($"GameId {snapshot.GameId}의 내러티브가 이미 저장됐습니다.");
            _matchNarrativeSnapshots.Add(snapshot);
        }

        /// <summary>과거 경기 화면과 뉴스가 라이브 기록 대신 고정 스냅샷을 조회하게 한다.</summary>
        public MatchNarrativeSnapshot FindMatchNarrative(int gameId)
        {
            for (int index = 0; index < _matchNarrativeSnapshots.Count; index++)
            {
                if (_matchNarrativeSnapshots[index].GameId == gameId)
                    return _matchNarrativeSnapshots[index];
            }
            return null;
        }

        /// <summary>
        /// 계약 완료 후 Rookie League 정규 시즌을 시작한다.
        /// </summary>
        public void StartRegularSeason(
            SeasonScheduleState schedule,
            TeamSeasonRecordState[] teamRecords,
            PlayerSeasonStatisticsState playerStatistics,
            PlayerState myPlayer = null)
        {
            if (Phase != SeasonPhase.Preseason)
                throw new InvalidOperationException("정규 시즌은 Preseason에서만 시작할 수 있습니다.");
            Schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            TeamRecords = teamRecords ?? throw new ArgumentNullException(nameof(teamRecords));
            PlayerStatistics = playerStatistics ?? throw new ArgumentNullException(nameof(playerStatistics));
            if (myPlayer != null)
            {
                PlayerCompetitionStatisticsState source = LeagueStatistics.RegularSeason.GetOrCreate(
                    myPlayer.PlayerId,
                    myPlayer.Name,
                    myPlayer.CurrentTeamId,
                    myPlayer.PrimaryPosition);
                PlayerStatistics.BindTo(source);
            }

            Phase = SeasonPhase.RegularSeason;
        }

        /// <summary>
        /// 포스트시즌을 생략하는 테스트 전용 지름길도 Completed가 아닌 결산 단계에 진입한다.
        /// </summary>
        public void CompleteRegularSeason()
        {
            if (Phase != SeasonPhase.RegularSeason)
                throw new InvalidOperationException("정규 시즌 진행 중에만 완료할 수 있습니다.");
            LeagueStatistics.FreezeRegularSeasonStatistics();
            Awards = new SeasonAwardsState();
            Review = new SeasonReviewState();
            Review.SkipToSeasonSummary();
            Phase = SeasonPhase.SeasonReview;
        }

        /// <summary>
        /// 마지막 정규 시즌 라운드가 끝난 뒤 확정된 대진으로 포스트시즌을 시작한다.
        /// </summary>
        public void BeginPostseason(
            PostseasonState postseason,
            PlayerSeasonStatisticsState postseasonPlayerStatistics,
            PlayerState myPlayer = null)
        {
            if (Phase != SeasonPhase.RegularSeason)
                throw new InvalidOperationException("정규 시즌 진행 중에만 포스트시즌을 시작할 수 있습니다.");
            Postseason = postseason ?? throw new ArgumentNullException(nameof(postseason));
            PostseasonPlayerStatistics = postseasonPlayerStatistics ??
                                         throw new ArgumentNullException(nameof(postseasonPlayerStatistics));
            LeagueStatistics.FreezeRegularSeasonStatistics();
            if (myPlayer != null)
            {
                PlayerCompetitionStatisticsState source = LeagueStatistics.Postseason.GetOrCreate(
                    myPlayer.PlayerId,
                    myPlayer.Name,
                    myPlayer.CurrentTeamId,
                    myPlayer.PrimaryPosition);
                PostseasonPlayerStatistics.BindTo(source);
            }
            Review = new SeasonReviewState();
            Phase = SeasonPhase.Postseason;
        }

        /// <summary>정규시즌 동결 직후 만든 결산 스냅샷을 현재 시즌에 한 번만 연결한다.</summary>
        public void AttachReviewSnapshot(SeasonReviewSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (ReviewSnapshot != null && !ReferenceEquals(ReviewSnapshot, snapshot))
                throw new InvalidOperationException("시즌 결산 스냅샷은 교체할 수 없습니다.");
            if (snapshot.SeasonId != SeasonId)
                throw new InvalidOperationException("현재 시즌과 결산 스냅샷의 SeasonId가 다릅니다.");
            ReviewSnapshot = snapshot;
        }

        /// <summary>v8 시즌의 현재 단계는 유지하면서 누락된 리뷰 진행 상태와 스냅샷을 v9 형식으로 채운다.</summary>
        public void MigrateSeasonReview(SeasonReviewSnapshot snapshot)
        {
            AttachReviewSnapshot(snapshot);
            Review ??= new SeasonReviewState();
            switch (Phase)
            {
                case SeasonPhase.SeasonReview:
                    if (Postseason?.IsCompleted == true)
                        Review.PreparePostseasonRecap();
                    else
                        Review.SkipToSeasonSummary();
                    break;
                case SeasonPhase.Offseason:
                case SeasonPhase.Completed:
                    Review.Complete();
                    break;
            }
        }

        /// <summary>
        /// 한국시리즈까지 끝난 포스트시즌을 마감하고 성장 결산이 가능한 완료 상태로 전환한다.
        /// </summary>
        public void CompletePostseason(SeasonAwardsState awards)
        {
            if (Phase != SeasonPhase.Postseason)
                throw new InvalidOperationException("포스트시즌 진행 중에만 완료할 수 있습니다.");
            if (Postseason?.IsCompleted != true)
                throw new InvalidOperationException("우승 구단이 확정되지 않았습니다.");
            Awards = awards ?? throw new ArgumentNullException(nameof(awards));
            Review ??= new SeasonReviewState();
            Review.PreparePostseasonRecap();
            Phase = SeasonPhase.SeasonReview;
        }

        /// <summary>
        /// 시즌 성장·노쇠 결산이 끝난 현재 시즌을 오프시즌으로 전환한다.
        /// </summary>
        public void BeginOffseason()
        {
            if (Phase != SeasonPhase.SeasonReview)
                throw new InvalidOperationException("결산 중인 시즌만 오프시즌으로 전환할 수 있습니다.");
            Phase = SeasonPhase.Offseason;
        }

        /// <summary>오프시즌까지 마친 시즌을 장기 기록에 보관할 수 있는 최종 상태로 확정한다.</summary>
        public void CompleteArchive()
        {
            if (Phase != SeasonPhase.Offseason)
                throw new InvalidOperationException("오프시즌을 마친 시즌만 보관 완료할 수 있습니다.");
            Phase = SeasonPhase.Completed;
        }

        /// <summary>시즌 시작 시 확정한 신인 자격을 이후 성적과 무관하게 저장한다.</summary>
        public void SnapshotRookieEligibility(
            IReadOnlyList<TeamState> teams,
            PlayerState myPlayer,
            SeasonAwardBalance balance,
            int myCareerPlateAppearances,
            int myCareerPitchingOuts,
            int myRegisteredSeasons)
        {
            if (myPlayer == null) throw new ArgumentNullException(nameof(myPlayer));
            SnapshotRookieEligibility(teams, balance);
            _rookieEligibilitySnapshot[myPlayer.PlayerId] = IsRookieEligible(
                myCareerPlateAppearances,
                myCareerPitchingOuts,
                myRegisteredSeasons,
                balance);
        }

        /// <summary>배경 리그 로스터 선수의 시즌 시작 시점 신인 자격을 고정한다.</summary>
        public void SnapshotRookieEligibility(
            IReadOnlyList<TeamState> teams,
            SeasonAwardBalance balance)
        {
            if (teams == null) throw new ArgumentNullException(nameof(teams));
            _rookieEligibilitySnapshot.Clear();
            for (int teamIndex = 0; teamIndex < teams.Count; teamIndex++)
            {
                IReadOnlyList<RosterCompetitorState> roster = teams[teamIndex].RosterCompetitors;
                for (int playerIndex = 0; playerIndex < roster.Count; playerIndex++)
                {
                    RosterCompetitorState player = roster[playerIndex];
                    _rookieEligibilitySnapshot[player.PlayerId] = IsRookieEligible(
                        player.CareerPlateAppearances,
                        player.CareerPitchingOuts,
                        player.RegisteredSeasons,
                        balance);
                }
            }
        }

        public bool IsRookieEligible(int playerId)
        {
            return _rookieEligibilitySnapshot.TryGetValue(playerId, out bool value) && value;
        }

        private static bool IsRookieEligible(
            int careerPlateAppearances,
            int careerPitchingOuts,
            int registeredSeasons,
            SeasonAwardBalance balance)
        {
            return careerPlateAppearances < balance.RookieCareerPlateAppearanceLimit &&
                   careerPitchingOuts < balance.RookieCareerPitchingOutLimit &&
                   registeredSeasons <= balance.RookieMaximumRegisteredSeasons;
        }

        public TeamSeasonRecordState GetTeamRecord(int teamId)
        {
            if (TeamRecords == null)
                return null;
            for (int index = 0; index < TeamRecords.Count; index++)
            {
                if (TeamRecords[index].TeamId == teamId)
                    return TeamRecords[index];
            }
            return null;
        }
    }
}
