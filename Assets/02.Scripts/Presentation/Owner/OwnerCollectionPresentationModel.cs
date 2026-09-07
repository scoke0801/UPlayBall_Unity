using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Growth;
using Baseball.Core.Teams;
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

    /// <summary>카드 뒷면에 표시할 구종의 안정 프로필이다.</summary>
    public readonly struct OwnerPitchCardSnapshot
    {
        public OwnerPitchCardSnapshot(PitchType pitchType, string displayName, string grade, double velocityKph)
        {
            PitchType = pitchType;
            DisplayName = displayName ?? string.Empty;
            Grade = grade ?? string.Empty;
            VelocityKph = velocityKph;
        }

        public PitchType PitchType { get; }
        public string DisplayName { get; }
        public string Grade { get; }
        public double VelocityKph { get; }
    }

    /// <summary>WorldHistory의 실제 집계 필드 하나를 카드용으로 복사한다.</summary>
    public readonly struct OwnerCardRecordFieldSnapshot
    {
        public OwnerCardRecordFieldSnapshot(string label, string value)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Label { get; }
        public string Value { get; }
    }

    /// <summary>카드 능력치 하나의 원본과 영구·현재 적용 보너스를 출처별로 보관한다.</summary>
    public readonly struct OwnerAbilityBreakdownSnapshot
    {
        public OwnerAbilityBreakdownSnapshot(
            int baseCard,
            int training,
            int skillBlock,
            int teamColor,
            int study,
            int enhancement)
        {
            if (baseCard < 0 || training < 0 || skillBlock < 0 || teamColor < 0 || study < 0 || enhancement < 0)
                throw new ArgumentOutOfRangeException(nameof(baseCard));
            BaseCard = baseCard;
            Training = training;
            SkillBlock = skillBlock;
            TeamColor = teamColor;
            Study = study;
            Enhancement = enhancement;
        }

        public int BaseCard { get; }
        public int Training { get; }
        public int SkillBlock { get; }
        public int TeamColor { get; }
        public int Study { get; }
        public int Enhancement { get; }
        public int GrowthTotal => checked(Training + SkillBlock + TeamColor + Study + Enhancement);
        public int Total => checked(BaseCard + GrowthTotal);
    }

    /// <summary>카드 뒷면 4×4 보드에 표시할 스킬 블록의 모양과 배치다.</summary>
    public sealed class OwnerSkillBlockPlacementSnapshot
    {
        private readonly BoardCell[] _shapeCells;

        public OwnerSkillBlockPlacementSnapshot(
            IReadOnlyList<BoardCell> shapeCells,
            int originX,
            int originY,
            int rotationQuarterTurns,
            SkillBlockRarity rarity = SkillBlockRarity.Normal)
        {
            if (shapeCells == null || shapeCells.Count == 0)
                throw new ArgumentException("스킬 블록 모양이 필요합니다.", nameof(shapeCells));
            if (rotationQuarterTurns < 0 || rotationQuarterTurns > 3)
                throw new ArgumentOutOfRangeException(nameof(rotationQuarterTurns));
            _shapeCells = new BoardCell[shapeCells.Count];
            for (int index = 0; index < _shapeCells.Length; index++) _shapeCells[index] = shapeCells[index];
            OriginX = originX;
            OriginY = originY;
            RotationQuarterTurns = rotationQuarterTurns;
            Rarity = rarity;
        }

        public int OriginX { get; }
        public int OriginY { get; }
        public int RotationQuarterTurns { get; }
        public SkillBlockRarity Rarity { get; }
        public BoardCell[] CreateShapeCells() => (BoardCell[])_shapeCells.Clone();
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
            bool isFavorite,
            AbilityRatings abilities = null,
            string currentLeagueLabel = null,
            string playerSeasonId = null,
            PitcherRole? pitcherRole = null,
            Handedness? throws = null,
            Handedness? bats = null,
            IReadOnlyList<OwnerPitchCardSnapshot> pitches = null,
            IReadOnlyList<OwnerCardRecordFieldSnapshot> seasonRecord = null,
            int trainingBonusTotal = 0,
            int placedSkillBlockCount = 0,
            int availableSkillBlockCount = 0,
            bool isActiveRoster = false,
            string studyStatus = "",
            string teamDisplayName = "",
            IReadOnlyList<OwnerSkillBlockPlacementSnapshot> skillBlockPlacements = null,
            int? condition = null,
            string conditionLabel = "",
            IReadOnlyList<OwnerAbilityBreakdownSnapshot> abilityBreakdowns = null,
            int abilityGraphMaximum = AbilityRatings.Maximum,
            bool isOwnedCard = true)
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
            _abilities = abilities?.Clone();
            CurrentLeagueLabel = currentLeagueLabel ?? "현재 리그";
            PlayerSeasonId = playerSeasonId ?? string.Empty;
            PitcherRole = pitcherRole;
            Throws = throws;
            Bats = bats;
            _pitches = Copy(pitches);
            _seasonRecord = Copy(seasonRecord);
            TrainingBonusTotal = trainingBonusTotal;
            PlacedSkillBlockCount = placedSkillBlockCount;
            AvailableSkillBlockCount = availableSkillBlockCount;
            IsActiveRoster = isActiveRoster;
            StudyStatus = studyStatus ?? string.Empty;
            TeamDisplayName = teamDisplayName ?? string.Empty;
            _skillBlockPlacements = Copy(skillBlockPlacements);
            Condition = condition;
            ConditionLabel = conditionLabel ?? string.Empty;
            if (abilityGraphMaximum < AbilityRatings.Maximum)
                throw new ArgumentOutOfRangeException(nameof(abilityGraphMaximum));
            AbilityGraphMaximum = abilityGraphMaximum;
            IsOwnedCard = isOwnedCard;
            if (abilityBreakdowns != null && abilityBreakdowns.Count != PlayerAbilityCatalog.AbilityCount)
                throw new ArgumentException("모든 능력치의 성장 출처가 필요합니다.", nameof(abilityBreakdowns));
            _abilityBreakdowns = abilityBreakdowns == null ? null : Copy(abilityBreakdowns);
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
        public string CurrentLeagueLabel { get; }
        public string PlayerSeasonId { get; }
        public PitcherRole? PitcherRole { get; }
        public Handedness? Throws { get; }
        public Handedness? Bats { get; }
        public IReadOnlyList<OwnerPitchCardSnapshot> Pitches => _pitches;
        public IReadOnlyList<OwnerCardRecordFieldSnapshot> SeasonRecord => _seasonRecord;
        public int TrainingBonusTotal { get; }
        public int PlacedSkillBlockCount { get; }
        public int AvailableSkillBlockCount { get; }
        public bool IsActiveRoster { get; }
        public string StudyStatus { get; }
        public string TeamDisplayName { get; }
        public int? Condition { get; }
        public string ConditionLabel { get; }
        public int AbilityGraphMaximum { get; }
        public bool IsOwnedCard { get; }
        public IReadOnlyList<OwnerSkillBlockPlacementSnapshot> SkillBlockPlacements => _skillBlockPlacements;
        private readonly AbilityRatings _abilities;
        private readonly OwnerPitchCardSnapshot[] _pitches;
        private readonly OwnerCardRecordFieldSnapshot[] _seasonRecord;
        private readonly OwnerSkillBlockPlacementSnapshot[] _skillBlockPlacements;
        private readonly OwnerAbilityBreakdownSnapshot[] _abilityBreakdowns;
        public int? GetAbility(PlayerAbility ability) => _abilities?.Get(ability);
        public int? GetEffectiveAbility(PlayerAbility ability) => _abilityBreakdowns == null
            ? _abilities?.Get(ability)
            : Math.Min(AbilityGraphMaximum, _abilityBreakdowns[(int)ability].Total);
        public OwnerAbilityBreakdownSnapshot? GetAbilityBreakdown(PlayerAbility ability) =>
            _abilityBreakdowns == null ? null : _abilityBreakdowns[(int)ability];

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<T>();
            var result = new T[source.Count];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }

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
                FormatPlayerRole(card.Position, card.PitcherRole),
                card.OriginYear.ToString(),
                $"비용 {card.Cost}",
                FormatEdition(card.Edition),
                status,
                card.PlayerPersonId,
                visualState: state, frameEdition: card.Edition, cost: card.Cost);
        }

        public static string FormatPosition(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Catcher => "포수",
                PlayerPosition.FirstBase => "1루수",
                PlayerPosition.SecondBase => "2루수",
                PlayerPosition.ThirdBase => "3루수",
                PlayerPosition.Shortstop => "유격수",
                PlayerPosition.LeftField => "좌익수",
                PlayerPosition.CenterField => "중견수",
                PlayerPosition.RightField => "우익수",
                PlayerPosition.DesignatedHitter => "지명타자",
                PlayerPosition.StartingPitcher => "선발투수",
                PlayerPosition.ReliefPitcher => "구원투수",
                _ => "포지션 미확인"
            };
        }

        /// <summary>투수는 시즌의 Natural Role을, 야수는 주 포지션을 카드 표기로 반환한다.</summary>
        public static string FormatPlayerRole(PlayerPosition position, PitcherRole? pitcherRole)
        {
            bool isPitcher = position == PlayerPosition.StartingPitcher ||
                             position == PlayerPosition.ReliefPitcher;
            return isPitcher && pitcherRole.HasValue
                ? FormatPitcherRole(pitcherRole.Value)
                : FormatPosition(position);
        }

        /// <summary>투수 시즌의 Natural Role을 플레이어용 한국어 표기로 반환한다.</summary>
        public static string FormatPitcherRole(PitcherRole role)
        {
            return role switch
            {
                PitcherRole.Starter => "선발",
                PitcherRole.Swingman => "스윙맨",
                PitcherRole.LongRelief => "롱릴리프",
                PitcherRole.MiddleRelief => "중간계투",
                PitcherRole.Setup => "셋업",
                PitcherRole.Closer => "마무리",
                _ => "역할 미정"
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
                _ => "카드 종류 미확인"
            };
        }

        private static bool Matches(OwnerCollectionCardSnapshot card, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            return Contains(card.DisplayName, query) ||
                   Contains(FormatPlayerRole(card.Position, card.PitcherRole), query) ||
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
