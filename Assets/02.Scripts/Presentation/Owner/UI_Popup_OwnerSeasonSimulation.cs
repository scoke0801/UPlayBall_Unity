using System;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>구단주 정규시즌 자동 진행의 현재 라운드·대진·처리량을 Owner Skin으로 표시한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Popup_OwnerSeasonSimulation : MonoBehaviour
    {
        private const float ProgressAnimationSmoothTime = 0.18f;
        private const float ProgressAnimationSnapThreshold = 0.0005f;

        private Image _progressFill;
        private Text _roundText;
        private Text _matchupText;
        private Text _leagueProgressText;
        private Text _playerProgressText;
        private Text _recordText;
        private Text _titleText;
        private Text _stateText;
        private float _displayedProgress;
        private float _targetProgress;
        private float _progressVelocity;
        private bool _hasProgressValue;

        public event Action StopRequested;

        public static UI_Popup_OwnerSeasonSimulation CreateRuntime(RectTransform popupHost)
        {
            if (popupHost == null) throw new ArgumentNullException(nameof(popupHost));
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerSeasonSimulation), popupHost);
            OwnerRuntimeUiFactory.Stretch(root);
            return root.gameObject.AddComponent<UI_Popup_OwnerSeasonSimulation>();
        }

        public void Bind(
            ManagerRegularSeasonSimulationProgress progress,
            Func<string, string> teamDisplayNameResolver)
        {
            if (teamDisplayNameResolver == null)
                throw new ArgumentNullException(nameof(teamDisplayNameResolver));

            bool isCompletingOtherLeagues = progress.TotalPlayerLeagueGames <= 0 &&
                                            progress.TotalLeagueGames > 0;
            float ratio = progress.TotalPlayerLeagueGames > 0
                ? Mathf.Clamp01((float)progress.PlayerLeagueGamesSimulated / progress.TotalPlayerLeagueGames)
                : progress.TotalLeagueGames > 0
                    ? Mathf.Clamp01((float)progress.LeagueGamesSimulated / progress.TotalLeagueGames)
                    : 1f;
            SetProgressTarget(ratio);
            _roundText.text = progress.NextRound > 0
                ? $"{progress.NextRound}라운드 시뮬레이션"
                : isCompletingOtherLeagues ? "남은 리그 정규시즌 마감" : "정규시즌 기록 집계";

            if (progress.NextRound > 0)
            {
                string away = ResolveTeamName(progress.NextAwayTeamSeasonKey, teamDisplayNameResolver, "원정 구단");
                string home = ResolveTeamName(progress.NextHomeTeamSeasonKey, teamDisplayNameResolver, "홈 구단");
                _matchupText.text = $"{away}  VS  {home}";
            }
            else
            {
                _matchupText.text = isCompletingOtherLeagues
                    ? "내 구단 일정은 완료됐습니다. 다른 리그의 남은 경기를 진행하고 있습니다."
                    : "정규시즌 최종 기록을 정리하고 있습니다.";
            }

            _leagueProgressText.text = progress.TotalPlayerLeagueGames > 0
                ? $"내 리그 경기  {progress.PlayerLeagueGamesSimulated:N0} / {progress.TotalPlayerLeagueGames:N0}"
                : progress.TotalLeagueGames > 0
                    ? $"남은 리그 경기  {progress.LeagueGamesSimulated:N0} / {progress.TotalLeagueGames:N0}"
                    : "정규시즌 경기 완료";
            _playerProgressText.text = progress.TotalPlayerGames > 0
                ? $"내 구단 경기  {progress.PlayerGamesSimulated:N0} / {progress.TotalPlayerGames:N0}"
                : "내 구단 일정 완료";
            _recordText.text =
                $"현재 성적  {progress.SeasonWins}승  {progress.SeasonDraws}무  {progress.SeasonLosses}패";
            _titleText.text = "정규시즌 시뮬레이션";
            _stateText.text = "리그 일정 순서대로 진행 중";
        }

        public void Bind(OwnerPostseasonSimulationProgress progress)
        {
            float ratio = progress.MaximumGames <= 0
                ? 1f
                : Mathf.Clamp01((float)progress.CompletedGames / progress.MaximumGames);
            SetProgressTarget(ratio);
            _titleText.text = "가을 야구 · 대진 진행";
            _stateText.text = "다음 경기를 준비하고 있습니다";
            _roundText.text = string.IsNullOrEmpty(progress.NextSeriesId)
                ? "포스트시즌 결과 집계"
                : progress.NextSeriesId == "championship" ? "챔피언십" : "준결승";
            _matchupText.text = string.IsNullOrEmpty(progress.NextLeagueGroupId)
                ? "모든 조의 우승 구단을 확인하고 있습니다."
                : "앞선 대진을 진행하고 있습니다.\n우리 구단 차례가 되면 경기 관전으로 이어집니다.";
            _leagueProgressText.text = $"종료된 경기  {progress.CompletedGames:N0}";
            _playerProgressText.text = $"완료 조  {progress.CompletedGroups:N0} / {progress.TotalGroups:N0}";
            _recordText.text = "중단해도 완료된 경기 결과는 유지됩니다.";
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            _hasProgressValue = false;
            _progressVelocity = 0f;
        }

        private void Awake()
        {
            Build();
            Hide();
        }

        private void Update()
        {
            AdvanceProgressAnimation(Time.unscaledDeltaTime);
        }

        private void SetProgressTarget(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            if (!_hasProgressValue)
            {
                _hasProgressValue = true;
                _displayedProgress = ratio;
                _targetProgress = ratio;
                _progressVelocity = 0f;
                ApplyDisplayedProgress();
                return;
            }

            // 진행 바는 확정된 경기 결과를 표현하므로 같은 세션 안에서 뒤로 움직이지 않는다.
            _targetProgress = Mathf.Max(_targetProgress, ratio);
        }

        private void AdvanceProgressAnimation(float deltaTime)
        {
            if (!_hasProgressValue || deltaTime <= 0f ||
                _targetProgress - _displayedProgress <= ProgressAnimationSnapThreshold)
            {
                if (_hasProgressValue && _displayedProgress != _targetProgress)
                {
                    _displayedProgress = _targetProgress;
                    _progressVelocity = 0f;
                    ApplyDisplayedProgress();
                }
                return;
            }

            float previousProgress = _displayedProgress;
            _displayedProgress = Mathf.SmoothDamp(
                _displayedProgress,
                _targetProgress,
                ref _progressVelocity,
                ProgressAnimationSmoothTime,
                Mathf.Infinity,
                deltaTime);
            _displayedProgress = Mathf.Clamp(_displayedProgress, previousProgress, _targetProgress);
            ApplyDisplayedProgress();
        }

        private void ApplyDisplayedProgress()
        {
            if (_progressFill != null)
                _progressFill.rectTransform.localScale = new Vector3(_displayedProgress, 1f, 1f);
        }

        private void Build()
        {
            RectTransform root = GetComponent<RectTransform>();
            Image blocker = root.gameObject.AddComponent<Image>();
            blocker.color = CareerUiTheme.InputBlocker;
            blocker.raycastTarget = true;
            root.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.InputBlocker);

            Image modalImage = OwnerRuntimeUiFactory.CreateImage(
                "SeasonSimulationDialog", root, CareerUiTheme.ReferencePanel);
            RectTransform modal = modalImage.rectTransform;
            modal.anchorMin = modal.anchorMax = new Vector2(0.5f, 0.5f);
            modal.pivot = new Vector2(0.5f, 0.5f);
            modal.sizeDelta = new Vector2(680f, 410f);
            modal.anchoredPosition = Vector2.zero;
            modalImage.raycastTarget = true;
            Outline outline = modal.gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.ReferenceBorder;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            Image header = OwnerRuntimeUiFactory.CreateImage(
                "Header", modal, new Color(0.035f, 0.07f, 0.12f, 1f));
            SetRect(header.rectTransform, new Vector2(0f, 344f), new Vector2(680f, 410f));
            _titleText = Label(header.transform, "Title", "정규시즌 시뮬레이션", 23, FontStyle.Bold,
                CareerUiTheme.TextPrimary, new Vector2(24f, 22f), new Vector2(470f, 62f));
            _titleText.color = CareerUiTheme.TextPrimary;
            _titleText.alignment = TextAnchor.MiddleLeft;
            _stateText = Label(header.transform, "State", "리그 일정 순서대로 진행 중", 15, FontStyle.Normal,
                CareerUiTheme.Number, new Vector2(470f, 22f), new Vector2(656f, 62f));
            _stateText.alignment = TextAnchor.MiddleRight;
            header.gameObject.AddComponent<CareerUiPreserveTextColor>();

            _roundText = Label(modal, "CurrentRound", string.Empty, 22, FontStyle.Bold,
                CareerUiTheme.ReferenceAccent, new Vector2(32f, 294f), new Vector2(648f, 336f));
            _roundText.alignment = TextAnchor.MiddleCenter;
            _matchupText = Label(modal, "CurrentMatchup", string.Empty, 18, FontStyle.Bold,
                CareerUiTheme.ReferenceText, new Vector2(32f, 220f), new Vector2(648f, 294f));
            _matchupText.alignment = TextAnchor.MiddleCenter;

            Image track = OwnerRuntimeUiFactory.CreateImage(
                "ProgressTrack", modal, new Color(0.72f, 0.74f, 0.76f, 1f));
            SetRect(track.rectTransform, new Vector2(48f, 192f), new Vector2(632f, 210f));
            _progressFill = OwnerRuntimeUiFactory.CreateImage(
                "ProgressFill", track.transform, CareerUiTheme.ReferenceAccentLight);
            _progressFill.rectTransform.anchorMin = Vector2.zero;
            _progressFill.rectTransform.anchorMax = Vector2.one;
            _progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _progressFill.rectTransform.offsetMin = new Vector2(3f, 3f);
            _progressFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            _progressFill.rectTransform.localScale = new Vector3(0f, 1f, 1f);

            Image metrics = OwnerRuntimeUiFactory.CreateImage(
                "ProgressSummary", modal, CareerUiTheme.ReferencePanelHeader);
            SetRect(metrics.rectTransform, new Vector2(48f, 104f), new Vector2(632f, 178f));
            _leagueProgressText = Label(metrics.transform, "LeagueProgress", string.Empty, 16, FontStyle.Bold,
                CareerUiTheme.ReferenceText, new Vector2(16f, 37f), new Vector2(284f, 70f));
            _playerProgressText = Label(metrics.transform, "PlayerProgress", string.Empty, 16, FontStyle.Bold,
                CareerUiTheme.ReferenceText, new Vector2(300f, 37f), new Vector2(568f, 70f));
            _playerProgressText.alignment = TextAnchor.MiddleRight;
            _recordText = Label(metrics.transform, "SeasonRecord", string.Empty, 16, FontStyle.Normal,
                CareerUiTheme.ReferenceTextSecondary, new Vector2(16f, 4f), new Vector2(568f, 37f));
            _recordText.alignment = TextAnchor.MiddleCenter;

            Button stopButton = OwnerWorkspaceUiFactory.CreateButton(
                modal, "StopSimulation", "완료한 경기까지 유지하고 중단", () => StopRequested?.Invoke());
            SetRect(stopButton.GetComponent<RectTransform>(), new Vector2(202f, 28f), new Vector2(478f, 82f));
            OwnerUiButtonSkin.Apply(stopButton, OwnerButtonRole.Secondary);
        }

        private static Text Label(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 min,
            Vector2 max)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(
                parent, name, value, fontSize, style, TextAnchor.MiddleLeft, color);
            SetRect(text.rectTransform, min, max);
            return text;
        }

        private static string ResolveTeamName(
            string teamSeasonKey,
            Func<string, string> resolver,
            string fallback)
        {
            string value = resolver(teamSeasonKey);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }
    }
}
