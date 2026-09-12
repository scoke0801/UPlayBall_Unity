using System;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>4주 일정과 귀환 대상을 확인하고 한 주씩 정산하는 오프시즌 업무 팝업이다.</summary>
    public sealed class UI_Popup_OwnerOffseason : MonoBehaviour, IUiCancelHandler, ICancelHandler
    {
        private RectTransform _frame;
        private Text _summary;
        private Text _message;
        private readonly Text[] _weeks = new Text[OwnerOffseasonState.DurationWeeks];
        private readonly Image[] _weekBackgrounds = new Image[OwnerOffseasonState.DurationWeeks];
        private Text _training;
        private readonly Text[] _playerNames = new Text[3];
        private readonly Text[] _programNames = new Text[3];
        private readonly Text[] _returnDates = new Text[3];
        private readonly Image[] _trainingRows = new Image[3];
        private Button _advance;
        private Button _close;
        private Button _trainingButton;
        private Button _previousPage, _nextPage;
        private Text _pageLabel;
        private int _trainingPage;
        private GameObject _previousFocus;
        private OwnerOffseasonPresentationModel _model;
        private bool _isConfirming;
        private bool _isSubmitting;
        private bool _isSeasonExit;
        public event Action<int> WeekAdvanceRequested;
        public event Action<int> SeasonAdvanceRequested;
        public event Action TrainingRequested;
        public event Action Closed;
        public bool IsVisible => gameObject.activeSelf;

        public static UI_Popup_OwnerOffseason CreateRuntime(RectTransform host)
        {
            var root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerOffseason), host);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Popup_OwnerOffseason>();
            view.Build();
            root.gameObject.SetActive(false);
            return view;
        }

        private void Build()
        {
            var blocker = gameObject.AddComponent<Image>();
            blocker.color = CareerUiTheme.InputBlocker;
            var panel = OwnerWorkspaceUiFactory.CreatePanel(transform, "OffseasonCalendar", "오프시즌 훈련 일정");
            _frame = panel.Root;
            // 공용 테두리의 불투명 Outline 사본이 본문 전체를 덮지 않도록 이 팝업의 장식만 보정한다.
            _frame.Find("ThinBorder").GetComponent<Outline>().useGraphicAlpha = true;
            var header = _frame.Find("HeaderSlot").GetComponent<Text>();
            header.fontSize = 20;
            header.fontStyle = FontStyle.Normal;
            _frame.anchorMin = _frame.anchorMax = new Vector2(.5f, .5f);
            _frame.sizeDelta = new Vector2(1120, 720);
            var illustration = OwnerRuntimeUiFactory.CreateRect("TrainingCampIllustration", panel.Content);
            Place(illustration, .015f, .50f, .43f, .97f);
            var art = OwnerRuntimeUiFactory.CreateRect("Artwork", illustration);
            OwnerRuntimeUiFactory.Stretch(art);
            var image = art.gameObject.AddComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>("UI/OwnerPowerUp/owner_offseason_camp_v1");
            image.raycastTarget = false;
            var aspect = art.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 1.5f;
            _summary = Text(panel.Content, "Summary", "", .46f, .83f, .985f, .97f, 26);
            for (int week = 0; week < _weeks.Length; week++)
            {
                float left = .46f + week * .132f;
                var cell = OwnerRuntimeUiFactory.CreateImage("Week" + (week + 1), panel.Content, CareerUiTheme.ReferencePanelHeader);
                Place(cell.rectTransform, left, .68f, left + .117f, .81f);
                cell.raycastTarget = false;
                _weekBackgrounds[week] = cell;
                _weeks[week] = Text(cell.transform, "State", "", 0, 0, 1, 1, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
            }
            Text(panel.Content, "CalendarHelp", "훈련 주차를 진행하면 파견 중인 선수의 귀환이 가까워집니다.\n새 과정을 시작하기 전에 남은 기간을 확인하세요.",
                .46f, .50f, .985f, .65f, 17);
            Text(panel.Content, "TrainingTitle", "파견 선수 · 귀환 일정", .015f, .42f, .60f, .48f, 21);
            _previousPage = OwnerDugoutDetailUiFactory.CreateButton(panel.Content,"PreviousTraining","이전",.64f,.42f,.74f,.49f,
                () => { _trainingPage--; Bind(_model); });
            _pageLabel = Text(panel.Content,"TrainingPage","",.75f,.42f,.85f,.49f,16,FontStyle.Normal,TextAnchor.MiddleCenter);
            _nextPage = OwnerDugoutDetailUiFactory.CreateButton(panel.Content,"NextTraining","다음",.87f,.42f,.985f,.49f,
                () => { _trainingPage++; Bind(_model); });
            Text(panel.Content, "PlayerColumn", "선수", .025f, .36f, .23f, .41f, 15);
            Text(panel.Content, "ProgramColumn", "훈련 과정", .25f, .36f, .73f, .41f, 15);
            Text(panel.Content, "ReturnColumn", "귀환 일정", .75f, .36f, .975f, .41f, 15);
            for (int index = 0; index < _trainingRows.Length; index++)
            {
                float top = .355f - index * .06f;
                var row = OwnerRuntimeUiFactory.CreateImage("TrainingRow" + index, panel.Content, CareerUiTheme.ReferenceDataHeader);
                Place(row.rectTransform, .015f, top - .055f, .985f, top);
                row.raycastTarget = false;
                _trainingRows[index] = row;
                _playerNames[index] = Text(row.transform, "Player", "", .01f, 0, .22f, 1, 18);
                _programNames[index] = Text(row.transform, "Program", "", .242f, 0, .73f, 1, 18);
                _returnDates[index] = Text(row.transform, "Return", "", .758f, 0, .99f, 1, 18);
            }
            _training = Text(panel.Content, "EmptyTraining", "", .025f, .19f, .975f, .35f, 18);
            _message = Text(panel.Content, "Message", "", .015f, .075f, .985f, .17f, 16);
            _close = OwnerWorkspaceUiFactory.CreateButton(panel.Content, "Close", "돌아가기", Hide);
            Place((RectTransform)_close.transform, .61f, 0, .78f, .065f);
            _advance = OwnerWorkspaceUiFactory.CreateButton(panel.Content, "AdvanceWeek", "1주 진행", RequestAdvance);
            OwnerUiButtonSkin.Apply(_advance, OwnerButtonRole.Primary);
            _advance.GetComponentInChildren<Text>().fontSize = 18;
            _close.GetComponentInChildren<Text>().fontSize = 18;
            Place((RectTransform)_advance.transform, .795f, 0, .985f, .065f);
            var closeNavigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnRight = _advance, selectOnUp = _advance, selectOnLeft = _advance, selectOnDown = _advance };
            _close.navigation = closeNavigation;
            _advance.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = _close, selectOnUp = _close, selectOnRight = _close, selectOnDown = _close };
            _trainingButton = OwnerWorkspaceUiFactory.CreateButton(panel.Content, "Training", "훈련 편성으로", () => TrainingRequested?.Invoke());
            _trainingButton.GetComponentInChildren<Text>().fontSize = 18;
            Place((RectTransform)_trainingButton.transform, .015f, 0, .24f, .065f);
            _trainingButton.gameObject.SetActive(false);
        }

        public void Show(OwnerOffseasonPresentationModel model)
        {
            _isSeasonExit = false;
            if (!IsVisible) _previousFocus = EventSystem.current?.currentSelectedGameObject;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Bind(model);
            _close.Select();
            Fit();
        }

        /// <summary>미사용 일정과 파견 선수를 확인한 뒤 다음 시즌으로 넘어가거나 육성으로 돌아간다.</summary>
        public void ShowSeasonExit(OwnerOffseasonPresentationModel model)
        {
            Show(model);
            _isSeasonExit = true;
            Bind(model);
            _trainingButton.Select();
        }

        public void Bind(OwnerOffseasonPresentationModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _isConfirming = false;
            _isSubmitting = false;
            int remaining = OwnerOffseasonState.DurationWeeks - model.CompletedWeeks;
            int pages = Math.Max(1, (model.Training.Length + _trainingRows.Length - 1) / _trainingRows.Length);
            _trainingPage = Math.Max(0, Math.Min(_trainingPage, pages - 1));
            _previousPage.interactable = _trainingPage > 0; _nextPage.interactable = _trainingPage + 1 < pages;
            _pageLabel.text = (_trainingPage + 1) + " / " + pages;
            _summary.text = model.Phase != OwnerSeasonPhase.Offseason ? "다음 시즌을 준비하는 4주"
                : remaining > 0 ? $"훈련 일정  {remaining}주 남음" : "오프시즌 훈련 완료";
            for (int week = 0; week < _weeks.Length; week++)
            {
                bool completed = week < model.CompletedWeeks;
                _weeks[week].text = $"{week + 1}주차\n" + (completed ? "완료" : week == model.CompletedWeeks ? "다음 정산" : "대기");
                _weekBackgrounds[week].color = completed ? CareerUiTheme.ReferenceAccent : CareerUiTheme.ReferencePanelHeader;
                _weeks[week].color = completed ? Color.white : CareerUiTheme.ReferenceText;
            }
            for (int index = 0; index < _trainingRows.Length; index++)
            {
                int rowIndex = _trainingPage * _trainingRows.Length + index;
                bool hasTraining = rowIndex < model.Training.Length;
                _trainingRows[index].gameObject.SetActive(hasTraining);
                if (!hasTraining) continue;
                var row = model.Training[rowIndex];
                _playerNames[index].text = row.PlayerName;
                _programNames[index].text = row.ProgramName;
                _returnDates[index].text = row.RemainingWeeks == 1 ? "이번 주 귀환" : $"{row.RemainingWeeks}주 후 귀환";
                _trainingRows[index].color = row.RemainingWeeks == 1 ? CareerUiTheme.ReferenceDataFocus : CareerUiTheme.ReferenceDataHeader;
            }
            _training.gameObject.SetActive(model.Training.Length == 0);
            _training.text = "파견 중인 선수가 없습니다. 유학에서 선수와 훈련 과정을 먼저 선택하세요.";
            _message.text = model.Phase != OwnerSeasonPhase.Offseason ? "정규시즌과 포스트시즌이 끝나면 훈련 일정을 진행할 수 있습니다."
                : remaining == 0 ? "선수의 성장 결과와 라인업을 확인한 뒤 시즌 보고에서 다음 시즌을 시작하세요."
                : "주차를 진행하면 되돌릴 수 없습니다. 훈련 중인 선수가 모두 귀환해야 다음 시즌을 시작할 수 있습니다.";
            _advance.interactable = model.CanAdvance;
            _advance.GetComponentInChildren<Text>().text = remaining > 0 ? "1주 진행" : "훈련 일정 완료";
            _trainingButton.gameObject.SetActive(_isSeasonExit);
            if (_isSeasonExit)
            {
                _advance.interactable = model.Phase == OwnerSeasonPhase.Offseason && model.Training.Length == 0;
                _advance.GetComponentInChildren<Text>().text = "다음 시즌 시작";
                _message.text = model.Training.Length > 0 ? "파견 중인 선수가 있습니다. 훈련 일정에서 모두 귀환시킨 뒤 다음 시즌을 시작하세요."
                    : remaining > 0 ? $"아직 오프시즌 {remaining}주가 남아 있습니다. 지금 다음 시즌을 시작하면 남은 훈련 기회는 사라집니다."
                    : "선수 성장과 스킬 블록 편성을 마쳤다면 다음 시즌을 시작하세요.";
                if (!string.IsNullOrEmpty(model.PendingActions)) _message.text += "\n" + model.PendingActions;
            }
            LinkFocus();
            _advance.GetComponent<OwnerUiButtonSkin>().Refresh();
            if (!_advance.interactable) _close.Select();
        }

        private void RequestAdvance()
        {
            if (!_advance.interactable || _isSubmitting) return;
            if (!_isConfirming)
            {
                _isConfirming = true;
                _message.text = _isSeasonExit ? "남은 훈련과 편성을 마치고 다음 시즌을 시작합니다. 확정할까요?"
                    : _model.Training.Length == 0
                    ? "파견 중인 선수가 없습니다. 훈련을 신청하지 않고 한 주를 보낼까요?"
                    : "모든 파견 선수의 훈련을 한 주 진행하고 결과를 저장합니다. 확정할까요?";
                _advance.GetComponentInChildren<Text>().text = "진행 확정";
                return;
            }
            _advance.interactable = false;
            _advance.GetComponent<OwnerUiButtonSkin>().Refresh();
            _isSubmitting = true;
            if (_isSeasonExit) SeasonAdvanceRequested?.Invoke(_model.CompletedWeeks);
            else WeekAdvanceRequested?.Invoke(_model.CompletedWeeks);
        }

        public void ShowError()
        {
            _isConfirming = false;
            _isSubmitting = false;
            _message.text = _isSeasonExit ? "다음 시즌을 시작하지 못했습니다. 현재 일정은 유지됩니다. 구단 상태를 확인하고 다시 시도해 주세요."
                : "훈련 진행을 저장하지 못했습니다. 현재 일정은 유지됩니다. 다시 시도해 주세요.";
            _advance.interactable = _isSeasonExit ? _model.Training.Length == 0 : _model.CanAdvance;
            _advance.GetComponentInChildren<Text>().text = "다시 시도";
            _advance.GetComponent<OwnerUiButtonSkin>().Refresh();
            _close.Select();
        }

        public bool TryHandleCancel()
        {
            if (!IsVisible) return false;
            if (_isConfirming) { Bind(_model); _close.Select(); }
            else Hide();
            return true;
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (TryHandleCancel()) eventData.Use();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (_previousFocus != null && _previousFocus.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(_previousFocus);
            else Closed?.Invoke();
        }

        private void LateUpdate()
        {
            Fit();
            var focused = EventSystem.current?.currentSelectedGameObject;
            if (focused == null || !focused.transform.IsChildOf(transform)) _close.Select();
        }

        private void Fit()
        {
            Rect bounds = ((RectTransform)transform).rect;
            _frame.localScale = Vector3.one * Mathf.Max(.1f, Mathf.Min(1f, Mathf.Min(bounds.width / 1168f, bounds.height / 768f)));
        }

        private void LinkFocus()
        {
            Button[] buttons = _isSeasonExit ? new[] { _trainingButton, _previousPage, _nextPage, _close, _advance } : new[] { _previousPage, _nextPage, _close, _advance };
            var available = new System.Collections.Generic.List<Button>();
            foreach (var button in buttons) if (button.interactable) available.Add(button);
            for (int index = 0; index < available.Count; index++)
                available[index].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = available[(index + available.Count - 1) % available.Count],
                    selectOnUp = available[(index + available.Count - 1) % available.Count],
                    selectOnRight = available[(index + 1) % available.Count],
                    selectOnDown = available[(index + 1) % available.Count]
                };
        }

        private static Text Text(Transform parent, string name, string value, float left, float bottom,
            float right, float top, int size, FontStyle style = FontStyle.Normal, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size, style, alignment, CareerUiTheme.ReferenceText);
            Place(text.rectTransform, left, bottom, right, top);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        private static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
