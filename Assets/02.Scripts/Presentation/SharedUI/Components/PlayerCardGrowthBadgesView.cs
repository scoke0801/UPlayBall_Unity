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
        private static readonly Sprite[] TraitSprites = new Sprite[System.Enum.GetValues(typeof(PlayerTraitBadgeRank)).Length];
        private static readonly Sprite[] StudySprites = new Sprite[System.Enum.GetValues(typeof(PlayerStudyBadgeRank)).Length];
        private static readonly Sprite[] BoardSprites = new Sprite[System.Enum.GetValues(typeof(PlayerBoardBadgeRank)).Length];
        private static Sprite _studySprite;
        private PlayerCardGrowthBadgeModel _model = PlayerCardGrowthBadgeModel.Empty;
        private Image _trait;
        private Image _study;
        private Image _support;
        private Image _board;
        private Image _enhancement;
        private Text _supportCount;
        private RectTransform _tooltip;
        private float _cardTop = 1;
        private bool _isDetail;
        private bool _isHovered;
        private bool _isFocused;
        private Coroutine _transition;
        private float _badgeScale = 1f;

        /// <summary>상세 화면 진입 시 기존 카드의 설명과 호버·포커스 표시 상태를 정리한다.</summary>
        public static void DismissTooltips(Transform root)
        {
            if (root == null) return;
            foreach (var view in root.GetComponentsInChildren<PlayerCardGrowthBadgesView>(true))
            {
                // 팝업은 아래 카드의 선택을 해제하지 않으므로 PointerExit만으로는 설명이 닫히지 않는다.
                view._isHovered = view._isFocused = false;
                view.HideTooltip();
                if (view._transition != null) view.StopCoroutine(view._transition);
                view._transition = null;
                view.SetBadgeScale(1f);
            }
        }

        /// <summary>카드 재사용 시 이전 선수의 배지와 열려 있는 설명을 함께 교체한다.</summary>
        public static void Bind(RectTransform card, PlayerCardGrowthBadgeModel model,
            bool isDetail = false, float cardTop = 1, Image enhancement = null)
        {
            var view = card.GetComponent<PlayerCardGrowthBadgesView>();
            if (view == null && enhancement == null && (model == null || (!model.HasStudy && !model.HasTrait && !model.HasSupport && !model.HasBoard))) return;
            if (view == null) view = card.gameObject.AddComponent<PlayerCardGrowthBadgesView>();
            view.HideTooltip();
            view._model = model ?? PlayerCardGrowthBadgeModel.Empty;
            view._cardTop = cardTop;
            view._isDetail = isDetail;
            view._enhancement = enhancement;
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
                view._model.HasStudy ? GetStudySprite(view._model.StudyRank) : null);
            view.SetIcon(ref view._support, "SupportBadge", view._model.HasSupport,
                view._model.HasSupport ? Resources.Load<Sprite>("UI/PlayerGrowthBadges/Support_v1") : null);
            view.SetIcon(ref view._board, "SkillBoardRankBadge", view._model.HasBoard,
                view._model.HasBoard ? GetBoardSprite(view._model.BoardRank) : null);
            if (view._model.HasSupport)
            {
                if (view._supportCount == null || view._supportCount.transform.parent != view._support.transform)
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

        /// <summary>성장판 등급별 원화 Sprite를 재사용하며 별도 문자를 겹치지 않는다.</summary>
        public static Sprite GetBoardSprite(PlayerBoardBadgeRank rank)
        {
            int index = (int)rank;
            if (index < 1 || index >= BoardSprites.Length) return null;
            if (BoardSprites[index] == null)
                BoardSprites[index] = Resources.Load<Sprite>("UI/PlayerGrowthBadges/SkillGrade_" + rank);
            return BoardSprites[index];
        }

        /// <summary>등급별 원화를 공유하고 등급 기록이 없는 카드만 공통 유학 아이콘을 쓴다.</summary>
        public static Sprite GetStudySprite(PlayerStudyBadgeRank rank = PlayerStudyBadgeRank.None)
        {
            int index = (int)rank;
            if (index > 0 && index < StudySprites.Length)
            {
                if (StudySprites[index] == null) StudySprites[index] = Resources.Load<Sprite>("UI/PlayerGrowthBadges/StudyRank_" + rank);
                return StudySprites[index];
            }
            if (_studySprite == null) _studySprite = Resources.Load<Sprite>("UI/PlayerGrowthBadges/Study");
            return _studySprite;
        }

        private void SetIcon(ref Image icon, string name, bool visible, Sprite sprite)
        {
            // 앞면 재구성 시 분리된 아이콘은 프레임 끝에 파괴된다. 아직 살아 있는 참조도 재사용하면 안 된다.
            if (icon != null && icon.transform.parent != transform) icon = null;
            if (icon == null && !visible) return;
            if (icon == null)
            {
                var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                item.transform.SetParent(transform, false);
                icon = item.GetComponent<Image>();
                icon.preserveAspect = true;
            }
            // 상세 앞면은 뒤집기 버튼의 자식이므로 배지가 포인터를 받아야 앞면까지 Enter/Exit가 전달된다.
            // 클릭 핸들러는 추가하지 않아 선택·우클릭·뒤집기는 기존 부모 카드가 계속 처리한다.
            icon.raycastTarget = _isDetail;
            icon.sprite = sprite;
            icon.color = Color.white;
            icon.transform.localScale = Vector3.one * _badgeScale;
            icon.gameObject.SetActive(visible);
            icon.transform.SetAsLastSibling();
            if (visible && sprite == null)
            {
                // 누락된 자산은 Image의 기본 단색 면으로 표시해 내장 리소스 로딩 오류가 겹치지 않게 한다.
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
            // CanvasScaler와 상위 카드 확대를 반영하되 작은 카드에서는 안전 영역을 우선한다.
            Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float screenScale = canvas == null ? 1f : Mathf.Max(.001f, Vector2.Distance(
                RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(Vector3.zero)),
                RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(Vector3.right))));
            float size = Mathf.Min(card.rect.width * (_isDetail ? .105f : .15f),
                (_isDetail ? 72f : 32f) / screenScale);
            float inset = card.rect.width * .045f;
            float height = card.rect.height * _cardTop;
            if (_isDetail)
            {
                // 강화까지 같은 열에서 정렬하고 미보유 항목은 건너뛰어 초상 옆의 과도한 공백을 없앤다.
                float detailTop = height * .94f;
                float detailInset = card.rect.width * .09f - size * .5f;
                PlaceDetail(_enhancement, size, detailInset, ref detailTop);
                PlaceDetail(_study, size, detailInset, ref detailTop);
                PlaceDetail(_trait, size, detailInset, ref detailTop);
                PlaceDetail(_board, size, detailInset, ref detailTop);
                PlaceDetail(_support, size, detailInset, ref detailTop);
                return;
            }
            // 초상 오른쪽의 한 열을 공유한다. 이름·능력치와 상단 강화 표시는 이 영역 밖에 둔다.
            float top = height * (_isDetail ? .83f : .79f);
            float bottom = height * (_isDetail ? .55f : .40f);
            int slotCount = _model.HasSupport ? 4 : 3;
            size = Mathf.Min(size, (top - bottom) / (slotCount + .2f * (slotCount - 1)));
            float step = (top - bottom - size) / (slotCount - 1);
            Place(_enhancement, size, inset, height * .96f);
            Place(_study, size, inset, top);
            Place(_trait, size, inset, top - step);
            Place(_board, size, inset, top - step * 2);
            Place(_support, size, inset, top - step * 3);
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

        private static void PlaceDetail(Image icon, float size, float inset, ref float top)
        {
            if (icon == null || !icon.gameObject.activeSelf) return;
            Place(icon, size, inset, top);
            top -= size * 1.3f;
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
            float start = _badgeScale;
            float elapsed = 0;
            while (elapsed < .12f)
            {
                elapsed += Time.unscaledDeltaTime;
                float ratio = Mathf.Clamp01(elapsed / .12f);
                float scale = Mathf.Lerp(start, target, 1 - (1 - ratio) * (1 - ratio));
                SetBadgeScale(scale);
                yield return null;
            }
            _transition = null;
        }

        private void SetBadgeScale(float scale)
        {
            _badgeScale = scale;
            Vector3 localScale = Vector3.one * scale;
            if (_trait != null) _trait.transform.localScale = localScale;
            if (_study != null) _study.transform.localScale = localScale;
            if (_board != null) _board.transform.localScale = localScale;
            if (_support != null) _support.transform.localScale = localScale;
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
            background.color = new Color32(13, 23, 36, 255);
            background.raycastTarget = false;
            // 반투명 FlatSurface 스킨 대신 불투명 설명 면과 공용 프레임을 보존한다.
            item.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.DataImage);
            Baseball.Presentation.Owner.UIOwnerPanelFrame.Attach(_tooltip);
            var textObject = new GameObject("Description", typeof(RectTransform), typeof(CanvasRenderer), typeof(UIProjectText));
            textObject.transform.SetParent(_tooltip, false);
            Text label = textObject.GetComponent<Text>();
            label.font = UIProjectFonts.Default;
            label.fontSize = 14;
            label.lineSpacing = 1.2f;
            label.supportRichText = true;
            label.color = new Color32(232, 237, 243, 255);
            label.gameObject.AddComponent<CareerUiPreserveTextColor>();
            label.text = BuildTooltipDescription();
            label.alignment = TextAnchor.UpperLeft;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            var textRect = label.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16, 16);
            textRect.offsetMax = new Vector2(-16, -16);
            Rect canvasRect = ((RectTransform)canvas.transform).rect;
            float width = Mathf.Min(320, canvasRect.width - 32);
            _tooltip.sizeDelta = new Vector2(width, 48);
            // 특성 효과와 유학 설명은 여러 줄일 수 있으므로 배지 개수가 아닌 실제 줄바꿈 높이를 쓴다.
            textRect.ForceUpdateRectTransforms();
            _tooltip.sizeDelta = new Vector2(width, Mathf.Max(48, label.preferredHeight + 32));
            _tooltip.pivot = new Vector2(0, 1);
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            Vector3 local = canvas.transform.InverseTransformPoint(corners[2]);
            float x = local.x + 12;
            if (x + width > canvasRect.xMax - 8)
                x = canvas.transform.InverseTransformPoint(corners[0]).x - width - 12;
            local.x = Mathf.Clamp(x, canvasRect.xMin + 8, canvasRect.xMax - width - 8);
            local.y = Mathf.Clamp(local.y, canvasRect.yMin + _tooltip.sizeDelta.y + 8, canvasRect.yMax - 8);
            local.z = 0;
            _tooltip.localPosition = local;
        }

        private string BuildTooltipDescription()
        {
            var text = new System.Text.StringBuilder();
            if (_model.HasStudy) AppendTooltipSection(text, "유학", _model.StudyDescription);
            if (_model.HasTrait)
            {
                string description = _model.TraitDescription;
                int separator = description.IndexOf(" · ", System.StringComparison.Ordinal);
                if (separator >= 0)
                    description = description.Substring(0, separator) + "\n" + description.Substring(separator + 3);
                AppendTooltipSection(text, "특성", description);
            }
            if (_model.HasBoard) AppendTooltipSection(text, "스킬블록", _model.BoardDescription);
            if (_model.HasSupport) AppendTooltipSection(text, "서포트", _model.SupportDescription);
            return text.ToString();
        }

        private static void AppendTooltipSection(System.Text.StringBuilder text, string title, string description)
        {
            if (text.Length > 0) text.Append("\n\n");
            text.Append("<color=#D6BE88>").Append(title).Append("</color>\n").Append(description);
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
            SetBadgeScale(1f);
        }

        private void OnDestroy() => HideTooltip();
    }
}
