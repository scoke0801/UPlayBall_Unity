using System;
using System.Globalization;
using System.Text;
using Baseball.Game.Historical;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>페넌트레이스·포스트시즌·시즌 결산을 한 흐름으로 보여주는 구단주 전용 결과 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Popup_OwnerSeasonReview : MonoBehaviour, ICancelHandler
    {
        private const float ModalWidth = 1160f;
        private const float ModalHeight = 720f;
        private static readonly Color Navy = new Color32(7, 18, 32, 255);
        private static readonly Color NavyPanel = new Color32(12, 28, 47, 238);
        private static readonly Color NavySoft = new Color32(17, 38, 61, 224);
        private static readonly Color Ivory = new Color32(247, 244, 232, 255);
        private static readonly Color Gold = new Color32(213, 176, 91, 255);
        private static readonly Color Muted = new Color32(174, 187, 197, 255);
        private static Sprite _backgroundSprite;

        private Text _eyebrow;
        private Text _title;
        private Text _summary;
        private Text _detailsCaption;
        private Text _details;
        private Text _status;
        private Text _worldProgress;
        private Text _insightTitle;
        private Text _insightBody;
        private Text _primaryLabel;
        private Text _hint;
        private RectTransform _modal;
        private readonly Text[] _metricValues = new Text[3];
        private readonly Text[] _metricLabels = new Text[3];
        private Button _primary;
        private Button _close;
        private GameObject _previousSelection;
        private Button[] _tabs;
        private OwnerSeasonReviewSnapshot _snapshot;
        private Func<string, string> _teamName;
        private int _page;

        public event Action PostseasonRequested;
        public event Action CloseRequested;

        public static UI_Popup_OwnerSeasonReview CreateRuntime(RectTransform popupHost)
        {
            if (popupHost == null) throw new ArgumentNullException(nameof(popupHost));
            RectTransform root = OwnerRuntimeUiFactory.CreateRect(nameof(UI_Popup_OwnerSeasonReview), popupHost);
            OwnerRuntimeUiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<UI_Popup_OwnerSeasonReview>();
            // 비활성 Popup Host와 EditMode에서도 Bind 전에 계층이 준비되어야 한다.
            view.EnsureBuilt();
            return view;
        }

        public void Bind(OwnerSeasonReviewSnapshot snapshot, Func<string, string> teamNameResolver, int initialPage)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _teamName = teamNameResolver ?? throw new ArgumentNullException(nameof(teamNameResolver));
            SetPage(Mathf.Clamp(initialPage, 0, snapshot.IsPostseasonCompleted ? 2 : 1));
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            FitModal();
            EventSystem events = EventSystem.current;
            if (events == null) return;
            GameObject selected = events.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(transform)) return;
            _previousSelection = selected;
            _tabs[_page].Select();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (EventSystem.current != null && _previousSelection != null && _previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(_previousSelection);
            _previousSelection = null;
        }

        /// <summary>공통 Cancel 입력은 결과를 진행하지 않고 팝업 닫기로 전달한다.</summary>
        public void OnCancel(BaseEventData eventData)
        {
            CloseRequested?.Invoke();
            eventData.Use();
        }

        private void Awake() => EnsureBuilt();

        private void EnsureBuilt()
        {
            if (_modal != null) return;
            Build();
            Hide();
        }

        private void OnRectTransformDimensionsChange() => FitModal();

        private void FitModal()
        {
            if (_modal == null) return;
            Rect bounds = GetComponent<RectTransform>().rect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            float scale = Mathf.Min(1f, (bounds.width - CareerUiTheme.Space6) / ModalWidth,
                (bounds.height - CareerUiTheme.Space6) / ModalHeight);
            _modal.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        }

        private void Build()
        {
            RectTransform root = GetComponent<RectTransform>();
            Image blocker = root.gameObject.AddComponent<Image>();
            blocker.color = new Color(0.005f, 0.01f, 0.02f, 0.88f);
            blocker.raycastTarget = true;
            root.gameObject.AddComponent<CareerUiVisualElement>().Initialize(CareerUiVisualRole.InputBlocker);
            RectTransform modal = Surface(root, "SeasonReview", Navy,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(ModalWidth, ModalHeight));
            _modal = modal;
            AddBackgroundArt(modal);

            RectTransform readableVeil = Surface(modal, "ReadableVeil", new Color(0.015f, 0.035f, 0.065f, 0.84f),
                Vector2.zero, Vector2.zero, Vector2.zero);
            SetRect(readableVeil, Vector2.zero, new Vector2(760f, ModalHeight));
            RectTransform footerVeil = Surface(modal, "FooterVeil", new Color(0.015f, 0.03f, 0.05f, 0.92f),
                Vector2.zero, Vector2.zero, Vector2.zero);
            SetRect(footerVeil, Vector2.zero, new Vector2(ModalWidth, 178f));

            RectTransform header = Surface(modal, "Header", new Color(0.018f, 0.045f, 0.078f, 0.96f),
                new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 76f));
            SetRect(header, new Vector2(0f, 634f), new Vector2(ModalWidth, ModalHeight));
            _eyebrow = Label(header, "Eyebrow", "OWNER SEASON REVIEW", 13, FontStyle.Bold,
                Gold, new Vector2(32f, 48f), new Vector2(850f, 72f));
            _title = Label(header, "Title", string.Empty, 28, FontStyle.Bold,
                Ivory, new Vector2(32f, 9f), new Vector2(900f, 48f));
            Button close = OwnerWorkspaceUiFactory.CreateButton(header, "Close", "닫기", () => CloseRequested?.Invoke());
            _close = close;
            SetRect(close.GetComponent<RectTransform>(), new Vector2(1024f, 18f), new Vector2(1128f, 66f));
            close.GetComponent<Image>().color = CareerUiTheme.ReferenceAccent;
            OwnerUiButtonSkin.Apply(close, OwnerButtonRole.Navigation);

            _tabs = new Button[3];
            string[] names = { "01  페넌트레이스", "02  포스트시즌", "03  시즌 결산" };
            for (int index = 0; index < _tabs.Length; index++)
            {
                int selected = index;
                _tabs[index] = OwnerWorkspaceUiFactory.CreateButton(modal, "Tab" + index, names[index], () => SetPage(selected));
                SetRect(_tabs[index].GetComponent<RectTransform>(),
                    new Vector2(32f + index * 196f, 574f), new Vector2(216f + index * 196f, 622f));
                OwnerUiButtonSkin.Apply(_tabs[index], OwnerButtonRole.Tab);
            }
            _worldProgress = Label(modal, "WorldProgress", string.Empty, 13, FontStyle.Bold,
                Muted, new Vector2(650f, 574f), new Vector2(1128f, 622f));
            _worldProgress.alignment = TextAnchor.MiddleRight;

            RectTransform hero = Surface(modal, "ResultHero", NavyPanel,
                Vector2.zero, Vector2.zero, new Vector2(0f, 0f));
            SetRect(hero, new Vector2(42f, 198f), new Vector2(724f, 554f));
            _summary = Label(hero, "Summary", string.Empty, 40, FontStyle.Bold,
                Ivory, new Vector2(24f, 280f), new Vector2(658f, 336f));
            _summary.resizeTextForBestFit = true;
            _summary.resizeTextMinSize = 28;
            _summary.resizeTextMaxSize = 40;
            _status = Label(hero, "Status", string.Empty, 18, FontStyle.Normal,
                Gold, new Vector2(24f, 232f), new Vector2(658f, 276f));
            _detailsCaption = Label(hero, "DetailsCaption", string.Empty, 12, FontStyle.Bold,
                Gold, new Vector2(22f, 194f), new Vector2(652f, 226f));
            _details = Label(hero, "Details", string.Empty, 17, FontStyle.Normal,
                Ivory, new Vector2(22f, 18f), new Vector2(652f, 194f));
            _details.alignment = TextAnchor.UpperLeft;
            _details.lineSpacing = 1.12f;

            for (int index = 0; index < _metricValues.Length; index++)
            {
                float top = 548f - index * 96f;
                RectTransform metric = Surface(modal, "Metric" + index, NavySoft,
                    Vector2.zero, Vector2.zero, Vector2.zero);
                SetRect(metric, new Vector2(760f, top - 80f), new Vector2(1128f, top));
                _metricValues[index] = Label(metric, "Value", string.Empty, 27, FontStyle.Bold,
                    Ivory, new Vector2(24f, 12f), new Vector2(344f, 48f));
                _metricValues[index].alignment = TextAnchor.MiddleRight;
                _metricLabels[index] = Label(metric, "Label", string.Empty, 12, FontStyle.Bold,
                    Muted, new Vector2(24f, 48f), new Vector2(344f, 72f));
                _metricLabels[index].alignment = TextAnchor.MiddleLeft;
            }

            // 설명은 기록 카드의 형제 영역을 따로 소유한다. 카드 위에 문구를 덧그리지 않는다.
            RectTransform insight = Surface(modal, "NextStep", NavyPanel,
                Vector2.zero, Vector2.zero, Vector2.zero);
            SetRect(insight, new Vector2(760f, 198f), new Vector2(1128f, 264f));
            _insightTitle = Label(insight, "InsightTitle", string.Empty, 13, FontStyle.Bold,
                Gold, new Vector2(16f, 40f), new Vector2(352f, 60f));
            _insightBody = Label(insight, "InsightBody", string.Empty, 14, FontStyle.Normal,
                Ivory, new Vector2(16f, 4f), new Vector2(352f, 40f));
            _insightBody.alignment = TextAnchor.UpperLeft;

            _primary = OwnerWorkspaceUiFactory.CreateButton(modal, "Primary", "포스트시즌 확인", HandlePrimary);
            SetRect(_primary.GetComponent<RectTransform>(), new Vector2(824f, 42f), new Vector2(1128f, 106f));
            _primary.GetComponent<Image>().color = CareerUiTheme.ReferenceAccent;
            OwnerUiButtonSkin.Apply(_primary, OwnerButtonRole.Primary);
            _primaryLabel = _primary.transform.Find("Label").GetComponent<Text>();
            _primaryLabel.fontSize = 18;
            _hint = Label(modal, "Hint", string.Empty, 15, FontStyle.Normal,
                Muted, new Vector2(32f, 42f), new Vector2(790f, 104f));
            _hint.alignment = TextAnchor.MiddleLeft;
            FitModal();
        }

        private void SetPage(int page)
        {
            if (_snapshot == null) return;
            _page = page;
            for (int index = 0; index < _tabs.Length; index++)
            {
                // 선택 탭을 Disabled로 만들면 회색 비활성 UI처럼 보인다. 선택은 Skin 상태로만 표현한다.
                _tabs[index].interactable = index < 2 || _snapshot.IsPostseasonCompleted;
                OwnerUiButtonSkin.SetSelected(_tabs[index], index == page);
            }
            _eyebrow.text = $"시즌 {_snapshot.SeasonNumber}  /  {OwnerLeagueDisplayNameFormatter.FormatFull(_snapshot.CurrentGrade)}";
            _worldProgress.text = _snapshot.IsPostseasonCompleted
                ? "전체 포스트시즌 종료"
                : $"포스트시즌 종료  ·  {_snapshot.CompletedPostseasonGroups} / {_snapshot.TotalPostseasonGroups}개 조";
            _hint.text = _snapshot.IsPostseasonCompleted
                ? "시즌 결산에서 승강 결과와 다음 시즌 등급을 확인하세요."
                : "시즌 결산은 모든 조의 포스트시즌이 끝나면 열립니다.";
            if (page == 0) BindPennantRace();
            else if (page == 1) BindPostseason();
            else BindRecap();
            ConfigureNavigation();
        }

        private void ConfigureNavigation()
        {
            int lastTab = _snapshot.IsPostseasonCompleted ? 2 : 1;
            for (int i = 0; i <= lastTab; i++)
                _tabs[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = i == 0 ? _close : _tabs[i - 1],
                    selectOnRight = i == lastTab ? _close : _tabs[i + 1],
                    selectOnUp = _close, selectOnDown = _primary };
            _close.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = _tabs[lastTab], selectOnRight = _tabs[0],
                selectOnUp = _primary, selectOnDown = _tabs[_page] };
            _primary.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = _tabs[_page], selectOnRight = _close,
                selectOnUp = _tabs[_page], selectOnDown = _close };
        }

        private void BindPennantRace()
        {
            _title.text = "페넌트레이스 최종 보고";
            _summary.text = $"{_snapshot.Rank}위로 정규시즌 마감";
            _status.text = _snapshot.IsQualified ? "가을 야구 진출권 확보" : "정규시즌 여정 종료";
            _detailsCaption.text = "정규시즌 결과";
            _details.text = _snapshot.IsQualified
                ? $"{_snapshot.TeamCount}개 구단 중 {_snapshot.Rank}위로 포스트시즌에 진출했습니다.\n정규시즌 순위에 따른 시드로 우승에 도전합니다.\n승강 결과는 모든 조의 포스트시즌이 끝나면 확정됩니다."
                : $"{_snapshot.TeamCount}개 구단 경쟁에서 포스트시즌 진출에 실패했습니다.\n다른 리그의 포스트시즌까지 마감하면 시즌 등급이 확정됩니다.\n이번 시즌 결과는 다음 로스터·계약 판단의 근거로 남습니다.";
            SetMetrics($"{_snapshot.Rank} / {_snapshot.TeamCount}", "최종 순위",
                $"{_snapshot.Wins}-{_snapshot.Draws}-{_snapshot.Losses}", "승-무-패",
                FormatSigned(_snapshot.RunDifferential), "득실차");
            _insightTitle.text = _snapshot.IsQualified ? "다음 단계 · 포스트시즌" : "다음 단계 · 전체 결과 확정";
            _insightBody.text = _snapshot.IsQualified
                ? "정규시즌의 선택이\n단기전에서 증명됩니다."
                : "타 리그 결과까지 확정해\n다음 시즌 편성을 준비합니다.";
            _primaryLabel.text = "포스트시즌 확인";
            _primary.interactable = true;
        }

        private void BindPostseason()
        {
            _title.text = "포스트시즌";
            _detailsCaption.text = "포스트시즌 대진 · 시리즈 전적";
            _details.text = BuildSeriesText(_snapshot.Series);
            if (!_snapshot.IsPostseasonCompleted)
            {
                if (_snapshot.IsPlayerPostseasonCompleted)
                {
                    _summary.text = FormatPostseasonResult(_snapshot.PostseasonResult);
                    _status.text = "우리 조 결과 확정 · 다른 리그 결과 마감 필요";
                    _primaryLabel.text = "남은 리그 마감";
                }
                else
                {
                    _summary.text = _snapshot.IsQualified ? "가을 야구가 시작됩니다" : "월드 포스트시즌 진행";
                    _status.text = _snapshot.IsQualified
                        ? "정규시즌 시드와 확정 로스터로 단기전을 시작합니다."
                        : "우리 구단은 미진출 · 타 리그 결과를 확정합니다.";
                    _primaryLabel.text = _snapshot.IsQualified ? "포스트시즌 시작" : "남은 리그 마감";
                }
                SetMetrics(_snapshot.IsQualified ? $"{Math.Min(_snapshot.Rank, 4)}번" : "미진출", "우리 구단 시드",
                    $"{_snapshot.CompletedPostseasonGroups}/{_snapshot.TotalPostseasonGroups}", "완료된 리그",
                    FormatPostseasonResult(_snapshot.PostseasonResult), "현재 결과");
                _insightTitle.text = "다음 단계 · 전체 결과 확정";
                _insightBody.text = "모든 리그 우승팀이 확정되어야\n승강과 다음 시즌이 열립니다.";
                _primary.interactable = true;
                return;
            }
            string result = FormatPostseasonResult(_snapshot.PostseasonResult);
            _summary.text = result;
            _status.text = "우승 구단  ·  " + Resolve(_snapshot.ChampionTeamSeasonKey);
            SetMetrics(_snapshot.IsQualified ? $"{Math.Min(_snapshot.Rank, 4)}번" : "미진출", "우리 구단 시드",
                $"{_snapshot.Series.Count}", "진행 시리즈",
                $"{_snapshot.CompletedPostseasonGroups}/{_snapshot.TotalPostseasonGroups}", "완료된 리그");
            _insightTitle.text = "다음 단계 · 시즌 결산";
            _insightBody.text = "성과와 다음 리그 등급을 확인하고\n계약·급여 마감으로 이어집니다.";
            _primaryLabel.text = "시즌 결산 보기";
            _primary.interactable = true;
        }

        private void BindRecap()
        {
            _title.text = $"시즌 {_snapshot.SeasonNumber} 결산";
            _summary.text = _snapshot.PostseasonResult == OwnerTeamPostseasonResult.Champion
                ? "우리가 만든 시즌, 우승으로." : $"정규시즌 {_snapshot.Rank}위 · {FormatPostseasonResult(_snapshot.PostseasonResult)}";
            _status.text = FormatLeagueMovement();
            _detailsCaption.text = "이번 시즌 기록";
            _details.text = $"정규시즌  {_snapshot.Wins}승 {_snapshot.Draws}무 {_snapshot.Losses}패  ·  승률 {_snapshot.WinningPercentage.ToString(".000", CultureInfo.InvariantCulture)}\n" +
                $"팀 득점 {_snapshot.Runs:N0}  ·  팀 실점 {_snapshot.RunsAllowed:N0}  ·  득실차 {FormatSigned(_snapshot.RunDifferential)}\n" +
                $"포스트시즌  {FormatPostseasonResult(_snapshot.PostseasonResult)}\n" +
                "만료 임박 계약을 갱신하세요. 부족한 급여·갱신 계약금은 이월하여 이후 수입으로 상환합니다.";
            SetMetrics($"{_snapshot.WinningPercentage.ToString(".000", CultureInfo.InvariantCulture)}", "정규시즌 승률",
                FormatPostseasonResult(_snapshot.PostseasonResult), "포스트시즌",
                FormatNextGrade(), "다음 시즌 등급");
            _insightTitle.text = "다음 단계 · 구단 업무";
            _insightBody.text = "계약 갱신 후 다음 시즌으로 진행합니다.\n급여 부족액은 미지급금으로 이월됩니다.";
            _primaryLabel.text = "구단 업무로 돌아가기";
            _primary.interactable = true;
        }

        private void HandlePrimary()
        {
            if (_page == 0) { SetPage(1); return; }
            if (_page == 1 && !_snapshot.IsPostseasonCompleted) { PostseasonRequested?.Invoke(); return; }
            if (_page == 1) { SetPage(2); return; }
            CloseRequested?.Invoke();
        }

        private void SetMetrics(string value0, string label0, string value1, string label1, string value2, string label2)
        {
            string[] values = { value0, value1, value2 };
            string[] labels = { label0, label1, label2 };
            for (int index = 0; index < _metricValues.Length; index++)
            {
                _metricValues[index].text = values[index];
                _metricLabels[index].text = labels[index];
            }
        }

        private string BuildSeriesText(System.Collections.Generic.IReadOnlyList<OwnerPostseasonSeriesReview> series)
        {
            if (series.Count == 0) return "대진 확정 전입니다. 포스트시즌을 시작하면 정규시즌 순위로 시드가 고정됩니다.";
            var text = new StringBuilder();
            for (int index = 0; index < series.Count; index++)
            {
                OwnerPostseasonSeriesReview item = series[index];
                text.Append(item.Round == OwnerPostseasonRound.Semifinal ? "준결승" : "챔피언십")
                    .Append("  |  ").Append(Resolve(item.HigherSeedTeamSeasonKey)).Append("  ")
                    .Append(item.HigherSeedWins).Append(" : ").Append(item.LowerSeedWins).Append("  ")
                    .Append(Resolve(item.LowerSeedTeamSeasonKey));
                if (!item.IsCompleted) text.Append("  ·  진행 중");
                if (index + 1 < series.Count) text.AppendLine();
            }
            return text.ToString();
        }

        private string Resolve(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "미정";
            string value = _teamName(key);
            return string.IsNullOrWhiteSpace(value) || value == key ? "구단명 확인 중" : value;
        }

        private string FormatLeagueMovement()
        {
            if (!_snapshot.NextGrade.HasValue || _snapshot.NextGrade.Value == _snapshot.CurrentGrade)
                return "다음 시즌  ·  잔류";
            return _snapshot.NextGrade.Value > _snapshot.CurrentGrade
                ? "다음 시즌  ·  승격 확정"
                : "다음 시즌  ·  강등 확정";
        }

        private string FormatNextGrade() => _snapshot.NextGrade.HasValue
            ? OwnerLeagueDisplayNameFormatter.FormatFull(_snapshot.NextGrade.Value)
            : "판정 대기";

        private static string FormatPostseasonResult(OwnerTeamPostseasonResult? result) => result switch
        {
            OwnerTeamPostseasonResult.Champion => "포스트시즌 우승",
            OwnerTeamPostseasonResult.RunnerUp => "포스트시즌 준우승",
            OwnerTeamPostseasonResult.SemifinalElimination => "준결승 탈락",
            OwnerTeamPostseasonResult.DidNotQualify => "포스트시즌 미진출",
            _ => "포스트시즌 진행 중"
        };

        private static string FormatSigned(int value) => value > 0 ? "+" + value : value.ToString();

        private static void AddBackgroundArt(RectTransform modal)
        {
            Image art = OwnerRuntimeUiFactory.CreateImage("SeasonBackdrop", modal, Color.white);
            OwnerRuntimeUiFactory.Stretch(art.rectTransform);
            art.sprite = LoadBackgroundSprite();
            art.preserveAspect = false;
            art.color = art.sprite == null ? Navy : new Color(0.88f, 0.91f, 0.95f, 1f);
            art.transform.SetAsFirstSibling();
        }

        private static Sprite LoadBackgroundSprite()
        {
            if (_backgroundSprite != null) return _backgroundSprite;
            Texture2D texture = Resources.Load<Texture2D>("UI/Generated/bg_owner_season_review_v1");
            if (texture == null) return null;
            _backgroundSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            _backgroundSprite.name = "OwnerSeasonReviewBackdrop";
            return _backgroundSprite;
        }

        private static RectTransform Surface(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            Image image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size; rect.anchoredPosition = Vector2.zero;
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = CareerUiTheme.ReferenceBorder; outline.effectDistance = new Vector2(1f, -1f);
            return rect;
        }

        private static Text Label(Transform parent, string name, string value, int size, FontStyle style,
            Color color, Vector2 min, Vector2 max)
        {
            Text text = OwnerWorkspaceUiFactory.CreateText(parent, name, value, size, style, TextAnchor.MiddleLeft, color);
            // Factory의 밝은 업무 화면 색 변환을 거치지 않고, 결산 무대의 명시적 대비를 보존한다.
            text.color = color;
            text.gameObject.AddComponent<CareerUiPreserveTextColor>();
            SetRect(text.rectTransform, min, max);
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.offsetMin = min; rect.offsetMax = max;
        }
    }
}
