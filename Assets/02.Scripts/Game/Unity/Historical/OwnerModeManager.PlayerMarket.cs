using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>구단주 화면이 선수 계약 Preview와 Command를 호출하는 공개 경계다.</summary>
    public sealed partial class OwnerModeManager
    {
        private OwnerPlayerMarketService _playerMarketService;

        public IReadOnlyList<OwnerPlayerContractState> GetPlayerContracts()
        {
            OwnerPlayerMarketService service = RequirePlayerMarketService();
            service.EnsureInitialized(RequireRuntime());
            return Runtime.ManagerMode.PlayerContracts;
        }

        public OwnerContractRenewalPreview PreviewPlayerContractRenewal(string cardId, int seasons) =>
            RequirePlayerMarketService().PreviewRenewal(RequireRuntime(), cardId, seasons);

        public OwnerContractRenewalPreview RenewPlayerContract(string cardId, int seasons)
        {
            OwnerContractRenewalPreview result = RequirePlayerMarketService().Renew(RequireRuntime(), cardId, seasons);
            if (result.CanCommit) NotifyRuntimeChanged();
            return result;
        }

        /// <summary>만료 임박 선수의 일괄 연장 조건을 조회한다.</summary>
        public OwnerContractBatchPreview PreviewExpiringPlayerContractRenewals(int seasons) =>
            RequirePlayerMarketService().PreviewExpiringRenewals(RequireRuntime(), seasons);

        /// <summary>만료 임박 선수의 계약을 일괄 연장하고 화면을 갱신한다.</summary>
        public OwnerContractBatchPreview RenewExpiringPlayerContracts(int seasons)
        {
            OwnerContractBatchPreview result = RequirePlayerMarketService().RenewExpiringContracts(RequireRuntime(), seasons);
            if (result.CanCommit) NotifyRuntimeChanged();
            return result;
        }

        private OwnerPlayerMarketService RequirePlayerMarketService()
        {
            if (_balance == null) throw new InvalidOperationException("Owner Balance가 준비되지 않았습니다.");
            return _playerMarketService ??= new OwnerPlayerMarketService(_balance);
        }
    }
}
