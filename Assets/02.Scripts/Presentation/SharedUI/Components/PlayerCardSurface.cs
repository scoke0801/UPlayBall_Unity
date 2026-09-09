using UnityEngine;
using UnityEngine.UI;
using Baseball.Core.Historical;
using System.Collections.Generic;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>표시 문자열과 무관하게 카드의 정본 등급으로 생성 프레임을 선택한다.</summary>
    internal static class OwnerPlayerCardFrames
    {
        private static readonly Sprite[] MiniFrames = new Sprite[4];
        private static readonly Sprite[] FullFrames = new Sprite[4];
        private static readonly Dictionary<string, Sprite> CostStars = new Dictionary<string, Sprite>();

        /// <summary>모든 등급 원화와 미니카드 텍스트가 공유하는 명찰 영역이다.</summary>
        public static Vector2 MiniNameBand => new Vector2(.18f, .28f);

        /// <summary>실제 Cost만큼 밝은 별과 남은 어두운 별을 개별 Image로 배치한다.</summary>
        public static void SetCostStars(RectTransform row, PlayerCardEdition edition, int cost)
        {
            string variant = edition == PlayerCardEdition.Mvp ? "MVP" : edition.ToString();
            SetCostStars(row, variant, cost);
        }

        /// <summary>발급 여부와 무관하게 디자인 등급에 맞는 Cost 별을 배치한다.</summary>
        public static void SetCostStars(RectTransform row, string variant, int cost)
        {
            Sprite sprite = GetCostStar(variant);
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
                star.sprite = sprite;
                star.color = slot < cost ? Color.white : new Color(.16f, .16f, .16f, .7f);
                star.preserveAspect = true;
                star.raycastTarget = false;
                RectTransform rect = star.rectTransform;
                rect.anchorMin = new Vector2(slot / (float)slots, 0);
                rect.anchorMax = new Vector2((slot + 1f) / slots, 1);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
        }

        /// <summary>카드와 확대 원화 보기에서 같은 등급별 Cost 별을 사용한다.</summary>
        public static Sprite GetCostStar(string variant)
        {
            if (!CostStars.TryGetValue(variant, out Sprite sprite) || sprite == null)
            {
                string path = "UI/PlayerCards/PlayerCard_CostStar_" + variant;
                sprite = Resources.Load<Sprite>(path + "_v3") ?? Resources.Load<Sprite>(path + "_v2");
                if (sprite == null)
                    sprite = Resources.Load<Sprite>("UI/PlayerCards/PlayerCard_CostStar_Normal_v2");
                CostStars[variant] = sprite;
            }
            return sprite;
        }

        /// <summary>미니카드 또는 전체 카드의 등급별 프레임을 가져온다.</summary>
        public static Sprite Get(PlayerCardEdition edition, bool isMini)
        {
            int index = (int)edition;
            if (index < 0 || index >= MiniFrames.Length) index = 0;
            Sprite[] frames = isMini ? MiniFrames : FullFrames;
            string variant = index == (int)PlayerCardEdition.Mvp ? "MVP" : ((PlayerCardEdition)index).ToString();
            return frames[index] != null ? frames[index] : frames[index] = Get(variant, isMini);
        }

        /// <summary>미발급 디자인도 동일한 리소스 규칙으로 선택하며 개선 원화를 우선한다.</summary>
        public static Sprite Get(string variant, bool isMini)
        {
            string path = "UI/PlayerCards/PlayerCard_" + (isMini ? "Mini_" : "Full_") + variant;
            return Resources.Load<Sprite>(path + "_v3") ?? Resources.Load<Sprite>(path + "_v2");
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
