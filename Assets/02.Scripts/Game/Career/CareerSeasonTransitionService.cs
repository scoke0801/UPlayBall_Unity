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
        CurrentTeamNegotiation,
        ContractOffers,
        Completed
    }

    /// <summary>
    /// 완료된 오프시즌을 마감하고, 다음 시즌의 리그·로스터·일정·계약을 결정론적으로 이어 붙인다.
    /// 계약이 만료된 시즌에는 재계약 오퍼를 제시하고 플레이어의 선택을 기다린다.
    /// </summary>
    public sealed class CareerSeasonTransitionService
    {
        private const ulong ContractRenewalStream = 0x52454E4557414C31UL;
        private const ulong HeldOfferStream = 0x484F4C444F464652UL;
        private const ulong LeagueMovementStream = 0x4C45414755454D56UL;

        private readonly CareerState _career;
        private readonly BalanceTable _balance;

        private TeamState[] _nextTeams;
        private WorldOffseasonMarketPlan _marketPlan;
        private ContractOffer[] _renewalOffers = Array.Empty<ContractOffer>();
        private ContractOffer? _currentTeamOffer;
        private bool _isCurrentTeamOfferHeld;
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
        public ContractOffer? CurrentTeamOffer => _currentTeamOffer;
        public bool IsCurrentTeamOfferHeld => _isCurrentTeamOfferHeld;
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
            _marketPlan = new WorldOffseasonMarketService(_balance)
                .CreatePlan(_career.World, _career.MyPlayerId, _nextYear);
            _nextTeams = _marketPlan.GetTeams(_career.CurrentLeague.LeagueId);

            PlayerContractState contract = _career.CurrentContract;
            bool stillCovered = contract.SignedYear + contract.ContractYears - 1 >= _nextYear;
            if (stillCovered)
            {
                CommitNextSeason(renewalOffer: null);
                return Step;
            }

            _currentTeamOffer = BuildCurrentTeamRenewalOffer();
            if (_currentTeamOffer.HasValue)
            {
                _renewalOffers = new[] { _currentTeamOffer.Value };
                Step = SeasonTransitionStep.CurrentTeamNegotiation;
                return Step;
            }

            OpenMarket(holdCurrentTeamOffer: false);
            return Step;
        }

        /// <summary>
        /// 기존 구단 제안을 보류하거나 거절하고 외부 구단 공개 시장을 연다.
        /// </summary>
        public void OpenMarket(bool holdCurrentTeamOffer)
        {
            if (Step != SeasonTransitionStep.CurrentTeamNegotiation &&
                Step != SeasonTransitionStep.NotStarted)
            {
                throw new InvalidOperationException("현재 단계에서는 공개 시장을 열 수 없습니다.");
            }

            ContractOffer? heldOffer = null;
            if (holdCurrentTeamOffer && _currentTeamOffer.HasValue && !ShouldWithdrawHeldOffer())
                heldOffer = _currentTeamOffer;
            _isCurrentTeamOfferHeld = heldOffer.HasValue;

            int heldCount = heldOffer.HasValue ? 1 : 0;
            int maximumExternalOffers = Math.Max(
                0,
                _balance.ContractOffer.MaximumOfferCount - heldCount);
            ContractOffer[] externalOffers = BuildOpenMarketOffers(maximumExternalOffers);
            int externalCount = externalOffers.Length;
            int totalCount = externalCount + heldCount;
            if (totalCount == 0)
            {
                _renewalOffers = new[] { BuildDevelopmentFallback() };
            }
            else
            {
                _renewalOffers = new ContractOffer[totalCount];
                int offset = 0;
                if (heldOffer.HasValue)
                    _renewalOffers[offset++] = heldOffer.Value;
                Array.Copy(externalOffers, 0, _renewalOffers, offset, externalCount);
            }

            _selectedOffer = null;
            Step = SeasonTransitionStep.ContractOffers;
        }

        /// <summary>
        /// 제시된 재계약 오퍼 중 계약할 구단을 선택한다.
        /// </summary>
        public void SelectRenewalOffer(int teamId)
        {
            RequireContractSelectionStep();
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
            RequireContractSelectionStep();
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
            SeasonTransitionStep step = BeginTransition();
            if (step == SeasonTransitionStep.CurrentTeamNegotiation)
            {
                OpenMarket(holdCurrentTeamOffer: true);
                step = Step;
            }
            if (step == SeasonTransitionStep.ContractOffers)
            {
                ContractOffer best = _renewalOffers[0];
                for (int index = 1; index < _renewalOffers.Length; index++)
                {
                    ContractOffer candidate = _renewalOffers[index];
                    if (candidate.OfferScore > best.OfferScore ||
                        Math.Abs(candidate.OfferScore - best.OfferScore) < 0.000001d &&
                        candidate.Team.TeamId < best.Team.TeamId)
                    {
                        best = candidate;
                    }
                }
                SelectRenewalOffer(best.Team.TeamId);
                return SignSelectedOffer();
            }

            return _result.Value;
        }

        /// <summary>
        /// 오프시즌 마감부터 다음 시즌 시작까지의 모든 상태 변경을 한 번에 적용한다.
        /// </summary>
        private CareerSeasonTransitionResult CommitNextSeason(ContractOffer? renewalOffer)
        {
            LeagueState completedLeague = _career.CurrentLeague;
            SeasonState completedSeason = completedLeague.CurrentSeason;
            _career.CurrentOffseason.CompleteRemainingWeeks();
            completedSeason.CompleteArchive();

            TeamState previousPlayerTeam = GetTeam(_career.CurrentLeague.Teams, _career.MyPlayer.CurrentTeamId);
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

            GetMyPlayerCareerUsage(
                out int careerPlateAppearances,
                out int careerPitchingOuts,
                out int registeredSeasons);

            _career.MyPlayer.AdvanceAge();

            int signedTeamId = previousPlayerTeam.TeamId;
            LeagueId targetLeagueId = completedLeague.LeagueId;
            if (renewalOffer.HasValue)
            {
                ContractOffer offer = renewalOffer.Value;
                signedTeamId = offer.Team.TeamId;
                targetLeagueId = _career.World.GetTeam(signedTeamId).LeagueId;
                if (signedTeamId != _career.MyPlayer.CurrentTeamId)
                {
                    if (targetLeagueId == completedLeague.LeagueId)
                    {
                        MoveRosteredPlayer(
                            _nextTeams,
                            _career.MyPlayer.CurrentTeamId,
                            signedTeamId,
                            _career.MyPlayer.PlayerId);
                    }
                    else
                    {
                        RemoveRosteredPlayer(
                            _nextTeams,
                            _career.MyPlayer.CurrentTeamId,
                            _career.MyPlayer.PlayerId);
                        TeamState[] targetTeams = _marketPlan.GetTeams(targetLeagueId);
                        AddRosteredPlayer(targetTeams, signedTeamId, _career.MyPlayer.PlayerId);
                        _marketPlan = _marketPlan.WithTeams(targetLeagueId, targetTeams);
                    }
                    _career.MyPlayer.TransferTo(signedTeamId, targetLeagueId);
                }
                _career.RenewContract(new PlayerContractState(
                    NewGameFlow.CurrentSaveVersion,
                    offer.Team.TeamId,
                    _nextYear,
                    offer.ContractYears,
                    offer.SigningBonus,
                    offer.AnnualSalary,
                    offer.ExpectedRole),
                    completedSeason.SeasonId);
                if (offer.SigningBonus > 0L)
                {
                    _career.Economy.Earn(
                        _nextYear,
                        MoneyTransactionType.ContractIncome,
                        $"contract_{_nextSeasonId}_signing_bonus",
                        offer.SigningBonus);
                }
            }

            var worldLifecycle = new WorldSeasonLifecycleService(_career, _balance);
            _marketPlan = _marketPlan.WithTeams(completedLeague.LeagueId, _nextTeams);
            LeagueState[] nextLeagues = BuildNextLeagues(
                completedLeague.LeagueId,
                targetLeagueId,
                careerPlateAppearances,
                careerPitchingOuts,
                registeredSeasons);
            int playerNextSeasonId = GetLeague(nextLeagues, targetLeagueId).CurrentSeason.SeasonId;
            _career.AdvanceToNextSeason(
                nextLeagues,
                archivedRecord,
                _marketPlan,
                completedSeason.SeasonId,
                playerNextSeasonId,
                _nextYear);
            worldLifecycle.CompleteWorldTransition(_nextYear);
            _career.TradeState.BeginSeason(
                playerNextSeasonId,
                _balance.TradeMarket.TradeDeadlineGame);
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

        private LeagueState[] BuildNextLeagues(
            LeagueId completedLeagueId,
            LeagueId playerLeagueId,
            int careerPlateAppearances,
            int careerPitchingOuts,
            int registeredSeasons)
        {
            var result = new LeagueState[_career.World.Leagues.Count];
            var rollover = new LeagueSeasonRolloverService(_balance);
            for (int index = 0; index < result.Length; index++)
            {
                LeagueState league = _career.World.Leagues[index];
                SeasonState completedSeason = league.CurrentSeason;
                if (league.LeagueId != completedLeagueId)
                {
                    if (completedSeason.Phase != SeasonPhase.Offseason)
                        throw new InvalidOperationException($"{league.LeagueId}가 다음 시즌 전환 가능한 상태가 아닙니다.");
                    completedSeason.CompleteArchive();
                }
                int nextSeasonId = completedSeason.SeasonId + 1;
                TeamState[] teams = _marketPlan.GetTeams(league.LeagueId);
                bool isPlayerLeague = league.LeagueId == playerLeagueId;
                SeasonState nextSeason = rollover.BuildNextRegularSeason(
                    league,
                    teams,
                    nextSeasonId,
                    _nextYear,
                    isPlayerLeague ? _career.MyPlayer : null,
                    isPlayerLeague ? careerPlateAppearances : 0,
                    isPlayerLeague ? careerPitchingOuts : 0,
                    isPlayerLeague ? registeredSeasons : 0);
                result[index] = league.CreateNextSeason(
                    NewGameFlow.CurrentSaveVersion,
                    _nextYear,
                    teams,
                    nextSeason);
            }
            return result;
        }

        private static void MoveRosteredPlayer(
            TeamState[] teams,
            int previousTeamId,
            int targetTeamId,
            int playerId)
        {
            int previousIndex = -1;
            int targetIndex = -1;
            for (int index = 0; index < teams.Length; index++)
            {
                if (teams[index].TeamId == previousTeamId) previousIndex = index;
                if (teams[index].TeamId == targetTeamId) targetIndex = index;
            }
            if (previousIndex < 0 || targetIndex < 0)
                throw new InvalidOperationException("계약 이동 구단을 다음 시즌 로스터에서 찾지 못했습니다.");

            teams[previousIndex] = teams[previousIndex].WithRosterAndPlayerIds(
                ToArray(teams[previousIndex].RosterCompetitors),
                RemoveRosterPlayer(teams[previousIndex].RosterPlayerIds, playerId));
            teams[targetIndex] = teams[targetIndex].WithRosteredPlayer(playerId);
        }

        private static void RemoveRosteredPlayer(TeamState[] teams, int teamId, int playerId)
        {
            for (int index = 0; index < teams.Length; index++)
            {
                if (teams[index].TeamId != teamId)
                    continue;
                teams[index] = teams[index].WithRosterAndPlayerIds(
                    ToArray(teams[index].RosterCompetitors),
                    RemoveRosterPlayer(teams[index].RosterPlayerIds, playerId));
                return;
            }
            throw new InvalidOperationException("이전 리그 로스터에서 이동 선수를 찾지 못했습니다.");
        }

        private static void AddRosteredPlayer(TeamState[] teams, int teamId, int playerId)
        {
            for (int index = 0; index < teams.Length; index++)
            {
                if (teams[index].TeamId != teamId)
                    continue;
                teams[index] = teams[index].WithRosteredPlayer(playerId);
                return;
            }
            throw new InvalidOperationException("대상 리그 로스터에서 계약 구단을 찾지 못했습니다.");
        }

        private static LeagueState GetLeague(IReadOnlyList<LeagueState> leagues, LeagueId leagueId)
        {
            for (int index = 0; index < leagues.Count; index++)
            {
                if (leagues[index].LeagueId == leagueId)
                    return leagues[index];
            }
            throw new InvalidOperationException($"{leagueId}의 다음 시즌 상태가 없습니다.");
        }

        private static int[] RemoveRosterPlayer(IReadOnlyList<int> source, int playerId)
        {
            var result = new int[source.Count - 1];
            int resultIndex = 0;
            for (int index = 0; index < source.Count; index++)
            {
                if (source[index] == playerId)
                    continue;
                if (resultIndex >= result.Length)
                    throw new InvalidOperationException("이전 구단 로스터에 이동 선수가 없습니다.");
                result[resultIndex++] = source[index];
            }
            if (resultIndex != result.Length)
                throw new InvalidOperationException("이전 구단 로스터에 이동 선수가 없습니다.");
            return result;
        }

        private static RosterCompetitorState[] ToArray(IReadOnlyList<RosterCompetitorState> source)
        {
            var result = new RosterCompetitorState[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }

        /// <summary>
        /// 현재 구단만 별도 공식으로 평가해 우선 협상 오퍼를 만든다. 기준 미달이면 null이다.
        /// </summary>
        private ContractOffer? BuildCurrentTeamRenewalOffer()
        {
            TeamState currentTeam = GetTeam(_nextTeams, _career.MyPlayer.CurrentTeamId);
            Player player = _career.MyPlayer.ToPlayer();
            var playerValueEvaluator = new PlayerValueEvaluator(_balance.PlayerEvaluation);
            int playerValue = playerValueEvaluator.CalculatePositionValue(player);
            int evaluationBonus = _career.CurrentLeague.CurrentSeason.Settlement.ContractEvaluationBonus;
            double marketValue = Math.Min(100d, playerValue + evaluationBonus);
            double currentRoleValue = _career.CurrentExpectedRole switch
            {
                Baseball.Core.Teams.ExpectedRole.StartingCompetition => 90d,
                Baseball.Core.Teams.ExpectedRole.RosterCompetition => 65d,
                _ => 40d
            };
            double recentPerformance = CalculateRecentPerformance();
            double ageAndPotential = CalculateAgeAndPotential();
            double expectedSalary = _balance.ContractOffer.BaseSalary * Math.Max(0.5d, marketValue / 50d);
            double costEfficiency = _career.CurrentContract.AnnualSalary <= 0L
                ? 100d
                : Math.Min(100d, expectedSalary / _career.CurrentContract.AnnualSalary * 50d);
            var input = new ContractRenewalEvaluationInput(
                ToGeneratedTeam(currentTeam),
                marketValue,
                currentRoleValue,
                recentPerformance,
                ageAndPotential,
                costEfficiency,
                _career.MyPlayer.ManagerEvaluation,
                currentTeam.GetStrongestCompetitorOverall(_career.MyPlayer.PrimaryPosition));
            return new ContractRenewalEvaluator(_balance.ContractRenewal, _balance.ContractOffer)
                .Evaluate(
                    input,
                    _career.MyPlayer.PrimaryPosition,
                    ContractOfferChannel.CurrentTeamRenewal);
        }

        /// <summary>
        /// 현재 구단을 제외하고 정식 기준을 넘은 외부 구단 오퍼를 만들며, 적격 인접 리그 선택지를 우선 보존한다.
        /// </summary>
        private ContractOffer[] BuildOpenMarketOffers(int maximumOfferCount)
        {
            if (maximumOfferCount <= 0)
                return Array.Empty<ContractOffer>();

            ulong offerSeed = DeterministicSeed.Derive(
                _career.CurrentLeague.RandomSeed,
                ContractRenewalStream ^ (uint)_nextSeasonId);
            var evaluator = new ContractOfferEvaluator(
                _balance.ContractOffer,
                _balance.PlayerEvaluation,
                new Pcg32Random(offerSeed));

            GeneratedTeam[] generatedTeams = BuildGeneratedTeams();
            ContractOffer[] sameLeagueOffers = ContractOfferBoard.SelectOpenMarketOffers(
                _balance.ContractOffer,
                evaluator,
                _career.MyPlayer.ToPlayer(),
                generatedTeams,
                _career.MyPlayer.CurrentTeamId,
                _career.CurrentLeague.CurrentSeason.Settlement.ContractEvaluationBonus);
            ContractOffer[] adjacentLeagueOffers = BuildAdjacentLeagueOffers();
            var result = new List<ContractOffer>(maximumOfferCount);
            for (int index = 0; index < adjacentLeagueOffers.Length && result.Count < maximumOfferCount; index++)
                result.Add(adjacentLeagueOffers[index]);
            for (int index = 0; index < sameLeagueOffers.Length && result.Count < maximumOfferCount; index++)
                result.Add(sameLeagueOffers[index]);
            result.Sort(CompareOffers);
            return result.ToArray();
        }

        private ContractOffer[] BuildAdjacentLeagueOffers()
        {
            var result = new List<ContractOffer>(4);
            LeagueLevel currentLevel = _career.CurrentLeague.LeagueLevel;
            if (currentLevel != LeagueLevel.Major)
            {
                LeagueLevel targetLevel = currentLevel == LeagueLevel.Rookie
                    ? LeagueLevel.Minor
                    : LeagueLevel.Major;
                AddLeagueMovementOffers(result, targetLevel, ContractOfferChannel.Promotion);
            }
            if (currentLevel != LeagueLevel.Rookie)
            {
                LeagueLevel targetLevel = currentLevel == LeagueLevel.Major
                    ? LeagueLevel.Minor
                    : LeagueLevel.Rookie;
                AddLeagueMovementOffers(result, targetLevel, ContractOfferChannel.Rehabilitation);
            }
            result.Sort(CompareOffers);
            return result.ToArray();
        }

        private void AddLeagueMovementOffers(
            List<ContractOffer> result,
            LeagueLevel targetLevel,
            ContractOfferChannel channel)
        {
            LeagueState targetLeague = GetLeague(targetLevel);
            TeamState[] targetTeams = _marketPlan.GetTeams(targetLeague.LeagueId);
            var evaluator = new LeagueMovementEvaluator(_balance.LeagueMovement);
            PlayerSeasonStatisticsState statistics = _career.CurrentLeague.CurrentSeason.PlayerStatistics;
            bool isPitcher = _career.MyPlayer.PrimaryPosition is
                PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            int sampleSize = isPitcher ? statistics.OutsRecorded : statistics.PlateAppearances;
            int reliableSampleSize = isPitcher
                ? _balance.LeagueMovement.ReliablePitchingOuts
                : _balance.LeagueMovement.ReliablePlateAppearances;
            int playerOverall = new PlayerValueEvaluator(_balance.PlayerEvaluation)
                .CalculatePositionValue(_career.MyPlayer.ToPlayer());
            double minimumProjected = targetLevel switch
            {
                LeagueLevel.Rookie => _balance.PlayerLifecycle.RookieEntryMinimumOverall,
                LeagueLevel.Minor => _balance.LeagueMovement.MinorMinimumProjectedOverall,
                LeagueLevel.Major => _balance.LeagueMovement.MajorMinimumProjectedOverall,
                _ => throw new ArgumentOutOfRangeException(nameof(targetLevel))
            };
            int levelPenalty = channel == ContractOfferChannel.Promotion
                ? _balance.LeagueMovement.UpperLeagueOverallPenalty
                : -Math.Max(1, _balance.LeagueMovement.UpperLeagueOverallPenalty / 2);
            var candidates = new List<ContractOffer>(targetTeams.Length);
            for (int index = 0; index < targetTeams.Length; index++)
            {
                TeamState team = targetTeams[index];
                GetCompetitorRange(
                    team,
                    _career.MyPlayer.PrimaryPosition,
                    out int strongest,
                    out int weakest);
                int positionNeed = CalculateDynamicPositionNeed(
                    team.GetPositionNeed(_career.MyPlayer.PrimaryPosition),
                    minimumProjected,
                    weakest);
                LeagueMovementEvaluationResult evaluation = evaluator.Evaluate(
                    new LeagueMovementEvaluationInput(
                        playerOverall,
                        CalculateRecentPerformance(),
                        CalculateAgeAndPotential(),
                        sampleSize,
                        reliableSampleSize,
                        levelPenalty,
                        minimumProjected,
                        strongest,
                        weakest,
                        positionNeed,
                        team.Archetype.Budget,
                        team.Archetype.Development));
                ulong seed = DeterministicSeed.Derive(
                    _career.World.WorldSeed,
                    LeagueMovementStream ^
                    ((ulong)(uint)_nextSeasonId << 32) ^
                    ((ulong)(uint)team.TeamId << 1) ^
                    (uint)channel);
                double varianceRange = _balance.ContractOffer.ScoutVarianceMaximum -
                                       _balance.ContractOffer.ScoutVarianceMinimum;
                double scoutVariance = _balance.ContractOffer.ScoutVarianceMinimum +
                    new Pcg32Random(seed).NextDouble() * varianceRange;
                double interestScore = evaluation.InterestScore * scoutVariance;
                if (!evaluation.IsEligible || interestScore < _balance.LeagueMovement.InterestScoreThreshold)
                    continue;

                long annualSalary = CalculateLeagueSalary(targetLevel, evaluation.ProjectedOverall);
                int contractYears = targetLevel switch
                {
                    LeagueLevel.Rookie => 1,
                    LeagueLevel.Minor => _balance.LeagueMovement.MinorContractYears,
                    LeagueLevel.Major => _balance.LeagueMovement.MajorContractYears,
                    _ => throw new ArgumentOutOfRangeException(nameof(targetLevel))
                };
                candidates.Add(new ContractOffer(
                    ToGeneratedTeam(team, _career.MyPlayer.PrimaryPosition, positionNeed),
                    signingBonus: channel == ContractOfferChannel.Promotion ? annualSalary / 3L : 0L,
                    annualSalary,
                    evaluation.ExpectedRole,
                    interestScore,
                    contractYears,
                    channel,
                    evaluation.EstimatedPlayingTime,
                    hasTradeProtection: false));
            }

            candidates.Sort(CompareOffers);
            int maximumOffers = channel == ContractOfferChannel.Promotion
                ? _balance.LeagueMovement.MaximumPromotionOffers
                : _balance.LeagueMovement.MaximumRehabilitationOffers;
            int count = Math.Min(candidates.Count, maximumOffers);
            for (int index = 0; index < count; index++)
                result.Add(candidates[index]);
        }

        private long CalculateLeagueSalary(LeagueLevel targetLevel, double projectedOverall)
        {
            PlayerLifecycleBalance lifecycle = _balance.PlayerLifecycle;
            long baseSalary = targetLevel switch
            {
                LeagueLevel.Rookie => lifecycle.RookieBaseSalary,
                LeagueLevel.Minor => lifecycle.MinorBaseSalary,
                LeagueLevel.Major => lifecycle.MajorBaseSalary,
                _ => throw new ArgumentOutOfRangeException(nameof(targetLevel))
            };
            return checked(baseSalary * (75L + (long)Math.Round(projectedOverall)) / 125L);
        }

        private static int CalculateDynamicPositionNeed(int baseNeed, double minimumProjected, int weakest)
        {
            int result = baseNeed;
            if (minimumProjected > weakest)
                result += (int)Math.Round((minimumProjected - weakest) * 2d);
            if (result < 5) return 5;
            return result > 95 ? 95 : result;
        }

        private static void GetCompetitorRange(
            TeamState team,
            PlayerPosition position,
            out int strongest,
            out int weakest)
        {
            strongest = 0;
            weakest = 100;
            bool found = false;
            for (int index = 0; index < team.RosterCompetitors.Count; index++)
            {
                RosterCompetitorState competitor = team.RosterCompetitors[index];
                if (competitor.Position != position)
                    continue;
                found = true;
                if (competitor.Overall > strongest) strongest = competitor.Overall;
                if (competitor.Overall < weakest) weakest = competitor.Overall;
            }
            if (!found)
                throw new InvalidOperationException($"TeamId {team.TeamId}의 {position} 경쟁자가 없습니다.");
        }

        private LeagueState GetLeague(LeagueLevel level)
        {
            for (int index = 0; index < _career.World.Leagues.Count; index++)
            {
                if (_career.World.Leagues[index].LeagueLevel == level)
                    return _career.World.Leagues[index];
            }
            throw new InvalidOperationException($"{level} 리그가 월드에 없습니다.");
        }

        private static int CompareOffers(ContractOffer left, ContractOffer right)
        {
            int score = right.OfferScore.CompareTo(left.OfferScore);
            return score != 0 ? score : left.Team.TeamId.CompareTo(right.Team.TeamId);
        }

        private ContractOffer BuildDevelopmentFallback()
        {
            ulong fallbackSeed = DeterministicSeed.Derive(
                _career.CurrentLeague.RandomSeed,
                ContractRenewalStream ^ 0x46414C4C4241434BUL ^ (uint)_nextSeasonId);
            var evaluator = new ContractOfferEvaluator(
                _balance.ContractOffer,
                _balance.PlayerEvaluation,
                new Pcg32Random(fallbackSeed));
            return ContractOfferBoard.SelectDevelopmentFallback(
                evaluator,
                _career.MyPlayer.ToPlayer(),
                BuildGeneratedTeams(),
                _career.MyPlayer.CurrentTeamId,
                _career.CurrentLeague.CurrentSeason.Settlement.ContractEvaluationBonus);
        }

        private GeneratedTeam[] BuildGeneratedTeams()
        {
            var result = new GeneratedTeam[_nextTeams.Length];
            for (int index = 0; index < _nextTeams.Length; index++)
                result[index] = ToGeneratedTeam(_nextTeams[index]);
            return result;
        }

        private bool ShouldWithdrawHeldOffer()
        {
            ulong seed = DeterministicSeed.Derive(
                _career.CurrentLeague.RandomSeed,
                HeldOfferStream ^ (uint)_nextSeasonId);
            return new Pcg32Random(seed).NextDouble() < _balance.ContractRenewal.HoldWithdrawalProbability;
        }

        private double CalculateRecentPerformance()
        {
            PlayerSeasonStatisticsState statistics = _career.CurrentLeague.CurrentSeason.PlayerStatistics;
            double result = _career.MyPlayer.ManagerEvaluation;
            if (_career.MyPlayer.PrimaryPosition is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher)
            {
                if (statistics.OutsRecorded >= 9)
                    result = 100d - (statistics.EarnedRunAverage - 2d) * (100d / 6d);
            }
            else if (statistics.PlateAppearances >= 15)
            {
                result = (statistics.OnBasePlusSlugging - 0.45d) * (100d / 0.65d);
            }
            return Clamp(result, 0d, 100d);
        }

        private double CalculateAgeAndPotential()
        {
            if (_career.MyPlayer.GrowthState == null)
                return Clamp(100d - Math.Max(0, _career.MyPlayer.Age - 18) * 3d, 25d, 100d);

            int[] potential = _career.MyPlayer.GrowthState.PotentialByAbility.ToArray();
            int total = 0;
            for (int index = 0; index < potential.Length; index++)
                total += potential[index];
            return potential.Length == 0 ? 50d : total / (double)potential.Length;
        }

        private SeasonState RequireOffseasonSeason()
        {
            SeasonState season = _career.CurrentLeague.CurrentSeason;
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

        private void RequireContractSelectionStep()
        {
            if (Step is not SeasonTransitionStep.CurrentTeamNegotiation and
                not SeasonTransitionStep.ContractOffers)
            {
                throw new InvalidOperationException("현재 단계에는 선택할 계약 오퍼가 없습니다.");
            }
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
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

            PlayerSeasonStatisticsState current = _career.CurrentLeague.CurrentSeason.PlayerStatistics;
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

        private static GeneratedTeam ToGeneratedTeam(
            TeamState team,
            PlayerPosition adjustedPosition,
            int adjustedNeed)
        {
            int[] positionNeeds = BuildPositionNeeds(team);
            positionNeeds[(int)adjustedPosition] = adjustedNeed;
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
