using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{

    /// <summary>구단주 선수단의 급여 상태를 초기화하고 1군 등록 변경에 맞춰 보존한다.</summary>
    public sealed class OwnerPlayerMarketService
    {
        private readonly OwnerPlayerMarketResolver _resolver;

        public OwnerPlayerMarketService(BalanceTable balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            _resolver = new OwnerPlayerMarketResolver(balance.OwnerPlayerMarket);
        }

        public void EnsureInitialized(ManagerHistoricalRuntimeState runtime)
        {
            ManagerModeRuntimeState mode = RequireMode(runtime);
            CurrentRosterState playerRoster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            if (mode.PlayerContracts.Count == 0)
            {
                OwnerPlayerContractState[] contracts = _resolver.CreateInitialContracts(
                    playerRoster,
                    runtime.WorldCardCatalog,
                    mode.LiveSeason.SeasonNumber);
                mode.ReplacePlayerContractState(contracts);
            }
            else if (mode.PlayerContracts.Count == playerRoster.Entries.Count &&
                !HasContractCoverage(mode, playerRoster))
            {
                // 구버전 1군 등록 Command가 만든 정확히 25개짜리 불일치만 안전하게 복구한다.
                mode.ReplacePlayerContractState(
                    _resolver.CreateActiveRosterContracts(
                        playerRoster,
                        runtime.WorldCardCatalog,
                        mode.LiveSeason.SeasonNumber,
                        mode.PlayerContracts));
            }

            ValidateContractCoverage(mode, playerRoster);
        }


        /// <summary>검증된 1군 후보에 맞춰 기존 계약을 보존한 25인 계약 후보를 만든다.</summary>
        public OwnerPlayerContractState[] CreateActiveRosterContracts(
            ManagerHistoricalRuntimeState runtime,
            CurrentRosterState roster)
        {
            EnsureInitialized(runtime);
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            return _resolver.CreateActiveRosterContracts(
                roster,
                runtime.WorldCardCatalog,
                runtime.ManagerMode.LiveSeason.SeasonNumber,
                runtime.ManagerMode.PlayerContracts);
        }




        private static ManagerModeRuntimeState RequireMode(ManagerHistoricalRuntimeState runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (!runtime.HasManagerMode)
                throw new InvalidOperationException("구단주 확장 상태가 없습니다.");
            return runtime.ManagerMode;
        }

        private static void ValidateContractCoverage(
            ManagerModeRuntimeState mode,
            CurrentRosterState playerRoster)
        {
            if (mode.PlayerContracts.Count != playerRoster.Entries.Count)
                throw new InvalidOperationException("현재 25인 로스터와 선수 계약 수가 일치하지 않습니다.");

            if (HasContractCoverage(mode, playerRoster)) return;

            for (int rosterIndex = 0; rosterIndex < playerRoster.Entries.Count; rosterIndex++)
            {
                string cardId = playerRoster.Entries[rosterIndex].CardId;
                bool hasContract = false;
                for (int contractIndex = 0; contractIndex < mode.PlayerContracts.Count; contractIndex++)
                {
                    if (!string.Equals(mode.PlayerContracts[contractIndex].CardId, cardId, StringComparison.Ordinal))
                        continue;
                    hasContract = true;
                    break;
                }
                if (!hasContract)
                    throw new InvalidOperationException($"현재 로스터 CardId {cardId}의 선수 계약이 없습니다.");
            }
        }

        private static bool HasContractCoverage(
            ManagerModeRuntimeState mode,
            CurrentRosterState playerRoster)
        {
            for (int rosterIndex = 0; rosterIndex < playerRoster.Entries.Count; rosterIndex++)
            {
                string cardId = playerRoster.Entries[rosterIndex].CardId;
                bool hasContract = false;
                for (int contractIndex = 0; contractIndex < mode.PlayerContracts.Count; contractIndex++)
                {
                    if (!string.Equals(mode.PlayerContracts[contractIndex].CardId, cardId, StringComparison.Ordinal))
                        continue;
                    hasContract = true;
                    break;
                }
                if (!hasContract) return false;
            }
            return true;
        }

    }
}
