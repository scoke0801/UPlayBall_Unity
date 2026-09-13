using System;
namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        private ManagerHistoricalRuntimeState _encyclopediaRuntime;
        private EncyclopediaCatalogService _encyclopediaCatalogService;

        /// <summary>카드 보호 상태를 복사본에 적용하고 저장 성공 이후에만 공개한다.</summary>
        public void SetPlayerCardLocked(string cardId, bool isLocked)
        {
            EnsureRegularSeasonSimulationIsNotRunning();
            CommitGrowthChange(runtime =>
            {
                runtime.SetPlayerCardLocked(cardId, isLocked);
                return true;
            });
            NotifyRuntimeChanged();
        }

        /// <summary>전체 Runtime Archive를 외부에 노출하지 않고 현재 Owner World와 결합한 도감 조회를 제공한다.</summary>
        public EncyclopediaCatalogService CreateEncyclopediaCatalogService()
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            if (ReferenceEquals(runtime, _encyclopediaRuntime) && _encyclopediaCatalogService != null)
                return _encyclopediaCatalogService;

            HistoricalBakedContent content = _contentProvider?.Load()
                ?? throw new InvalidOperationException("Historical Content가 없습니다.");
            _encyclopediaCatalogService = new EncyclopediaCatalogService(content, runtime);
            _encyclopediaRuntime = runtime;
            return _encyclopediaCatalogService;
        }

        /// <summary>현재 미보유 Catalog 카드만 위시로 등록하고 정확한 CardId 단위로 해제한다.</summary>
        public bool ToggleWishlist(string cardId)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime();
            if (string.IsNullOrWhiteSpace(cardId) || !runtime.WorldCardCatalog.TryGetCard(cardId.Trim(), out _))
                throw new ArgumentException("현재 월드에 없는 카드는 위시리스트에 등록할 수 없습니다.", nameof(cardId));

            string id = cardId.Trim();
            bool isAdded;
            if (runtime.Wishlist.Contains(id))
            {
                runtime.Wishlist.Remove(id);
                isAdded = false;
            }
            else
            {
                if (runtime.TryGetOwnedCard(id, out _))
                    throw new InvalidOperationException("현재 보유 중인 카드는 위시리스트에 등록할 수 없습니다.");
                runtime.Wishlist.Add(id);
                isAdded = true;
            }

            NotifyRuntimeChanged();
            return isAdded;
        }
    }
}
