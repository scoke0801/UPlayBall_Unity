using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 시즌 중 기존 구단 연장 제안을 평가하고 수락·거절 결과를 커리어 계약에 반영한다.
    /// </summary>
    public sealed class ContractRenewalService
    {
        private readonly CareerState _career;
        private readonly BalanceTable _balance;

        public ContractRenewalService(CareerState career, BalanceTable balance)
        {
            _career = career ?? throw new ArgumentNullException(nameof(career));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public ContractOffer? BuildExtensionOffer()
        {
            SeasonState season = _career.CurrentLeague.CurrentSeason;
            if (season?.Phase != SeasonPhase.RegularSeason ||
                _career.HasResolvedExtension(season.SeasonId))
            {
                return null;
            }

            TeamSeasonRecordState record = season.GetTeamRecord(_career.MyPlayer.CurrentTeamId);
            int gamesPlayed = record?.GamesPlayed ?? 0;
            ContractRenewalBalance renewal = _balance.ContractRenewal;
            if (gamesPlayed < renewal.ExtensionStartGame || gamesPlayed > renewal.ExtensionEndGame)
                return null;
            if (_career.CurrentContract.GetRemainingSeasonsAfter(season.Year) > 1)
                return null;
            if (_career.CurrentExpectedRole != ExpectedRole.StartingCompetition ||
                _career.MyPlayer.ManagerEvaluation < 60)
            {
                return null;
            }

            TeamState team = GetCurrentTeam();
            Player player = _career.MyPlayer.ToPlayer();
            int playerValue = new PlayerValueEvaluator(_balance.PlayerEvaluation)
                .CalculatePositionValue(player);
            double marketSalary = _balance.ContractOffer.BaseSalary * Math.Max(0.5d, playerValue / 50d);
            if (marketSalary < _career.CurrentContract.AnnualSalary * renewal.ExtensionMarketValueRatio)
                return null;

            var input = new ContractRenewalEvaluationInput(
                ToGeneratedTeam(team),
                playerValue,
                currentRoleValue: 90d,
                recentPerformance: CalculateRecentPerformance(),
                ageAndPotential: CalculateAgeAndPotential(),
                costEfficiency: 100d,
                managerRelationship: _career.MyPlayer.ManagerEvaluation,
                strongestCompetitorOverall: team.GetStrongestCompetitorOverall(player.PrimaryPosition));
            ContractOffer? offer = new ContractRenewalEvaluator(renewal, _balance.ContractOffer)
                .Evaluate(input, player.PrimaryPosition, ContractOfferChannel.CurrentTeamExtension);
            return offer.HasValue
                ? offer.Value.WithMovementClauses(
                    _career.CurrentContract.HasUpperLeagueReleaseClause,
                    _career.CurrentContract.UpperLeagueReleaseCompensation,
                    _career.CurrentContract.HasRelegationTransferRequestClause)
                : null;
        }

        public PlayerContractState AcceptExtension()
        {
            ContractOffer offer = BuildExtensionOffer() ??
                                  throw new InvalidOperationException("수락할 수 있는 연장 계약이 없습니다.");
            offer = offer.WithMovementClauses(
                _career.CurrentContract.HasUpperLeagueReleaseClause,
                _career.CurrentContract.UpperLeagueReleaseCompensation,
                _career.CurrentContract.HasRelegationTransferRequestClause);
            SeasonState season = _career.CurrentLeague.CurrentSeason;
            int preservedSeasons = 1 + _career.CurrentContract.GetRemainingSeasonsAfter(season.Year);
            var contract = new PlayerContractState(
                NewGameFlow.CurrentSaveVersion,
                _career.MyPlayer.CurrentTeamId,
                season.Year,
                preservedSeasons + offer.ContractYears,
                offer.SigningBonus,
                offer.AnnualSalary,
                offer.ExpectedRole,
                offer.HasUpperLeagueReleaseClause,
                offer.UpperLeagueReleaseCompensation,
                offer.HasRelegationTransferRequestClause);
            _career.RenewContract(contract);
            _career.ResolveExtension(season.SeasonId);
            if (offer.SigningBonus > 0L)
            {
                _career.Economy.Earn(
                    season.Year,
                    Baseball.Core.Growth.MoneyTransactionType.ContractIncome,
                    $"contract_{season.SeasonId}_extension_bonus",
                    offer.SigningBonus);
            }
            return contract;
        }

        public void DeclineExtension()
        {
            SeasonState season = _career.CurrentLeague.CurrentSeason ??
                                 throw new InvalidOperationException("현재 시즌이 없습니다.");
            if (!BuildExtensionOffer().HasValue)
                throw new InvalidOperationException("거절할 수 있는 연장 계약이 없습니다.");
            _career.ResolveExtension(season.SeasonId);
        }

        private double CalculateRecentPerformance()
        {
            PlayerSeasonStatisticsState statistics = _career.CurrentLeague.CurrentSeason.PlayerStatistics;
            double score = _career.MyPlayer.ManagerEvaluation;
            if (_career.MyPlayer.PrimaryPosition is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher)
            {
                if (statistics.OutsRecorded >= 9)
                    score = 100d - (statistics.EarnedRunAverage - 2d) * (100d / 6d);
            }
            else if (statistics.PlateAppearances >= 15)
            {
                score = (statistics.OnBasePlusSlugging - 0.45d) * (100d / 0.65d);
            }
            return Clamp(score, 0d, 100d);
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

        private TeamState GetCurrentTeam()
        {
            for (int index = 0; index < _career.CurrentLeague.Teams.Count; index++)
            {
                if (_career.CurrentLeague.Teams[index].TeamId == _career.MyPlayer.CurrentTeamId)
                    return _career.CurrentLeague.Teams[index];
            }
            throw new InvalidOperationException("현재 소속 구단을 찾을 수 없습니다.");
        }

        private static GeneratedTeam ToGeneratedTeam(TeamState team)
        {
            int positionCount = (int)PlayerPosition.ReliefPitcher + 1;
            var needs = new int[positionCount];
            for (int position = (int)PlayerPosition.Catcher; position < positionCount; position++)
                needs[position] = team.GetPositionNeed((PlayerPosition)position);
            var competitors = new RosterCompetitor[team.RosterCompetitors.Count];
            for (int index = 0; index < competitors.Length; index++)
            {
                RosterCompetitorState source = team.RosterCompetitors[index];
                competitors[index] = new RosterCompetitor(
                    source.PlayerId,
                    source.Name,
                    source.Position,
                    source.Overall);
            }
            return new GeneratedTeam(
                team.TeamId,
                team.Name,
                team.Archetype,
                team.PrimaryColor,
                needs,
                competitors);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
