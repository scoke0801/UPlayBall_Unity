using System;

namespace Baseball.Game.Historical
{
    /// <summary>구단주 선수단 등록에 필요한 급여 상태 서비스를 제공한다.</summary>
    public sealed partial class OwnerModeManager
    {
        private OwnerPlayerMarketService _playerMarketService;

        /// <summary>현재 선수단의 급여 저장 상태를 조회한다.</summary>
        public System.Collections.Generic.IReadOnlyList<Baseball.Core.Historical.OwnerPlayerContractState> GetPlayerContracts()
        {
            RequirePlayerMarketService().EnsureInitialized(RequireRuntime());
            return Runtime.ManagerMode.PlayerContracts;
        }

        private OwnerPlayerMarketService RequirePlayerMarketService()
        {
            if (_balance == null) throw new InvalidOperationException("Owner Balance가 준비되지 않았습니다.");
            return _playerMarketService ??= new OwnerPlayerMarketService(_balance);
        }
    }
}
