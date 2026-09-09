using System;
using System.Collections.Generic;
using System.Text;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Historical;
using Baseball.Game.Shop;
using Baseball.Presentation.Encyclopedia;
using Baseball.Presentation.SharedScreens;
using Baseball.Presentation.SharedUI;
using Baseball.Simulation.Historical;

namespace Baseball.Presentation.Owner
{
    public sealed partial class OwnerModeRuntimeSnapshotFactory
    {
        /// <summary>전체 Archive와 현재 Owner 수집 상태를 가상화 UI용 불변 Snapshot으로 투영한다.</summary>
        public EncyclopediaScreenSnapshot CreateEncyclopedia(OwnerModeManager manager)
        {
            ManagerHistoricalRuntimeState runtime = RequireRuntime(manager);
            WorldIdentityRegistry identities = runtime.IdentityRegistry;
            EncyclopediaCatalogService service = manager.CreateEncyclopediaCatalogService();
            IReadOnlyList<EncyclopediaCardEntry> queriedCards = service.QueryCards();
            IReadOnlyList<EncyclopediaPlayerSeasonEntry> queriedSeasons = service.QueryPlayerSeasons();
            EncyclopediaCollectionProgress progress = service.GetCollectionProgress();
            PerCardBonusMap teamColorBonuses = CreateCurrentTeamColorBonuses(
                manager,
                runtime,
                runtime.GetRoster(runtime.PlayerTeamSeasonKey),
                runtime.ManagerMode.GetSelectedLineupPreset());
            ScoutFeaturePolicy scoutFeaturePolicy = OwnerShopComposer.ResolveScoutFeaturePolicy(runtime.WorldCardCatalog);
            IReadOnlyList<ScoutPoolDefinition> scoutPools = OwnerShopComposer.CreateScoutPools(
                runtime.WorldCardCatalog,
                scoutFeaturePolicy);

            var cards = new EncyclopediaScreenEntry[queriedCards.Count];
            var representativeCards = new Dictionary<string, EncyclopediaCardEntry>(
                queriedSeasons.Count,
                StringComparer.Ordinal);
            for (int index = 0; index < cards.Length; index++)
            {
                EncyclopediaCardEntry entry = queriedCards[index];
                if (!representativeCards.TryGetValue(entry.PlayerSeasonId, out EncyclopediaCardEntry current) ||
                    entry.Edition < current.Edition ||
                    (entry.Edition == current.Edition &&
                     string.CompareOrdinal(entry.CardId, current.CardId) < 0))
                {
                    representativeCards[entry.PlayerSeasonId] = entry;
                }
                EncyclopediaScreenEntry screenEntry = CreateCardScreenEntry(entry, identities);
                screenEntry.SetDetailResolver(target => PopulateCardDetail(
                    target,
                    manager,
                    runtime,
                    entry,
                    teamColorBonuses,
                    scoutPools,
                    scoutFeaturePolicy));
                cards[index] = screenEntry;
            }

            var seasons = new EncyclopediaScreenEntry[queriedSeasons.Count];
            for (int index = 0; index < seasons.Length; index++)
            {
                EncyclopediaPlayerSeasonEntry entry = queriedSeasons[index];
                EncyclopediaScreenEntry screenEntry = CreateSeasonScreenEntry(entry, identities);
                if (representativeCards.TryGetValue(entry.PlayerSeasonId, out EncyclopediaCardEntry representative))
                {
                    screenEntry.SetDetailResolver(target => PopulateCardDetail(
                        target,
                        manager,
                        runtime,
                        representative,
                        teamColorBonuses,
                        scoutPools,
                        scoutFeaturePolicy));
                }
                seasons[index] = screenEntry;
            }

            return new EncyclopediaScreenSnapshot
            {
                IsReadOnly = false,
                Cards = cards,
                Seasons = seasons,
                Progress = CreateProgressCells(progress.FranchiseYears, identities),
                Editions = CreateEditionProgress(progress.Editions),
                EditionSummary = CreateEditionSummary(progress.Editions)
            };
        }

        private static EncyclopediaScreenEntry CreateCardScreenEntry(
            EncyclopediaCardEntry entry,
            WorldIdentityRegistry identities)
        {
            CardEditionPresentationMetadata metadata =
                CardEditionPresentationMetadataCatalog.Resolve(entry.Edition);
            string playerDisplayName = identities.GetPresentationPlayerName(entry.PlayerPersonId);
            string franchiseDisplayName = identities.GetPresentationTeamSeasonName(
                entry.OriginTeamSeasonKey,
                entry.OriginFranchiseId);
            return new EncyclopediaScreenEntry
            {
                CardId = entry.CardId,
                PlayerSeasonId = entry.PlayerSeasonId,
                PlayerPersonId = entry.PlayerPersonId,
                FranchiseId = entry.OriginFranchiseId,
                FranchiseDisplayName = franchiseDisplayName,
                DisplayName = playerDisplayName,
                OriginYear = entry.OriginYear,
                Cost = entry.Cost,
                Position = OwnerCollectionPresentationBuilder.FormatPosition(entry.Position),
                PitcherRole = FormatPitcherRole(entry.PlayerType, entry.PitcherRole),
                Bats = CareerSharedSnapshotFormatters.FormatHandedness(entry.Bats),
                Throws = CareerSharedSnapshotFormatters.FormatHandedness(entry.Throws),
                EditionId = metadata.EditionId,
                EditionDisplayName = metadata.DisplayName,
                EditionSortPriority = metadata.SortPriority,
                IsPitcher = entry.PlayerType == PlayerType.Pitcher,
                IsCurrentlyOwned = entry.IsCurrentlyOwned,
                WasEverAcquired = entry.WasEverAcquired,
                IsWishlisted = entry.IsWishlisted,
                OwnedCount = entry.OwnedCount,
                AddedSequence = entry.WishlistAddedSequence ?? -1,
                MiniCard = new PlayerMiniCardModel(
                    entry.CardId,
                    playerDisplayName,
                    OwnerCollectionPresentationBuilder.FormatPlayerRole(
                        entry.Position,
                        entry.PlayerType == PlayerType.Pitcher ? entry.PitcherRole : null),
                    entry.OriginYear.ToString(),
                    "COST " + entry.Cost,
                    metadata.DisplayName,
                    portraitAssetKey: entry.PlayerSeasonId,
                    frameEdition: entry.Edition,
                    cost: entry.Cost),
                CardInformation = CreateCardInformation(entry, metadata, franchiseDisplayName),
            };
        }

        private static EncyclopediaScreenEntry CreateSeasonScreenEntry(
            EncyclopediaPlayerSeasonEntry entry,
            WorldIdentityRegistry identities)
        {
            string playerDisplayName = identities.GetPresentationPlayerName(entry.PlayerPersonId);
            string franchiseDisplayName = identities.GetPresentationTeamSeasonName(
                entry.OriginTeamSeasonKey,
                entry.OriginFranchiseId);
            return new EncyclopediaScreenEntry
            {
                PlayerSeasonId = entry.PlayerSeasonId,
                PlayerPersonId = entry.PlayerPersonId,
                FranchiseId = entry.OriginFranchiseId,
                FranchiseDisplayName = franchiseDisplayName,
                DisplayName = playerDisplayName,
                OriginYear = entry.OriginYear,
                Cost = entry.Cost,
                Position = OwnerCollectionPresentationBuilder.FormatPosition(entry.Position),
                PitcherRole = FormatPitcherRole(entry.PlayerType, entry.PitcherRole),
                Bats = CareerSharedSnapshotFormatters.FormatHandedness(entry.Bats),
                Throws = CareerSharedSnapshotFormatters.FormatHandedness(entry.Throws),
                IsPitcher = entry.PlayerType == PlayerType.Pitcher,
                IsCurrentlyOwned = entry.IsCurrentlyOwned,
                WasEverAcquired = entry.WasEverAcquired,
                IsWishlisted = entry.IsWishlisted,
                AddedSequence = entry.NewestWishlistSequence ?? -1,
                CollectibleCardCount = entry.CollectibleCardCount,
                OwnedCardCount = entry.OwnedCardCount,
                EverAcquiredCardCount = entry.EverAcquiredCardCount,
                MiniCard = new PlayerMiniCardModel(
                    entry.PlayerSeasonId,
                    playerDisplayName,
                    OwnerCollectionPresentationBuilder.FormatPlayerRole(
                        entry.Position,
                        entry.PlayerType == PlayerType.Pitcher ? entry.PitcherRole : null),
                    entry.OriginYear.ToString(),
                    "COST " + entry.Cost,
                    "카드 " + entry.CollectibleCardCount + "종",
                    portraitAssetKey: entry.PlayerSeasonId,
                    cost: entry.Cost),
                CardInformation = $"현재 활성 카드 {entry.CollectibleCardCount}종 · 보유 {entry.OwnedCardCount} · 획득 {entry.EverAcquiredCardCount}"
            };
        }

        private static void PopulateCardDetail(
            EncyclopediaScreenEntry target,
            OwnerModeManager manager,
            ManagerHistoricalRuntimeState runtime,
            EncyclopediaCardEntry entry,
            PerCardBonusMap teamColorBonuses,
            IReadOnlyList<ScoutPoolDefinition> scoutPools,
            ScoutFeaturePolicy scoutFeaturePolicy)
        {
            OwnerCollectionCardSnapshot detail = CreateEncyclopediaCardDetail(
                manager,
                runtime,
                entry,
                teamColorBonuses);
            target.DetailCard = detail;
            target.AbilityInformation = CreateAbilityInformation(detail);
            target.WorldRecordInformation = CreateWorldRecordInformation(detail);
            target.AcquisitionInformation = CreateAcquisitionInformation(
                entry,
                runtime.WorldCardCatalog,
                scoutPools,
                scoutFeaturePolicy,
                runtime.IdentityRegistry);
            target.HasScoutRoute = target.AcquisitionInformation.Length > 0;
        }

        private static OwnerCollectionCardSnapshot CreateEncyclopediaCardDetail(
            OwnerModeManager manager,
            ManagerHistoricalRuntimeState runtime,
            EncyclopediaCardEntry entry,
            PerCardBonusMap teamColorBonuses)
        {
            if (runtime.TryGetOwnedCard(entry.CardId, out OwnedPlayerCardState owned))
                return CreateCollectionCard(manager, runtime, owned, entry.Card, teamColorBonuses);

            AbilityRatings abilities = new OwnerCardAbilityResolver(manager.Balance.Growth)
                .ResolvePermanent(entry.Season, entry.Card, null);
            return new OwnerCollectionCardSnapshot(
                entry.CardId,
                entry.PlayerPersonId,
                runtime.IdentityRegistry.GetPresentationPlayerName(entry.PlayerPersonId),
                entry.OriginYear,
                entry.Position,
                entry.Cost,
                entry.Edition,
                0,
                0,
                false,
                false,
                abilities,
                entry.OriginYear + "년 · 월드 기록",
                entry.PlayerSeasonId,
                entry.PlayerType == PlayerType.Pitcher ? entry.PitcherRole : null,
                entry.Throws,
                entry.Bats,
                CreatePitchSnapshots(manager, entry.Season, abilities),
                CreateSeasonRecord(runtime.WorldHistory, entry.Season, entry.OriginTeamSeasonKey, entry.OriginYear),
                teamDisplayName: runtime.IdentityRegistry.GetPresentationTeamSeasonName(
                    entry.OriginTeamSeasonKey,
                    entry.OriginFranchiseId),
                abilityGraphMaximum: manager.Balance.MatchRatingCurve.Caps.HardCap,
                isOwnedCard: false, preferredBattingOrder: entry.Card.PreferredBattingOrder);
        }

        private static EncyclopediaProgressCell[] CreateProgressCells(
            IReadOnlyList<FranchiseYearCollectionProgress> source,
            WorldIdentityRegistry identities)
        {
            var result = new EncyclopediaProgressCell[source.Count];
            for (int index = 0; index < result.Length; index++)
            {
                FranchiseYearCollectionProgress entry = source[index];
                result[index] = new EncyclopediaProgressCell
                {
                    HasTeamSeason = entry.HasTeamSeason,
                    FranchiseId = entry.FranchiseId,
                    FranchiseDisplayName = identities.GetPresentationTeamSeasonName(
                        entry.HasTeamSeason
                            ? entry.FranchiseId + "_" + entry.OriginYear
                            : string.Empty,
                        entry.FranchiseId),
                    OriginYear = entry.OriginYear,
                    CollectibleCardCount = entry.CollectibleCardCount,
                    EverAcquiredCardCount = entry.EverAcquiredCardCount,
                    OwnedCardCount = entry.OwnedCardCount,
                    WishlistCardCount = entry.WishlistCardCount,
                    PlayerSeasonCount = entry.PlayerSeasonCount,
                    AcquiredPlayerSeasonCount = entry.AcquiredPlayerSeasonCount
                };
            }
            return result;
        }

        private static EncyclopediaEditionProgressSnapshot[] CreateEditionProgress(
            IReadOnlyList<EditionCollectionProgress> source)
        {
            var result = new EncyclopediaEditionProgressSnapshot[source.Count];
            for (int index = 0; index < result.Length; index++)
            {
                EditionCollectionProgress entry = source[index];
                CardEditionPresentationMetadata metadata = CardEditionPresentationMetadataCatalog.Resolve(entry.Edition);
                result[index] = new EncyclopediaEditionProgressSnapshot
                {
                    EditionId = metadata.EditionId,
                    DisplayName = metadata.DisplayName,
                    Total = entry.CollectibleCardCount,
                    EverAcquired = entry.EverAcquiredCardCount,
                    Owned = entry.OwnedCardCount,
                    Wishlist = entry.WishlistCardCount,
                    SortPriority = metadata.SortPriority
                };
            }
            Array.Sort(result, (left, right) =>
            {
                int comparison = left.SortPriority.CompareTo(right.SortPriority);
                return comparison != 0 ? comparison : string.CompareOrdinal(left.EditionId, right.EditionId);
            });
            return result;
        }

        private static string CreateEditionSummary(IReadOnlyList<EditionCollectionProgress> source)
        {
            EncyclopediaEditionProgressSnapshot[] entries = CreateEditionProgress(source);
            var text = new StringBuilder();
            for (int index = 0; index < entries.Length; index++)
            {
                if (index > 0) text.Append('\n');
                text.Append(entries[index].DisplayName).Append("  ")
                    .Append(entries[index].EverAcquired).Append(" / ").Append(entries[index].Total);
            }
            return text.ToString();
        }

        private static string CreateCardInformation(
            EncyclopediaCardEntry entry,
            CardEditionPresentationMetadata metadata,
            string franchiseDisplayName)
        {
            return $"{entry.OriginYear}년 {franchiseDisplayName}\n{metadata.DisplayName} · COST {entry.Cost}\n" +
                   (entry.IsCurrentlyOwned
                       ? $"현재 {entry.OwnedCount}장 보유"
                       : entry.WasEverAcquired ? "획득 이력 있음 · 현재 미보유" : "미획득");
        }

        private static string CreateAbilityInformation(OwnerCollectionCardSnapshot detail)
        {
            var text = new StringBuilder();
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
            {
                var ability = (PlayerAbility)index;
                int? value = detail.GetEffectiveAbility(ability);
                if (!value.HasValue) continue;
                if (text.Length > 0) text.Append('\n');
                text.Append(CareerSharedSnapshotFormatters.FormatAbility(ability)).Append("  ").Append(value.Value);
            }
            return text.ToString();
        }

        private static string CreateWorldRecordInformation(OwnerCollectionCardSnapshot detail)
        {
            if (detail.SeasonRecord.Count == 0)
                return string.Empty;
            var text = new StringBuilder();
            for (int index = 0; index < detail.SeasonRecord.Count; index++)
            {
                if (index > 0) text.Append(index % 4 == 0 ? '\n' : ' ');
                text.Append(detail.SeasonRecord[index].Label).Append(' ').Append(detail.SeasonRecord[index].Value);
            }
            return text.ToString();
        }

        private static string CreateAcquisitionInformation(
            EncyclopediaCardEntry entry,
            WorldCardCatalog catalog,
            IReadOnlyList<ScoutPoolDefinition> scoutPools,
            ScoutFeaturePolicy scoutFeaturePolicy,
            WorldIdentityRegistry identities)
        {
            var text = new StringBuilder();
            string franchiseDisplayName = identities.GetPresentationTeamSeasonName(
                entry.OriginTeamSeasonKey,
                entry.OriginFranchiseId);
            for (int index = 0; index < scoutPools.Count; index++)
            {
                ScoutPoolDefinition pool = scoutPools[index];
                if (!ScoutRoller.IsCandidate(pool, catalog, scoutFeaturePolicy, entry.Card))
                    continue;
                if (text.Length > 0) text.Append('\n');
                text.Append(DescribeScoutPool(pool, entry, franchiseDisplayName));
            }
            return text.ToString();
        }

        private static string DescribeScoutPool(
            ScoutPoolDefinition pool,
            EncyclopediaCardEntry entry,
            string franchiseDisplayName)
        {
            switch (pool.ScoutType)
            {
                case ScoutType.General: return "일반 스카우트";
                case ScoutType.Franchise: return franchiseDisplayName + " 집중 스카우트";
                case ScoutType.Year: return entry.OriginYear + "년 집중 스카우트";
                case ScoutType.YearFranchise:
                    return franchiseDisplayName + " + " + entry.OriginYear + "년 정밀 스카우트";
                case ScoutType.Award:
                    return CardEditionPresentationMetadataCatalog.Resolve(entry.Edition).DisplayName + " 스카우트";
                default: throw new ArgumentOutOfRangeException(nameof(pool.ScoutType));
            }
        }

        private static string FormatPitcherRole(PlayerType playerType, PitcherRole role)
        {
            if (playerType != PlayerType.Pitcher) return "타자";
            return OwnerCollectionPresentationBuilder.FormatPitcherRole(role);
        }
    }
}
