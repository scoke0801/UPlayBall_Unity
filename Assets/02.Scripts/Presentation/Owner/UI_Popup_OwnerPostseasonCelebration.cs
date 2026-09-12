using System;
using Baseball.Presentation.Career;
using Baseball.Presentation.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>확정된 시리즈 승리와 우승을 관전 종료 뒤에 표현한다.</summary>
    public sealed class UI_Popup_OwnerPostseasonCelebration : MonoBehaviour, ICancelHandler
    {
        private RectTransform _modal;
        private RawImage _art;
        private Text _category, _title, _team, _score, _description;
        private Button _continue, _records, _skip;
        private Sequence _sequence;
        private Image[] _confetti;
        private GameObject _previousSelection;
        public event Action ContinueRequested;
        public event Action RecordsRequested;
        public bool IsAnimating => _sequence != null && _sequence.IsActive();

        /// <summary>공용 Popup Host에 결과 전용 화면을 생성한다.</summary>
        public static UI_Popup_OwnerPostseasonCelebration CreateRuntime(RectTransform host)
        {
            var root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerPostseasonCelebration), host);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Popup_OwnerPostseasonCelebration>();
            view.Build();
            view.gameObject.SetActive(false);
            return view;
        }

        private void Build()
        {
            var blocker = gameObject.AddComponent<Image>();
            blocker.color = new Color(0.01f, 0.02f, 0.04f, 0.9f);
            gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.InputBlocker);
            _modal = OwnerRuntimeUiFactory.CreateRect("Celebration", transform);
            _modal.anchorMin = _modal.anchorMax = new Vector2(0.5f, 0.5f);
            _modal.sizeDelta = new Vector2(1160f, 720f);
            _modal.gameObject.AddComponent<CareerUiPreserveTextColor>();
            var backdrop = _modal.gameObject.AddComponent<Image>();
            backdrop.color = new Color32(8, 22, 38, 255);
            var artRect = OwnerRuntimeUiFactory.CreateRect("Artwork", _modal);
            Place(artRect, 40, 112, 1080, 608);
            _art = artRect.gameObject.AddComponent<RawImage>();
            _art.raycastTarget = false;
            int count = Mathf.Clamp(OwnerPostseasonPresentationData.Load().confettiCount, 0, 60);
            _confetti = new Image[count];
            for (int index = 0; index < count; index++)
            {
                RectTransform piece = OwnerRuntimeUiFactory.CreateRect("Confetti" + index, _modal);
                Place(piece, 610f + index * 137 % 470, 660f, 5f, 12f);
                _confetti[index] = piece.gameObject.AddComponent<Image>();
                _confetti[index].raycastTarget = false;
                _confetti[index].color = new Color(1f, 0.8f, 0.4f, 0f);
            }
            _category = Label("Category", 48, 614, 460, 42, 17, new Color32(225, 189, 110, 255));
            var categoryOutline = _category.gameObject.AddComponent<Outline>();
            categoryOutline.effectColor = new Color(0f, 0.02f, 0.05f, 0.9f);
            categoryOutline.effectDistance = new Vector2(1f, -1f);
            _title = Label("Title", 48, 508, 510, 96, 56, Color.white);
            _team = Label("Team", 48, 422, 485, 64, 32, Color.white);
            _score = Label("SeriesScore", 48, 326, 480, 86, 48, new Color32(242, 204, 126, 255));
            _description = Label("Description", 48, 176, 470, 128, 21, new Color32(225, 230, 234, 255));
            _continue = Button("Continue", "대진 확인", 850, 28, 262, () => ContinueRequested?.Invoke());
            _records = Button("Records", "경기 기록 보기", 560, 28, 262, () => RecordsRequested?.Invoke());
            _skip = Button("Skip", "연출 건너뛰기", 48, 28, 220, Skip);
            OwnerUiButtonSkin.Apply(_continue, OwnerButtonRole.Primary);
            OwnerUiButtonSkin.Apply(_records, OwnerButtonRole.Secondary);
            OwnerUiButtonSkin.Apply(_skip, OwnerButtonRole.Secondary);
            ConfigureNavigation();
        }

        private Text Label(string name, float x, float y, float width, float height, int size, Color color)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(_modal, name, "", size, FontStyle.Bold, TextAnchor.MiddleLeft, color);
            text.color = color;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = size - 5;
            text.resizeTextMaxSize = size;
            Place(text.rectTransform, x, y, width, height);
            text.gameObject.AddComponent<CanvasGroup>();
            return text;
        }

        private Button Button(string name, string label, float x, float y, float width, Action action)
        {
            Button button = OwnerWorkspaceUiFactory.CreateButton(_modal, name, label, action);
            Place(button.GetComponent<RectTransform>(), x, y, width, 60);
            return button;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>이미 계산된 결과만 바인딩하며 경기 진행 상태는 변경하지 않는다.</summary>
        public void Show(OwnerPostseasonCelebration result, Func<string, string> teamName)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            Skip();
            bool champion = result.Kind == OwnerPostseasonCelebrationKind.Championship;
            var data = OwnerPostseasonPresentationData.Load();
            _art.texture = Resources.Load<Texture2D>(champion ? data.championshipArt : data.seriesArt);
            _category.text = $"시즌 {result.SeasonNumber}  /  포스트시즌";
            _title.text = champion ? "챔피언의 탄생" : "결승 진출";
            _team.text = teamName(result.TeamKey);
            _score.text = $"{result.Wins} : {result.Losses}  시리즈 승리";
            _description.text = champion
                ? $"{teamName(result.OpponentKey)}를 넘어\n리그 정상에 올랐습니다.\n우리 구단의 우승을 축하합니다."
                : $"{teamName(result.OpponentKey)}와의 승부를 끝냈습니다.\n이제 우승을 향한 마지막 시리즈입니다.";
            _previousSelection = EventSystem.current?.currentSelectedGameObject;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            FitModal();
            _continue.Select();
            if (CareerPresentationSettings.Mode == CareerPresentationMode.ResultOnly) return;
            float factor = CareerPresentationSettings.Mode == CareerPresentationMode.Simplified ? 0.5f : 1f;
            _sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this).Pause();
            Reveal(_category, 0f, data.revealFade * factor);
            Reveal(_title, data.titleDelay * factor, data.revealFade * factor);
            Reveal(_team, data.teamDelay * factor, data.revealFade * factor);
            Reveal(_score, data.scoreDelay * factor, data.revealFade * factor);
            Reveal(_description, (data.scoreDelay + data.revealFade) * factor, data.revealFade * factor);
            _art.color = new Color(1f, 1f, 1f, 0f);
            _sequence.Insert(0f, DOTween.To(() => _art.color.a, a => _art.color = new Color(1f, 1f, 1f, a), 1f, data.revealFade * factor));
            _sequence.InsertCallback((champion ? data.championshipDuration : data.seriesDuration) * factor, () => { });
            if (champion && CareerPresentationSettings.Mode == CareerPresentationMode.Full)
                AnimateConfetti(data);
            _skip.gameObject.SetActive(true);
            ConfigureNavigation();
            _sequence.OnComplete(Skip).Play();
        }

        private void Reveal(Text text, float delay, float duration)
        {
            CanvasGroup group = text.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            _sequence.Insert(delay, DOTween.To(() => group.alpha, a => group.alpha = a, 1f, duration).SetEase(Ease.OutQuad));
        }

        private void AnimateConfetti(OwnerPostseasonPresentationData data)
        {
            for (int index = 0; index < _confetti.Length; index++)
            {
                Image piece = _confetti[index];
                RectTransform rect = piece.rectTransform;
                Vector2 start = new Vector2(610f + index * 137 % 470, 660f - index % 4 * 18f);
                rect.anchoredPosition = start;
                rect.localRotation = Quaternion.Euler(0f, 0f, index * 47f);
                float delay = data.confettiDelay + index % 6 * 0.06f;
                _sequence.Insert(delay, DOTween.To(() => piece.color.a,
                    a => piece.color = new Color(1f, 0.8f, 0.4f, a), 0.8f, 0.2f));
                _sequence.Insert(delay, DOTween.To(() => rect.anchoredPosition,
                    p => rect.anchoredPosition = p, start + new Vector2(index % 2 == 0 ? 24f : -24f, -290f), 1.5f).SetEase(Ease.InQuad));
                _sequence.Insert(delay + 1f, DOTween.To(() => piece.color.a,
                    a => piece.color = new Color(1f, 0.8f, 0.4f, a), 0f, 0.5f));
            }
        }

        /// <summary>연출을 중단해도 같은 확정 결과와 다음 행동을 유지한다.</summary>
        public void Skip()
        {
            _sequence?.Kill();
            _sequence = null;
            if (_modal == null) return;
            foreach (CanvasGroup group in _modal.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1f;
            _art.color = Color.white;
            if (_confetti != null)
                foreach (Image piece in _confetti) piece.color = new Color(1f, 0.8f, 0.4f, 0f);
            if (EventSystem.current?.currentSelectedGameObject == _skip.gameObject) _continue.Select();
            _skip.gameObject.SetActive(false);
            ConfigureNavigation();
        }

        private void ConfigureNavigation()
        {
            _continue.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = _records };
            _records.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = _continue,
                selectOnLeft = _skip.gameObject.activeSelf ? _skip : _continue };
            _skip.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = _records };
        }

        /// <summary>현재 연출을 정리하고 이전 경기 기록의 포커스를 복원한다.</summary>
        public void Hide()
        {
            Skip();
            gameObject.SetActive(false);
            if (_previousSelection != null && _previousSelection.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(_previousSelection);
            _previousSelection = null;
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (IsAnimating) Skip(); else RecordsRequested?.Invoke();
            eventData?.Use();
        }

        private void FitModal()
        {
            if (_modal == null) return;
            Rect rect = GetComponent<RectTransform>().rect;
            _modal.localScale = Vector3.one * Mathf.Clamp(Mathf.Min((rect.width - 32f) / 1160f, (rect.height - 32f) / 720f), 0.01f, 1f);
        }
        private void OnRectTransformDimensionsChange() => FitModal();
        private void OnDisable() => Skip();
        private void OnDestroy() => _sequence?.Kill();
    }
}
