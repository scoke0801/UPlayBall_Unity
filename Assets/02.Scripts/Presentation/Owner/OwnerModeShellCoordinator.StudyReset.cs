using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Game.Shop;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using Baseball.Presentation.Shop;

namespace Baseball.Presentation.Owner
{
    public sealed partial class OwnerModeShellCoordinator
    {
        private void BindStudyResetTargets()
        {
            var targets = new List<ShopTargetSnapshot>();
            var runtime = _manager.Runtime;
            foreach (var owned in runtime.OwnedCards)
            {
                if (StudyResetFulfillment.GetBlockedReason(runtime, owned.CardId).Length > 0) continue;
                var card = runtime.WorldCardCatalog.GetRequiredCard(owned.CardId);
                var season = runtime.WorldCardCatalog.GetPlayerSeason(card);
                string name = runtime.IdentityRegistry.GetPresentationPlayerName(season.PlayerPersonId);
                int total = 0;
                for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
                    total += owned.Training.GetStudyBonus((PlayerAbility)index);
                targets.Add(new ShopTargetSnapshot(owned.CardId,
                    $"{name} · {season.OriginYear} · {OwnerCollectionPresentationBuilder.FormatEdition(card.Edition)} · 유학 능력치 -{total}"));
            }
            _expansionWorkspace.BindShopTargets(targets);
        }
    }
}
