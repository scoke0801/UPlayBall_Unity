using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>경기·카드 조건과 소비 방침을 선택하고 경기별 변경안을 확정한다.</summary>
    public sealed class UI_Popup_OwnerAutoTactics : MonoBehaviour, IUiCancelHandler, ICancelHandler
    {
        private RectTransform _frame;
        private readonly List<Dropdown> _filters = new List<Dropdown>();
        private readonly List<Text> _rows = new List<Text>();
        private readonly TacticAutoOptions _options = new TacticAutoOptions();
        private OwnerTacticsSnapshot _snapshot;
        private Func<TacticAutoOptions, IReadOnlyList<TacticAutoGamePlan>> _preview;
        private Action<IReadOnlyList<TacticAutoGamePlan>> _apply;
        private IReadOnlyList<TacticAutoGamePlan> _plans;
        private Text _summary, _feedback;
        private RectTransform _planContent;
        private ScrollRect _planScroll;
        private Button _previous, _next;
        private Button _confirm, _cancel;
        private GameObject _previousFocus;
        private bool _isApplying;

        /// <summary>저장 상태를 변경하지 않는 독립 미리보기 팝업을 연다.</summary>
        public static UI_Popup_OwnerAutoTactics Show(RectTransform parent, OwnerTacticsSnapshot snapshot,
            Func<TacticAutoOptions, IReadOnlyList<TacticAutoGamePlan>> preview,
            Action<IReadOnlyList<TacticAutoGamePlan>> apply)
        {
            var root = OwnerWorkspaceUiFactory.CreateRoot(parent, nameof(UI_Popup_OwnerAutoTactics), false);
            var shade = root.gameObject.AddComponent<Image>();
            var dim = CareerUiTheme.Background; dim.a = .85f; shade.color = dim;
            var view = root.gameObject.AddComponent<UI_Popup_OwnerAutoTactics>();
            view._snapshot = snapshot; view._preview = preview; view._apply = apply;
            view._previousFocus = EventSystem.current?.currentSelectedGameObject;
            view.Build();
            return view;
        }

        private void Build()
        {
            var panel = OwnerWorkspaceUiFactory.CreatePanel(transform, "AutoTacticsPanel", "작전 카드 자동 설정");
            _frame = panel.Root;
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(.5f, .5f);
            _frame.anchoredPosition = Vector2.zero;
            UIOwnerFrontOfficePanel.ApplyFramedSurface(_frame);
            var body = panel.Content;
            Label(body, "Intro", "다가올 경기를 위한 작전 계획", .02f, .91f, .98f, .99f, 20);
            Label(body, "SettingsTitle", "01  필터 · 적용 방침", .02f, .84f, .45f, .90f, 16);
            Label(body, "PreviewTitle", "02  적용 미리보기", .50f, .84f, .75f, .90f, 16);
            _previous = OwnerDugoutDetailUiFactory.CreateButton(body, "PreviousPlans", "이전", .76f, .84f, .86f, .90f, () => ScrollPlans(1));
            _next = OwnerDugoutDetailUiFactory.CreateButton(body, "NextPlans", "다음", .88f, .84f, .98f, .90f, () => ScrollPlans(-1));
            AddFilter(body, "경기 범위", new[] { $"향후 {TacticAutoPlanner.MaximumPlanningWeeks}주 · {TacticAutoPlanner.MaximumPlanningGames}경기", "다음 10경기", "다음 5경기", "다음 3경기", "다음 경기" }, 0,
                .02f, .225f, .70f, i => _options.GameCount = new[] { TacticAutoPlanner.MaximumPlanningGames, 10, 5, 3, 1 }[i]);
            AddFilter(body, "경기 장소", new[] { "홈 · 원정 모두", "홈 경기만", "원정 경기만" }, 0,
                .245f, .45f, .70f, i => _options.Venue = (TacticAutoVenue)i);
            AddFilter(body, "카드 분류", new[] { "모든 분류", "야수", "투수", "분석", "공통" }, 0,
                .02f, .225f, .55f, i => _options.Category = i == 0 ? (TacticCardCategory?)null : (TacticCardCategory)(i - 1));
            AddFilter(body, "사용 등급 상한", new[] { "일반까지", "레어까지", "스페셜까지", "모든 등급" }, 3,
                .245f, .45f, .55f, i => _options.MaximumTier = (TacticTier)i);
            AddFilter(body, "기존 설정 처리", new[] { "빈 슬롯만 채우기", "대상 경기 전체 교체" }, 0,
                .02f, .45f, .40f, i => _options.Policy = (TacticAutoPolicy)i);
            AddFilter(body, "카드 선택 우선순위", new[] { "낮은 등급부터 · 희귀 카드 절약", "높은 등급부터", "남은 수량이 많은 카드부터" }, 0,
                .02f, .45f, .25f, i => _options.Priority = (TacticAutoPriority)i);
            AddFilter(body, "경기당 장착 목표", new[] { "2장", "1장" }, 0,
                .02f, .225f, .10f, i => _options.CardsPerGame = 2 - i);
            AddFilter(body, "방해 카드", new[] { "사용하지 않음", "사용 허용" }, 0,
                .245f, .45f, .10f, i => _options.CanUseDisruption = i == 1);

            _summary = Label(body, "Summary", "", .50f, .75f, .98f, .83f, 18);
            _summary.color = OwnerDashboardStyle.Gold;
            var content = OwnerDugoutDetailUiFactory.CreateScrollContent(body, "PlanRows", .50f, .28f, .98f, .73f, out _planScroll);
            _planContent = content;
            content.sizeDelta = new Vector2(0, OwnerModeManager.MaximumTacticPlanningGames * 92);
            for (int i = 0; i < OwnerModeManager.MaximumTacticPlanningGames; i++)
            {
                var row = Label(content, "Game" + i, "", 0, 0, 1, 1, 14);
                row.rectTransform.anchorMin = new Vector2(0, 1);
                row.rectTransform.anchorMax = new Vector2(1, 1);
                row.rectTransform.pivot = new Vector2(.5f, 1);
                row.rectTransform.offsetMin = new Vector2(0, -(i + 1) * 92);
                row.rectTransform.offsetMax = new Vector2(0, -i * 92 - 8);
                _rows.Add(row);
            }
            _feedback = Label(body, "Feedback", "", .50f, .12f, .98f, .26f, 14);
            _cancel = OwnerDugoutDetailUiFactory.CreateButton(body, "Cancel", "취소", .62f, .01f, .76f, .095f, Close);
            _confirm = OwnerDugoutDetailUiFactory.CreateButton(body, "Apply", "작전 적용", .78f, .01f, .98f, .095f, Apply);
            OwnerUiButtonSkin.Apply(_confirm, OwnerButtonRole.Primary);
            var sequence = new List<Selectable>(_filters); sequence.Add(_previous); sequence.Add(_next); sequence.Add(_confirm); sequence.Add(_cancel);
            for (int i = 0; i < sequence.Count; i++)
                sequence[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = sequence[(i + sequence.Count - 1) % sequence.Count],
                    selectOnLeft = sequence[(i + sequence.Count - 1) % sequence.Count],
                    selectOnDown = sequence[(i + 1) % sequence.Count], selectOnRight = sequence[(i + 1) % sequence.Count] };
            Resize(); RefreshPreview(); _filters[0].Select();
        }

        private void AddFilter(RectTransform parent, string title, string[] choices, int selected,
            float left, float right, float bottom, Action<int> changed)
        {
            Label(parent, title + "Label", title, left, bottom + .075f, right, bottom + .13f, 13);
            var dropdown = OwnerCardFilters.CreateDropdown(parent, title, new List<string>(choices), selected);
            OwnerDugoutDetailUiFactory.Place((RectTransform)dropdown.transform, left, bottom, right, bottom + .075f);
            OwnerDashboardStyle.SetDataSurface(dropdown.GetComponent<Image>(), OwnerDashboardStyle.TableSurface, true);
            OwnerDashboardStyle.ConfigureDataControl(dropdown);
            foreach (var text in dropdown.GetComponentsInChildren<Text>(true))
            { OwnerDashboardStyle.SetDataText(text); text.fontSize = 14; }
            OwnerDashboardStyle.SetDataSurface(dropdown.template.GetComponent<Image>(), OwnerDashboardStyle.TableHeader, true);
            foreach (var toggle in dropdown.template.GetComponentsInChildren<Toggle>(true))
            {
                if (toggle.targetGraphic is Image item) OwnerDashboardStyle.SetDataSurface(item, OwnerDashboardStyle.TableSelected, true);
                if (toggle.graphic != null) toggle.graphic.color = OwnerDashboardStyle.Gold;
            }
            dropdown.onValueChanged.AddListener(i => { changed(i); RefreshPreview(); });
            _filters.Add(dropdown);
        }

        private void RefreshPreview()
        {
            try
            {
                _plans = _preview?.Invoke(_options);
                int changes = 0, empty = 0, cards = 0;
                for (int i = 0; i < _rows.Count; i++)
                {
                    bool visible = _plans != null && i < _plans.Count;
                    _rows[i].gameObject.SetActive(visible);
                    if (!visible) continue;
                    var plan = _plans[i];
                    if (plan.HasChanges) changes++;
                    cards += plan.CardIds.Count;
                    empty += Math.Max(0, _options.CardsPerGame - plan.CardIds.Count);
                    var game = FindGame(plan.GameId);
                    _rows[i].text = $"제 {game?.Round}경기 · {game?.OpponentName} · {(game?.IsHome == true ? "홈" : "원정")}\n" +
                        $"현재  {Names(plan.OriginalIds)}\n{(plan.HasChanges ? "변경" : "유지")}  {Names(plan.CardIds)}";
                }
                _summary.text = $"대상 {_plans?.Count ?? 0}경기 · 변경 {changes}경기 · 배치 {cards}장";
                _planContent.sizeDelta = new Vector2(0, (_plans?.Count ?? 0) * 92);
                _planScroll.verticalNormalizedPosition = 1;
                _confirm.interactable = changes > 0 && _apply != null;
                _confirm.GetComponentInChildren<Text>().text = changes > 0 ? $"{changes}경기 적용" : "변경 없음";
                string policy = _options.Policy == TacticAutoPolicy.Replace
                    ? "대상 경기의 기존 카드를 교체합니다. 후보가 없으면 해제됩니다."
                    : "기존 카드는 유지하고 빈 슬롯만 채웁니다.";
                _feedback.text = (_plans == null || _plans.Count == 0) ? "조건에 맞는 예정 경기가 없습니다. 경기 범위·장소를 바꿔 주세요." :
                    policy + "\n" + (empty > 0 ? $"수량·필터·조합 제한으로 {empty}슬롯을 채울 수 없습니다." : "가까운 경기부터 예약하며 실제 소비는 경기 시작 시 이루어집니다.");
                _feedback.color = empty > 0 ? CareerUiTheme.Warning : OwnerDashboardStyle.Muted;
            }
            catch (Exception error) { ShowError(error); }
        }

        private OwnerTacticScheduleRowSnapshot FindGame(int id)
        {
            foreach (var row in _snapshot.ScheduleRows) if (row.GameId == id) return row;
            return null;
        }

        private string Names(IReadOnlyList<string> ids)
        {
            if (ids.Count == 0) return "미설정";
            var names = new List<string>();
            foreach (string id in ids)
            {
                string name = "작전 카드";
                foreach (var card in _snapshot.Cards) if (card.Id == id) { name = card.Name; break; }
                names.Add(name);
            }
            return string.Join(" / ", names);
        }

        private void Apply()
        {
            if (_isApplying || !_confirm.interactable) return;
            _isApplying = true;
            try { _apply(_plans); Close(); }
            catch (Exception error) { ShowError(error); }
            finally { _isApplying = false; }
        }

        private void ShowError(Exception error)
        {
            _confirm.interactable = false;
            _feedback.text = error is InvalidOperationException || error is ArgumentException ? error.Message : "자동 설정을 완료하지 못했습니다. 팝업을 다시 열어 주세요.";
            _feedback.color = CareerUiTheme.Error;
            if (!(error is InvalidOperationException) && !(error is ArgumentException)) Debug.LogException(error);
        }

        private void LateUpdate() => Resize();
        private void ScrollPlans(int direction)
        {
            float range = _planContent.rect.height - _planScroll.viewport.rect.height;
            if (range <= 0) return;
            _planScroll.verticalNormalizedPosition = Mathf.Clamp01(_planScroll.verticalNormalizedPosition + direction * 184f / range);
        }
        private void Resize()
        {
            var bounds = ((RectTransform)transform).rect;
            var size = new Vector2(Mathf.Min(1120, bounds.width - 32), Mathf.Min(660, bounds.height - 24));
            if (_frame.sizeDelta != size) _frame.sizeDelta = size;
        }

        /// <summary>드롭다운을 먼저 닫고 이후 취소 시 진입 버튼 포커스를 복원한다.</summary>
        public bool TryHandleCancel()
        {
            foreach (var filter in _filters)
                if (filter.transform.Find("Dropdown List") != null) { filter.Hide(); return true; }
            Close(); return true;
        }
        public void OnCancel(BaseEventData eventData) { TryHandleCancel(); eventData.Use(); }
        public void Close()
        {
            gameObject.SetActive(false);
            if (_previousFocus != null && _previousFocus.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(_previousFocus);
            Destroy(gameObject);
        }
        private static Text Label(Transform parent, string name, string value, float left, float bottom, float right, float top, int size)
        {
            var text = OwnerDugoutDetailUiFactory.CreateLabel(parent, name, value, left, bottom, right, top, size);
            OwnerDashboardStyle.SetDataText(text);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
    }
}
