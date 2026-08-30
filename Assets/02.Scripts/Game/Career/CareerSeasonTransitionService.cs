using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Growth;
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
    /// 계약 만료 또는 이동 조항이 발동한 시즌에는 계약 선택지를 제시하고 플레이어의 선택을 기다린다.
    /// </summary>
    public sealed class CareerSeasonTransitionService
    {
        private const ulong ContractRenewalStream = 0x52454E4557414C31UL;
        private const ulong HeldOfferStream = 0x484F4C444F464652UL;
        private const ulong LeagueMovementStream = 0x4C45414755454D56UL;

        private readonly CareerState _career;
        private readonly BalanceTable _balance;
        private readonly SkillBoardService _skillBoardService;

        private TeamState[] _nextTeams;
        private WorldOffseasonMarketPlan _marketPlan;
        private LeagueMovementPlan _leagueMovementPlan;
        private LeagueId _plannedPlayerLeagueId;
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
            _skillBoardService = new SkillBoardService(balance.Growth.SkillBoard, balance.Growth.SkillBlocks);
        }

        public SeasonTransitionStep Step { get; private set; }
        public IReadOnlyList<ContractOffer> RenewalOffers => _renewalOffers;
        public ContractOffer? CurrentTeamOffer => _currentTeamOffer;
        public bool IsCurrentTeamOfferHeld => _isCurrentTeamOfferHeld;
        public ContractOffer? SelectedOffer => _selectedOffer;
        public CareerSeasonTransitionResult? Result => _result;
        public int RookieTryoutAttemptCount { get; private set; }
        public bool IsUnsignedRetirementRequired { get; private set; }

        /// <summary>
        /// 승강 이동을 모두 반영한 다음 시즌 구단의 리그 단계를 반환한다.
        /// </summary>
        public LeagueLevel GetPlannedLeagueLevel(int teamId)
        {
            if (_marketPlan == null)
                return _career.World.GetLeagueForTeam(teamId).LeagueLevel;

            LeagueId leagueId = _marketPlan.GetLeagueIdForTeam(teamId);
            return _career.World.GetLeague(leagueId).LeagueLevel;
        }

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
            for (int leagueIndex = 0; leagueIndex < _career.World.Leagues.Count; leagueIndex++)
            {
                LeagueState league = _career.World.Leagues[leagueIndex];
                league.CurrentSeason.FinalizeAdjustedStatistics(league);
            }
            _leagueMovementPlan = new LeagueMovementPlanner(_career, _balance).CreatePlan(_career.World);
            _marketPlan = new WorldOffseasonMarketService(_balance)
                .CreatePlan(_career.World, _career.MyPlayerId, _nextYear, _leagueMovementPlan);
            _plannedPlayerLeagueId = _marketPlan.GetLeagueIdForTeam(_career.MyPlayer.CurrentTeamId);
            _nextTeams = _marketPlan.GetTeams(_plannedPlayerLeagueId);

            PlayerContractState contract = _career.CurrentContract;
            bool stillCovered = contract.SignedYear + contract.ContractYears - 1 >= _nextYear;
            if (stillCovered)
            {
                ContractOffer[] activeContractOffers = BuildActiveContractOffers(completedSeason.LeagueLevel);
                if (activeContractOffers.Length > 1)
                {
                    _renewalOffers = activeContractOffers;
                    RecordUpperLeagueInterestEvents(activeContractOffers);
                    Step = SeasonTransitionStep.ContractOffers;
                    return Step;
                }
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
            RecordUpperLeagueInterestEvents(externalOffers);
            int externalCount = externalOffers.Length;
            int totalCount = externalCount + heldCount;
            if (totalCount == 0)
            {
                ContractOffer? tryoutOffer = BuildRookieTryoutOffer();
                if (tryoutOffer.HasValue)
                {
                    _renewalOffers = new[] { tryoutOffer.Value };
                }
                else
                {
                    _renewalOffers = Array.Empty<ContractOffer>();
                    IsUnsignedRetirementRequired = RookieTryoutAttemptCount >= 2;
                }
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

        private void RecordUpperLeagueInterestEvents(IReadOnlyList<ContractOffer> offers)
        {
            for (int index = 0; index < offers.Count; index++)
            {
                ContractOffer offer = offers[index];
                if (offer.Channel != ContractOfferChannel.Promotion)
                    continue;

                string eventId = $"upper-league-interest:{_nextSeasonId}:{_career.MyPlayerId}:{offer.Team.TeamId}";
                if (_career.World.DomainEvents.Contains(eventId))
                    continue;
                _career.World.DomainEvents.Append(new WorldDomainEvent(
                    eventId,
                    "UpperLeagueInterestConfirmed",
                    _career.World.Calendar.CurrentDate,
                    _career.MyPlayerId,
                    offer.Team.TeamId,
                    (int)GetPlannedLeagueLevel(offer.Team.TeamId)));
            }
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
                if (_renewalOffers.Length == 0 && IsUnsignedRetirementRequired)
                    throw new InvalidOperationException("Rookie 테스트 입단에 연속 실패해 커리어 종료 처리가 필요합니다.");
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
            bool requiresInjuryReturnObservation = _career.CurrentOffseason.MandatoryRehabWeeks > 0;
            _career.CurrentOffseason.CompleteRemainingWeeks();
            _career.MyPlayer.GrowthState.ApplyOffseasonRecoveryBenefits(
                _career.CurrentOffseason.NextSeasonInjuryRiskReduction,
                _career.CurrentOffseason.PhysicalDeclineProtectionPoints);
            completedSeason.CompleteArchive();

            TeamState previousPlayerTeam = GetTeam(_career.CurrentLeague.Teams, _career.MyPlayer.CurrentTeamId);
            new RetirementRecapService(_balance)
                .ArchiveCompletedSeason(_career, previousPlayerTeam);
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
                completedSeason.Settlement,
                completedLeague.LeagueId,
                completedSeason.AdjustedStatistics?.LeagueStrengthIndex ?? 100d,
                completedSeason.AdjustedStatistics?.GetPlayer(_career.MyPlayerId) ?? default,
                completedSeason.SeasonId,
                _career.MyPlayerId,
                _career.CurrentExpectedRole);

            GetMyPlayerCareerUsage(
                out int careerPlateAppearances,
                out int careerPitchingOuts,
                out int registeredSeasons);

            RecordCareerAchievement(completedLeague);

            _career.MyPlayer.AdvanceAge();

            int signedTeamId = previousPlayerTeam.TeamId;
            LeagueId targetLeagueId = _plannedPlayerLeagueId;
            bool continuesCurrentContract = renewalOffer.HasValue &&
                                            renewalOffer.Value.Channel == ContractOfferChannel.ContractContinuation;
            if (renewalOffer.HasValue && !continuesCurrentContract)
            {
                ContractOffer offer = renewalOffer.Value;
                PlayerContractState previousContract = _career.CurrentContract;
                signedTeamId = offer.Team.TeamId;
                targetLeagueId = _marketPlan.GetLeagueIdForTeam(signedTeamId);
                if (signedTeamId != _career.MyPlayer.CurrentTeamId)
                {
                    TeamState[] targetTeams = targetLeagueId == _plannedPlayerLeagueId
                        ? _nextTeams
                        : _marketPlan.GetTeams(targetLeagueId);
                    RosterCompetitorState displacedPlayer = SwapRosteredPlayer(
                        _nextTeams,
                        _career.MyPlayer.CurrentTeamId,
                        _plannedPlayerLeagueId,
                        targetTeams,
                        signedTeamId,
                        targetLeagueId,
                        _career.MyPlayer.PlayerId,
                        _career.MyPlayer.PrimaryPosition);
                    _marketPlan = _marketPlan.WithTeams(_plannedPlayerLeagueId, _nextTeams);
                    if (targetLeagueId != _plannedPlayerLeagueId)
                        _marketPlan = _marketPlan.WithTeams(targetLeagueId, targetTeams);
                    _marketPlan = _marketPlan.WithDecision(BuildDisplacedPlayerDecision(
                        displacedPlayer,
                        _career.MyPlayer.CurrentTeamId,
                        _plannedPlayerLeagueId));
                    _nextTeams = _marketPlan.GetTeams(_plannedPlayerLeagueId);
                    _career.MyPlayer.TransferTo(signedTeamId, targetLeagueId);
                }
                else if (targetLeagueId != _career.MyPlayer.CurrentLeagueId)
                {
                    _career.MyPlayer.TransferTo(signedTeamId, targetLeagueId);
                }
                LeagueLevel previousContractLevel = completedLeague.LeagueLevel;
                LeagueLevel targetContractLevel = _career.World.GetLeague(targetLeagueId).LeagueLevel;
                long transferCompensation = previousContract.IsActive &&
                                            previousContract.EndYear >= _nextYear &&
                                            previousContract.HasUpperLeagueReleaseClause &&
                                            targetContractLevel > previousContractLevel
                    ? previousContract.UpperLeagueReleaseCompensation
                    : 0L;
                _career.RenewContract(new PlayerContractState(
                    NewGameFlow.CurrentSaveVersion,
                    offer.Team.TeamId,
                    _nextYear,
                    offer.ContractYears,
                    offer.SigningBonus,
                    offer.AnnualSalary,
                    offer.ExpectedRole,
                    offer.HasUpperLeagueReleaseClause,
                    offer.UpperLeagueReleaseCompensation,
                    offer.HasRelegationTransferRequestClause),
                    completedSeason.SeasonId,
                    targetLeagueId,
                    transferCompensation);
                if (offer.SigningBonus > 0L)
                {
                    _career.Economy.Earn(
                        _nextYear,
                        MoneyTransactionType.ContractIncome,
                        $"contract_{_nextSeasonId}_signing_bonus",
                        offer.SigningBonus);
                }
            }
            else if (targetLeagueId != _career.MyPlayer.CurrentLeagueId)
            {
                LeagueLevel previousTier = completedLeague.LeagueLevel;
                LeagueLevel targetTier = _career.World.GetLeague(targetLeagueId).LeagueLevel;
                PlayerMovementType movementType = targetTier > previousTier
                    ? PlayerMovementType.TeamPromotion
                    : PlayerMovementType.TeamRelegation;
                _career.MyPlayer.TransferTo(signedTeamId, targetLeagueId);
                _career.CurrentContract.TransferTo(signedTeamId, targetLeagueId);
                _career.World.MovementLedger.Record(new PlayerMovementRecord(
                    _career.World.Calendar.CurrentDate,
                    completedSeason.SeasonId,
                    _career.MyPlayerId,
                    movementType,
                    completedLeague.LeagueId,
                    previousPlayerTeam.TeamId,
                    targetLeagueId,
                    signedTeamId,
                    _career.CurrentExpectedRole,
                    _career.CurrentExpectedRole,
                    _career.CurrentExpectedRole,
                    _career.CurrentContract.ContractId,
                    movementType == PlayerMovementType.TeamPromotion
                        ? "구단 승격에 따른 계약 승계"
                        : "구단 강등에 따른 계약 승계"));
            }

            LeagueLevel reachedTier = _career.World.GetLeague(targetLeagueId).LeagueLevel;
            if (_career.Reputation.RecordLeagueReach(reachedTier))
            {
                _career.GrowthMilestones.RecordFirstReach(
                    reachedTier,
                    _balance.Growth.Progression);
                LeagueDefinition reachedDefinition = WorldGenerationConfiguration
                    .GetDefaultDefinition(reachedTier);
                if (reachedDefinition.FirstReachReward > 0L)
                {
                    _career.Economy.Earn(
                        _nextYear,
                        MoneyTransactionType.BonusIncome,
                        $"first_league_reach_{(int)reachedTier}",
                        reachedDefinition.FirstReachReward);
                }
                _career.World.DomainEvents.Append(new WorldDomainEvent(
                    $"first-league-reach:{_nextYear}:{_career.MyPlayerId}:{(int)reachedTier}",
                    "FirstLeagueReached",
                    _career.World.Calendar.CurrentDate,
                    _career.MyPlayerId,
                    (int)reachedTier));
            }

            var worldLifecycle = new WorldSeasonLifecycleService(_career, _balance);
            _marketPlan = _marketPlan.WithTeams(_plannedPlayerLeagueId, _nextTeams);
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
            new CareerRoleEvaluationService(_career, _balance)
                .BeginSeason(requiresInjuryReturnObservation);
            if (_career.MyPlayer.Age >= _balance.PlayerLifecycle.GuaranteedRetirementAge &&
                !_career.Retirement.IsFinalSeasonDeclared)
            {
                _career.Retirement.DeclareFinalSeason(playerNextSeasonId);
                _career.World.DomainEvents.Append(new WorldDomainEvent(
                    $"final-season-announced:{_nextYear}:{_career.MyPlayerId}",
                    "FinalSeasonAnnounced",
                    _career.World.Calendar.CurrentDate,
                    _career.MyPlayerId,
                    _nextYear));
            }

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

        /// <summary>
        /// 내 선수의 새 구단에서 같은 포지션의 가장 약한 선수를 이전 구단으로 보내 두 구단의 25인 로스터를 유지한다.
        /// </summary>
        private static RosterCompetitorState SwapRosteredPlayer(
            TeamState[] sourceTeams,
            int sourceTeamId,
            LeagueId sourceLeagueId,
            TeamState[] targetTeams,
            int targetTeamId,
            LeagueId targetLeagueId,
            int playerId,
            PlayerPosition position)
        {
            int sourceIndex = -1;
            int targetIndex = -1;
            for (int index = 0; index < sourceTeams.Length; index++)
            {
                if (sourceTeams[index].TeamId == sourceTeamId)
                    sourceIndex = index;
            }
            for (int index = 0; index < targetTeams.Length; index++)
            {
                if (targetTeams[index].TeamId == targetTeamId)
                    targetIndex = index;
            }
            if (sourceIndex < 0 || targetIndex < 0)
                throw new InvalidOperationException("계약 이동 구단을 다음 시즌 로스터에서 찾지 못했습니다.");
            if (sourceTeamId == targetTeamId && sourceLeagueId == targetLeagueId)
                throw new InvalidOperationException("같은 구단에는 로스터 교환을 적용할 수 없습니다.");

            TeamState source = sourceTeams[sourceIndex];
            TeamState target = targetTeams[targetIndex];
            RosterCompetitorState displaced = GetWeakestCompetitor(target, position);

            sourceTeams[sourceIndex] = AddRosteredCompetitor(
                source.WithoutRosteredPlayer(playerId),
                displaced);
            targetTeams[targetIndex] = target
                .WithoutRosteredPlayer(displaced.PlayerId)
                .WithRosteredPlayer(playerId);
            return displaced;
        }

        private static RosterCompetitorState GetWeakestCompetitor(
            TeamState team,
            PlayerPosition position)
        {
            bool found = false;
            RosterCompetitorState weakest = default;
            for (int index = 0; index < team.RosterCompetitors.Count; index++)
            {
                RosterCompetitorState candidate = team.RosterCompetitors[index];
                if (candidate.Position != position)
                    continue;
                if (!found || candidate.Overall < weakest.Overall ||
                    candidate.Overall == weakest.Overall && candidate.PlayerId < weakest.PlayerId)
                {
                    weakest = candidate;
                    found = true;
                }
            }
            if (!found)
                throw new InvalidOperationException($"TeamId {team.TeamId}의 {position} 교환 선수가 없습니다.");
            return weakest;
        }

        private static TeamState AddRosteredCompetitor(
            TeamState team,
            RosterCompetitorState competitor)
        {
            var competitors = new RosterCompetitorState[team.RosterCompetitors.Count + 1];
            for (int index = 0; index < team.RosterCompetitors.Count; index++)
                competitors[index] = team.RosterCompetitors[index];
            competitors[^1] = competitor;
            Array.Sort(competitors, (left, right) =>
            {
                int position = left.Position.CompareTo(right.Position);
                return position != 0 ? position : left.PlayerId.CompareTo(right.PlayerId);
            });
            var playerIds = new int[team.RosterPlayerIds.Count + 1];
            for (int index = 0; index < team.RosterPlayerIds.Count; index++)
                playerIds[index] = team.RosterPlayerIds[index];
            playerIds[^1] = competitor.PlayerId;
            Array.Sort(playerIds);
            return team.WithRosterAndPlayerIds(competitors, playerIds);
        }

        private AiMarketDecision BuildDisplacedPlayerDecision(
            RosterCompetitorState displaced,
            int targetTeamId,
            LeagueId targetLeagueId)
        {
            PlayerState player = null;
            bool isNewPlayer = false;
            for (int index = 0; index < _career.World.Players.Count; index++)
            {
                if (_career.World.Players[index].PlayerId == displaced.PlayerId)
                {
                    player = _career.World.Players[index];
                    break;
                }
            }
            if (player == null)
            {
                for (int index = 0; index < _marketPlan.NewPlayers.Count; index++)
                {
                    if (_marketPlan.NewPlayers[index].PlayerId != displaced.PlayerId)
                        continue;
                    player = _marketPlan.NewPlayers[index];
                    isNewPlayer = true;
                    break;
                }
            }
            if (player == null)
                throw new InvalidOperationException($"교환 선수 PlayerId {displaced.PlayerId}의 상태가 없습니다.");

            LeagueId previousLeagueId = isNewPlayer ? LeagueId.Unassigned : player.CurrentLeagueId;
            int previousTeamId = isNewPlayer ? 0 : player.CurrentTeamId;
            LeagueLevel targetLevel = _career.World.GetLeague(targetLeagueId).LeagueLevel;
            PlayerMovementType movementType;
            if (isNewPlayer)
            {
                movementType = PlayerMovementType.InitialSigning;
            }
            else if (previousTeamId == targetTeamId && previousLeagueId == targetLeagueId)
            {
                movementType = PlayerMovementType.CurrentTeamRenewal;
            }
            else if (previousTeamId == targetTeamId)
            {
                movementType = targetLevel > _career.World.GetLeague(previousLeagueId).LeagueLevel
                    ? PlayerMovementType.TeamPromotion
                    : PlayerMovementType.TeamRelegation;
            }
            else if (previousLeagueId == targetLeagueId)
            {
                movementType = PlayerMovementType.SameLeagueTransfer;
            }
            else
            {
                movementType = targetLevel > _career.World.GetLeague(previousLeagueId).LeagueLevel
                    ? PlayerMovementType.Promotion
                    : PlayerMovementType.Rehabilitation;
            }

            PlayerContractState activeContract = isNewPlayer
                ? null
                : FindActiveContract(player.ActiveContractId);
            bool preservesContract = activeContract != null &&
                                     previousTeamId == targetTeamId &&
                                     activeContract.EndYear >= _nextYear;
            int contractYears = preservesContract ? 0 : GetContractYears(targetLevel);
            long annualSalary = preservesContract
                ? 0L
                : CalculateLeagueSalary(targetLevel, displaced.Overall);
            return new AiMarketDecision(
                displaced.PlayerId,
                movementType,
                previousLeagueId,
                previousTeamId,
                targetLeagueId,
                targetTeamId,
                ExpectedRole.RosterCompetition,
                contractYears,
                annualSalary,
                "플레이어 계약 이동에 따른 동일 포지션 로스터 교환",
                preservesContract);
        }

        private PlayerContractState FindActiveContract(int contractId)
        {
            if (contractId <= 0)
                return null;
            for (int index = 0; index < _career.World.Contracts.Count; index++)
            {
                PlayerContractState contract = _career.World.Contracts[index];
                if (contract.ContractId == contractId && contract.IsActive)
                    return contract;
            }
            return null;
        }

        private int GetContractYears(LeagueLevel targetLevel)
        {
            if (targetLevel == LeagueLevel.Rookie)
                return _balance.PlayerLifecycle.RookieContractYears;
            if (targetLevel == LeagueLevel.Minor)
                return _balance.PlayerLifecycle.MinorContractYears;
            return _balance.PlayerLifecycle.MajorContractYears;
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

        /// <summary>
        /// 현재 구단만 별도 공식으로 평가해 우선 협상 오퍼를 만든다. 기준 미달이면 null이다.
        /// </summary>
        private ContractOffer? BuildCurrentTeamRenewalOffer()
        {
            TeamState currentTeam = GetTeam(_nextTeams, _career.MyPlayer.CurrentTeamId);
            Player player = _career.MyPlayer.ToRosterPlayer(_skillBoardService);
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
            ContractOffer? offer = new ContractRenewalEvaluator(_balance.ContractRenewal, _balance.ContractOffer)
                .Evaluate(
                    input,
                    _career.MyPlayer.PrimaryPosition,
                    ContractOfferChannel.CurrentTeamRenewal);
            return offer.HasValue
                ? ApplyDefaultMovementClauses(
                    offer.Value,
                    _career.World.GetLeague(_plannedPlayerLeagueId).LeagueLevel,
                    preserveCurrentClauses: true)
                : null;
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
                _career.MyPlayer.ToRosterPlayer(_skillBoardService),
                generatedTeams,
                _career.MyPlayer.CurrentTeamId,
                _career.CurrentLeague.CurrentSeason.Settlement.ContractEvaluationBonus);
            LeagueLevel plannedLevel = _career.World.GetLeague(_plannedPlayerLeagueId).LeagueLevel;
            for (int index = 0; index < sameLeagueOffers.Length; index++)
                sameLeagueOffers[index] = ApplyDefaultMovementClauses(sameLeagueOffers[index], plannedLevel);
            ContractOffer[] adjacentLeagueOffers = BuildAdjacentLeagueOffers();
            var result = new List<ContractOffer>(maximumOfferCount);
            for (int index = 0; index < adjacentLeagueOffers.Length && result.Count < maximumOfferCount; index++)
                result.Add(adjacentLeagueOffers[index]);
            for (int index = 0; index < sameLeagueOffers.Length && result.Count < maximumOfferCount; index++)
                result.Add(sameLeagueOffers[index]);
            result.Sort(CompareOffers);
            return result.ToArray();
        }

        /// <summary>
        /// 잔여 계약을 지키는 선택과, 실제 계약 조항으로 협상 가능한 외부 구단만 함께 제시한다.
        /// </summary>
        private ContractOffer[] BuildActiveContractOffers(LeagueLevel completedLevel)
        {
            PlayerContractState contract = _career.CurrentContract;
            LeagueLevel plannedLevel = _career.World.GetLeague(_plannedPlayerLeagueId).LeagueLevel;
            bool relegationRequestActivated = contract.HasRelegationTransferRequestClause &&
                                               plannedLevel < completedLevel;
            if (!contract.HasUpperLeagueReleaseClause && !relegationRequestActivated)
                return Array.Empty<ContractOffer>();

            int maximumExternalOffers = Math.Max(0, _balance.ContractOffer.MaximumOfferCount - 1);
            ContractOffer[] marketOffers = BuildOpenMarketOffers(maximumExternalOffers + 2);
            var eligible = new List<ContractOffer>(maximumExternalOffers + 1)
            {
                BuildContractContinuationOffer()
            };
            for (int index = 0; index < marketOffers.Length && eligible.Count <= maximumExternalOffers; index++)
            {
                ContractOffer offer = marketOffers[index];
                LeagueLevel targetLevel = GetPlannedLeagueLevel(offer.Team.TeamId);
                bool upperReleaseOffer = contract.HasUpperLeagueReleaseClause &&
                                         offer.Channel == ContractOfferChannel.Promotion &&
                                         targetLevel > completedLevel;
                if (!upperReleaseOffer && !relegationRequestActivated)
                    continue;
                eligible.Add(offer);
            }
            return eligible.ToArray();
        }

        private ContractOffer BuildContractContinuationOffer()
        {
            PlayerContractState contract = _career.CurrentContract;
            TeamState team = GetTeam(_nextTeams, _career.MyPlayer.CurrentTeamId);
            double estimatedPlayingTime = _career.CurrentExpectedRole switch
            {
                ExpectedRole.BenchCompetition => 0.25d,
                ExpectedRole.RosterCompetition => 0.50d,
                _ => 0.75d
            };
            double continuationScore = Clamp(
                20d + CalculateRecentPerformance() * 0.45d + estimatedPlayingTime * 30d,
                0d,
                100d);
            return new ContractOffer(
                ToGeneratedTeam(team),
                signingBonus: 0L,
                contract.AnnualSalary,
                _career.CurrentExpectedRole,
                continuationScore,
                contract.EndYear - _nextYear + 1,
                ContractOfferChannel.ContractContinuation,
                estimatedPlayingTime,
                hasTradeProtection: false,
                contract.HasUpperLeagueReleaseClause,
                contract.UpperLeagueReleaseCompensation,
                contract.HasRelegationTransferRequestClause);
        }

        private ContractOffer[] BuildAdjacentLeagueOffers()
        {
            var result = new List<ContractOffer>(6);
            LeagueLevel currentLevel = _career.World.GetLeague(_plannedPlayerLeagueId).LeagueLevel;
            if (LeagueLevelRules.TryGetHigher(currentLevel, out LeagueLevel higher))
            {
                AddLeagueMovementOffers(result, higher, ContractOfferChannel.Promotion);
                bool hasSecondHigher = LeagueLevelRules.TryGetHigher(higher, out LeagueLevel secondHigher);
                bool canJumpTwoLevels = hasSecondHigher &&
                                        currentLevel < LeagueLevel.Classic &&
                                        _career.MyPlayer.Age <= 27 &&
                                        CalculateRecentPerformance() >= 90d;
                if (canJumpTwoLevels)
                    AddLeagueMovementOffers(result, secondHigher, ContractOfferChannel.Promotion);
            }
            if (LeagueLevelRules.TryGetLower(currentLevel, out LeagueLevel lower))
            {
                AddLeagueMovementOffers(result, lower, ContractOfferChannel.Rehabilitation);
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
                .CalculatePositionValue(_career.MyPlayer.ToRosterPlayer(_skillBoardService));
            LeagueDefinition targetDefinition = WorldGenerationConfiguration.GetDefaultDefinition(targetLevel);
            double minimumProjected = targetLevel switch
            {
                LeagueLevel.Rookie => _balance.PlayerLifecycle.RookieEntryMinimumOverall,
                LeagueLevel.Minor => _balance.LeagueMovement.MinorMinimumProjectedOverall,
                LeagueLevel.Major => _balance.LeagueMovement.MajorMinimumProjectedOverall,
                _ => targetDefinition.TargetRosterOverall - targetDefinition.OverallSpread
            };
            LeagueLevel currentLevel = _career.World.GetLeague(_plannedPlayerLeagueId).LeagueLevel;
            int levelDistance = LeagueLevelRules.GetDistance(currentLevel, targetLevel);
            int levelPenalty = channel == ContractOfferChannel.Promotion
                ? _balance.LeagueMovement.UpperLeagueOverallPenalty * levelDistance
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
                        team.Archetype.Development,
                        CalculateRecentGrowthRating(),
                        CalculateDurabilityRating(),
                        _career.Reputation.Reputation));
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
                int contractYears = targetLevel == LeagueLevel.Rookie
                    ? 1
                    : targetLevel == LeagueLevel.Minor
                        ? _balance.LeagueMovement.MinorContractYears
                        : _balance.LeagueMovement.MajorContractYears;
                ContractOffer candidate = new ContractOffer(
                    ToGeneratedTeam(team, _career.MyPlayer.PrimaryPosition, positionNeed),
                    signingBonus: channel == ContractOfferChannel.Promotion ? annualSalary / 3L : 0L,
                    annualSalary,
                    evaluation.ExpectedRole,
                    interestScore,
                    contractYears,
                    channel,
                    evaluation.EstimatedPlayingTime,
                    hasTradeProtection: false);
                candidates.Add(ApplyDefaultMovementClauses(candidate, targetLevel));
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
            LeagueDefinition definition = WorldGenerationConfiguration.GetDefaultDefinition(targetLevel);
            long baseSalary = checked((long)Math.Round(
                lifecycle.RookieBaseSalary * definition.SalaryMultiplier));
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

        /// <summary>
        /// 정식 오퍼가 없을 때 실제 Rookie 구단 두 곳의 경쟁자·수요를 사용해 테스트 입단을 판정한다.
        /// </summary>
        private ContractOffer? BuildRookieTryoutOffer()
        {
            ulong tryoutSeed = DeterministicSeed.Derive(
                _career.CurrentLeague.RandomSeed,
                ContractRenewalStream ^ 0x46414C4C4241434BUL ^ (uint)_nextSeasonId);
            var random = new Pcg32Random(tryoutSeed);
            var evaluator = new RookieTryoutEvaluator(_balance.PlayerLifecycle.RookieTryoutPassingScore);
            TeamState[] rookieTeams = _marketPlan.GetTeams(LeagueId.RookieMain);
            int playerOverall = new PlayerValueEvaluator(_balance.PlayerEvaluation)
                .CalculatePositionValue(_career.MyPlayer.ToRosterPlayer(_skillBoardService));
            int bestTeamIndex = -1;
            int secondTeamIndex = -1;
            int bestPositionNeed = 0;
            double bestScore = double.MinValue;
            double secondScore = double.MinValue;
            for (int index = 0; index < rookieTeams.Length; index++)
            {
                TeamState team = rookieTeams[index];
                if (team.TeamId == _career.MyPlayer.CurrentTeamId)
                    continue;
                GetCompetitorRange(
                    team,
                    _career.MyPlayer.PrimaryPosition,
                    out int strongest,
                    out int weakest);
                int positionNeed = CalculateDynamicPositionNeed(
                    team.GetPositionNeed(_career.MyPlayer.PrimaryPosition),
                    WorldGenerationConfiguration.GetDefaultDefinition(LeagueLevel.Rookie).TargetRosterOverall,
                    weakest);
                double scoutAdjustment = random.NextDouble() * 10d - 5d;
                double score = evaluator.CalculateScore(new RookieTryoutEvaluationInput(
                    playerOverall,
                    positionNeed,
                    strongest,
                    CalculateAgeAndPotential(),
                    CalculateDurabilityRating(),
                    CalculateRecentPerformance(),
                    scoutAdjustment));
                if (score > bestScore ||
                    Math.Abs(score - bestScore) < 0.000001d &&
                    (bestTeamIndex < 0 || team.TeamId < rookieTeams[bestTeamIndex].TeamId))
                {
                    secondScore = bestScore;
                    secondTeamIndex = bestTeamIndex;
                    bestScore = score;
                    bestTeamIndex = index;
                    bestPositionNeed = positionNeed;
                }
                else if (score > secondScore)
                {
                    secondScore = score;
                    secondTeamIndex = index;
                }
            }

            bool passed = bestTeamIndex >= 0 && evaluator.IsPassed(bestScore);
            RookieTryoutAttemptCount = passed
                ? 1
                : secondTeamIndex >= 0
                    ? 2
                    : bestTeamIndex >= 0 ? 1 : 0;
            if (!passed)
                return null;

            TeamState selectedTeam = rookieTeams[bestTeamIndex];
            long salary = CalculateLeagueSalary(LeagueLevel.Rookie, playerOverall);
            var offer = new ContractOffer(
                ToGeneratedTeam(
                    selectedTeam,
                    _career.MyPlayer.PrimaryPosition,
                    bestPositionNeed),
                signingBonus: 0L,
                salary,
                ExpectedRole.BenchCompetition,
                bestScore,
                contractYears: 1,
                ContractOfferChannel.TryoutContract,
                estimatedPlayingTime: 0.15d,
                hasTradeProtection: false);
            return ApplyDefaultMovementClauses(offer, LeagueLevel.Rookie);
        }

        private ContractOffer ApplyDefaultMovementClauses(
            ContractOffer offer,
            LeagueLevel targetLevel,
            bool preserveCurrentClauses = false)
        {
            bool hasUpperClause = targetLevel <= LeagueLevel.Minor ||
                                  preserveCurrentClauses &&
                                  _career.CurrentContract.HasUpperLeagueReleaseClause;
            long releaseCompensation = hasUpperClause
                ? Math.Max(
                    offer.AnnualSalary,
                    preserveCurrentClauses
                        ? _career.CurrentContract.UpperLeagueReleaseCompensation
                        : 0L)
                : 0L;
            bool hasRelegationClause = targetLevel > LeagueLevel.Rookie ||
                                       preserveCurrentClauses &&
                                       _career.CurrentContract.HasRelegationTransferRequestClause;
            return offer.WithMovementClauses(
                hasUpperClause,
                releaseCompensation,
                hasRelegationClause);
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
            LeagueAdjustedStatisticsSnapshot adjusted =
                _career.CurrentLeague.CurrentSeason.AdjustedStatistics;
            if (adjusted != null)
            {
                return adjusted.GetPlayer(_career.MyPlayerId).AdjustedPerformance;
            }
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

        private void RecordCareerAchievement(LeagueState league)
        {
            SeasonState season = league.CurrentSeason;
            int teamId = _career.MyPlayer.CurrentTeamId;
            int expectedRank = season.GetExpectedTeamRank(teamId);
            int actualRank = GetRegularSeasonRank(season, teamId);
            double teamExpectation = Clamp(50d + (expectedRank - actualRank) * 12.5d, 0d, 100d);
            double adjustedPerformance = season.AdjustedStatistics == null
                ? 50d
                : season.AdjustedStatistics.GetPlayer(_career.MyPlayerId).AdjustedPerformance;
            bool reachedPostseason = actualRank > 0 && actualRank <= 4;
            bool wonChampionship = season.Postseason?.ChampionTeamId == teamId;
            double postseasonScore = wonChampionship ? 100d : reachedPostseason ? 70d : 35d;
            int awardCount = 0;
            if (season.Awards != null)
            {
                for (int index = 0; index < season.Awards.Results.Count; index++)
                {
                    if (season.Awards.Results[index].IncludesWinner(_career.MyPlayerId))
                        awardCount++;
                }
            }
            double awardScore = Math.Min(100d, awardCount * 35d);
            ExpectedRole expectedRole = _career.CurrentExpectedRole;
            double expectedAppearanceRate = expectedRole switch
            {
                ExpectedRole.BenchCompetition => 0.25d,
                ExpectedRole.RosterCompetition => 0.50d,
                _ => 0.75d
            };
            double actualAppearanceRate = season.PlayerStatistics.GamesPlayed /
                                          (double)Math.Max(
                                              1,
                                              _balance.CareerSeason.RegularSeasonGamesPerTeam);
            double roleExpectationScore = Clamp(
                50d + (actualAppearanceRate - expectedAppearanceRate) * 100d,
                0d,
                100d);
            double score = teamExpectation * 0.25d +
                           adjustedPerformance * 0.40d +
                           postseasonScore * 0.15d +
                           awardScore * 0.10d +
                           roleExpectationScore * 0.10d;
            SeasonEvaluationGrade grade = score >= 85d
                ? SeasonEvaluationGrade.S
                : score >= 70d
                    ? SeasonEvaluationGrade.A
                    : score >= 55d
                        ? SeasonEvaluationGrade.B
                        : score >= 40d
                            ? SeasonEvaluationGrade.C
                            : SeasonEvaluationGrade.D;
            double prestige = WorldGenerationConfiguration
                .GetDefaultDefinition(league.LeagueLevel)
                .PrestigeMultiplier;
            double reputationChange = (score - 50d) * prestige / 10d;
            _career.Reputation.RecordSeason(new CareerSeasonAchievementState(
                season.Year,
                league.LeagueId,
                league.LeagueLevel,
                expectedRank,
                actualRank,
                adjustedPerformance,
                reachedPostseason,
                wonChampionship,
                awardCount,
                expectedRole,
                roleExpectationScore,
                score,
                grade,
                reputationChange));
        }

        private static int GetRegularSeasonRank(SeasonState season, int teamId)
        {
            var entries = new TeamStandingEntry[season.TeamRecords.Count];
            for (int index = 0; index < entries.Length; index++)
            {
                TeamSeasonRecordState record = season.TeamRecords[index];
                entries[index] = new TeamStandingEntry(
                    record.TeamId,
                    record.Wins,
                    record.Losses,
                    record.RunsScored,
                    record.RunsAllowed,
                    record.FixedTiebreaker,
                    record.GetHeadToHeadEntries());
            }
            int[] ordered = PostseasonBracket.SelectSeeds(entries, entries.Length);
            for (int index = 0; index < ordered.Length; index++)
            {
                if (ordered[index] == teamId)
                    return index + 1;
            }
            return 0;
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

        private double CalculateRecentGrowthRating()
        {
            PlayerGrowthState growth = _career.MyPlayer.GrowthState;
            if (growth == null || growth.GrowthHistory.Count == 0)
                return 50d;

            int seasonYear = _career.CurrentLeague.CurrentSeason.Year;
            int totalChange = 0;
            for (int recordIndex = growth.GrowthHistory.Count - 1; recordIndex >= 0; recordIndex--)
            {
                GrowthResultRecord record = growth.GrowthHistory[recordIndex];
                if (record.SeasonYear < seasonYear)
                    break;
                if (record.SeasonYear != seasonYear)
                    continue;
                for (int changeIndex = 0; changeIndex < record.AbilityChanges.Length; changeIndex++)
                    totalChange += record.AbilityChanges[changeIndex].Amount;
            }
            return Clamp(50d + totalChange * 4d, 0d, 100d);
        }

        private double CalculateDurabilityRating()
        {
            PlayerGrowthState growth = _career.MyPlayer.GrowthState;
            if (growth == null)
                return 75d;
            int injuryPenalty = Math.Min(20, growth.InjuryHistory.Count * 3);
            return Clamp(growth.Durability - injuryPenalty, 0d, 100d);
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
