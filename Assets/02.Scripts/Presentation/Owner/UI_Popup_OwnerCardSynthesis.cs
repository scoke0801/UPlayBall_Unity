using UnityEngine;

namespace Baseball.Presentation.Owner
{
    /// <summary>기존 합성 진입점을 공용 카드 결과 화면에 연결한다.</summary>
    public static class UI_Popup_OwnerCardSynthesis
    {
        /// <summary>강화 성공 이후 확정된 카드 결과를 표시한다.</summary>
        public static void Show(RectTransform host, OwnerCollectionCardSnapshot card) =>
            UI_Popup_OwnerCardResult.Show(host, card, OwnerCardResultKind.Synthesis);
    }
}
