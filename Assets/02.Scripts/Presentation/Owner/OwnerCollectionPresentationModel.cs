using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단주 보유 선수 목록의 정렬 기준이다.</summary>
    public enum OwnerCollectionSort
    {
        Name,
        Position,
        Cost,
        Edition
    }

    /// <summary>OwnedCards와 WorldCardCatalog에서 읽은 카드 한 장의 불변 표시 Snapshot이다.</summary>
    public sealed class OwnerCollectionCardSnapshot
    {
        public OwnerCollectionCardSnapshot(
            string cardId,
            string playerPersonId,
            string displayName,
            int originYear,
            PlayerPosition position,
            int cost,
            PlayerCardEdition edition,
            int enhancementLevel,
            int duplicateCount,
            bool isLocked,
            bool isFavorite)
        {
            CardId = RequireText(cardId, nameof(cardId));
            PlayerPersonId = RequireText(playerPersonId, nameof(playerPersonId));
            DisplayName = RequireText(displayName, nameof(displayName));
            OriginYear = originYear;
            Position = position;
            Cost = cost;
            Edition = edition;
            EnhancementLevel = enhancementLevel;
            DuplicateCount = duplicateCount;
            IsLocked = isLocked;
            IsFavorite = isFavorite;
        }

        public string CardId { get; }
        public string PlayerPersonId { get; }
        public string DisplayName { get; }
        public int OriginYear { get; }
        public PlayerPosition Position { get; }
        public int Cost { get; }
        public PlayerCardEdition Edition { get; }
        public int EnhancementLevel { get; }
        public int DuplicateCount { get; }
        public bool IsLocked { get; }
        public bool IsFavorite { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("표시 식별자와 이름은 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>현재 Save의 보유 카드만 보관하는 Collection 화면 Snapshot이다.</summary>
    public sealed class OwnerCollectionSnapshot
    {
        private readonly OwnerCollectionCardSnapshot[] _cards;

        public OwnerCollectionSnapshot(IReadOnlyList<OwnerCollectionCardSnapshot> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            _cards = new OwnerCollectionCardSnapshot[cards.Count];
            for (int index = 0; index < cards.Count; index++)
                _cards[index] = cards[index] ??
                    throw new ArgumentException("null 카드 Snapshot이 있습니다.", nameof(cards));
        }

        public IReadOnlyList<OwnerCollectionCardSnapshot> Cards => _cards;
    }

    /// <summary>공용 Mini Card와 Inspector 원본을 함께 전달하는 보유 카드 표시 모델이다.</summary>
    public sealed class OwnerCollectionCardModel
    {
        internal OwnerCollectionCardModel(
            OwnerCollectionCardSnapshot snapshot,
            PlayerMiniCardModel miniCard)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            MiniCard = miniCard ?? throw new ArgumentNullException(nameof(miniCard));
        }

        public OwnerCollectionCardSnapshot Snapshot { get; }
        public PlayerMiniCardModel MiniCard { get; }
    }

    /// <summary>검색·정렬 결과와 전체 카드 수를 Collection View에 제공한다.</summary>
    public sealed class OwnerCollectionPresentationModel
    {
        private readonly OwnerCollectionCardModel[] _cards;

        internal OwnerCollectionPresentationModel(
            int totalCount,
            string query,
            OwnerCollectionSort sort,
            IReadOnlyList<OwnerCollectionCardModel> cards)
        {
            TotalCount = totalCount;
            Query = query ?? string.Empty;
            Sort = sort;
            _cards = new OwnerCollectionCardModel[cards.Count];
            for (int index = 0; index < cards.Count; index++)
                _cards[index] = cards[index];
        }

        public int TotalCount { get; }
        public string Query { get; }
        public OwnerCollectionSort Sort { get; }
        public IReadOnlyList<OwnerCollectionCardModel> Cards => _cards;
        public string CountText => string.IsNullOrWhiteSpace(Query)
            ? $"보유 카드 {TotalCount}장"
            : $"검색 결과 {_cards.Length}/{TotalCount}장";
    }

    /// <summary>보유 카드 Snapshot을 공용 Mini Card 기반 검색·정렬 모델로 변환한다.</summary>
    public static class OwnerCollectionPresentationBuilder
    {
        public static OwnerCollectionPresentationModel Build(
            OwnerCollectionSnapshot snapshot,
            string query = null,
            OwnerCollectionSort sort = OwnerCollectionSort.Name)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string normalizedQuery = query?.Trim() ?? string.Empty;
            var filtered = new List<OwnerCollectionCardSnapshot>(snapshot.Cards.Count);
            for (int index = 0; index < snapshot.Cards.Count; index++)
            {
                OwnerCollectionCardSnapshot card = snapshot.Cards[index];
                if (Matches(card, normalizedQuery))
                    filtered.Add(card);
            }

            filtered.Sort((left, right) => Compare(left, right, sort));
            var cards = new OwnerCollectionCardModel[filtered.Count];
            for (int index = 0; index < cards.Length; index++)
                cards[index] = new OwnerCollectionCardModel(filtered[index], CreateMiniCard(filtered[index], false));
            return new OwnerCollectionPresentationModel(snapshot.Cards.Count, normalizedQuery, sort, cards);
        }

        public static PlayerMiniCardModel CreateMiniCard(OwnerCollectionCardSnapshot card, bool isSelected)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            string status = CreateStatus(card);
            PlayerMiniCardVisualState state = isSelected
                ? PlayerMiniCardVisualState.Selected
                : card.IsFavorite ? PlayerMiniCardVisualState.Highlighted : PlayerMiniCardVisualState.Normal;
            return new PlayerMiniCardModel(
                card.CardId,
                card.DisplayName,
                FormatPosition(card.Position),
                card.OriginYear.ToString(),
                $"Cost {card.Cost}",
                FormatEdition(card.Edition),
                status,
                card.PlayerPersonId,
                visualState: state);
        }

        public static string FormatPosition(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "포수 (C)",
                PlayerPosition.FirstBase => "1루수 (1B)",
                PlayerPosition.SecondBase => "2루수 (2B)",
                PlayerPosition.ThirdBase => "3루수 (3B)",
                PlayerPosition.Shortstop => "유격수 (SS)",
                PlayerPosition.LeftField => "좌익수 (LF)",
                PlayerPosition.CenterField => "중견수 (CF)",
                PlayerPosition.RightField => "우익수 (RF)",
                PlayerPosition.DesignatedHitter => "지명타자 (DH)",
                PlayerPosition.StartingPitcher => "선발투수 (SP)",
                PlayerPosition.ReliefPitcher => "구원투수 (RP)",
                _ => "포지션 미확인"
            };
        }

        public static string FormatEdition(PlayerCardEdition edition)
        {
            return edition switch
            {
                PlayerCardEdition.Normal => "일반",
                PlayerCardEdition.AllStar => "올스타",
                PlayerCardEdition.GoldenGlove => "골든글러브",
                PlayerCardEdition.Mvp => "MVP",
                _ => "Edition 미확인"
            };
        }

        private static bool Matches(OwnerCollectionCardSnapshot card, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            return Contains(card.DisplayName, query) ||
                   Contains(FormatPosition(card.Position), query) ||
                   Contains(FormatEdition(card.Edition), query) ||
                   Contains(card.OriginYear.ToString(), query) ||
                   Contains(card.Cost.ToString(), query);
        }

        private static int Compare(
            OwnerCollectionCardSnapshot left,
            OwnerCollectionCardSnapshot right,
            OwnerCollectionSort sort)
        {
            int comparison = sort switch
            {
                OwnerCollectionSort.Position => left.Position.CompareTo(right.Position),
                OwnerCollectionSort.Cost => right.Cost.CompareTo(left.Cost),
                OwnerCollectionSort.Edition => right.Edition.CompareTo(left.Edition),
                _ => string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCulture)
            };
            if (comparison != 0) return comparison;
            return string.Compare(left.CardId, right.CardId, StringComparison.Ordinal);
        }

        private static bool Contains(string value, string query) =>
            value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;

        private static string CreateStatus(OwnerCollectionCardSnapshot card)
        {
            var parts = new List<string>(4);
            if (card.IsFavorite) parts.Add("즐겨찾기");
            if (card.IsLocked) parts.Add("잠금");
            if (card.EnhancementLevel > 0) parts.Add($"+{card.EnhancementLevel}");
            if (card.DuplicateCount > 0) parts.Add($"중복 {card.DuplicateCount}");
            return string.Join(" · ", parts);
        }
    }
}
