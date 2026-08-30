using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Growth;
using Baseball.Simulation.Random;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 실제 계약·시즌 기록·오퍼 평가 결과를 계약 화면의 읽기 모델로 조합한다.
    /// </summary>
    public sealed class CareerContractViewBuilder
    {
        private const ulong MarketPreviewStream = 0x434F4E5452414354UL;

        private readonly CareerState _career;
        private readonly BalanceTable _balance;
        private readonly SkillBoardService _skillBoardService;

        public CareerContractViewBuilder(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _skillBoardService = new SkillBoardService(balance.Growth.SkillBoard, balance.Growth.SkillBlocks);
        }

        /// <summary>
        /// 화면을 열 때마다 현재 시즌 진행도와 보류 중인 재계약 제안을 반영한다.
        /// </summary>
        public CareerContractView Build(
            CareerSeasonTransitionService transitionService,
            string lastError)
        {
            SeasonState season = _career.CurrentLeague.CurrentSeason;
            PlayerState player = _career.MyPlayer;
            PlayerContractState contract = _career.CurrentContract;
            TeamState currentTeam = GetTeam(contract.TeamId);
            int regularSeasonGames = CountRegularSeasonGames(player.CurrentTeamId);
            ContractBonusProgress[] bonusProgress = new ContractBonusService(_balance.ContractBonus)
                .Evaluate(_career, regularSeasonGames);
            ContractOffer[] marketOffers = BuildMarketOffers(transitionService);
            RenewalContractOfferView[] renewalOffers = BuildRenewalOfferViews(transitionService);
            ContractOffer? extensionOffer = transitionService == null
                ? new ContractRenewalService(_career, _balance).BuildExtensionOffer()
                : null;
            ContractNegotiationStatus negotiationStatus = ResolveNegotiationStatus(
                transitionService,
                season,
                contract,
                extensionOffer.HasValue);

            long achievedBonus = 0L;
            long maximumBonus = 0L;
            var bonusViews = new ContractBonusProgressView[bonusProgress.Length];
            for (int index = 0; index < bonusProgress.Length; index++)
            {
                bonusViews[index] = new ContractBonusProgressView(bonusProgress[index]);
                maximumBonus += bonusProgress[index].Clause.Reward;
                if (bonusProgress[index].IsCompleted)
                    achievedBonus += bonusProgress[index].Clause.Reward;
            }

            long marketMinimum = 0L;
            long marketMaximum = 0L;
            if (marketOffers.Length > 0)
            {
                marketMinimum = marketOffers[0].AnnualSalary;
                marketMaximum = marketOffers[0].AnnualSalary;
                for (int index = 1; index < marketOffers.Length; index++)
                {
                    marketMinimum = Math.Min(marketMinimum, marketOffers[index].AnnualSalary);
                    marketMaximum = Math.Max(marketMaximum, marketOffers[index].AnnualSalary);
                }
            }

            PlayerLifecycleBalance lifecycle = _balance.PlayerLifecycle;
            bool isOfferStep = negotiationStatus is ContractNegotiationStatus.CurrentTeamOfferAvailable or
                ContractNegotiationStatus.OffersAvailable;
            var evaluator = new PlayerValueEvaluator(_balance.PlayerEvaluation);
            return new CareerContractView
            {
                PlayerName = player.Name,
                Age = player.Age,
                Position = player.PrimaryPosition,
                Overall = evaluator.CalculatePositionValue(player.ToRosterPlayer(_skillBoardService)),
                SeasonYear = season.Year,
                LeagueLevel = season.LeagueLevel,
                SeasonPhase = season.Phase,
                AvailableMoney = _career.AvailableMoney,
                CurrentContract = new CurrentContractView(
                    currentTeam.Name,
                    contract.SignedYear,
                    contract.EndYear,
                    contract.ContractYears,
                    contract.GetRemainingSeasonsAfter(season.Year),
                    contract.SigningBonus,
                    contract.AnnualSalary,
                    contract.GuaranteedValue,
                    contract.ExpectedRole),
                ContractHistory = BuildContractHistory(contract),
                BonusProgress = bonusViews,
                AchievedBonus = achievedBonus,
                MaximumBonus = maximumBonus,
                MarketSalaryMinimum = marketMinimum,
                MarketSalaryMaximum = marketMaximum,
                MarketOfferCount = marketOffers.Length,
                MarketExpectedRole = marketOffers.Length > 0
                    ? marketOffers[0].ExpectedRole
                    : contract.ExpectedRole,
                CurrentTeamPositionNeed = currentTeam.GetPositionNeed(player.PrimaryPosition),
                NegotiationStatus = negotiationStatus,
                RenewalOffers = renewalOffers,
                ExtensionOffer = extensionOffer.HasValue
                    ? BuildOfferView(extensionOffer.Value, isSelected: false, transitionService: null)
                    : null,
                CanBeginNegotiation = negotiationStatus == ContractNegotiationStatus.NegotiationAvailable,
                CanAcceptExtension = extensionOffer.HasValue,
                CanSignSelectedOffer = transitionService?.SelectedOffer.HasValue == true,
                CanOpenMarket = transitionService?.Step == SeasonTransitionStep.CurrentTeamNegotiation,
                IsCurrentTeamOfferHeld = transitionService?.IsCurrentTeamOfferHeld == true,
                IsUnsignedRetirementRequired = transitionService?.IsUnsignedRetirementRequired == true,
                CanRetireInsteadOfSigning = isOfferStep &&
                    (player.Age >= lifecycle.RetirementMinimumAge ||
                     transitionService?.IsUnsignedRetirementRequired == true),
                // 오퍼 단계에서는 아직 나이를 올리지 않았으므로 서명 후 시즌의 나이는 Age + 1이다.
                IsNextSeasonForcedFinal = player.Age + 1 >= lifecycle.GuaranteedRetirementAge,
                RetirementEligibleAge = lifecycle.RetirementMinimumAge,
                GuaranteedRetirementAge = lifecycle.GuaranteedRetirementAge,
                LastError = lastError ?? string.Empty
            };
        }

        private ContractHistoryView[] BuildContractHistory(PlayerContractState currentContract)
        {
            IReadOnlyList<PlayerContractState> source = _career.ContractHistory;
            var result = new ContractHistoryView[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                PlayerContractState contract = source[source.Count - 1 - index];
                result[index] = new ContractHistoryView(
                    GetTeam(contract.SigningTeamId).Name,
                    contract.SignedYear,
                    contract.EndYear,
                    contract.ContractYears,
                    contract.AnnualSalary,
                    contract.GuaranteedValue,
                    contract.ExpectedRole,
                    ReferenceEquals(contract, currentContract));
            }
            return result;
        }

        private ContractOffer[] BuildMarketOffers(CareerSeasonTransitionService transitionService)
        {
            if (transitionService?.Step is SeasonTransitionStep.CurrentTeamNegotiation or
                SeasonTransitionStep.ContractOffers)
            {
                int count = 0;
                for (int index = 0; index < transitionService.RenewalOffers.Count; index++)
                {
                    ContractOfferChannel channel = transitionService.RenewalOffers[index].Channel;
                    if (channel is ContractOfferChannel.OpenMarket or
                        ContractOfferChannel.Promotion or
                        ContractOfferChannel.Rehabilitation or
                        ContractOfferChannel.TryoutContract)
                        count++;
                }
                var actual = new ContractOffer[count];
                int resultIndex = 0;
                for (int index = 0; index < transitionService.RenewalOffers.Count; index++)
                {
                    ContractOffer offer = transitionService.RenewalOffers[index];
                    if (offer.Channel is ContractOfferChannel.OpenMarket or
                        ContractOfferChannel.Promotion or
                        ContractOfferChannel.Rehabilitation or
                        ContractOfferChannel.TryoutContract)
                        actual[resultIndex++] = offer;
                }
                return actual;
            }

            int seasonId = _career.CurrentLeague.CurrentSeason.SeasonId;
            ulong previewSeed = DeterministicSeed.Derive(
                _career.CurrentLeague.RandomSeed,
                MarketPreviewStream ^ (uint)seasonId);
            var evaluator = new ContractOfferEvaluator(
                _balance.ContractOffer,
                _balance.PlayerEvaluation,
                new Pcg32Random(previewSeed));
            var teams = new GeneratedTeam[_career.CurrentLeague.Teams.Count];
            for (int index = 0; index < teams.Length; index++)
                teams[index] = ToGeneratedTeam(_career.CurrentLeague.Teams[index]);
            int evaluationBonus = _career.CurrentLeague.CurrentSeason.Settlement.ContractEvaluationBonus;
            return ContractOfferBoard.SelectOpenMarketOffers(
                _balance.ContractOffer,
                evaluator,
                _career.MyPlayer.ToRosterPlayer(_skillBoardService),
                teams,
                _career.CurrentContract.TeamId,
                evaluationBonus);
        }

        private RenewalContractOfferView[] BuildRenewalOfferViews(
            CareerSeasonTransitionService transitionService)
        {
            if (transitionService?.Step is not SeasonTransitionStep.CurrentTeamNegotiation and
                not SeasonTransitionStep.ContractOffers)
                return Array.Empty<RenewalContractOfferView>();

            ContractOffer? selected = transitionService.SelectedOffer;
            var result = new RenewalContractOfferView[transitionService.RenewalOffers.Count];
            for (int index = 0; index < result.Length; index++)
            {
                ContractOffer offer = transitionService.RenewalOffers[index];
                result[index] = BuildOfferView(
                    offer,
                    selected.HasValue && selected.Value.Team.TeamId == offer.Team.TeamId,
                    transitionService);
            }
            return result;
        }

        private RenewalContractOfferView BuildOfferView(
            ContractOffer offer,
            bool isSelected,
            CareerSeasonTransitionService transitionService)
        {
            LeagueLevel targetLeagueLevel = transitionService == null
                ? _career.World.GetLeagueForTeam(offer.Team.TeamId).LeagueLevel
                : transitionService.GetPlannedLeagueLevel(offer.Team.TeamId);
            return new RenewalContractOfferView(
                offer.Team.TeamId,
                offer.Team.Name,
                targetLeagueLevel,
                offer.Team.PrimaryColor,
                offer.Team.GetPositionNeed(_career.MyPlayer.PrimaryPosition),
                offer.Team.Archetype.Development,
                offer.SigningBonus,
                offer.AnnualSalary,
                offer.ContractYears,
                offer.ExpectedRole,
                offer.Channel,
                offer.EstimatedPlayingTime,
                BuildCompetitorSummary(offer.Team.GetPositionCompetitors(
                    _career.MyPlayer.PrimaryPosition)),
                offer.HasUpperLeagueReleaseClause,
                offer.UpperLeagueReleaseCompensation,
                offer.HasRelegationTransferRequestClause,
                isSelected);
        }

        private static string BuildCompetitorSummary(IReadOnlyList<RosterCompetitor> competitors)
        {
            if (competitors == null || competitors.Count == 0)
                return "없음";
            int count = Math.Min(2, competitors.Count);
            string result = string.Empty;
            for (int index = 0; index < count; index++)
            {
                if (index > 0) result += ", ";
                result += $"{competitors[index].Name} OVR {competitors[index].Overall}";
            }
            return result;
        }

        private static ContractNegotiationStatus ResolveNegotiationStatus(
            CareerSeasonTransitionService transitionService,
            SeasonState season,
            PlayerContractState contract,
            bool hasExtensionOffer)
        {
            if (transitionService?.Step == SeasonTransitionStep.CurrentTeamNegotiation)
                return ContractNegotiationStatus.CurrentTeamOfferAvailable;
            if (transitionService?.Step == SeasonTransitionStep.ContractOffers)
                return ContractNegotiationStatus.OffersAvailable;
            if (hasExtensionOffer)
                return ContractNegotiationStatus.ExtensionOfferAvailable;
            if (contract.EndYear > season.Year)
                return ContractNegotiationStatus.Active;
            return season.Phase == SeasonPhase.Offseason
                ? ContractNegotiationStatus.NegotiationAvailable
                : ContractNegotiationStatus.ExpiringThisSeason;
        }

        private int CountRegularSeasonGames(int playerTeamId)
        {
            SeasonScheduleState schedule = _career.CurrentLeague.CurrentSeason.Schedule;
            if (schedule == null)
                return _balance.CareerSeason.RegularSeasonGamesPerTeam;

            int count = 0;
            for (int index = 0; index < schedule.Games.Count; index++)
            {
                if (schedule.Games[index].IncludesTeam(playerTeamId))
                    count++;
            }
            return count > 0 ? count : _balance.CareerSeason.RegularSeasonGamesPerTeam;
        }

        private TeamState GetTeam(int teamId)
        {
            return _career.World.GetTeam(teamId);
        }

        private static GeneratedTeam ToGeneratedTeam(TeamState team)
        {
            int positionCount = (int)PlayerPosition.ReliefPitcher + 1;
            var needs = new int[positionCount];
            for (int rawPosition = (int)PlayerPosition.Catcher;
                 rawPosition < positionCount;
                 rawPosition++)
            {
                needs[rawPosition] = team.GetPositionNeed((PlayerPosition)rawPosition);
            }

            var competitors = new RosterCompetitor[team.RosterCompetitors.Count];
            for (int index = 0; index < competitors.Length; index++)
            {
                RosterCompetitorState competitor = team.RosterCompetitors[index];
                competitors[index] = new RosterCompetitor(
                    competitor.PlayerId,
                    competitor.Name,
                    competitor.Position,
                    competitor.Overall);
            }
            return new GeneratedTeam(
                team.TeamId,
                team.Name,
                team.Archetype,
                team.PrimaryColor,
                needs,
                competitors);
        }
    }
}
