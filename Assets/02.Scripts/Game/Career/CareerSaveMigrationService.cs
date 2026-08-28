using System;
using System.Collections.Generic;

namespace Baseball.Game.Career
{
    /// <summary>
    /// v7 단일 리그 커리어를 기존 상태 손실 없이 v8 다중 리그 월드로 승격한다.
    /// </summary>
    public sealed class CareerSaveMigrationService
    {
        private readonly NewGameConfiguration _configuration;

        public CareerSaveMigrationService(NewGameConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public void MigrateV7ToV8(CareerState career, ulong migrationSeed)
        {
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (career.SaveVersion != 7)
                throw new InvalidOperationException("SaveVersion 7 커리어만 v8로 마이그레이션할 수 있습니다.");

            LeagueState legacyLeague = career.CurrentLeague;
            NormalizeLegacyContracts(career, legacyLeague.LeagueId);
            WorldState world = new CareerWorldFactory(_configuration).CreateMigratedWorld(
                career.World.WorldSeed,
                migrationSeed,
                legacyLeague,
                career.MyPlayer,
                career.ContractHistory);
            career.UpgradeToWorld(NewGameFlow.CurrentSaveVersion, world);
            ImportLegacyMovementHistory(career);
            career.World.ValidateInvariants();
        }

        private static void NormalizeLegacyContracts(CareerState career, LeagueId leagueId)
        {
            var usedContractIds = new HashSet<int>();
            int nextContractId = 1;
            for (int index = 0; index < career.ContractHistory.Count; index++)
            {
                int contractId = career.ContractHistory[index].ContractId;
                if (contractId <= 0)
                    continue;
                if (!usedContractIds.Add(contractId))
                    throw new InvalidOperationException($"기존 ContractId {contractId}가 중복되었습니다.");
                nextContractId = Math.Max(nextContractId, contractId + 1);
            }

            for (int index = 0; index < career.ContractHistory.Count; index++)
            {
                PlayerContractState contract = career.ContractHistory[index];
                int contractId = contract.ContractId > 0 ? contract.ContractId : nextContractId;
                if (contract.ContractId <= 0)
                {
                    usedContractIds.Add(contractId);
                    nextContractId++;
                }
                contract.MigrateLegacyIdentity(
                    contractId,
                    career.MyPlayerId,
                    leagueId,
                    ReferenceEquals(contract, career.CurrentContract));
            }

            career.MyPlayer.ReplaceActiveContract(
                career.CurrentContract.ContractId,
                leagueId);
        }

        private static void ImportLegacyMovementHistory(CareerState career)
        {
            for (int index = 0; index < career.TradeState.History.Count; index++)
            {
                TradeHistoryRecord history = career.TradeState.History[index];
                DateTime movementDate = new DateTime(history.Year, 1, 1).AddDays(history.GameIndex);
                career.World.MovementLedger.Record(new PlayerMovementRecord(
                    movementDate,
                    history.SeasonId,
                    career.MyPlayer.PlayerId,
                    PlayerMovementType.Trade,
                    career.CurrentLeague.LeagueId,
                    history.PreviousTeamId,
                    career.CurrentLeague.LeagueId,
                    history.NewTeamId,
                    history.PreviousRole,
                    career.CurrentContract.PromisedRole,
                    history.ProjectedRole,
                    career.CurrentContract.ContractId,
                    "LegacySingleLeague 트레이드 이력"));
                if (movementDate > career.World.Calendar.CurrentDate)
                    career.World.Calendar.AdvanceTo(movementDate);
            }
        }
    }
}
