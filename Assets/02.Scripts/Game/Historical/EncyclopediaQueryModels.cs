using System;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Historical
{
    /// <summary>선수 도감에서 현재 보유와 영구 획득 이력을 구분해 검색하는 상태다.</summary>
    public enum EncyclopediaCollectionFilter
    {
        All,
        CurrentlyOwned,
        AcquiredButNotOwned,
        NeverAcquired,
        Wishlisted,
        NotWishlisted
    }

    /// <summary>선수 시즌과 카드 조회에 공통으로 적용하는 결정론적 정렬 기준이다.</summary>
    public enum EncyclopediaSort
    {
        Default,
        DisplayName,
        OriginYearAscending,
        OriginYearDescending,
        CostAscending,
        CostDescending,
        Position,
        Edition,
        CurrentlyOwnedFirst,
        NeverAcquiredFirst,
        WishlistedFirst,
        WishlistNewest,
        WishlistOldest
    }

    /// <summary>전체 Archive 인덱스에 AND로 적용하는 Strongly Typed 도감 필터다.</summary>
    public sealed class EncyclopediaFilter
    {
        public string SearchText { get; set; } = string.Empty;
        public string FranchiseId { get; set; } = string.Empty;
        public string PlayerPersonId { get; set; } = string.Empty;
        public string PlayerSeasonId { get; set; } = string.Empty;
        public int OriginYear { get; set; }
        public int DecadeStartYear { get; set; }
        public PlayerType? PlayerType { get; set; }
        public PlayerPosition? Position { get; set; }
        public PitcherRole? PitcherRole { get; set; }
        public int MinimumCost { get; set; }
        public int MaximumCost { get; set; }
        public PlayerCardEdition? Edition { get; set; }
        public EncyclopediaCollectionFilter Collection { get; set; }
        public RegistrationType? RegistrationType { get; set; }
        public Handedness? Bats { get; set; }
        public Handedness? Throws { get; set; }
    }

    /// <summary>한 WorldCardCatalog CardId에 Canonical·Identity·수집 상태를 합성한 조회 결과다.</summary>
    public sealed class EncyclopediaCardEntry
    {
        internal EncyclopediaCardEntry(
            PlayerCardDefinition card,
            PlayerSeasonDefinition season,
            PlayerPersonDefinition person,
            string displayName,
            string franchiseDisplayName,
            bool isCurrentlyOwned,
            int ownedCount,
            bool wasEverAcquired,
            bool isWishlisted,
            long? wishlistAddedSequence)
        {
            Card = card ?? throw new ArgumentNullException(nameof(card));
            Season = season ?? throw new ArgumentNullException(nameof(season));
            Person = person ?? throw new ArgumentNullException(nameof(person));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            FranchiseDisplayName = franchiseDisplayName ?? throw new ArgumentNullException(nameof(franchiseDisplayName));
            IsCurrentlyOwned = isCurrentlyOwned;
            OwnedCount = ownedCount;
            WasEverAcquired = wasEverAcquired;
            IsWishlisted = isWishlisted;
            WishlistAddedSequence = wishlistAddedSequence;
        }

        public PlayerCardDefinition Card { get; }
        public PlayerSeasonDefinition Season { get; }
        public PlayerPersonDefinition Person { get; }
        public string CardId => Card.CardId;
        public string PlayerSeasonId => Season.PlayerSeasonId;
        public string PlayerPersonId => Season.PlayerPersonId;
        public string DisplayName { get; }
        public string OriginFranchiseId => Season.OriginFranchiseId;
        public string OriginTeamSeasonKey => Season.OriginTeamSeasonKey;
        public string FranchiseDisplayName { get; }
        public int OriginYear => Season.OriginYear;
        public PlayerType PlayerType => Season.PlayerType;
        public PlayerPosition Position => Season.Position;
        public PitcherRole PitcherRole => Season.PitcherRole;
        public RegistrationType RegistrationType => Season.RegistrationType;
        public Handedness Bats => Person.Bats;
        public Handedness Throws => Person.Throws;
        public int Cost => Season.Cost;
        public PlayerCardEdition Edition => Card.Edition;
        public bool IsCurrentlyOwned { get; }
        public int OwnedCount { get; }
        public bool WasEverAcquired { get; }
        public bool IsWishlisted { get; }
        public long? WishlistAddedSequence { get; }
    }

    /// <summary>한 Canonical PlayerSeason에 현재 발급된 Edition 수집 상태를 집계한 조회 결과다.</summary>
    public sealed class EncyclopediaPlayerSeasonEntry
    {
        internal EncyclopediaPlayerSeasonEntry(
            PlayerSeasonDefinition season,
            PlayerPersonDefinition person,
            string displayName,
            string franchiseDisplayName,
            int collectibleCardCount,
            int ownedCardCount,
            int everAcquiredCardCount,
            int wishlistCardCount,
            long? newestWishlistSequence)
        {
            Season = season ?? throw new ArgumentNullException(nameof(season));
            Person = person ?? throw new ArgumentNullException(nameof(person));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            FranchiseDisplayName = franchiseDisplayName ?? throw new ArgumentNullException(nameof(franchiseDisplayName));
            CollectibleCardCount = collectibleCardCount;
            OwnedCardCount = ownedCardCount;
            EverAcquiredCardCount = everAcquiredCardCount;
            WishlistCardCount = wishlistCardCount;
            NewestWishlistSequence = newestWishlistSequence;
        }

        public PlayerSeasonDefinition Season { get; }
        public PlayerPersonDefinition Person { get; }
        public string PlayerSeasonId => Season.PlayerSeasonId;
        public string PlayerPersonId => Season.PlayerPersonId;
        public string DisplayName { get; }
        public string OriginFranchiseId => Season.OriginFranchiseId;
        public string OriginTeamSeasonKey => Season.OriginTeamSeasonKey;
        public string FranchiseDisplayName { get; }
        public int OriginYear => Season.OriginYear;
        public PlayerType PlayerType => Season.PlayerType;
        public PlayerPosition Position => Season.Position;
        public PitcherRole PitcherRole => Season.PitcherRole;
        public RegistrationType RegistrationType => Season.RegistrationType;
        public Handedness Bats => Person.Bats;
        public Handedness Throws => Person.Throws;
        public int Cost => Season.Cost;
        public int CollectibleCardCount { get; }
        public int OwnedCardCount { get; }
        public int EverAcquiredCardCount { get; }
        public int WishlistCardCount { get; }
        public long? NewestWishlistSequence { get; }
        public bool IsCurrentlyOwned => OwnedCardCount > 0;
        public bool WasEverAcquired => EverAcquiredCardCount > 0;
        public bool IsWishlisted => WishlistCardCount > 0;
    }

    /// <summary>같은 PlayerPerson의 다른 연도를 Origin 기준으로 연결하는 타임라인 항목이다.</summary>
    public sealed class EncyclopediaPlayerTimelineEntry
    {
        internal EncyclopediaPlayerTimelineEntry(EncyclopediaPlayerSeasonEntry season)
        {
            Season = season ?? throw new ArgumentNullException(nameof(season));
        }

        public EncyclopediaPlayerSeasonEntry Season { get; }
        public string PlayerSeasonId => Season.PlayerSeasonId;
        public int OriginYear => Season.OriginYear;
        public string OriginFranchiseId => Season.OriginFranchiseId;
        public string FranchiseDisplayName => Season.FranchiseDisplayName;
    }

    /// <summary>카드 상세에서 기존 Canonical 능력치와 World 기록을 재계산 없이 참조한다.</summary>
    public sealed class EncyclopediaCardDetail
    {
        internal EncyclopediaCardDetail(EncyclopediaCardEntry entry, SeasonStatistics worldStatistics)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            WorldStatistics = worldStatistics;
        }

        public EncyclopediaCardEntry Entry { get; }
        public SeasonStatistics WorldStatistics { get; }
        public bool HasWorldStatistics => WorldStatistics != null;
    }

    /// <summary>선수 시즌 상세에 현재 발급 Edition과 다년도 타임라인을 묶는다.</summary>
    public sealed class EncyclopediaPlayerSeasonDetail
    {
        internal EncyclopediaPlayerSeasonDetail(
            EncyclopediaPlayerSeasonEntry season,
            EncyclopediaCardEntry[] cards,
            EncyclopediaPlayerTimelineEntry[] timeline)
        {
            Season = season ?? throw new ArgumentNullException(nameof(season));
            Cards = cards ?? throw new ArgumentNullException(nameof(cards));
            Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        }

        public EncyclopediaPlayerSeasonEntry Season { get; }
        public EncyclopediaCardEntry[] Cards { get; }
        public EncyclopediaPlayerTimelineEntry[] Timeline { get; }
    }
}
