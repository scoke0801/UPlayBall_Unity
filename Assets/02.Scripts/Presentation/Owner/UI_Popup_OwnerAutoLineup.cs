using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>연도·구단 덱을 선택하고 취소 가능한 자동 편성을 기존 오더 미리보기로 전달한다.</summary>
    public sealed class UI_Popup_OwnerAutoLineup : MonoBehaviour, IUiCancelHandler, ICancelHandler
    {
        private RectTransform _frame;
        private Dropdown _year, _team;
        private Button _confirm, _cancel;
        private Text _summary, _feedback;
        private IReadOnlyList<OwnerCollectionCardSnapshot> _cards;
        private readonly List<int> _years = new List<int>();
        private readonly List<KeyValuePair<string, string>> _teams = new List<KeyValuePair<string, string>>();
        private Func<int, string, CancellationToken, Task<string>> _apply;
        private Action<string> _completed;
        private CancellationTokenSource _cancellation;
        private GameObject _previousFocus;

        /// <summary>현재 보유 카드에서 실제 존재하는 연도·구단 조합만 선택하게 한다.</summary>
        public static UI_Popup_OwnerAutoLineup Show(RectTransform parent,
            IReadOnlyList<OwnerCollectionCardSnapshot> cards,
            Func<int, string, CancellationToken, Task<string>> apply, Action<string> completed)
        {
            var root = OwnerWorkspaceUiFactory.CreateRoot(parent, nameof(UI_Popup_OwnerAutoLineup), false);
            var shade = root.gameObject.AddComponent<Image>();
            Color dim = CareerUiTheme.Background;
            dim.a = .85f;
            shade.color = dim;
            var view = root.gameObject.AddComponent<UI_Popup_OwnerAutoLineup>();
            view._cards = cards; view._apply = apply; view._completed = completed;
            view._previousFocus = EventSystem.current?.currentSelectedGameObject;
            view.Build();
            return view;
        }

        private void Build()
        {
            var panel = OwnerWorkspaceUiFactory.CreatePanel(transform, "AutoLineupPanel", "자동 배치 · 연도 구단 덱");
            _frame = panel.Root;
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(.5f, .5f);
            _frame.anchoredPosition = Vector2.zero;
            _frame.GetComponent<Image>().color = CareerUiTheme.RosterSurface;
            var header = _frame.Find("HeaderSlot").GetComponent<Text>();
            header.color = CareerUiTheme.RosterText;
            header.gameObject.AddComponent<CareerUiPreserveTextColor>();
            RectTransform body = panel.Content;
            Label(body, "Intro", "원하는 시즌의 구단으로 25인 오더를 구성하세요.", .02f, .86f, .98f, .98f, 18);
            Label(body, "YearLabel", "연도", .02f, .77f, .33f, .85f, 13);
            Label(body, "TeamLabel", "구단", .37f, .77f, .98f, .85f, 13);
            foreach (var card in _cards)
                if (!_years.Contains(card.OriginYear) && !string.IsNullOrWhiteSpace(card.OriginFranchiseId)) _years.Add(card.OriginYear);
            _years.Sort((a, b) => b.CompareTo(a));
            var labels = new List<string>();
            foreach (int year in _years) labels.Add(year + "년");
            if (labels.Count == 0) labels.Add("보유 카드 없음");
            _year = OwnerCardFilters.CreateDropdown(body, "AutoYear", labels, 0);
            Place(_year.transform, .02f, .64f, .33f, .77f);
            _team = OwnerCardFilters.CreateDropdown(body, "AutoTeam", new List<string> { "구단 선택" }, 0);
            Place(_team.transform, .37f, .64f, .98f, .77f);
            _summary = Label(body, "DeckSummary", "", .02f, .49f, .98f, .62f, 18);
            _summary.color = CareerUiTheme.RosterAccent;
            Label(body, "Rules", "선택 덱 우선 · 같은 계보의 특수 카드 포함\n빈 포지션은 고 코스트로 보완 · 동일 선수 중복 제외\n선발/구원 구분 · 백업 포수 확보\n커리어하이 + 레전드: 합계 " + OwnerSpecialCardRosterRule.MaxTotalCount +
                "명 / 타자·투수 각각 " + OwnerSpecialCardRosterRule.MaxHitterCount + "명까지", .02f, .25f, .98f, .48f, 14);
            _feedback = Label(body, "Feedback", "변경안으로 배치한 뒤 선수 오더에서 확인하고 저장하세요.", .02f, .12f, .98f, .24f, 14);
            _cancel = OwnerWorkspaceUiFactory.CreateButton(body, "CancelAutoLineup", "취소", Close);
            Place(_cancel.transform, .50f, .01f, .68f, .115f);
            _confirm = OwnerWorkspaceUiFactory.CreateButton(body, "ConfirmAutoLineup", "자동 배치", Apply);
            Place(_confirm.transform, .70f, .01f, .98f, .115f);
            OwnerUiButtonSkin.SetBoardStyle(_cancel);
            OwnerUiButtonSkin.SetBoardStyle(_confirm);
            OwnerUiButtonSkin.Apply(_confirm, OwnerButtonRole.Primary);
            _year.onValueChanged.AddListener(_ => RefreshTeams());
            _team.onValueChanged.AddListener(_ => RefreshSummary());
            RefreshTeams(); LinkFocus(); Resize();
            if (_year.interactable) _year.Select(); else _cancel.Select();
        }

        private void RefreshTeams()
        {
            string previous = _teams.Count > _team.value ? _teams[_team.value].Key : null;
            _teams.Clear();
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            if (_years.Count > 0)
                foreach (var card in _cards)
                    if (card.OriginYear == _years[_year.value] && !string.IsNullOrWhiteSpace(card.OriginFranchiseId))
                        labels[card.OriginFranchiseId] = card.TeamDisplayName;
            foreach (var entry in labels) _teams.Add(entry);
            _teams.Sort((a, b) => string.CompareOrdinal(a.Value, b.Value));
            var names = new List<string>();
            int selected = 0;
            for (int i = 0; i < _teams.Count; i++)
            {
                names.Add(_teams[i].Value);
                if (_teams[i].Key == previous) selected = i;
            }
            if (names.Count == 0) names.Add("선택 가능한 구단 없음");
            _team.ClearOptions(); _team.AddOptions(names); _team.SetValueWithoutNotify(selected);
            _team.template.sizeDelta = new Vector2(0, Mathf.Min(7, names.Count) * 28 + 8);
            RefreshSummary();
        }

        private void RefreshSummary()
        {
            _feedback.color = CareerUiTheme.RosterText;
            bool available = _years.Count > 0 && _teams.Count > 0 && _apply != null;
            _year.interactable = _years.Count > 0;
            _team.interactable = _teams.Count > 0;
            _confirm.interactable = available;
            if (!available)
            {
                _summary.text = "자동 배치할 덱이 없습니다.";
                _feedback.text = "선수 카드를 영입한 뒤 다시 열어 주세요.";
                return;
            }
            var hitters = new HashSet<string>(StringComparer.Ordinal);
            var pitchers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var card in _cards)
                if (card.OriginYear == _years[_year.value] && card.OriginFranchiseId == _teams[_team.value].Key)
                    (card.Position == PlayerPosition.StartingPitcher || card.Position == PlayerPosition.ReliefPitcher ? pitchers : hitters)
                        .Add(string.IsNullOrWhiteSpace(card.PlayerPersonId) ? card.CardId : card.PlayerPersonId);
            _summary.text = "해당 시즌 보유 · 타자 " + hitters.Count + "명 / 투수 " + pitchers.Count + "명";
            _feedback.text = "기존 변경안을 새 자동 배치로 교체합니다. 배치 저장 전까지 되돌릴 수 있습니다.";
        }

        private async void Apply()
        {
            if (_cancellation != null || !_confirm.interactable) return;
            var cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            _year.interactable = _team.interactable = _confirm.interactable = false;
            _feedback.text = "포지션과 특수 카드 제한에 맞는 조합을 찾고 있습니다…";
            _cancel.Select();
            try
            {
                string message = await _apply(_years[_year.value], _teams[_team.value].Key, cancellation.Token);
                if (this == null || cancellation.IsCancellationRequested) return;
                _completed?.Invoke(message);
                Close();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (this == null || cancellation.IsCancellationRequested) return;
                RefreshSummary();
                _feedback.text = error is InvalidOperationException ? error.Message : "자동 배치에 실패했습니다. 다시 시도해 주세요.";
                _feedback.color = CareerUiTheme.Warning;
                _confirm.Select();
                if (!(error is InvalidOperationException)) Debug.LogException(error);
            }
            finally
            {
                if (ReferenceEquals(_cancellation, cancellation)) _cancellation = null;
                cancellation.Dispose();
            }
        }

        private void LinkFocus()
        {
            Selectable[] sequence = { _year, _team, _confirm, _cancel };
            for (int i = 0; i < sequence.Length; i++)
            {
                var navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = sequence[(i + sequence.Length - 1) % sequence.Length],
                    selectOnUp = sequence[(i + sequence.Length - 1) % sequence.Length],
                    selectOnRight = sequence[(i + 1) % sequence.Length], selectOnDown = sequence[(i + 1) % sequence.Length] };
                sequence[i].navigation = navigation;
            }
        }

        private void LateUpdate() => Resize();
        private void Resize()
        {
            var bounds = ((RectTransform)transform).rect;
            _frame.sizeDelta = new Vector2(Mathf.Min(760, bounds.width - 32), Mathf.Min(440, bounds.height - 24));
        }

        /// <summary>계산 중 취소해도 원래 오더와 기존 변경안을 보존한다.</summary>
        public bool TryHandleCancel()
        {
            if (_year.transform.Find("Dropdown List") != null) { _year.Hide(); return true; }
            if (_team.transform.Find("Dropdown List") != null) { _team.Hide(); return true; }
            Close(); return true;
        }
        public void OnCancel(BaseEventData eventData) { TryHandleCancel(); eventData.Use(); }
        public void Close()
        {
            _cancellation?.Cancel();
            gameObject.SetActive(false);
            if (_previousFocus != null && _previousFocus.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(_previousFocus);
            Destroy(gameObject);
        }
        private void OnDestroy() => _cancellation?.Cancel();

        private static Text Label(Transform parent, string name, string value, float left, float bottom,
            float right, float top, int size)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size, FontStyle.Normal,
                TextAnchor.MiddleLeft, CareerUiTheme.RosterText);
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            Place(text.transform, left, bottom, right, top);
            return text;
        }
        private static void Place(Transform target, float left, float bottom, float right, float top) =>
            OwnerRuntimeUiFactory.SetAnchors((RectTransform)target, new Vector2(left, bottom), new Vector2(right, top), Vector2.zero, Vector2.zero);
    }
}
