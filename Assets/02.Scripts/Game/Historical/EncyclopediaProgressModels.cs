using System;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>Franchise×OriginYear 셀의 존재 여부와 수집 분모·분자를 보관한다.</summary>
    public sealed class FranchiseYearCollectionProgress
    {
        internal FranchiseYearCollectionProgress(
            string franchiseId,
            string franchiseDisplayName,
            int originYear,
            bool hasTeamSeason,
            int playerSeasonCount,
            int acquiredPlayerSeasonCount,
            int collectibleCardCount,
            int everAcquiredCardCount,
            int ownedCardCount,
            int wishlistCardCount)
        {
            FranchiseId = franchiseId ?? throw new ArgumentNullException(nameof(franchiseId));
            FranchiseDisplayName = franchiseDisplayName ?? throw new ArgumentNullException(nameof(franchiseDisplayName));
            OriginYear = originYear;
            HasTeamSeason = hasTeamSeason;
            PlayerSeasonCount = playerSeasonCount;
            AcquiredPlayerSeasonCount = acquiredPlayerSeasonCount;
            CollectibleCardCount = collectibleCardCount;
            EverAcquiredCardCount = everAcquiredCardCount;
            OwnedCardCount = ownedCardCount;
            WishlistCardCount = wishlistCardCount;
        }

        public string FranchiseId { get; }
        public string FranchiseDisplayName { get; }
        public int OriginYear { get; }
        public bool HasTeamSeason { get; }
        public int PlayerSeasonCount { get; }
        public int AcquiredPlayerSeasonCount { get; }
        public int CollectibleCardCount { get; }
        public int EverAcquiredCardCount { get; }
        public int OwnedCardCount { get; }
        public int WishlistCardCount { get; }
    }

    /// <summary>Catalog에 실제 존재하는 Edition별 수집 수치다.</summary>
    public sealed class EditionCollectionProgress
    {
        internal EditionCollectionProgress(
            PlayerCardEdition edition,
            int collectibleCardCount,
            int everAcquiredCardCount,
            int ownedCardCount,
            int wishlistCardCount)
        {
            Edition = edition;
            CollectibleCardCount = collectibleCardCount;
            EverAcquiredCardCount = everAcquiredCardCount;
            OwnedCardCount = ownedCardCount;
            WishlistCardCount = wishlistCardCount;
        }

        public PlayerCardEdition Edition { get; }
        public int CollectibleCardCount { get; }
        public int EverAcquiredCardCount { get; }
        public int OwnedCardCount { get; }
        public int WishlistCardCount { get; }
    }

    /// <summary>전체 Catalog 수집률과 Matrix·Edition 집계를 한 번에 제공하는 파생 스냅샷이다.</summary>
    public sealed class EncyclopediaCollectionProgress
    {
        internal EncyclopediaCollectionProgress(
            int collectiblePlayerSeasonCount,
            int acquiredPlayerSeasonCount,
            int collectibleCardCount,
            int everAcquiredCardCount,
            int ownedCardCount,
            int wishlistCardCount,
            FranchiseYearCollectionProgress[] franchiseYears,
            EditionCollectionProgress[] editions)
        {
            CollectiblePlayerSeasonCount = collectiblePlayerSeasonCount;
            AcquiredPlayerSeasonCount = acquiredPlayerSeasonCount;
            CollectibleCardCount = collectibleCardCount;
            EverAcquiredCardCount = everAcquiredCardCount;
            OwnedCardCount = ownedCardCount;
            WishlistCardCount = wishlistCardCount;
            FranchiseYears = franchiseYears ?? throw new ArgumentNullException(nameof(franchiseYears));
            Editions = editions ?? throw new ArgumentNullException(nameof(editions));
        }

        public int CollectiblePlayerSeasonCount { get; }
        public int AcquiredPlayerSeasonCount { get; }
        public int CollectibleCardCount { get; }
        public int EverAcquiredCardCount { get; }
        public int OwnedCardCount { get; }
        public int WishlistCardCount { get; }
        public FranchiseYearCollectionProgress[] FranchiseYears { get; }
        public EditionCollectionProgress[] Editions { get; }
    }
}
