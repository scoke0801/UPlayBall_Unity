using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>정본 컨디션 10단계를 두 단계씩 묶어 한 장의 화살표 시트로 표시한다.</summary>
    internal static class PlayerCardConditionSprites
    {
        private static readonly Sprite[] Arrows = new Sprite[5];

        public static Sprite Get(int? level)
        {
            if (!level.HasValue || level < 1 || level > 10) return null;
            int index = 4 - (level.Value - 1) / 2;
            if (Arrows[index] != null) return Arrows[index];
            Texture2D texture = Resources.Load<Texture2D>("UI/PlayerCards/PlayerCard_ConditionArrows_v1");
            if (texture == null) return null;
            float cell = texture.width / 5f;
            // 시트의 위아래 여백을 제외해 작은 카드에서도 화살표가 크게 보이게 한다.
            Arrows[index] = Sprite.Create(texture,
                new Rect(index * cell, texture.height * .22f, cell, texture.height * .56f),
                new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            Arrows[index].name = "ConditionArrow" + index;
            return Arrows[index];
        }

        public static Image Bind(Transform parent, int? level, Vector2 min, Vector2 max)
        {
            Transform existing = parent.Find("ConditionArrow");
            Image icon = existing != null ? existing.GetComponent<Image>() : null;
            if (icon == null)
            {
                var item = new GameObject("ConditionArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                item.transform.SetParent(parent, false);
                icon = item.GetComponent<Image>();
            }
            icon.sprite = Get(level);
            icon.gameObject.SetActive(icon.sprite != null);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = min;
            icon.rectTransform.anchorMax = max;
            icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
            icon.transform.SetAsLastSibling();
            return icon;
        }
    }

    /// <summary>표시 문자열과 무관하게 카드의 정본 등급으로 생성 프레임을 선택한다.</summary>
    internal static class OwnerPlayerCardFrames
    {
        private static readonly int EditionCount = Enum.GetValues(typeof(PlayerCardEdition)).Length;
        private static readonly Sprite[] MiniFrames = new Sprite[EditionCount];
        private static readonly Sprite[] FullFrames = new Sprite[EditionCount];
        private static readonly Dictionary<string, Sprite> CostStars = new Dictionary<string, Sprite>();

        /// <summary>원화의 명찰 안쪽에서 문장과 테두리를 피하는 이름 영역을 반환한다.</summary>
        public static Rect GetNameRect(PlayerCardEdition edition, bool isMini)
        {
            // 원화마다 리본 높이가 다르므로 프레임 전체 기준의 정규 좌표를 사용한다.
            if (!isMini)
            {
                // 문장이 명찰 안으로 들어오는 레전드는 문장 아래의 빈 영역을 기준으로 한다.
                float center;
                switch (edition)
                {
                    case PlayerCardEdition.Mvp: center = .454f; break;
                    case PlayerCardEdition.Rare: center = .453f; break;
                    case PlayerCardEdition.GoldenGlove: center = .449f; break;
                    case PlayerCardEdition.Legend: center = .424f; break;
                    case PlayerCardEdition.CareerHigh: center = .437f; break;
                    case PlayerCardEdition.Ex: center = .443f; break;
                    default: center = .440f; break;
                }
                return new Rect(.23f, center - .0225f, .54f, .045f);
            }
            switch (edition)
            {
                case PlayerCardEdition.Ex:
                case PlayerCardEdition.Legend:
                case PlayerCardEdition.Rare:
                case PlayerCardEdition.CareerHigh: return new Rect(.23f, .24f, .54f, .065f);
                default: return new Rect(.23f, .19f, .54f, .075f);
            }
        }

        /// <summary>이름과 연도가 어두운 명찰에서도 읽히도록 원화에 맞는 대비를 사용한다.</summary>
        public static Color GetNameColor(PlayerCardEdition edition) =>
            edition == PlayerCardEdition.CareerHigh ? Color.white : new Color32(18, 20, 24, 255);

        /// <summary>등급 장식과 무관한 공통 초상 하단이다. 장식은 초상 위의 별도 메시로 그린다.</summary>
        public static float GetPortraitBottom(PlayerCardEdition edition, bool isMini) => isMini ? .35f : .535f;

        /// <summary>동일한 초상 Sprite가 모든 등급에서 같은 크기로 표시되는 공통 상단이다.</summary>
        public static float GetPortraitTop(PlayerCardEdition edition) => .94f;

        /// <summary>배경 원화의 장식 부분만 다시 그려 초상보다 앞에 배치한다.</summary>
        public static void SetDecoration(RectTransform parent, Sprite frame, PlayerCardEdition edition,
            bool isMini, float top, int siblingIndex)
        {
            Transform existing = parent.Find("CardDecoration");
            var decoration = existing != null ? existing.GetComponent<PlayerCardDecorationGraphic>() : null;
            if (decoration == null)
            {
                var item = new GameObject("CardDecoration", typeof(RectTransform), typeof(CanvasRenderer), typeof(PlayerCardDecorationGraphic));
                item.transform.SetParent(parent, false);
                decoration = item.GetComponent<PlayerCardDecorationGraphic>();
            }
            RectTransform rect = decoration.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetSiblingIndex(siblingIndex);
            decoration.Bind(frame, edition, isMini);
        }

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
                sprite = Resources.Load<Sprite>(path + "_v4")
                    ?? Resources.Load<Sprite>(path + "_v3")
                    ?? Resources.Load<Sprite>(path + "_v2");
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
            Sprite[] frames = isMini ? MiniFrames : FullFrames;
            if (index < 0 || index >= frames.Length)
            {
                edition = PlayerCardEdition.Normal;
                index = (int)edition;
            }
            string variant = edition == PlayerCardEdition.Mvp ? "MVP" : edition.ToString();
            return frames[index] != null ? frames[index] : frames[index] = Get(variant, isMini);
        }

        /// <summary>미발급 디자인도 동일한 리소스 규칙으로 선택하며 최신 원화를 우선한다.</summary>
        public static Sprite Get(string variant, bool isMini)
        {
            string path = "UI/PlayerCards/PlayerCard_" + (isMini ? "Mini_" : "Full_") + variant;
            return Resources.Load<Sprite>(path + "_v7")
                ?? Resources.Load<Sprite>(path + "_v6")
                ?? Resources.Load<Sprite>(path + "_v5")
                ?? Resources.Load<Sprite>(path + "_v4")
                ?? Resources.Load<Sprite>(path + "_v3")
                ?? Resources.Load<Sprite>(path + "_v2");
        }
    }

    /// <summary>불투명 원화를 복제하지 않고 UV 메시로 외곽·명찰·문장 부분만 초상 위에 그린다.</summary>
    internal sealed class PlayerCardDecorationGraphic : MaskableGraphic
    {
        private Sprite _sprite;
        private PlayerCardEdition _edition;
        private bool _isMini;
        private static Material _decorationMaterial;
        private float _islandMode;
        public override Texture mainTexture => _sprite != null ? _sprite.texture : base.mainTexture;

        /// <summary>배경과 동일한 원화의 UV를 사용해 장식 경계의 색과 위치를 보존한다.</summary>
        public void Bind(Sprite sprite, PlayerCardEdition edition, bool isMini)
        {
            _sprite = sprite;
            _edition = edition;
            _isMini = isMini;
            if (_decorationMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("UI/PlayerCards/PlayerCardDecoration");
                if (shader != null) _decorationMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            material = _decorationMaterial;
            if (canvas != null) canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            raycastTarget = false;
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_sprite == null) return;
            _islandMode = 0;
            // 전체 사각형을 덮으면 초상이 사라진다. 사진 창을 비워 둔 네 가장자리만 그린다.
            AddRect(mesh, 0, 0, 1, _isMini ? .31f : .51f);
            AddRect(mesh, 0, .98f, 1, 1);
            AddRect(mesh, 0, _isMini ? .31f : .51f, .025f, .98f);
            AddRect(mesh, .975f, _isMini ? .31f : .51f, 1, .98f);
            // 장식 윤곽은 원화 기준이다. 초상 크기를 바꾸는 레이아웃 분기로 사용하지 않는다.
            switch (_edition)
            {
                case PlayerCardEdition.AllStar:
                    _islandMode = 2;
                    AddRect(mesh, .35f, _isMini ? .278f : .495f, .65f, _isMini ? .335f : .555f);
                    break;
                case PlayerCardEdition.Ex:
                    _islandMode = 1;
                    AddRect(mesh, .345f, _isMini ? .315f : .485f, .655f, _isMini ? .37f : .55f);
                    break;
                case PlayerCardEdition.Mvp:
                    _islandMode = 1;
                    AddRect(mesh, _isMini ? .40f : .42f, _isMini ? .278f : .505f,
                        _isMini ? .60f : .58f, _isMini ? .35f : .57f);
                    break;
                case PlayerCardEdition.Legend:
                    _islandMode = 1;
                    AddRect(mesh, .22f, _isMini ? .31f : .455f, .78f, _isMini ? .37f : .535f);
                    break;
                case PlayerCardEdition.Rare:
                    // 원화에서 상단 마크를 제거했으므로 이름표 위의 유일한 마크만 덧그린다.
                    _islandMode = 3;
                    AddRect(mesh, .385f, _isMini ? .325f : .495f, .615f, _isMini ? .395f : .555f);
                    break;
            }
        }

        private void AddRect(VertexHelper mesh, float x0, float y0, float x1, float y1)
        {
            Rect bounds = GetPixelAdjustedRect();
            Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(_sprite);
            int start = mesh.currentVertCount;
            AddVertex(mesh, bounds, uv, x0, y0);
            AddVertex(mesh, bounds, uv, x0, y1);
            AddVertex(mesh, bounds, uv, x1, y1);
            AddVertex(mesh, bounds, uv, x1, y0);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }

        private void AddVertex(VertexHelper mesh, Rect bounds, Vector4 uv, float x, float y)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(bounds.xMin + bounds.width * x, bounds.yMin + bounds.height * y);
            vertex.color = color;
            vertex.uv0 = new Vector2(Mathf.Lerp(uv.x, uv.z, x), Mathf.Lerp(uv.y, uv.w, y));
            vertex.uv1 = new Vector2(_islandMode, 0);
            mesh.AddVert(vertex);
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
