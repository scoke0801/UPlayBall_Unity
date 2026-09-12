using System.Collections;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>공용 미니·상세 카드에 유학·특성 아이콘과 포인터·포커스 설명을 표시한다.</summary>
    [ExecuteAlways]
    public sealed class PlayerCardGrowthBadgesView : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private static readonly Sprite[] TraitSprites = new Sprite[5];
        private static Sprite _studySprite;
        private PlayerCardGrowthBadgeModel _model = PlayerCardGrowthBadgeModel.Empty;
        private Image _trait;
        private Image _study;
        private Image _support;
        private Image _board;
        private Text _supportCount;
        private RectTransform _tooltip;
        private float _cardTop = 1;
        private bool _isDetail;
        private bool _isHovered;
        private bool _isFocused;
        private Coroutine _transition;

        /// <summary>카드 재사용 시 이전 선수의 배지와 열려 있는 설명을 함께 교체한다.</summary>
        public static void Bind(RectTransform card, PlayerCardGrowthBadgeModel model,
            bool isDetail = false, float cardTop = 1)
        {
            var view = card.GetComponent<PlayerCardGrowthBadgesView>();
            if (view == null && (model == null || (!model.HasStudy && !model.HasTrait && !model.HasSupport && !model.HasBoard))) return;
            if (view == null) view = card.gameObject.AddComponent<PlayerCardGrowthBadgesView>();
            view.HideTooltip();
            view._model = model ?? PlayerCardGrowthBadgeModel.Empty;
            view._cardTop = cardTop;
            view._isDetail = isDetail;
            if (isDetail && card.parent != null)
            {
                Selectable button = card.parent.GetComponentInParent<Selectable>();
                if (button != null)
                {
                    var relay = button.GetComponent<PlayerCardGrowthBadgeFocusRelay>();
                    if (relay == null) relay = button.gameObject.AddComponent<PlayerCardGrowthBadgeFocusRelay>();
                    relay.Target = view;
                    view._isFocused = EventSystem.current != null &&
                        EventSystem.current.currentSelectedGameObject == button.gameObject;
                }
            }
            view.SetIcon(ref view._trait, "TraitRankBadge", view._model.HasTrait,
                view._model.HasTrait ? GetTraitSprite(view._model.TraitRank) : null);
            view.SetIcon(ref view._study, "StudyBadge", view._model.HasStudy,
                view._model.HasStudy ? GetStudySprite() : null);
            view.SetIcon(ref view._support, "SupportBadge", view._model.HasSupport,
                view._model.HasSupport ? Resources.Load<Sprite>("UI/PlayerGrowthBadges/Support_v1") : null);
            view.SetIcon(ref view._board, "SkillBoardRankBadge", view._model.HasBoard,
                view._model.HasBoard ? GetTraitSprite((PlayerTraitBadgeRank)view._model.BoardRank) : null);
            if (view._model.HasSupport)
            {
                if (view._supportCount == null)
                {
                    view._supportCount = Baseball.Presentation.Owner.OwnerWorkspaceUiFactory.CreateText(view._support.transform,
                        "RemainingGames", "", 13, FontStyle.Bold, TextAnchor.LowerRight, Color.white);
                    Baseball.Presentation.Owner.OwnerWorkspaceUiFactory.Stretch(view._supportCount.rectTransform);
                    view._supportCount.gameObject.AddComponent<Outline>().effectColor = Color.black;
                }
                view._supportCount.text = view._model.SupportGames.ToString();
            }
            view.RefreshLayout();
            if (view._isHovered || view._isFocused) view.ShowTooltip();
        }

        /// <summary>카탈로그의 개별 Sprite를 재사용한다. 미보유는 자산을 로드하지 않는다.</summary>
        public static Sprite GetTraitSprite(PlayerTraitBadgeRank rank)
        {
            int index = (int)rank;
            if (index < 1 || index >= TraitSprites.Length) return null;
            if (TraitSprites[index] == null)
                TraitSprites[index] = Resources.Load<Sprite>("UI/PlayerGrowthBadges/TraitRank_" + rank);
            return TraitSprites[index];
        }

        public static Sprite GetStudySprite()
        {
            if (_studySprite == null) _studySprite = Resources.Load<Sprite>("UI/PlayerGrowthBadges/Study");
            return _studySprite;
        }

        private void SetIcon(ref Image icon, string name, bool visible, Sprite sprite)
        {
            if (icon == null && !visible) return;
            if (icon == null)
            {
                var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                item.transform.SetParent(transform, false);
                icon = item.GetComponent<Image>();
                icon.preserveAspect = true;
                // 아이콘 위에서도 기존 카드의 선택·우클릭·뒤집기 입력을 그대로 사용한다.
                icon.raycastTarget = false;
            }
            icon.sprite = sprite;
            icon.color = Color.white;
            icon.gameObject.SetActive(visible);
            icon.transform.SetAsLastSibling();
            if (visible && sprite == null)
            {
                icon.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
                icon.color = name == "StudyBadge" ? CareerUiTheme.ConditionNormal :
                    _model.TraitRank >= PlayerTraitBadgeRank.A ? CareerUiTheme.ConditionExcellent :
                    _model.TraitRank == PlayerTraitBadgeRank.B ? CareerUiTheme.ConditionGood : Color.gray;
                Debug.LogWarning("선수 성장 배지 자산을 찾을 수 없어 임시 표시합니다: " + name, this);
            }
        }

        private void OnRectTransformDimensionsChange() => RefreshLayout();

        private void OnEnable() => Canvas.preWillRenderCanvases += RefreshLayout;

        private void RefreshLayout()
        {
            var card = (RectTransform)transform;
            if (card.rect.width <= 0 || card.rect.height <= 0) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            // CanvasScaler와 상위 카드 확대를 포함한 실제 화면 크기로 최소 24px을 확보한다.
            Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float screenScale = canvas == null ? 1f : Mathf.Max(.001f, Vector2.Distance(
                RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(Vector3.zero)),
                RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(Vector3.right))));
            float size = Mathf.Clamp(Mathf.Min(card.rect.width, card.rect.height) * .13f,
                24f / screenScale, (_isDetail ? 48f : 32f) / screenScale);
            float gap = 2f / screenScale;
            float inset = 4f / screenScale;
            // 상단 카드 종류와 잠금·배치 라벨을 피해 초상 오른쪽 어깨 영역에 쌓는다.
            float top = card.rect.height * _cardTop * (_isDetail ? .84f : .95f);
            Place(_trait, size, inset, top);
            Place(_study, size, inset, top - (_model.HasTrait ? size + gap : 0));
            Place(_support, size, inset, top - ((_model.HasTrait ? 1 : 0) + (_model.HasStudy ? 1 : 0)) * (size + gap));
            Place(_board, size, inset, size + inset);
        }

        private static void Place(Image icon, float size, float inset, float top)
        {
            if (icon == null) return;
            RectTransform rect = icon.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(-inset, top);
        }

        public void OnPointerEnter(PointerEventData eventData) { _isHovered = true; RefreshFocus(); }
        public void OnPointerExit(PointerEventData eventData) { _isHovered = false; RefreshFocus(); }
        public void OnSelect(BaseEventData eventData) { _isFocused = true; RefreshFocus(); }
        public void OnDeselect(BaseEventData eventData) { _isFocused = false; RefreshFocus(); }

        private void RefreshFocus()
        {
            bool active = _isHovered || _isFocused;
            if (active) ShowTooltip(); else HideTooltip();
            if (_transition != null) StopCoroutine(_transition);
            if (Application.isPlaying && isActiveAndEnabled)
                _transition = StartCoroutine(AnimateScale(active ? 1.08f : 1f));
        }

        private IEnumerator AnimateScale(float target)
        {
            float start = _trait != null ? _trait.transform.localScale.x :
                _study != null ? _study.transform.localScale.x : 1;
            float elapsed = 0;
            while (elapsed < .12f)
            {
                elapsed += Time.unscaledDeltaTime;
                float ratio = Mathf.Clamp01(elapsed / .12f);
                float scale = Mathf.Lerp(start, target, 1 - (1 - ratio) * (1 - ratio));
                if (_trait != null) _trait.transform.localScale = Vector3.one * scale;
                if (_study != null) _study.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            _transition = null;
        }

        private void ShowTooltip()
        {
            if (!isActiveAndEnabled || _tooltip != null || (!_model.HasStudy && !_model.HasTrait && !_model.HasSupport && !_model.HasBoard)) return;
            Canvas canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) return;
            var item = new GameObject("PlayerGrowthTooltip", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasGroup), typeof(Image));
            _tooltip = item.GetComponent<RectTransform>();
            _tooltip.SetParent(canvas.transform, false);
            var layer = item.GetComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingOrder = 250;
            item.GetComponent<CanvasGroup>().blocksRaycasts = false;
            var background = item.GetComponent<Image>();
            background.color = CareerUiTheme.RosterSurface;
            background.raycastTarget = false;
            item.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.FlatSurface);
            CareerUiSkin.ApplyVisualElement(background);
            var textObject = new GameObject("Description", typeof(RectTransform), typeof(CanvasRenderer), typeof(UIProjectText));
            textObject.transform.SetParent(_tooltip, false);
            Text label = textObject.GetComponent<Text>();
            label.font = UIProjectFonts.Default;
            label.fontSize = 16;
            label.color = CareerUiTheme.RosterText;
            label.text = _model.Description;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            var textRect = label.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 8);
            textRect.offsetMax = new Vector2(-12, -8);
            Rect canvasRect = ((RectTransform)canvas.transform).rect;
            float width = Mathf.Min(340, canvasRect.width - 24);
            _tooltip.sizeDelta = new Vector2(width, 48 + ((_model.HasStudy ? 1 : 0) + (_model.HasTrait ? 1 : 0) + (_model.HasSupport ? 1 : 0) + (_model.HasBoard ? 1 : 0)) * 24);
            _tooltip.pivot = new Vector2(0, 1);
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            Vector3 local = canvas.transform.InverseTransformPoint(corners[2]);
            local.x = Mathf.Clamp(local.x + 8, canvasRect.xMin + 8, canvasRect.xMax - width - 8);
            local.y = Mathf.Clamp(local.y, canvasRect.yMin + _tooltip.sizeDelta.y + 8, canvasRect.yMax - 8);
            local.z = 0;
            _tooltip.localPosition = local;
        }

        private void HideTooltip()
        {
            if (_tooltip == null) return;
            _tooltip.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(_tooltip.gameObject); else DestroyImmediate(_tooltip.gameObject);
            _tooltip = null;
        }

        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= RefreshLayout;
            HideTooltip();
            _isHovered = _isFocused = false;
            if (_transition != null) StopCoroutine(_transition);
            _transition = null;
            if (_trait != null) _trait.transform.localScale = Vector3.one;
            if (_study != null) _study.transform.localScale = Vector3.one;
        }

        private void OnDestroy() => HideTooltip();
    }
}
