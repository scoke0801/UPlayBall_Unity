using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// 공용 Mini Card의 선택, 강조, 경고 상태를 구분한다.
    /// </summary>
    public enum PlayerMiniCardVisualState
    {
        Normal = 0,
        Highlighted = 1,
        Selected = 2,
        Warning = 3,
        Disabled = 4
    }

    /// <summary>작은 선수 카드의 능력 막대 한 줄에 필요한 형식화된 표시 값이다.</summary>
    public readonly struct PlayerMiniCardStatModel
    {
        public PlayerMiniCardStatModel(string label, int value, int maximum)
        {
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            Label = label ?? string.Empty;
            Value = Math.Max(0, Math.Min(maximum, value));
            Maximum = maximum;
        }

        public string Label { get; }
        public int Value { get; }
        public int Maximum { get; }
        public float NormalizedValue => Value / (float)Maximum;
    }

    /// <summary>
    /// Owner 소유 상태나 Career State를 직접 참조하지 않는 공용 선수 Mini Card 표시 모델이다.
    /// </summary>
    public sealed class PlayerMiniCardModel
    {
        /// <summary>
        /// 선수 식별 정보와 카드 표면에 표시할 형식화된 값을 만든다.
        /// </summary>
        public PlayerMiniCardModel(
            string playerId,
            string displayName,
            string positionLabel,
            string yearLabel,
            string costLabel,
            string editionLabel,
            string statusLabel = null,
            string portraitAssetKey = null,
            string teamAccentHex = null,
            PlayerMiniCardVisualState visualState = PlayerMiniCardVisualState.Normal,
            bool isInteractable = true,
            IReadOnlyList<PlayerMiniCardStatModel> stats = null,
            PlayerCardEdition? frameEdition = null,
            int? cost = null,
            int? conditionLevel = null,
            PlayerCardGrowthBadgeModel growthBadges = null,
            string nameBandPositionLabel = null,
            int enhancementLevel = 0)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("선수 식별자는 비어 있을 수 없습니다.", nameof(playerId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("선수 표시 이름은 비어 있을 수 없습니다.", nameof(displayName));

            PlayerId = playerId;
            DisplayName = displayName;
            PositionLabel = positionLabel ?? string.Empty;
            NameBandPositionLabel = nameBandPositionLabel ?? PositionLabel;
            YearLabel = yearLabel ?? string.Empty;
            CostLabel = costLabel ?? string.Empty;
            EditionLabel = editionLabel ?? string.Empty;
            StatusLabel = statusLabel ?? string.Empty;
            PortraitAssetKey = portraitAssetKey ?? string.Empty;
            TeamAccentHex = teamAccentHex ?? string.Empty;
            VisualState = visualState;
            IsInteractable = isInteractable && visualState != PlayerMiniCardVisualState.Disabled;
            Stats = CopyStats(stats);
            FrameEdition = frameEdition;
            Cost = cost;
            EnhancementLevel = Math.Max(0, enhancementLevel);
            ConditionLevel = conditionLevel;
            GrowthBadges = growthBadges ?? PlayerCardGrowthBadgeModel.Empty;
        }

        /// <summary>
        /// 화면 갱신 뒤에도 선수를 식별하는 안정적인 ID다.
        /// </summary>
        public string PlayerId { get; }

        /// <summary>
        /// 선수 이름 표시다.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 야구 표준 Position 코드 표시다.
        /// </summary>
        public string PositionLabel { get; }

        /// <summary>상단 편성 슬롯명과 독립적으로 이름 띠에 표시할 선수 포지션이다.</summary>
        public string NameBandPositionLabel { get; }

        /// <summary>
        /// Origin Year 또는 카드 연도 표시다.
        /// </summary>
        public string YearLabel { get; }

        /// <summary>
        /// 이미 형식화된 Cost 표시다.
        /// </summary>
        public string CostLabel { get; }

        /// <summary>
        /// Edition 표시다.
        /// </summary>
        public string EditionLabel { get; }

        /// <summary>구단주 카드 프레임의 정본 등급이다. null은 기존 공용 카드 표현을 유지한다.</summary>
        public PlayerCardEdition? FrameEdition { get; }

        /// <summary>별 개수를 결정하는 실제 비용이다. 표시 문자열에서 역산하지 않는다.</summary>
        public int? Cost { get; }

        /// <summary>실제 카드 강화 단계이며 0은 배지를 숨긴다.</summary>
        public int EnhancementLevel { get; }

        /// <summary>정본 컨디션 테이블의 1~10 단계다. 비공개·미확인은 null이다.</summary>
        public int? ConditionLevel { get; }

        /// <summary>모드에서 공급하는 유학·특성훈련 배지다.</summary>
        public PlayerCardGrowthBadgeModel GrowthBadges { get; }

        /// <summary>
        /// Condition, 감독 결정, 경고처럼 모드 Presenter가 공급한 보조 상태다.
        /// </summary>
        public string StatusLabel { get; }

        /// <summary>
        /// 모드별 Asset Resolver가 초상화를 찾을 때 사용할 키다.
        /// </summary>
        public string PortraitAssetKey { get; }

        /// <summary>
        /// 카드 전체가 아니라 얇은 강조선에만 사용할 팀 Accent HTML 색상이다.
        /// </summary>
        public string TeamAccentHex { get; }

        /// <summary>
        /// 선택, 내 선수 강조, 경고를 나타내는 시각 상태다.
        /// </summary>
        public PlayerMiniCardVisualState VisualState { get; }

        /// <summary>
        /// 상세 보기 선택 입력을 받을 수 있는지 나타낸다.
        /// </summary>
        public bool IsInteractable { get; }

        /// <summary>레퍼런스형 선수 카드에 표시할 최대 여섯 개의 능력 막대다.</summary>
        public IReadOnlyList<PlayerMiniCardStatModel> Stats { get; }

        private static PlayerMiniCardStatModel[] CopyStats(IReadOnlyList<PlayerMiniCardStatModel> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<PlayerMiniCardStatModel>();
            int count = Math.Min(6, source.Count);
            var result = new PlayerMiniCardStatModel[count];
            for (int index = 0; index < count; index++) result[index] = source[index];
            return result;
        }
    }
}
