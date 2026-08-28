using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Simulation.Career;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 시즌 전환이 계약 만료로 플레이어의 오퍼 선택을 기다리는 중인지 구분한다.
    /// </summary>
    public enum SeasonTransitionStep
    {
        NotStarted,
        ContractOffers,
        Completed
    }

    /// <summary>
    /// 완료된 오프시즌을 마감하고, 다음 시즌의 리그·로스터·일정·계약을 결정론적으로 이어 붙인다.
    /// 계약이 만료된 시즌에는 재계약 오퍼를 제시하고 플레이어의 선택을 기다린다.
    /// </summary>
    public sealed class CareerSeasonTransitionService
    {
        private const ulong RosterTurnoverStream = 0x524F535445525455UL;
        private const ulong ScheduleStream = 0x5343484544554C45UL;
        private const ulong ContractRenewalStream = 0x52454E4557414C31UL;

        private readonly CareerState _career;
        private readonly BalanceTable _balance;

        private TeamState[] _nextTeams;
        private ContractOffer[] _renewalOffers = Array.Empty<ContractOffer>();
        private ContractOffer? _selectedOffer;
        private CareerSeasonTransitionResult? _result;
        private int _nextYear;
        private int _nextSeasonId;

        public CareerSeasonTransitionService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public SeasonTransitionStep Step { get; private set; }
        public IReadOnlyList<ContractOffer> RenewalOffers => _renewalOffers;
        public ContractOffer? SelectedOffer => _selectedOffer;
        public CareerSeasonTransitionResult? Result => _result;

        /// <summary>
        /// 다음 시즌 로스터를 확정하고, 계약이 만료됐으면 재계약 오퍼를 제시한 채 멈춘다.
        /// 계약 기간이 남아 있으면 곧바로 다음 시즌 정규 시즌까지 전환한다.
        /// </summary>
        /// <remarks>
        /// 이 단계에서는 커리어 상태를 전혀 바꾸지 않는다. 오퍼 화면에서 플레이어가 중단해도
        /// 세이브가 반쯤 전환된 상태로 남지 않게 하려는 의도이며, 모든 변경은 확정 단계에서 한 번에 일어난다.
        /// </remarks>
        public SeasonTransitionStep BeginTransition()
        {
            RequireStep(SeasonTransitionStep.NotStarted);
            SeasonState completedSeason = RequireOffseasonSeason();

            _nextYear = completedSeason.Year + 1;
            _nextSeasonId = completedSeason.SeasonId + 1;
            _nextTeams = AdvanceRosters(_nextSeasonId, _career.League.RandomSeed);

            PlayerContractState contract = _career.CurrentContract;
            bool stillCovered = contract.SignedYear + contract.ContractYears - 1 >= _nextYear;
            if (stillCovered)
            {
                CommitNextSeason(renewalOffer: null);
                return Step;
            }

            _renewalOffers = BuildRenewalOffers();
            Step = SeasonTransitionStep.ContractOffers;
            return Step;
        }

        /// <summary>
        /// 제시된 재계약 오퍼 중 계약할 구단을 선택한다.
        /// </summary>
        public void SelectRenewalOffer(int teamId)
        {
            RequireStep(SeasonTransitionStep.ContractOffers);
            for (int index = 0; index < _renewalOffers.Length; index++)
            {
                if (_renewalOffers[index].Team.TeamId != teamId)
                    continue;

                _selectedOffer = _renewalOffers[index];
                return;
            }

            throw new ArgumentException("선택할 수 없는 계약 오퍼입니다.", nameof(teamId));
        }

        /// <summary>
        /// 선택한 오퍼로 재계약을 확정하고 다음 시즌 정규 시즌을 시작한다.
        /// </summary>
        public CareerSeasonTransitionResult SignSelectedOffer()
        {
            RequireStep(SeasonTransitionStep.ContractOffers);
            if (!_selectedOffer.HasValue)
                throw new InvalidOperationException("먼저 계약할 구단을 선택해 주세요.");
            return CommitNextSeason(_selectedOffer.Value);
        }

        /// <summary>
        /// 오퍼 선택 없이 시즌 전환을 끝까지 진행한다. 계약이 만료됐으면 점수가 가장 높은 오퍼를 수락한다.
        /// </summary>
        /// <remarks>
        /// EditMode 테스트와 여러 시즌 대량 시뮬레이션은 화면 없이 돌아야 하므로 자동 진행 경로를 남긴다.
        /// 실제 플레이는 BeginTransition → SelectRenewalOffer → SignSelectedOffer 경로를 쓴다.
        /// </remarks>
        public CareerSeasonTransitionResult AdvanceToNextSeason()
        {
            if (BeginTransition() == SeasonTransitionStep.ContractOffers)
            {
                SelectRenewalOffer(_renewalOffers[0].Team.TeamId);
                return SignSelectedOffer();
            }

            return _result.Value;
        }

        /// <summary>
        /// 오프시즌 마감부터 다음 시즌 시작까지의 모든 상태 변경을 한 번에 적용한다.
        /// </summary>
        private CareerSeasonTransitionResult CommitNextSeason(ContractOffer? renewalOffer)
        {
            SeasonState completedSeason = _career.League.CurrentSeason;
            ulong rootSeed = _career.League.RandomSeed;
            _career.CurrentOffseason.CompleteRemainingWeeks();
            completedSeason.CompleteArchive();

            TeamState previousPlayerTeam = GetTeam(_career.League.Teams, _career.MyPlayer.CurrentTeamId);
            var archivedRecord = new CareerSeasonHistoryRecord(
                completedSeason.Year,
                completedSeason.LeagueLevel,
                previousPlayerTeam.TeamId,
                previousPlayerTeam.Name,
                completedSeason.GetTeamRecord(previousPlayerTeam.TeamId),
                completedSeason.PlayerStatistics,
                completedSeason.PostseasonPlayerStatistics,
                completedSeason.Postseason,
                completedSeason.Awards,
                completedSeason.Settlement);

            _career.MyPlayer.AdvanceAge();

            int signedTeamId = previousPlayerTeam.TeamId;
            if (renewalOffer.HasValue)
            {
                ContractOffer offer = renewalOffer.Value;
                _career.RenewContract(new PlayerContractState(
                    NewGameFlow.CurrentSaveVersion,
                    offer.Team.TeamId,
                    _nextYear,
                    offer.ContractYears,
                    offer.SigningBonus,
                    offer.AnnualSalary,
                    offer.ExpectedRole));
                if (offer.SigningBonus > 0L)
                {
                    _career.Economy.Earn(
                        _nextYear,
                        MoneyTransactionType.ContractIncome,
                        $"contract_{_nextSeasonId}_signing_bonus",
                        offer.SigningBonus);
                }
                signedTeamId = offer.Team.TeamId;
                if (signedTeamId != _career.MyPlayer.CurrentTeamId)
                    _career.MyPlayer.TransferTo(signedTeamId);
            }

            SeasonState nextSeason = BuildNextSeason(
                _nextSeasonId,
                _nextYear,
                completedSeason.LeagueLevel,
                _nextTeams,
                rootSeed);
            var nextLeague = new LeagueState(
                NewGameFlow.CurrentSaveVersion,
                _nextYear,
                rootSeed,
                _nextTeams,
                nextSeason);

            _career.AdvanceToNextSeason(nextLeague, archivedRecord);
            _career.MyPlayer.InitializeSeasonStatus(
                _balance.CareerSeason.InitialCondition,
                _balance.CareerSeason.InitialManagerEvaluation);

            Step = SeasonTransitionStep.Completed;
            _result = new CareerSeasonTransitionResult(
                _nextYear,
                signedTeamId,
                signedTeamId != previousPlayerTeam.TeamId);
            return _result.Value;
        }

        /// <summary>
        /// 새 게임 입단과 같은 규칙으로 다음 시즌 로스터 기준의 재계약 오퍼 목록을 만든다.
        /// </summary>
        private ContractOffer[] BuildRenewalOffers()
        {
            ulong offerSeed = DeterministicSeed.Derive(
                _career.League.RandomSeed,
                ContractRenewalStream ^ (uint)_nextSeasonId);
            var evaluator = new ContractOfferEvaluator(
                _balance.ContractOffer,
                _balance.PlayerEvaluation,
                new Pcg32Random(offerSeed));

            var generatedTeams = new GeneratedTeam[_nextTeams.Length];
            for (int index = 0; index < _nextTeams.Length; index++)
                generatedTeams[index] = ToGeneratedTeam(_nextTeams[index]);

            return ContractOfferBoard.SelectOffers(
                _balance.ContractOffer,
                evaluator,
                _career.MyPlayer.ToPlayer(),
                generatedTeams,
                _career.League.CurrentSeason.Settlement.ContractEvaluationBonus);
        }

        private SeasonState RequireOffseasonSeason()
        {
            SeasonState season = _career.League.CurrentSeason;
            if (season?.Phase != SeasonPhase.Offseason)
                throw new InvalidOperationException("오프시즌 상태의 커리어만 다음 시즌으로 전환할 수 있습니다.");
            if (_career.CurrentOffseason == null)
                throw new InvalidOperationException("진행 중인 오프시즌이 없습니다.");
            return season;
        }

        private void RequireStep(SeasonTransitionStep expected)
        {
            if (Step != expected)
            {
                throw new InvalidOperationException(
                    $"현재 단계({Step})에서는 {expected} 작업을 수행할 수 없습니다.");
            }
        }

        private TeamState[] AdvanceRosters(int nextSeasonId, ulong rootSeed)
        {
            IReadOnlyList<TeamState> teams = _career.League.Teams;
            CompetitionStatisticsState completedStatistics =
                _career.League.CurrentSeason.LeagueStatistics.RegularSeason;
            var result = new TeamState[teams.Count];
            for (int index = 0; index < teams.Count; index++)
            {
                TeamState team = teams[index];
                ulong teamSeed = DeterministicSeed.Derive(
                    rootSeed,
                    RosterTurnoverStream ^ ((ulong)(uint)nextSeasonId << 32) ^ (uint)team.TeamId);
                var resolver = new RosterTurnoverResolver(
                    _balance.TeamGeneration,
                    _balance.RosterTurnover,
                    new Pcg32Random(teamSeed));

                RosterCompetitor[] currentRoster = ToRosterCompetitors(team.RosterCompetitors);
                int[] positionNeeds = BuildPositionNeeds(team);
                RosterCompetitor[] nextRoster = resolver.AdvanceSeason(currentRoster, positionNeeds);
                result[index] = team.WithRoster(ToRosterCompetitorStates(
                    nextRoster,
                    team.RosterCompetitors,
                    completedStatistics));
            }
            return result;
        }

        private SeasonState BuildNextSeason(
            int nextSeasonId,
            int nextYear,
            LeagueLevel leagueLevel,
            TeamState[] teams,
            ulong rootSeed)
        {
            var season = new SeasonState(NewGameFlow.CurrentSaveVersion, nextSeasonId, nextYear, leagueLevel);
            var teamIds = new int[teams.Length];
            var teamRecords = new TeamSeasonRecordState[teams.Length];
            for (int index = 0; index < teams.Length; index++)
            {
                teamIds[index] = teams[index].TeamId;
                teamRecords[index] = new TeamSeasonRecordState(
                    teams[index].TeamId,
                    DeterministicSeed.Derive(
                        rootSeed,
                        0x544945425245414BUL ^ ((ulong)(uint)nextSeasonId << 32) ^ (uint)teams[index].TeamId));
            }

            ulong scheduleSeed = DeterministicSeed.Derive(rootSeed, ScheduleStream ^ (uint)nextSeasonId);
            var scheduleGenerator = new SeasonScheduleGenerator(new Pcg32Random(scheduleSeed));
            ScheduledGameDefinition[] definitions = scheduleGenerator.Generate(
                teamIds,
                _balance.CareerSeason.RegularSeasonGamesPerTeam);
            var games = new ScheduledGameState[definitions.Length];
            for (int index = 0; index < definitions.Length; index++)
            {
                ScheduledGameDefinition definition = definitions[index];
                ulong streamId = ((ulong)nextSeasonId << 32) | (uint)definition.GameId;
                games[index] = new ScheduledGameState(
                    definition.GameId,
                    definition.Round,
                    DeterministicSeed.Derive(rootSeed, streamId),
                    definition.AwayTeamId,
                    definition.HomeTeamId);
            }

            season.StartRegularSeason(
                new SeasonScheduleState(games),
                teamRecords,
                new PlayerSeasonStatisticsState(),
                _career.MyPlayer);
            GetMyPlayerCareerUsage(
                out int careerPlateAppearances,
                out int careerPitchingOuts,
                out int registeredSeasons);
            season.SnapshotRookieEligibility(
                teams,
                _career.MyPlayer,
                _balance.SeasonAwards,
                careerPlateAppearances,
                careerPitchingOuts,
                registeredSeasons);
            return season;
        }

        private static RosterCompetitor[] ToRosterCompetitors(IReadOnlyList<RosterCompetitorState> source)
        {
            var result = new RosterCompetitor[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                RosterCompetitorState state = source[index];
                result[index] = new RosterCompetitor(state.PlayerId, state.Name, state.Position, state.Overall);
            }
            return result;
        }

        private static RosterCompetitorState[] ToRosterCompetitorStates(
            RosterCompetitor[] source,
            IReadOnlyList<RosterCompetitorState> previousRoster,
            CompetitionStatisticsState completedStatistics)
        {
            var result = new RosterCompetitorState[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                RosterCompetitor competitor = source[index];
                RosterCompetitorState? previous = FindPreviousRosterPlayer(
                    previousRoster,
                    competitor.PlayerId);
                int careerPlateAppearances = 0;
                int careerPitchingOuts = 0;
                int registeredSeasons = 0;
                if (previous.HasValue)
                {
                    RosterCompetitorState previousValue = previous.Value;
                    PlayerCompetitionStatisticsState seasonStatistics =
                        completedStatistics.GetPlayer(competitor.PlayerId);
                    careerPlateAppearances = previousValue.CareerPlateAppearances +
                                             (seasonStatistics?.Batting.PlateAppearances ?? 0);
                    careerPitchingOuts = previousValue.CareerPitchingOuts +
                                         (seasonStatistics?.Pitching.OutsRecorded ?? 0);
                    registeredSeasons = previousValue.RegisteredSeasons + 1;
                }
                result[index] = new RosterCompetitorState(
                    competitor.PlayerId,
                    competitor.Name,
                    competitor.Position,
                    competitor.Overall,
                    careerPlateAppearances,
                    careerPitchingOuts,
                    registeredSeasons);
            }
            return result;
        }

        private static RosterCompetitorState? FindPreviousRosterPlayer(
            IReadOnlyList<RosterCompetitorState> previousRoster,
            int playerId)
        {
            for (int index = 0; index < previousRoster.Count; index++)
            {
                if (previousRoster[index].PlayerId == playerId)
                    return previousRoster[index];
            }
            return null;
        }

        private void GetMyPlayerCareerUsage(
            out int careerPlateAppearances,
            out int careerPitchingOuts,
            out int registeredSeasons)
        {
            careerPlateAppearances = 0;
            careerPitchingOuts = 0;
            IReadOnlyList<CareerSeasonHistoryRecord> history = _career.SeasonHistory;
            for (int index = 0; index < history.Count; index++)
            {
                PlayerSeasonStatisticsState statistics = history[index].Statistics;
                if (statistics == null) continue;
                careerPlateAppearances += statistics.PlateAppearances;
                careerPitchingOuts += statistics.OutsRecorded;
            }

            PlayerSeasonStatisticsState current = _career.League.CurrentSeason.PlayerStatistics;
            if (current != null)
            {
                careerPlateAppearances += current.PlateAppearances;
                careerPitchingOuts += current.OutsRecorded;
            }
            registeredSeasons = history.Count + 1;
        }

        private static int[] BuildPositionNeeds(TeamState team)
        {
            var needs = new int[(int)PlayerPosition.ReliefPitcher + 1];
            for (int rawPosition = (int)PlayerPosition.Catcher;
                 rawPosition <= (int)PlayerPosition.ReliefPitcher;
                 rawPosition++)
            {
                needs[rawPosition] = team.GetPositionNeed((PlayerPosition)rawPosition);
            }
            return needs;
        }

        private static GeneratedTeam ToGeneratedTeam(TeamState team)
        {
            int[] positionNeeds = BuildPositionNeeds(team);
            var competitors = new RosterCompetitor[team.RosterCompetitors.Count];
            for (int index = 0; index < competitors.Length; index++)
            {
                RosterCompetitorState state = team.RosterCompetitors[index];
                competitors[index] = new RosterCompetitor(state.PlayerId, state.Name, state.Position, state.Overall);
            }
            return new GeneratedTeam(team.TeamId, team.Name, team.Archetype, team.PrimaryColor, positionNeeds, competitors);
        }

        private static TeamState GetTeam(IReadOnlyList<TeamState> teams, int teamId)
        {
            for (int index = 0; index < teams.Count; index++)
            {
                if (teams[index].TeamId == teamId)
                    return teams[index];
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 시즌 전환 결과로 다음 시즌 연도와 소속 구단, 이적 여부를 Presentation에 전달한다.
    /// </summary>
    public readonly struct CareerSeasonTransitionResult
    {
        public CareerSeasonTransitionResult(int year, int teamId, bool wasTraded)
        {
            Year = year;
            TeamId = teamId;
            WasTraded = wasTraded;
        }

        public int Year { get; }
        public int TeamId { get; }
        public bool WasTraded { get; }
    }
}
