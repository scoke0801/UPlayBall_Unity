using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>구단주 화면이 선수 계약·트레이드 Preview와 Command를 호출하는 공개 경계다.</summary>
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

        public OwnerTradePreview PreviewPlayerTrade(
            string partnerTeamSeasonKey,
            string outgoingCardId,
            string incomingCardId) => RequirePlayerMarketService().PreviewTrade(
                RequireRuntime(), partnerTeamSeasonKey, outgoingCardId, incomingCardId);

        public OwnerTradePreview CommitPlayerTrade(
            string partnerTeamSeasonKey,
            string outgoingCardId,
            string incomingCardId)
        {
            OwnerTradePreview result = RequirePlayerMarketService().CommitTrade(
                RequireRuntime(), partnerTeamSeasonKey, outgoingCardId, incomingCardId);
            if (!result.CanCommit) return result;

            CurrentPregame = null;
            ConfigureTeamColors(_contentProvider.Load(), Runtime.PlayerTeamSeasonKey);
            RefreshAvailableTacticCards();
            NotifyRuntimeChanged();
            return result;
        }

        private OwnerPlayerMarketService RequirePlayerMarketService()
        {
            if (_balance == null) throw new InvalidOperationException("Owner Balance가 준비되지 않았습니다.");
            return _playerMarketService ??= new OwnerPlayerMarketService(_balance);
        }
    }
}
