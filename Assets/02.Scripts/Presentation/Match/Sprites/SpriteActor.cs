using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match.Sprites
{
    /// <summary>원본 셀의 발 기준점을 보존하는 선수 그림과 독립 지면 그림자다.</summary>
    public sealed class SpriteActor
    {
        private readonly RectTransform _parent;
        private readonly RectTransform _root;
        private readonly Image _image;
        private readonly RectTransform _shadow;
        private readonly FieldProjection _projection;
        public Vector2 Position { get; private set; }
        public bool IsVisible => _root.gameObject.activeSelf;

        /// <summary>재사용할 선수 그림과 그림자를 한 번 생성한다.</summary>
        public SpriteActor(RectTransform parent, FieldProjection projection, string name)
        {
            _parent = parent;
            _projection = projection;
            _root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            _root.SetParent(parent, false);
            _root.anchorMin = _root.anchorMax = new Vector2(0, 1);
            _shadow = CreateImage(_root, "GroundShadow", true).rectTransform;
            _shadow.GetComponent<Image>().color = new Color(0, 0, 0, 0.22f);
            _shadow.sizeDelta = new Vector2(48, 12);
            _image = CreateImage(_root, "Pose", false);
            SetVisible(false);
        }

        /// <summary>셀 높이의 고정 배율로 trim 크기를 표시해 프레임마다 확대되는 흔들림을 막는다.</summary>
        public void Render(SpriteClipDefinition clip, float seconds, Vector2 position)
        {
            SpriteFrameDefinition frame = SpriteSequencePlayer.Sample(clip, seconds);
            if (frame?.sprite == null) { SetVisible(false); return; }
            Position = position;
            SetVisible(true);
            _root.anchoredPosition = FieldProjection.ToScreen(position, _parent.rect.size);
            _root.localScale = Vector3.one * _projection.DepthScale(position.y);
            Sprite sprite = frame.sprite;
            _image.sprite = sprite;
            _image.rectTransform.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            _image.rectTransform.sizeDelta = sprite.rect.size * GetPixelScale(clip);
        }

        /// <summary>원본 셀 접점을 실제 선수 그림과 같은 배율로 구장 정규화 좌표에 투영한다.</summary>
        public Vector2 ProjectSourcePoint(SpriteClipDefinition clip, Vector2 sourcePositionNormalized,
            Vector2 sourceCellSize, Vector2 sourceRootNormalized, Vector2 actorPosition)
        {
            Vector2 size = _parent.rect.size;
            if (size.x <= 0 || size.y <= 0) return actorPosition;
            Vector2 sourceOffset = Vector2.Scale(sourcePositionNormalized - sourceRootNormalized, sourceCellSize);
            Vector2 screenOffset = sourceOffset * (GetPixelScale(clip) * _projection.DepthScale(actorPosition.y));
            // 원본 셀과 구장 좌표가 모두 아래쪽을 양수로 사용하므로 Y 부호를 뒤집지 않는다.
            return actorPosition + new Vector2(screenOffset.x / size.x, screenOffset.y / size.y);
        }

        /// <summary>검수된 사건 접점이 있을 때 그 선수의 손·배트 위치를 반환한다.</summary>
        public bool TryProjectEvent(SpriteClipDefinition clip, SpriteAnimationEvent marker, Vector2 actorPosition, out Vector2 point)
        {
            point = actorPosition;
            if (!clip.TryGetEventAnchor(marker, out SpriteEventAnchorDefinition anchor)) return false;
            point = ProjectSourcePoint(clip, anchor.sourcePositionNormalized, anchor.sourceCellSize,
                anchor.sourceRootNormalized, actorPosition);
            return true;
        }

        private float GetPixelScale(SpriteClipDefinition clip) =>
            _projection.Layout.foregroundHeight * _parent.rect.height / 552f / Mathf.Max(1, clip.referenceHeightPixels);

        /// <summary>그림과 지면 그림자의 노출을 함께 바꾼다.</summary>
        public void SetVisible(bool value) => _root.gameObject.SetActive(value);
        /// <summary>무대의 깊이 정렬 결과를 적용한다.</summary>
        public void SetSiblingIndex(int index) => _root.SetSiblingIndex(index);

        internal static Image CreateImage(RectTransform parent, string name, bool circle)
        {
            var go = new GameObject(name, typeof(RectTransform), circle ? typeof(UICircleGraphic) : typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
