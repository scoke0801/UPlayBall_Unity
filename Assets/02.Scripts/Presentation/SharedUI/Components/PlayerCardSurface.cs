using UnityEngine;
using UnityEngine.UI;
using Baseball.Core.Historical;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>표시 문자열과 무관하게 카드의 정본 등급으로 생성 프레임을 선택한다.</summary>
    internal static class OwnerPlayerCardFrames
    {
        private static readonly Sprite[] MiniFrames = new Sprite[4];
        private static readonly Sprite[] FullFrames = new Sprite[4];
        private static readonly Sprite[] CostStars = new Sprite[4];

        /// <summary>미니 프레임 원화의 명찰 영역을 아래쪽 기준 정규화 좌표로 반환한다.</summary>
        public static Vector2 GetMiniNameBand(PlayerCardEdition edition)
        {
            // 생성 원화의 명찰 위치를 보존하며 초상과 이름이 장식을 덮지 않게 한다.
            return edition switch
            {
                PlayerCardEdition.AllStar => new Vector2(.335f, .455f),
                PlayerCardEdition.GoldenGlove => new Vector2(.25f, .345f),
                PlayerCardEdition.Mvp => new Vector2(.265f, .345f),
                _ => new Vector2(.18f, .27f)
            };
        }

        /// <summary>실제 Cost만큼 밝은 별과 남은 어두운 별을 개별 Image로 배치한다.</summary>
        public static void SetCostStars(RectTransform row, PlayerCardEdition edition, int cost)
        {
            int index = Mathf.Clamp((int)edition, 0, CostStars.Length - 1);
            string variant = index == (int)PlayerCardEdition.Mvp ? "MVP" : ((PlayerCardEdition)index).ToString();
            if (CostStars[index] == null)
                CostStars[index] = Resources.Load<Sprite>("UI/PlayerCards/PlayerCard_CostStar_" + variant + "_v2");
            int slots = Mathf.Max(10, cost);
            for (int slot = 0; slot < Mathf.Max(slots, row.childCount); slot++)
            {
                Image star;
                if (slot < row.childCount) star = row.GetChild(slot).GetComponent<Image>();
                else
                {
                    var item = new GameObject("CostStar" + slot, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    item.transform.SetParent(row, false);
                    star = item.GetComponent<Image>();
                }
                star.gameObject.SetActive(slot < slots);
                star.sprite = CostStars[index];
                star.color = slot < cost ? Color.white : new Color(.16f, .16f, .16f, .7f);
                star.preserveAspect = true;
                star.raycastTarget = false;
                RectTransform rect = star.rectTransform;
                rect.anchorMin = new Vector2(slot / (float)slots, 0);
                rect.anchorMax = new Vector2((slot + 1f) / slots, 1);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
        }

        /// <summary>미니카드 또는 전체 카드의 등급별 프레임을 가져온다.</summary>
        public static Sprite Get(PlayerCardEdition edition, bool isMini)
        {
            int index = (int)edition;
            if (index < 0 || index >= MiniFrames.Length) index = 0;
            Sprite[] frames = isMini ? MiniFrames : FullFrames;
            string variant = index == (int)PlayerCardEdition.Mvp ? "MVP" : ((PlayerCardEdition)index).ToString();
            return frames[index] != null ? frames[index] : frames[index] = Resources.Load<Sprite>(
                "UI/PlayerCards/PlayerCard_" + (isMini ? "Mini_" : "Full_") + variant + "_v2");
        }
    }

    /// <summary>선수 카드의 얇은 금속 프레임과 명찰을 해상도에 독립적인 UI 메시로 그린다.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PlayerCardSurface : MaskableGraphic
    {
        [SerializeField] private Color _top = new Color32(103, 111, 122, 255);
        [SerializeField] private Color _bottom = new Color32(20, 24, 31, 255);

        /// <summary>표면의 위아래 색을 지정한다.</summary>
        public void SetColors(Color top, Color bottom)
        {
            _top = top;
            _bottom = bottom;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = GetPixelAdjustedRect();
            helper.AddVert(new Vector3(rect.xMin, rect.yMin), _bottom, Vector2.zero);
            helper.AddVert(new Vector3(rect.xMin, rect.yMax), _top, Vector2.up);
            helper.AddVert(new Vector3(rect.xMax, rect.yMax), _top, Vector2.one);
            helper.AddVert(new Vector3(rect.xMax, rect.yMin), _bottom, Vector2.right);
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(2, 3, 0);
        }
    }
}
