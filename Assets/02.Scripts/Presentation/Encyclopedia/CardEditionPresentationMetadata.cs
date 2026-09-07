using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Presentation.Encyclopedia
{
    /// <summary>카드 종류의 화면 표기·정렬·프레임을 한곳에서 정의한다.</summary>
    public sealed class CardEditionPresentationMetadata
    {
        public CardEditionPresentationMetadata(PlayerCardEdition edition, string displayName, int sortPriority, string icon)
        {
            Edition = edition;
            DisplayName = displayName;
            SortPriority = sortPriority;
            Icon = icon;
        }

        public PlayerCardEdition Edition { get; }
        public string EditionId => Edition.ToString();
        public string DisplayName { get; }
        public int SortPriority { get; }
        public string Icon { get; }
        public PlayerCardEdition FrameEdition => Edition;
    }

    /// <summary>Production Edition 메타데이터를 조회한다. 목록에는 현재 Catalog에 존재하는 항목만 넣는다.</summary>
    public static class CardEditionPresentationMetadataCatalog
    {
        private static readonly Dictionary<PlayerCardEdition, CardEditionPresentationMetadata> Definitions =
            new Dictionary<PlayerCardEdition, CardEditionPresentationMetadata>
            {
                { PlayerCardEdition.Normal, new CardEditionPresentationMetadata(PlayerCardEdition.Normal, "일반", 0, "") },
                { PlayerCardEdition.AllStar, new CardEditionPresentationMetadata(PlayerCardEdition.AllStar, "올스타", 10, "★") },
                { PlayerCardEdition.GoldenGlove, new CardEditionPresentationMetadata(PlayerCardEdition.GoldenGlove, "골든글러브", 20, "◆") },
                { PlayerCardEdition.Mvp, new CardEditionPresentationMetadata(PlayerCardEdition.Mvp, "MVP", 30, "★") }
            };

        /// <summary>새 Catalog Edition은 기존 화면의 switch 수정 없이 기본 표시로 조회할 수 있다.</summary>
        public static CardEditionPresentationMetadata Resolve(PlayerCardEdition edition)
        {
            if (Definitions.TryGetValue(edition, out CardEditionPresentationMetadata metadata)) return metadata;
            return new CardEditionPresentationMetadata(edition, edition.ToString(), 1000 + Convert.ToInt32(edition), "");
        }
    }
}
