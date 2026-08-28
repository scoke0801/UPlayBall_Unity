using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.Career;
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

        public CareerContractViewBuilder(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        /// <summary>
        /// 화면을 열 때마다 현재 시즌 진행도와 보류 중인 재계약 제안을 반영한다.
        /// </summary>
        public CareerContractView Build(
            CareerSeasonTransitionService transitionService,
            string lastError)
        {
            SeasonState season = _career.League.CurrentSeason;
            PlayerState player = _career.MyPlayer;
            PlayerContractState contract = _career.CurrentContract;
            TeamState currentTeam = GetTeam(contract.TeamId);
            int regularSeasonGames = CountRegularSeasonGames(player.CurrentTeamId);
            ContractBonusProgress[] bonusProgress = new ContractBonusService(_balance.ContractBonus)
                .Evaluate(_career, regularSeasonGames);
            ContractOffer[] marketOffers = BuildMarketOffers(transitionService);
            RenewalContractOfferView[] renewalOffers = BuildRenewalOfferViews(transitionService);
            ContractNegotiationStatus negotiationStatus = ResolveNegotiationStatus(
                transitionService,
                season,
                contract);

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

            var evaluator = new PlayerValueEvaluator(_balance.PlayerEvaluation);
            return new CareerContractView
            {
                PlayerName = player.Name,
                Age = player.Age,
                Position = player.PrimaryPosition,
                Overall = evaluator.CalculatePositionValue(player.ToPlayer()),
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
                CanBeginNegotiation = negotiationStatus == ContractNegotiationStatus.NegotiationAvailable,
                CanSignSelectedOffer = transitionService?.SelectedOffer.HasValue == true,
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
                    GetTeam(contract.TeamId).Name,
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
            if (transitionService?.Step == SeasonTransitionStep.ContractOffers)
            {
                var actual = new ContractOffer[transitionService.RenewalOffers.Count];
                for (int index = 0; index < actual.Length; index++)
                    actual[index] = transitionService.RenewalOffers[index];
                return actual;
            }

            int seasonId = _career.League.CurrentSeason.SeasonId;
            ulong previewSeed = DeterministicSeed.Derive(
                _career.League.RandomSeed,
                MarketPreviewStream ^ (uint)seasonId);
            var evaluator = new ContractOfferEvaluator(
                _balance.ContractOffer,
                _balance.PlayerEvaluation,
                new Pcg32Random(previewSeed));
            var teams = new GeneratedTeam[_career.League.Teams.Count];
            for (int index = 0; index < teams.Length; index++)
                teams[index] = ToGeneratedTeam(_career.League.Teams[index]);
            int evaluationBonus = _career.League.CurrentSeason.Settlement.ContractEvaluationBonus;
            return ContractOfferBoard.SelectOffers(
                _balance.ContractOffer,
                evaluator,
                _career.MyPlayer.ToPlayer(),
                teams,
                evaluationBonus);
        }

        private RenewalContractOfferView[] BuildRenewalOfferViews(
            CareerSeasonTransitionService transitionService)
        {
            if (transitionService?.Step != SeasonTransitionStep.ContractOffers)
                return Array.Empty<RenewalContractOfferView>();

            ContractOffer? selected = transitionService.SelectedOffer;
            var result = new RenewalContractOfferView[transitionService.RenewalOffers.Count];
            for (int index = 0; index < result.Length; index++)
            {
                ContractOffer offer = transitionService.RenewalOffers[index];
                result[index] = new RenewalContractOfferView(
                    offer.Team.TeamId,
                    offer.Team.Name,
                    offer.Team.PrimaryColor,
                    offer.Team.GetPositionNeed(_career.MyPlayer.PrimaryPosition),
                    offer.Team.Archetype.Development,
                    offer.SigningBonus,
                    offer.AnnualSalary,
                    offer.ContractYears,
                    offer.ExpectedRole,
                    selected.HasValue && selected.Value.Team.TeamId == offer.Team.TeamId);
            }
            return result;
        }

        private static ContractNegotiationStatus ResolveNegotiationStatus(
            CareerSeasonTransitionService transitionService,
            SeasonState season,
            PlayerContractState contract)
        {
            if (transitionService?.Step == SeasonTransitionStep.ContractOffers)
                return ContractNegotiationStatus.OffersAvailable;
            if (contract.EndYear > season.Year)
                return ContractNegotiationStatus.Active;
            return season.Phase == SeasonPhase.Offseason
                ? ContractNegotiationStatus.NegotiationAvailable
                : ContractNegotiationStatus.ExpiringThisSeason;
        }

        private int CountRegularSeasonGames(int playerTeamId)
        {
            SeasonScheduleState schedule = _career.League.CurrentSeason.Schedule;
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
            for (int index = 0; index < _career.League.Teams.Count; index++)
            {
                TeamState team = _career.League.Teams[index];
                if (team.TeamId == teamId)
                    return team;
            }
            throw new InvalidOperationException($"TeamId {teamId}를 찾을 수 없습니다.");
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
