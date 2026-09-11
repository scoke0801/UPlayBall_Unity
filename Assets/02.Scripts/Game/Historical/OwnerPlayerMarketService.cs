using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>일괄 연장 대상과 총비용을 전달한다.</summary>
    public sealed class OwnerContractBatchPreview
    {
        public OwnerContractBatchPreview(OwnerContractRenewalPreview[] renewals, long signingCost,
            long annualSalaryTotal, string reason, long deferredSigningCost = 0L)
        {
            Renewals = Array.AsReadOnly(renewals);
            SigningCost = signingCost;
            AnnualSalaryTotal = annualSalaryTotal;
            Reason = reason;
            DeferredSigningCost = deferredSigningCost;
        }

        public IReadOnlyList<OwnerContractRenewalPreview> Renewals { get; }
        public long SigningCost { get; }
        public long DeferredSigningCost { get; }
        public long AnnualSalaryTotal { get; }
        public string Reason { get; }
        public bool CanCommit => Renewals.Count > 0 && string.IsNullOrEmpty(Reason);
    }

    /// <summary>구단주 선수 계약을 Preview/Validate/Command 경계로 조정한다.</summary>
    public sealed class OwnerPlayerMarketService
    {
        private readonly BalanceTable _balance;
        private readonly OwnerPlayerMarketResolver _resolver;

        public OwnerPlayerMarketService(BalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
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

        public OwnerContractRenewalPreview PreviewRenewal(
            ManagerHistoricalRuntimeState runtime,
            string cardId,
            int seasons)
        {
            EnsureInitialized(runtime);
            OwnerPlayerContractState contract = runtime.ManagerMode.GetPlayerContract(cardId);
            if (!runtime.WorldCardCatalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new ArgumentException("WorldCardCatalog에 없는 계약 선수입니다.", nameof(cardId));
            return _resolver.PreviewRenewal(
                contract,
                card,
                runtime.WorldCardCatalog.GetPlayerSeason(card),
                runtime.ManagerMode.LiveSeason.SeasonNumber,
                seasons,
                runtime.Economy.Money,
                CanDeferRenewal(runtime, contract) ? ContractPaymentMode.AllowArrears : ContractPaymentMode.RequireCash);
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

        public OwnerContractRenewalPreview Renew(
            ManagerHistoricalRuntimeState runtime,
            string cardId,
            int seasons)
        {
            OwnerContractRenewalPreview preview = PreviewRenewal(runtime, cardId, seasons);
            if (!preview.CanCommit) return preview;
            if (preview.DeferredSigningCost > 0L)
                runtime.Economy.SettleContractPayment(preview.SigningCost);
            else if (preview.SigningCost > 0L && !runtime.Economy.TrySpendMoney(preview.SigningCost))
                throw new InvalidOperationException("검증된 선수 계약금을 반영할 수 없습니다.");
            runtime.ManagerMode.RenewPlayerContract(
                preview.CardId,
                runtime.ManagerMode.LiveSeason.SeasonNumber,
                preview.Seasons,
                preview.AnnualSalary);
            return preview;
        }

        /// <summary>만료 임박 선수 전원의 계약금과 연봉을 합산한다.</summary>
        public OwnerContractBatchPreview PreviewExpiringRenewals(ManagerHistoricalRuntimeState runtime, int seasons)
        {
            EnsureInitialized(runtime);
            var renewals = new List<OwnerContractRenewalPreview>();
            long signingCost = 0L;
            long annualSalary = runtime.ManagerMode.GetAnnualPlayerSalaryTotal();
            string reason = string.Empty;
            foreach (OwnerPlayerContractState contract in runtime.ManagerMode.PlayerContracts)
            {
                if (!contract.IsExpiring) continue;
                OwnerContractRenewalPreview preview = PreviewRenewal(runtime, contract.CardId, seasons);
                renewals.Add(preview);
                signingCost = checked(signingCost + preview.SigningCost);
                annualSalary = checked(annualSalary - contract.AnnualSalary + preview.AnnualSalary);
                if (!preview.CanCommit) reason = preview.Reason;
            }
            if (renewals.Count == 0) reason = "연장할 만료 임박 선수가 없습니다.";
            else if (runtime.Economy.Money < signingCost && !IsSeasonCompleted(runtime))
                reason = "일괄 연장 계약금이 부족합니다.";
            return new OwnerContractBatchPreview(renewals.ToArray(), signingCost, annualSalary, reason,
                IsSeasonCompleted(runtime) ? Math.Max(0L, signingCost - runtime.Economy.Money) : 0L);
        }

        /// <summary>전체 비용을 검증한 뒤 한 번 차감하고 모든 대상의 계약을 연장한다.</summary>
        public OwnerContractBatchPreview RenewExpiringContracts(ManagerHistoricalRuntimeState runtime, int seasons)
        {
            OwnerContractBatchPreview preview = PreviewExpiringRenewals(runtime, seasons);
            if (!preview.CanCommit) return preview;
            if (preview.DeferredSigningCost > 0L)
                runtime.Economy.SettleContractPayment(preview.SigningCost);
            else if (preview.SigningCost > 0L && !runtime.Economy.TrySpendMoney(preview.SigningCost))
                throw new InvalidOperationException("일괄 연장 계약금을 반영할 수 없습니다.");
            foreach (OwnerContractRenewalPreview renewal in preview.Renewals)
                runtime.ManagerMode.RenewPlayerContract(renewal.CardId,
                    runtime.ManagerMode.LiveSeason.SeasonNumber, renewal.Seasons, renewal.AnnualSalary);
            return preview;
        }

        private static bool IsSeasonCompleted(ManagerHistoricalRuntimeState runtime) =>
            runtime.ManagerMode.LiveSeason.IsCompleted &&
            (runtime.LeagueWorld == null || runtime.LeagueWorld.IsCompleted);

        private static bool CanDeferRenewal(ManagerHistoricalRuntimeState runtime, OwnerPlayerContractState contract) =>
            contract.IsExpiring && IsSeasonCompleted(runtime);

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
