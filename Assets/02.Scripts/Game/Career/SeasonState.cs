using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Rules;
using Baseball.Game.Career.Narrative;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 선수 커리어가 진행되는 리그 단계를 구분한다.
    /// </summary>
    public enum LeagueLevel
    {
        Rookie = 0,
        Minor = 1,
        Major = 2,
        World = 3,
        AllStar = 4,
        Classic = 5,
        Winners = 6,
        Champion = 7,
        Master = 8,
        Galaxy = 9
    }

    /// <summary>리그 단계의 순서와 경계 탐색을 이름 분기 없이 제공한다.</summary>
    public static class LeagueLevelRules
    {
        public const int Count = 10;

        public static bool IsValid(LeagueLevel level) =>
            (int)level >= (int)LeagueLevel.Rookie && (int)level <= (int)LeagueLevel.Galaxy;

        public static bool TryGetHigher(LeagueLevel level, out LeagueLevel higher)
        {
            if (!IsValid(level))
                throw new ArgumentOutOfRangeException(nameof(level));
            if (level == LeagueLevel.Galaxy)
            {
                higher = level;
                return false;
            }
            higher = level + 1;
            return true;
        }

        public static bool TryGetLower(LeagueLevel level, out LeagueLevel lower)
        {
            if (!IsValid(level))
                throw new ArgumentOutOfRangeException(nameof(level));
            if (level == LeagueLevel.Rookie)
            {
                lower = level;
                return false;
            }
            lower = level - 1;
            return true;
        }

        public static int GetDistance(LeagueLevel from, LeagueLevel to)
        {
            if (!IsValid(from)) throw new ArgumentOutOfRangeException(nameof(from));
            if (!IsValid(to)) throw new ArgumentOutOfRangeException(nameof(to));
            return Math.Abs((int)to - (int)from);
        }
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
        private readonly Dictionary<int, int> _expectedTeamRanks = new();
        private readonly List<MatchNarrativeSnapshot> _matchNarrativeSnapshots = new();
        private int[] _finalStandingTeamIds = Array.Empty<int>();
        private LeagueTiebreakGameState[] _tiebreakGames = Array.Empty<LeagueTiebreakGameState>();

        public SeasonState(
            int saveVersion,
            int seasonId,
            int year,
            LeagueLevel leagueLevel,
            SimulationVersionStamp? versionStamp = null)
        {
            if (seasonId <= 0)
                throw new ArgumentOutOfRangeException(nameof(seasonId));

            SaveVersion = saveVersion;
            SeasonId = seasonId;
            Year = year;
            LeagueLevel = leagueLevel;
            VersionStamp = versionStamp ?? SimulationVersionStamp.CreateCurrent(balanceVersion: 0);
            Phase = SeasonPhase.Preseason;
            LeagueStatistics = new LeagueSeasonStatisticsState();
            Settlement = new SeasonSettlementState();
        }

        public int SaveVersion { get; }
        public int SeasonId { get; }
        public int Year { get; }
        public LeagueLevel LeagueLevel { get; }
        public SimulationVersionStamp VersionStamp { get; private set; }
        public SeasonPhase Phase { get; private set; }
        public SeasonScheduleState Schedule { get; private set; }
        public IReadOnlyList<TeamSeasonRecordState> TeamRecords { get; private set; }
        public PlayerSeasonStatisticsState PlayerStatistics { get; private set; }
        public LeagueSeasonStatisticsState LeagueStatistics { get; }
        public PostseasonState Postseason { get; private set; }
        public SeasonAwardsState Awards { get; private set; }
        public SeasonReviewState Review { get; private set; }
        public SeasonReviewSnapshot ReviewSnapshot { get; private set; }
        public LeagueAdjustedStatisticsSnapshot AdjustedStatistics { get; private set; }
        public SeasonSettlementState Settlement { get; }
        public IReadOnlyList<MatchNarrativeSnapshot> MatchNarrativeSnapshots => _matchNarrativeSnapshots;
        public IReadOnlyList<int> FinalStandingTeamIds => _finalStandingTeamIds;
        public IReadOnlyList<LeagueTiebreakGameState> TiebreakGames => _tiebreakGames;

        /// <summary>시즌 첫 경기 전에 최신 규칙을 고정하며 진행 중에는 변경을 거부한다.</summary>
        public void PinVersionStamp(SimulationVersionStamp versionStamp)
        {
            if (Phase != SeasonPhase.Preseason)
                throw new InvalidOperationException("시즌 규칙 버전은 Preseason에서만 고정할 수 있습니다.");
            VersionStamp = versionStamp;
        }

        /// <summary>구버전 세이브의 진행 단계는 유지하고 이후 판정에 사용할 버전만 채운다.</summary>
        public void MigrateVersionStamp(SimulationVersionStamp versionStamp)
        {
            VersionStamp = versionStamp;
        }

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
            PlayerState myPlayer = null,
            IReadOnlyList<TeamState> teams = null)
        {
            if (Phase != SeasonPhase.Preseason)
                throw new InvalidOperationException("정규 시즌은 Preseason에서만 시작할 수 있습니다.");
            Schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            TeamRecords = teamRecords ?? throw new ArgumentNullException(nameof(teamRecords));
            PlayerStatistics = playerStatistics ?? throw new ArgumentNullException(nameof(playerStatistics));
            if (myPlayer != null)
            {
                myPlayer.SkillBoardState.LockForSeason();
                PlayerCompetitionStatisticsState source = LeagueStatistics.RegularSeason.GetOrCreate(
                    myPlayer.PlayerId,
                    myPlayer.Name,
                    myPlayer.CurrentTeamId,
                    myPlayer.PrimaryPosition);
                PlayerStatistics.BindTo(source);
            }

            SnapshotExpectedTeamRanks(teamRecords, teams);

            Phase = SeasonPhase.RegularSeason;
        }

        public int GetExpectedTeamRank(int teamId) =>
            _expectedTeamRanks.TryGetValue(teamId, out int rank) ? rank : 0;

        /// <summary>승격·포스트시즌·잔류 경계 결정전을 반영한 정규시즌 최종 순서를 고정한다.</summary>
        public void FinalizeStandings(
            int[] orderedTeamIds,
            LeagueTiebreakGameState[] tiebreakGames)
        {
            if (Phase != SeasonPhase.RegularSeason)
                throw new InvalidOperationException("정규시즌 진행 중에만 최종 순위를 확정할 수 있습니다.");
            if (orderedTeamIds == null || TeamRecords == null || orderedTeamIds.Length != TeamRecords.Count)
                throw new ArgumentException("최종 순위에는 모든 구단이 한 번씩 포함되어야 합니다.", nameof(orderedTeamIds));
            if (_finalStandingTeamIds.Length > 0)
                throw new InvalidOperationException("정규시즌 최종 순위는 한 번만 확정할 수 있습니다.");
            for (int index = 0; index < orderedTeamIds.Length; index++)
            {
                bool exists = false;
                for (int recordIndex = 0; recordIndex < TeamRecords.Count; recordIndex++)
                {
                    if (TeamRecords[recordIndex].TeamId == orderedTeamIds[index])
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    throw new ArgumentException("현재 리그에 없는 구단이 최종 순위에 포함됐습니다.", nameof(orderedTeamIds));
                for (int previous = 0; previous < index; previous++)
                {
                    if (orderedTeamIds[previous] == orderedTeamIds[index])
                        throw new ArgumentException("최종 순위에 같은 구단이 중복됐습니다.", nameof(orderedTeamIds));
                }
            }
            _finalStandingTeamIds = (int[])orderedTeamIds.Clone();
            _tiebreakGames = tiebreakGames == null
                ? Array.Empty<LeagueTiebreakGameState>()
                : (LeagueTiebreakGameState[])tiebreakGames.Clone();
        }

        private void SnapshotExpectedTeamRanks(
            IReadOnlyList<TeamSeasonRecordState> teamRecords,
            IReadOnlyList<TeamState> teams)
        {
            _expectedTeamRanks.Clear();
            var ordered = new TeamSeasonRecordState[teamRecords.Count];
            for (int index = 0; index < teamRecords.Count; index++)
                ordered[index] = teamRecords[index];
            Array.Sort(ordered, (left, right) =>
            {
                int strength = GetTeamStrength(teams, right.TeamId)
                    .CompareTo(GetTeamStrength(teams, left.TeamId));
                if (strength != 0)
                    return strength;
                int tiebreaker = right.FixedTiebreaker.CompareTo(left.FixedTiebreaker);
                return tiebreaker != 0 ? tiebreaker : left.TeamId.CompareTo(right.TeamId);
            });
            for (int index = 0; index < ordered.Length; index++)
                _expectedTeamRanks[ordered[index].TeamId] = index + 1;
        }

        private static double GetTeamStrength(IReadOnlyList<TeamState> teams, int teamId)
        {
            if (teams == null)
                return 0d;
            for (int teamIndex = 0; teamIndex < teams.Count; teamIndex++)
            {
                TeamState team = teams[teamIndex];
                if (team.TeamId != teamId)
                    continue;
                int total = 0;
                for (int playerIndex = 0; playerIndex < team.RosterCompetitors.Count; playerIndex++)
                    total += team.RosterCompetitors[playerIndex].Overall;
                return team.RosterCompetitors.Count == 0
                    ? 0d
                    : total / (double)team.RosterCompetitors.Count;
            }
            return 0d;
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

        /// <summary>계약·커리어 평가가 소비할 당시 리그 강도와 선수 백분위를 한 번 고정한다.</summary>
        public void FinalizeAdjustedStatistics(LeagueState league)
        {
            if (league == null) throw new ArgumentNullException(nameof(league));
            if (league.CurrentSeason != this)
                throw new InvalidOperationException("다른 시즌의 리그 조정 기록을 연결할 수 없습니다.");
            AdjustedStatistics ??= new LeagueAdjustedStatisticsService().Build(league);
        }
    }
}
