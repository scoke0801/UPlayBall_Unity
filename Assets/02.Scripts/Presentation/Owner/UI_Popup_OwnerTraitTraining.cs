using System;
using System.Collections;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>대상·파트너·예상 결과를 한 작업면에서 비교하는 구단주 특성훈련 화면이다.</summary>
    public sealed partial class UI_Popup_OwnerTraitTraining : MonoBehaviour, IUiCancelHandler, ICancelHandler
    {
        private OwnerModeManager _manager;
        private OwnerCollectionSnapshot _collection;
        private RectTransform _frame, _main, _candidates, _help;
        private Text _wallet, _current, _progress, _preview, _cost, _feedback, _slots, _guide, _helpRewards;
        private Button _animationPreference, _confirmationPreference, _helpBegin;
        private Image _progressFill;
        private Button _train, _change, _reroll, _decide, _close, _chooseTarget, _cancelConfirm;
        private PlayerMiniCardView _targetCard;
        private readonly List<string> _partners = new List<string>();
        private readonly Button[] _candidateButtons = new Button[3];
        private string _cardId = "";
        private CardTraitKind _chosen;
        private bool _isSubmitting, _isConfirming, _hasPreview;
        private GameObject _previousFocus;
        private OwnerTraitTrainingPreview _trainingPreview;
        private Action _schedule;
        private Action _closed;
        private int _seenSeason, _seenRevision;
        private Coroutine _animation;
        public bool IsVisible => gameObject.activeSelf;

        public static UI_Popup_OwnerTraitTraining Show(Transform parent, OwnerModeManager manager,
            string cardId = null, Action schedule = null, Action closed = null)
        {
            if (manager?.Runtime == null) throw new InvalidOperationException("구단주 진행 상태를 불러오지 못했습니다.");
            var root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerTraitTraining), parent);
            OwnerRuntimeUiFactory.Stretch(root);
            var shade = root.gameObject.AddComponent<Image>(); shade.color = new Color(0, 0, 0, .94f);
            var view = root.gameObject.AddComponent<UI_Popup_OwnerTraitTraining>();
            view._manager = manager; view._cardId = cardId ?? ""; view._schedule = schedule;
            view._closed = closed;
            view._previousFocus = EventSystem.current?.currentSelectedGameObject;
            view.Reload(); view.Build();
            try { manager.PrepareTraitTraining(); }
            catch (Exception) { view._feedback.text = "오프시즌 보상을 저장하지 못했습니다. 다시 열어 주세요."; }
            view.Reload(); view.Refresh(); view._chooseTarget.Select();
            // 진입 즉시 실제 선수와 파트너를 비교하며 안내는 도움말에서 연다.
            return view;
        }

        private void Build()
        {
            _frame = OwnerDugoutDetailUiFactory.CreatePanel(transform, "TraitOffice", .035f, .045f, .965f, .955f);
            UIOwnerFrontOfficePanel.Apply(_frame, "ManagerReport");
            Label(_frame, "Title", "특성훈련", .025f, .91f, .25f, .975f, 27, true);
            _wallet = Label(_frame, "Wallet", "", .28f, .915f, .78f, .975f, 18, true);
            _wallet.alignment = TextAnchor.MiddleRight;
            Button(_frame, "Help", "도움말", .80f, .92f, .885f, .975f, ShowHelp);
            _close = Button(_frame, "Close", "닫기", .895f, .92f, .975f, .975f, Close);
            _main = Rect(_frame, "Training", .025f, .18f, .975f, .895f);
            var left = Surface(_main, "TargetSurface", 0, 0, .365f, 1);
            var right = Surface(_main, "PartnerSurface", .385f, 0, 1, 1);
            OwnerDashboardStyle.ApplyInset(left.GetComponent<Image>());
            OwnerDashboardStyle.ApplyInset(right.GetComponent<Image>());
            Label(left, "Step", "01  훈련 대상", .05f, .9f, .68f, .98f, 19, true);
            _chooseTarget = Button(left, "ChooseTarget", "선수 변경", .68f, .91f, .95f, .98f, OpenPicker);
            _targetCard = PlayerMiniCardView.CreateRuntime(left, "TargetCard");
            Place((RectTransform)_targetCard.transform, .28f, .36f, .72f, .89f);
            _targetCard.Selected += _ => OpenCard();
            _targetCard.DetailRequested += _ => OpenCard();
            _current = Label(left, "CurrentTrait", "훈련할 선수를 선택하세요.", .05f, .19f, .95f, .35f, 18, true);
            _progress = Label(left, "Experience", "", .05f, .105f, .95f, .18f, 16);
            var bar = Surface(left, "ExperienceTrack", .05f, .08f, .95f, .095f);
            _progressFill = Surface(bar, "ExperienceFill", 0, 0, 0, 1).GetComponent<Image>();
            OwnerDashboardStyle.SetDataSurface(_progressFill, OwnerDashboardStyle.Gold);
            _change = Button(left, "ChangeTrait", "특성 변경", .05f, .01f, .57f, .07f, RequestChange);
            Button(left, "InspectCard", "카드 확인", .61f, .01f, .95f, .07f, OpenCard);
            Label(right, "Step", "02  훈련 파트너", .035f, .9f, .64f, .98f, 19, true);
            Button(right, "Schedule", "일정 보기", .73f, .91f, .965f, .98f, () => { Close(); _schedule?.Invoke(); });
            Label(right, "PartnerSafety", "선수는 사라지지 않습니다 · 파트너 횟수만 사용", .035f, .82f, .96f, .90f, 16);
            BuildPartnerList(right);
            _slots = Label(right, "SelectedPartners", "", .035f, .23f, .96f, .34f, 17, true);
            _preview = Label(right, "Preview", "훈련 파트너를 선택하세요.", .035f, .04f, .96f, .21f, 20, true);
            var actionBar = Rect(_frame, "TrainingActionBar", .025f, .025f, .975f, .165f);
            OwnerDashboardStyle.ApplyActionBar(actionBar);
            _cost = Label(_frame, "Cost", "", .025f, .105f, .68f, .165f, 18, true);
            _cost.alignment = TextAnchor.MiddleRight;
            _feedback = Label(_frame, "Feedback", "", .025f, .025f, .68f, .10f, 16);
            _train = Button(_frame, "Train", "훈련하기", .79f, .04f, .975f, .15f, Train);
            OwnerUiButtonSkin.Apply(_train, OwnerButtonRole.Primary);
            _cancelConfirm = Button(_frame, "CancelConfirm", "취소", .695f, .04f, .775f, .15f,
                () => { _isConfirming = false; Refresh(); _train.Select(); });
            BuildCandidates(right); BuildPicker(); BuildHelp();
        }

        private void Reload() => _collection = new OwnerModeRuntimeSnapshotFactory().CreateCollectionSummary(_manager);

        private OwnedPlayerCardState Card() => _manager.Runtime.TryGetOwnedCard(_cardId, out var card) ? card : null;
        private OwnerCollectionCardSnapshot Snapshot(string id)
        {
            foreach (var card in _collection.Cards) if (card.CardId == id) return card;
            return null;
        }
        private bool CanTrainNow => OwnerScheduleGateService.GetPhase(_manager.Runtime) == OwnerSeasonPhase.Offseason;

        private void Refresh()
        {
            var runtime = _manager.Runtime; var balance = _manager.TraitBalance; var card = Card();
            _wallet.text = $"특성 포인트 {runtime.PlayerGrowth.Traits.points:N0} TP   ·   구단 자금 {OwnerMoneyFormatter.Format(runtime.Economy.Money)}";
            _seenSeason = runtime.ManagerMode.LiveSeason.SeasonNumber; _seenRevision = card?.Trait.revision ?? 0;
            _targetCard.gameObject.SetActive(card != null && Snapshot(_cardId) != null);
            if (_targetCard.gameObject.activeSelf) _targetCard.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(Snapshot(_cardId), false));
            _current.text = card == null ? "훈련할 선수를 선택하세요." : OwnerCardGrowthBadgeBuilder.DescribeTrait(card.Trait, balance);
            int total = card?.Trait.experience ?? 0;
            int nextRank = Math.Min(3, (int)balance.GetRank(total));
            _progress.text = card == null ? "선수 선택 후 성장 목표가 표시됩니다." : card.Trait.rank == CardTraitRank.S
                ? $"S등급 · {total:N0} 경험치 · 최고 등급" : $"{(card.Trait.rank == CardTraitRank.None ? "미보유" : card.Trait.rank + "등급")} → {(CardTraitRank)(nextRank + 1)}   {total:N0} / {balance.experience[nextRank]:N0}";
            _progressFill.rectTransform.anchorMax = new Vector2(card == null ? 0 : Mathf.Clamp01((float)total / balance.experience[nextRank]), 1);
            bool pending = card != null && card.Trait.HasCandidates;
            _candidates.gameObject.SetActive(pending);
            _partnerScroll.gameObject.SetActive(!pending); _partnerSearch.gameObject.SetActive(!pending);
            _change.interactable = CanTrainNow && card != null && card.Trait.trait != CardTraitKind.None && !pending;
            SetButtonText(_change, card?.Trait.freeChangeSeason == _seenSeason ? $"특성 변경 · {balance.changeCost} TP" : "특성 변경 · 이번 시즌 무료");
            _hasPreview = false; _train.interactable = false; _cost.text = "";
            _feedback.text = "선택한 파트너는 사라지지 않습니다.";
            _slots.text = "선택 파트너 " + _partners.Count + " / " + balance.slots[nextRank];
            foreach (string id in _partners) _slots.text += "  ·  " + Snapshot(id)?.DisplayName;
            _preview.text = card == null ? "훈련할 선수를 선택하세요." : pending ? "03  후보를 비교하고 하나를 확정하세요." : "파트너 선택 즉시 비용과 성장 결과를 확인할 수 있습니다.";
            RefreshPartners();
            try
            {
                if (card != null && !pending)
                {
                    _trainingPreview = OwnerTraitTrainingService.Preview(runtime, _cardId, _partners, balance); _hasPreview = true;
                    _preview.text = $"경험치 +{_trainingPreview.Experience:N0}   →   {_trainingPreview.TotalExperience:N0}\n"
                        + (_trainingPreview.Rank > card.Trait.rank ? $"{_trainingPreview.Rank}등급 도달 · " + (card.Trait.trait == CardTraitKind.None ? "특성 후보 3개 선택" : "확정 승급") : "경험치는 다음 훈련에도 그대로 유지됩니다.");
                    _cost.text = $"이번 훈련  {_trainingPreview.Points:N0} TP  +  {OwnerMoneyFormatter.Format(_trainingPreview.Money)}";
                    _train.interactable = !_isSubmitting && runtime.PlayerGrowth.Traits.points >= _trainingPreview.Points && runtime.Economy.Money >= _trainingPreview.Money;
                    if (!_train.interactable) _feedback.text = runtime.PlayerGrowth.Traits.points < _trainingPreview.Points ? "특성 훈련 포인트가 부족합니다." : "구단 자금이 부족합니다.";
                }
            }
            catch (InvalidOperationException e) { _feedback.text = e.Message; }
            if (!CanTrainNow) _feedback.text = "특성훈련은 오프시즌에 진행할 수 있습니다. 모든 조의 포스트시즌 종료 후 이용하세요.";
            if (_isConfirming && _train.interactable) _feedback.text = "표시된 비용과 파트너 횟수를 사용합니다. 선수는 사라지지 않습니다.";
            _cancelConfirm.gameObject.SetActive(_isConfirming);
            SetButtonText(_train, _isConfirming ? "훈련 확정" : "훈련하기");
            if (pending) RefreshCandidates(card);
            RefreshNavigation();
        }

        private void Train()
        {
            if (!_hasPreview || !_train.interactable || _isSubmitting) return;
            if (!_isConfirming && !_manager.Runtime.PlayerGrowth.Traits.skipConfirmation) { _isConfirming = true; Refresh(); return; }
            int before = Card().Trait.experience;
            string[] partners = _partners.ToArray();
            Execute(() => _manager.TrainTrait(_cardId, partners, _seenSeason, _seenRevision), "훈련을 완료하고 저장했습니다.");
            if (Card() != null && Card().Trait.experience > before && !_manager.Runtime.PlayerGrowth.Traits.skipAnimation)
            {
                if (_animation != null) StopCoroutine(_animation);
                _animation = StartCoroutine(AnimateProgress(before, Card().Trait.experience));
            }
        }

        private IEnumerator AnimateProgress(int before, int after)
        {
            int denominator = _manager.TraitBalance.experience[Math.Min(3, (int)_manager.TraitBalance.GetRank(after))];
            for (float elapsed = 0; elapsed < .5f; elapsed += Time.unscaledDeltaTime)
            { _progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(Mathf.Lerp(before, after, elapsed / .5f) / denominator), 1); yield return null; }
            _progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)after / denominator), 1); _animation = null;
        }

        private void Execute(Action action, string message)
        {
            if (_isSubmitting) return;
            _isSubmitting = true;
            try { action(); _partners.Clear(); _chosen = CardTraitKind.None; }
            catch (InvalidOperationException e) { message = e.Message; }
            catch (Exception) { message = "저장하지 못했습니다. 변경 전 상태에서 다시 시도해 주세요."; }
            finally { _isSubmitting = false; _isConfirming = false; }
            Reload(); Refresh(); _feedback.text = message;
            if (Card()?.Trait.HasCandidates == true) _candidateButtons[0].Select(); else _chooseTarget.Select();
        }

        private void RequestChange() => Execute(() => _manager.ChangeTrait(_cardId, _seenSeason, _seenRevision), "등급과 경험치는 유지됩니다. 교체할 특성을 선택하세요.");

        private void BuildCandidates(RectTransform right)
        {
            _candidates = Surface(right, "CandidateSelection", .015f, .015f, .985f, .985f);
            _candidates.GetComponent<Image>().raycastTarget = true;
            Label(_candidates, "Title", "03  새로운 특성 선택", .035f, .83f, .96f, .96f, 22, true);
            Label(_candidates, "Persistence", "후보는 저장됩니다. 창을 닫아도 같은 후보로 이어집니다.", .035f, .73f, .96f, .83f, 16);
            for (int i = 0; i < 3; i++)
            {
                int index = i; float top = .70f - i * .18f;
                _candidateButtons[i] = Button(_candidates, "Candidate" + i, "후보", .035f, top - .155f, .965f, top,
                    () => { _chosen = Card().Trait.candidates[index]; RefreshCandidates(Card()); RefreshNavigation(); });
            }
            _reroll = Button(_candidates, "Reroll", "재추첨", .035f, .02f, .47f, .13f,
                () => Execute(() => _manager.RerollTrait(_cardId, _seenSeason, _seenRevision), "새 후보를 저장했습니다."));
            _decide = Button(_candidates, "Decide", "이 특성으로 결정", .51f, .02f, .965f, .13f,
                () => Execute(() => _manager.ChooseTrait(_cardId, _chosen, _seenSeason, _seenRevision), "특성을 확정했습니다. 카드 오른쪽 배지를 확인하세요."));
            OwnerUiButtonSkin.Apply(_decide, OwnerButtonRole.Primary);
        }

        private void RefreshCandidates(OwnedPlayerCardState card)
        {
            var balance = _manager.TraitBalance;
            for (int i = 0; i < 3; i++)
            {
                var kind = card.Trait.candidates[i]; var definition = balance.Get(kind);
                SetButtonText(_candidateButtons[i], (_chosen == kind ? "선택  ·  " : "") + OwnerCardGrowthBadgeBuilder.DescribeEffect(definition, balance.GetRank(card.Trait.experience), balance));
                OwnerDashboardStyle.SetDataRow(_candidateButtons[i], _chosen == kind, i % 2 == 0 ? OwnerDashboardStyle.TableSurface : OwnerDashboardStyle.TableAlternate);
                var label = _candidateButtons[i].GetComponentInChildren<Text>(); OwnerDashboardStyle.SetDataText(label, true); label.alignment = TextAnchor.MiddleLeft;
                _candidateButtons[i].interactable = CanTrainNow;
            }
            int cost = card.Trait.freeRerollSeason == _seenSeason ? balance.rerollCost : 0;
            SetButtonText(_reroll, cost == 0 ? "무료 재추첨 · 1회" : $"재추첨 · {cost} TP");
            _reroll.interactable = CanTrainNow && _manager.Runtime.PlayerGrowth.Traits.points >= cost;
            _decide.interactable = CanTrainNow && _chosen != CardTraitKind.None;
            _cost.text = "후보 결정에는 추가 비용이 없습니다.";
        }

        private void OpenCard()
        {
            var card = Snapshot(_cardId); if (card == null) return;
            UI_Popup_OwnerPlayerCard.Show(_targetCard.transform, new[] { card }, 0);
        }

        private void BuildHelp()
        {
            _help = Surface(_frame, "HelpSurface", .025f, .025f, .975f, .895f);
            OwnerDashboardStyle.ApplyInset(_help.GetComponent<Image>(), true);
            Label(_help, "Heading", "선수의 강점을 완성하는 특성훈련", .035f, .88f, .965f, .97f, 25, true);
            Label(_help, "Subtitle", "오프시즌에 동료와 훈련하고, 경기에서 발휘할 특성을 직접 선택하세요.",
                .035f, .80f, .965f, .88f, 17);
            BuildHelpStep("Target", "01", "훈련할 선수", "성장시킬 선수와 현재 특성을 확인합니다.", .035f, .335f);
            BuildHelpStep("Partner", "02", "함께할 파트너", "예상 경험치와 비용을 비교합니다. 파트너 카드는 소모되지 않습니다.", .35f, .65f);
            BuildHelpStep("Trait", "03", "새로운 특성", "최초 획득 시 세 후보 중 하나를 선택합니다. 결과는 카드 배지에 남습니다.", .665f, .965f);
            var ranks = Surface(_help, "RankProgression", .035f, .35f, .61f, .57f);
            OwnerDashboardStyle.ApplyInset(ranks.GetComponent<Image>());
            Label(ranks, "Title", "누적 경험치로 성장하는 특성", .035f, .69f, .965f, .95f, 18, true);
            for (int i = 0; i < 4; i++)
            {
                float left = .035f + i * .24f;
                Label(ranks, "Rank" + i, ((CardTraitRank)(i + 1)).ToString(), left, .34f, left + .21f, .67f, 24, true).color = OwnerDashboardStyle.Gold;
                Label(ranks, "Experience" + i, $"{_manager.TraitBalance.experience[i]:N0} 경험치", left, .06f, left + .21f, .34f, 16);
            }
            var rewards = Surface(_help, "Rewards", .63f, .35f, .965f, .57f);
            OwnerDashboardStyle.ApplyInset(rewards.GetComponent<Image>());
            Label(rewards, "Title", "훈련 포인트 획득", .05f, .69f, .95f, .95f, 18, true);
            _helpRewards = Label(rewards, "Details", "", .05f, .06f, .95f, .68f, 16);
            _guide = Label(_help, "Guide", "", .035f, .235f, .965f, .33f, 16);
            _animationPreference = Button(_help, "AnimationPreference", "결과 연출", .035f, .145f, .32f, .22f,
                () => SetPreferences(!_manager.Runtime.PlayerGrowth.Traits.skipAnimation, _manager.Runtime.PlayerGrowth.Traits.skipConfirmation));
            _confirmationPreference = Button(_help, "ConfirmationPreference", "훈련 전 확인", .335f, .145f, .62f, .22f,
                () => SetPreferences(_manager.Runtime.PlayerGrowth.Traits.skipAnimation, !_manager.Runtime.PlayerGrowth.Traits.skipConfirmation));
            Button(_help, "Schedule", "훈련 일정 보기", .035f, .035f, .26f, .115f, () => { Close(); _schedule?.Invoke(); });
            _helpBegin = Button(_help, "Begin", "선수 훈련으로 돌아가기", .665f, .035f, .965f, .115f, () => {
                _help.gameObject.SetActive(false); RefreshNavigation(); _chooseTarget.Select(); });
            OwnerUiButtonSkin.Apply(_helpBegin, OwnerButtonRole.Primary);
            _help.gameObject.SetActive(false);
        }

        private void BuildHelpStep(string name, string number, string title, string description, float left, float right)
        {
            var step = Surface(_help, name, left, .60f, right, .78f);
            OwnerDashboardStyle.ApplyInset(step.GetComponent<Image>());
            Label(step, "Number", number, .05f, .57f, .20f, .92f, 24, true).color = OwnerDashboardStyle.Gold;
            Label(step, "Title", title, .23f, .57f, .95f, .92f, 20, true);
            Label(step, "Description", description, .05f, .07f, .95f, .55f, 16);
        }

        private void SetPreferences(bool skipAnimation, bool skipConfirmation)
        {
            var focus = EventSystem.current?.currentSelectedGameObject;
            Execute(() => _manager.SetTraitPreferences(true, skipAnimation, skipConfirmation), "훈련 표시 설정을 저장했습니다.");
            ShowHelp();
            if (focus != null && focus.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(focus);
        }
        private void ShowHelp()
        {
            var state = _manager.Runtime.PlayerGrowth.Traits;
            var balance = _manager.TraitBalance;
            _guide.text = $"파트너는 오프시즌마다 {balance.partnerUses}회 참여 · 시즌당 재추첨 1회 무료\n특성을 변경해도 등급과 누적 경험치는 유지됩니다.";
            _helpRewards.text = $"경기당 {balance.gameReward} TP · 오프시즌 {balance.offseasonReward} TP\n시즌 승률 {balance.seasonGoalWinPercent}% 달성 시 {balance.seasonGoalReward} TP 추가";
            SetButtonText(_animationPreference, "결과 연출  ·  " + (state.skipAnimation ? "꺼짐" : "켜짐"));
            SetButtonText(_confirmationPreference, "훈련 전 확인  ·  " + (state.skipConfirmation ? "꺼짐" : "켜짐"));
            _help.gameObject.SetActive(true); _help.SetAsLastSibling(); _helpBegin.Select();
            RefreshNavigation();
        }
        public bool TryHandleCancel()
        {
            foreach (var dropdown in GetComponentsInChildren<Dropdown>())
                if (dropdown.transform.Find("Dropdown List") != null) { dropdown.Hide(); dropdown.Select(); return true; }
            if (_help.gameObject.activeSelf) { _help.gameObject.SetActive(false); RefreshNavigation(); _chooseTarget.Select(); return true; }
            if (_picker.gameObject.activeSelf) { ClosePicker(); return true; }
            if (_isConfirming) { _isConfirming = false; Refresh(); _train.Select(); return true; }
            Close(); return true;
        }
        public void OnCancel(BaseEventData eventData) { TryHandleCancel(); eventData.Use(); }
        public void Close()
        {
            if (_isSubmitting) return;
            gameObject.SetActive(false);
            if (_previousFocus != null && _previousFocus.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(_previousFocus);
            else _closed?.Invoke();
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        private static RectTransform Rect(Transform parent, string name, float l, float b, float r, float t) => OwnerDugoutDetailUiFactory.CreateRect(parent, name, l, b, r, t);
        private static void Place(RectTransform rect, float l, float b, float r, float t) { rect.anchorMin = new Vector2(l,b); rect.anchorMax = new Vector2(r,t); rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static RectTransform Surface(Transform parent, string name, float l, float b, float r, float t)
        { var rect = Rect(parent, name, l,b,r,t); OwnerDashboardStyle.SetDataSurface(rect.gameObject.AddComponent<Image>(), OwnerDashboardStyle.TableSurface); return rect; }
        private static Text Label(Transform parent, string name, string value, float l, float b, float r, float t, int size, bool primary = false)
        { var text = OwnerDugoutDetailUiFactory.CreateLabel(parent,name,value,l,b,r,t,size,FontStyle.Normal,TextAnchor.MiddleLeft);
            OwnerDashboardStyle.SetDataText(text, primary); text.horizontalOverflow = HorizontalWrapMode.Wrap; return text; }
        private static Button Button(Transform parent, string name, string value, float l, float b, float r, float t, Action action)
        { var button = OwnerDugoutDetailUiFactory.CreateButton(parent,name,value,l,b,r,t,action);
            OwnerDashboardStyle.SetTypography(button.GetComponentInChildren<Text>(), true); return button; }
        private static void SetButtonText(Button button, string value) => button.GetComponentInChildren<Text>().text = value;
    }
}
