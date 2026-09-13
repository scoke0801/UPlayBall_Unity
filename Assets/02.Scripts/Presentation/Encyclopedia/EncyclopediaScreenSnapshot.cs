using System;
using Baseball.Game.Historical;
using System.Collections.Generic;
using Baseball.Presentation.Owner;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Encyclopedia
{
    /// <summary>도감의 파생 조회 행이다. 저장 정본이나 카드 능력치 계산을 소유하지 않는다.</summary>
    public sealed class EncyclopediaScreenEntry
    {
        private Action<EncyclopediaScreenEntry> _detailResolver;
        private bool _isDetailResolved = true;
        private OwnerCollectionCardSnapshot _detailCard;
        private string _abilityInformation = string.Empty;
        private string _worldRecordInformation = string.Empty;
        private string _acquisitionInformation = string.Empty;
        private bool _hasScoutRoute;

        public string CardId { get; set; } = string.Empty;
        public string PlayerSeasonId { get; set; } = string.Empty;
        public string PlayerPersonId { get; set; } = string.Empty;
        public string FranchiseId { get; set; } = string.Empty;
        public string FranchiseDisplayName { get; set; } = string.Empty;
        /// <summary>구단 필터에서 사용하는 연도 없는 구단 계보명이다.</summary>
        public string FranchiseHistoryDisplayName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int OriginYear { get; set; }
        public int Cost { get; set; }
        public string Position { get; set; } = string.Empty;
        public string PitcherRole { get; set; } = string.Empty;
        public string Bats { get; set; } = string.Empty;
        public string Throws { get; set; } = string.Empty;
        public string EditionId { get; set; } = string.Empty;
        public string EditionDisplayName { get; set; } = string.Empty;
        public int EditionSortPriority { get; set; }
        public bool IsPitcher { get; set; }
        public bool IsCurrentlyOwned { get; set; }
        public bool WasEverAcquired { get; set; }
        public bool IsWishlisted { get; set; }
        public int OwnedCount { get; set; }
        public long AddedSequence { get; set; }
        public int CollectibleCardCount { get; set; }
        public int OwnedCardCount { get; set; }
        public int EverAcquiredCardCount { get; set; }
        public PlayerMiniCardModel MiniCard { get; set; }
        public OwnerCollectionCardSnapshot DetailCard { get { EnsureDetailResolved(); return _detailCard; } set => _detailCard = value; }
        public string CardInformation { get; set; } = string.Empty;
        public string AbilityInformation { get { EnsureDetailResolved(); return _abilityInformation; } set => _abilityInformation = value ?? string.Empty; }
        public string WorldRecordInformation { get { EnsureDetailResolved(); return _worldRecordInformation; } set => _worldRecordInformation = value ?? string.Empty; }
        public string AcquisitionInformation { get { EnsureDetailResolved(); return _acquisitionInformation; } set => _acquisitionInformation = value ?? string.Empty; }
        public bool HasScoutRoute { get { EnsureDetailResolved(); return _hasScoutRoute; } set => _hasScoutRoute = value; }
        public string StableId => string.IsNullOrEmpty(CardId) ? PlayerSeasonId : CardId;

        /// <summary>큰 능력치·기록·획득 경로는 선택된 행에서 한 번만 계산한다.</summary>
        public void SetDetailResolver(Action<EncyclopediaScreenEntry> resolver)
        {
            _detailResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _isDetailResolved = false;
        }

        private void EnsureDetailResolved()
        {
            if (_isDetailResolved) return;
            _isDetailResolved = true;
            Action<EncyclopediaScreenEntry> resolver = _detailResolver;
            _detailResolver = null;
            resolver?.Invoke(this);
        }
    }

    /// <summary>구단과 연도별 수집률의 분모·분자를 분리한다.</summary>
    public sealed class EncyclopediaProgressCell
    {
        public bool HasTeamSeason { get; set; } = true;
        public string FranchiseId { get; set; } = string.Empty;
        public string FranchiseDisplayName { get; set; } = string.Empty;
        public int OriginYear { get; set; }
        public int CollectibleCardCount { get; set; }
        public int EverAcquiredCardCount { get; set; }
        public int OwnedCardCount { get; set; }
        public int WishlistCardCount { get; set; }
        public int PlayerSeasonCount { get; set; }
        public int AcquiredPlayerSeasonCount { get; set; }
    }

    /// <summary>현재 카탈로그 메타데이터의 Edition별 수집 현황이다.</summary>
    public sealed class EncyclopediaEditionProgressSnapshot
    {
        public string EditionId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Total { get; set; }
        public int EverAcquired { get; set; }
        public int Owned { get; set; }
        public int Wishlist { get; set; }
        public int SortPriority { get; set; }
    }

    /// <summary>공용 도감 화면의 파생 조회 결과와 모드별 읽기 권한이다.</summary>
    public sealed class EncyclopediaScreenSnapshot
    {
        public bool IsReadOnly { get; set; }
        public IReadOnlyList<EncyclopediaScreenEntry> Cards { get; set; } = Array.Empty<EncyclopediaScreenEntry>();
        public IReadOnlyList<EncyclopediaScreenEntry> Seasons { get; set; } = Array.Empty<EncyclopediaScreenEntry>();
        public IReadOnlyList<EncyclopediaProgressCell> Progress { get; set; } = Array.Empty<EncyclopediaProgressCell>();
        public string EditionSummary { get; set; } = string.Empty;
        public IReadOnlyList<EncyclopediaEditionProgressSnapshot> Editions { get; set; } = Array.Empty<EncyclopediaEditionProgressSnapshot>();
    }

    /// <summary>모든 필터를 AND로 적용하며 검색은 월드 표시 이름만 사용한다.</summary>
    public sealed class EncyclopediaScreenFilter
    {
        public string Search { get; set; } = string.Empty;
        public string FranchiseId { get; set; } = string.Empty;
        public int OriginYear { get; set; }
        public int Decade { get; set; }
        public string Position { get; set; } = string.Empty;
        public string PitcherRole { get; set; } = string.Empty;
        public string EditionId { get; set; } = string.Empty;
        public string Bats { get; set; } = string.Empty;
        public string Throws { get; set; } = string.Empty;
        public int Cost { get; set; }
        public int PlayerType { get; set; }
        public int CollectionState { get; set; }
        public int Sort { get; set; }
        public string PlayerPersonId { get; set; } = string.Empty;
        public string PlayerSeasonId { get; set; } = string.Empty;

        /// <summary>읽기 전용 모드는 보유·위시 필터를 적용하지 않는다.</summary>
        public bool Matches(EncyclopediaScreenEntry entry, bool isReadOnly)
        {
            if (!string.IsNullOrWhiteSpace(Search) && entry.DisplayName.IndexOf(Search.Trim(), StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (FranchiseId.Length > 0 && DevelopmentRealIdentitySettings.GetFranchiseFilterKey(entry.FranchiseId) != FranchiseId) return false;
            if (OriginYear != 0 && entry.OriginYear != OriginYear) return false;
            if (Decade != 0 && entry.OriginYear / 10 * 10 != Decade) return false;
            if (Position.Length > 0 && entry.Position != Position) return false;
            if (PitcherRole.Length > 0 && entry.PitcherRole != PitcherRole) return false;
            if (EditionId.Length > 0 && entry.EditionId != EditionId) return false;
            if (Bats.Length > 0 && entry.Bats != Bats) return false;
            if (Throws.Length > 0 && entry.Throws != Throws) return false;
            if (Cost != 0 && entry.Cost != Cost) return false;
            if ((PlayerType == 1 && entry.IsPitcher) || (PlayerType == 2 && !entry.IsPitcher)) return false;
            if (PlayerPersonId.Length > 0 && entry.PlayerPersonId != PlayerPersonId) return false;
            if (PlayerSeasonId.Length > 0 && entry.PlayerSeasonId != PlayerSeasonId) return false;
            if (isReadOnly) return true;
            switch (CollectionState)
            {
                case 1: return entry.IsCurrentlyOwned;
                case 2: return entry.WasEverAcquired && !entry.IsCurrentlyOwned;
                case 3: return !entry.WasEverAcquired;
                case 4: return entry.IsWishlisted;
                case 5: return !entry.IsWishlisted;
                default: return true;
            }
        }

        /// <summary>정렬 동점은 Stable ID로 결정한다.</summary>
        public int Compare(EncyclopediaScreenEntry left, EncyclopediaScreenEntry right)
        {
            int result;
            switch (Sort)
            {
                case 1: result = string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal); break;
                case 2: result = left.OriginYear.CompareTo(right.OriginYear); break;
                case 3: result = right.OriginYear.CompareTo(left.OriginYear); break;
                case 4: result = left.Cost.CompareTo(right.Cost); break;
                case 5: result = right.Cost.CompareTo(left.Cost); break;
                case 6: result = string.Compare(left.Position, right.Position, StringComparison.Ordinal); break;
                case 7: result = left.EditionSortPriority.CompareTo(right.EditionSortPriority); break;
                case 8: result = right.IsCurrentlyOwned.CompareTo(left.IsCurrentlyOwned); break;
                case 9: result = left.WasEverAcquired.CompareTo(right.WasEverAcquired); break;
                case 10: result = right.IsWishlisted.CompareTo(left.IsWishlisted); break;
                case 11: result = right.AddedSequence.CompareTo(left.AddedSequence); break;
                case 12: result = left.AddedSequence.CompareTo(right.AddedSequence); break;
                default:
                    result = right.OriginYear.CompareTo(left.OriginYear);
                    if (result == 0) result = string.Compare(left.FranchiseDisplayName, right.FranchiseDisplayName, StringComparison.Ordinal);
                    if (result == 0) result = left.IsPitcher.CompareTo(right.IsPitcher);
                    if (result == 0) result = string.Compare(left.Position, right.Position, StringComparison.Ordinal);
                    if (result == 0) result = right.Cost.CompareTo(left.Cost);
                    if (result == 0) result = string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
                    if (result == 0) result = left.EditionSortPriority.CompareTo(right.EditionSortPriority);
                    break;
            }
            return result != 0 ? result : string.Compare(left.StableId, right.StableId, StringComparison.Ordinal);
        }
    }
}
