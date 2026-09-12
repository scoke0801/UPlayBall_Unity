using Baseball.Core.Players;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match
{
    public sealed partial class UI_Scene_OwnerMatchSpectator
    {
        private static readonly Color Ink = new Color32(29, 34, 40, 255);
        private static readonly Color Paper = new Color32(246, 248, 249, 255);
        private static readonly Color Silver = new Color32(216, 225, 231, 255);
        private static readonly Color Blue = new Color32(29, 122, 187, 255);
        private static readonly Color Muted = new Color32(103, 122, 137, 255);
        private static Font _font;
        private Text _awayLabel, _homeLabel, _inningLabel, _statusLabel, _pauseLabel;
        private Text _pitcherLabel, _batterLabel, _pitcherDetail, _batterDetail;
        private Text _announcement, _commentary, _resultHeading, _resultSummary, _recordHeader;
        private Text _scoreCaption, _resultToggleLabel;
        private Button _pauseButton, _advanceButton, _revealAllButton, _homeButton, _resultButton;
        private Button[] _speedButtons;
        private Button[] _viewingModeButtons;
        private readonly Image[] _balls = new Image[4];
        private readonly Image[] _strikes = new Image[3];
        private readonly Image[] _outs = new Image[3];
        private readonly Image[] _bases = new Image[3];
        private MatchGameCastConfig _gameCastConfig;
        private MatchPlayVisualizer _playVisualizer;
        private Text _pitchHistory, _decisionNote, _playDetail, _currentPitch;
        private RectTransform _playExplanation, _decisionExplanation;
        private RectTransform _miniLineScore;
        private Text _miniAwayTeam, _miniHomeTeam;
        private readonly System.Collections.Generic.List<Text[]> _miniInningColumns = new();
        private Text _pitcherRole, _batterRole;
        private RectTransform _strikeZone;
        private readonly Image[] _pitchDots = new Image[12];
        private readonly Text[] _pitchNumbers = new Text[12];
        private RectTransform _zoneBall;
        private RectTransform _scorePanel, _scoreRows, _resultPanel, _recordContent;
        private RectTransform _resultScoreRows;
        private ScrollRect _recordScroll;
        private Scrollbar _recordScrollbar;

        private void Build()
        {
            _canvas = Panel("BroadcastCanvas", _root, Ink, 0, 0, 1440, 810);
            _canvas.anchorMin = _canvas.anchorMax = new Vector2(0.5f, 0.5f);
            _canvas.pivot = new Vector2(0.5f, 0.5f);
            _canvas.anchoredPosition = Vector2.zero;
            _gameCastConfig = MatchGameCastConfig.Load();
            _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RectTransform field = Panel("Field", _canvas, new Color32(27, 56, 42, 255), 12, 124, 900, 552);
            field.gameObject.AddComponent<RectMask2D>();
            // 원본 3:2 비율을 유지하고 외곽만 여백 처리한다.
            RectTransform ground = Panel("Ground", field, Color.clear, 36, 0, 828, 552);
            _playVisualizer = new MatchPlayVisualizer(ground, _gameCastConfig, _font,
                playerId => _session?.GetParticipantName(playerId) ?? string.Empty,
                (pitcherId, batterId) => _session.GetHandedness(pitcherId, batterId));
            BuildHeader();
            BuildFieldOverlay();
            BuildFooter();
            BuildResults();
            SetCompletionControlVisibility(false);
            FitWorkspace();
        }

        private void BuildHeader()
        {
            Panel("Header", _canvas, Silver, 0, 0, 1440, 62);
            Panel("AwayRibbon", _canvas, Ink, 0, 0, 305, 59);
            Panel("HomeRibbon", _canvas, Blue, 379, 0, 305, 59);
            _awayLabel = Label("Away", _canvas, "원정", 22, 14, 4, 278, 48, Color.white);
            _homeLabel = Label("Home", _canvas, "홈", 22, 391, 4, 278, 48, Color.white);
            _inningLabel = Label("Inning", _canvas, "1회 초", 19, 305, 0, 74, 58, Ink);
            _inningLabel.alignment = TextAnchor.MiddleCenter;
            BuildCount("B", _balls, 700, new Color32(76, 177, 57, 255));
            BuildCount("S", _strikes, 824, new Color32(224, 169, 17, 255));
            BuildCount("O", _outs, 930, new Color32(207, 47, 48, 255));
            _pauseButton = Control("Pause", _canvas, "일시정지", 1050, 12, 110, HandlePauseRequested);
            _pauseLabel = _pauseButton.GetComponentInChildren<Text>();
            _speedButtons = new Button[3];
            var speeds = new[] { OwnerMatchPlaybackSpeed.Normal, OwnerMatchPlaybackSpeed.Fast, OwnerMatchPlaybackSpeed.FourTimes };
            for (int i = 0; i < speeds.Length; i++)
            {
                OwnerMatchPlaybackSpeed speed = speeds[i];
                _speedButtons[i] = Control("Speed" + (int)speed, _canvas, (int)speed + "배", 1168 + i * 59, 12, 53,
                    () => HandleSpeedRequested(speed));
            }
            _revealAllButton = Control("RevealAll", _canvas, "즉시 결과", 1350, 12, 82, HandleRevealAllRequested);
        }

        private void BuildCount(string name, Image[] lamps, float x, Color color)
        {
            Label(name, _canvas, name, 18, x, 16, 20, 30, color);
            for (int i = 0; i < lamps.Length; i++)
            {
                var rect = Panel(name + i, _canvas, Muted, x + 25 + i * 19, 25, 12, 12);
                lamps[i] = rect.GetComponent<Image>();
                rect.localEulerAngles = new Vector3(0, 0, 45);
            }
        }

        private void BuildFieldOverlay()
        {
            var badge = Panel("LiveBadge", _canvas, new Color(0.08f, 0.12f, 0.15f, 0.88f), 12, 74, 270, 38);
            _statusLabel = Label("LiveStatus", badge, "경기 중계", 17, 12, 0, 246, 36, Color.white);
            _viewingModeButtons = new Button[3];
            var viewingModes = new[]
            {
                OwnerMatchViewingMode.EveryMoment,
                OwnerMatchViewingMode.KeyMoments,
                OwnerMatchViewingMode.ResultOnly
            };
            string[] viewingLabels = { "모든 순간", "중요 순간", "경기 결과" };
            for (int index = 0; index < viewingModes.Length; index++)
            {
                OwnerMatchViewingMode mode = viewingModes[index];
                _viewingModeButtons[index] = Control(
                    "ViewingMode" + mode,
                    _canvas,
                    viewingLabels[index],
                    294 + index * 112,
                    74,
                    106,
                    () => HandleViewingModeRequested(mode));
            }
            var runners = Panel("BaseOccupancy", _canvas, new Color(0.08f, 0.12f, 0.15f, 0.85f), 782, 76, 124, 43);
            Label("BaseTitle", runners, "주자 상황", 12, 0, 7, 52, 27, Color.white).alignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < 3; i++)
            {
                float x = i == 0 ? 105 : i == 1 ? 84 : 63;
                float y = i == 1 ? 7 : 25;
                var rect = Panel("Base" + (i + 1), runners, Muted, x, y, 10, 10);
                rect.localEulerAngles = new Vector3(0, 0, 45);
                _bases[i] = rect.GetComponent<Image>();
            }
            _announcement = Label("PlayAnnouncement", _canvas, "", 34, 210, 170, 510, 60, Color.white);
            _announcement.alignment = TextAnchor.MiddleCenter;
            var shadow = _announcement.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(3, -4);
            BuildGameCastSidebar();
            _scorePanel = Panel("InningOverlay", _canvas, new Color(0.06f, 0.09f, 0.13f, 0.83f), 140, 233, 1160, 226);
            _scoreCaption = Label("Caption", _scorePanel, "공수 교대", 31, 20, 8, 1120, 54, Color.white);
            _scoreCaption.alignment = TextAnchor.MiddleCenter;
            _scoreRows = Panel("LineScore", _scorePanel, Paper, 20, 78, 1120, 126);
            _scorePanel.gameObject.SetActive(false);
        }

        private void BuildGameCastSidebar()
        {
            Sprite baseballSprite = _gameCastConfig.LoadBaseballSprite();
            var side = Panel("GameCastSidebar", _canvas, Paper, 924, 74, 504, 602);
            Panel("Accent", side, Blue, 0, 0, 504, 3);
            Label("Heading", side, "현재 승부", 15, 16, 8, 472, 27, Blue);
            _pitcherRole = Label("PitcherRole", side, "마운드 · 투수", 12, 16, 42, 226, 23, Muted);
            _batterRole = Label("BatterRole", side, "타석 · 타자", 12, 266, 42, 222, 23, Muted);
            _pitcherLabel = Label("PitcherName", side, "등판 대기", 24, 16, 68, 226, 36, Ink);
            _batterLabel = Label("BatterName", side, "타석 대기", 24, 266, 68, 222, 36, Ink);
            _pitcherDetail = Label("PitcherDetail", side, "", 13, 16, 106, 226, 25, Muted);
            _batterDetail = Label("BatterDetail", side, "", 13, 266, 106, 222, 25, Muted);
            Panel("DuelRule", side, Silver, 16, 142, 472, 1);
            RectTransform detail = BuildPitchContext(side);
            _currentPitch = Label("CurrentPitch", detail, "투구 기록", 16, 16, 152, 472, 28, Ink);
            _strikeZone = Panel("StrikeZone", detail, new Color32(231, 237, 241, 255), 16, 188, 200, 180);
            _strikeZone.gameObject.AddComponent<RectMask2D>();
            for (int index = 0; index <= 3; index++)
            {
                Panel("Vertical" + index, _strikeZone, Muted, 40 + index * 40, 30, 1, 120);
                Panel("Horizontal" + index, _strikeZone, Muted, 40, 30 + index * 40, 120, 1);
            }
            for (int index = 0; index < _pitchDots.Length; index++)
            {
                Image dot = CirclePanel("Pitch" + index, _strikeZone, Blue, 21).GetComponent<Image>();
                _pitchDots[index] = dot;
                SpritePanel("Baseball", dot.transform, baseballSprite, 2, 2, 17, 17);
                _pitchNumbers[index] = Label("Number", dot.transform, "", 11, 0, 0, 21, 21, Color.white);
                _pitchNumbers[index].color = Ink;
                _pitchNumbers[index].alignment = TextAnchor.MiddleCenter;
                dot.gameObject.SetActive(false);
            }
            _zoneBall = SpritePanel("PitchInFlight", _strikeZone, baseballSprite, 0, 0,
                _gameCastConfig.strikeZoneBallSize, _gameCastConfig.strikeZoneBallSize);
            _zoneBall.gameObject.SetActive(false);
            _pitchHistory = Label("PitchHistory", detail, "첫 투구를 기다립니다.", 14, 232, 188, 256, 180, Ink);
            _pitchHistory.alignment = TextAnchor.UpperLeft;
            _pitchHistory.fontStyle = FontStyle.Normal;
            Label("ZoneNote", detail, "포수 시점", 11, 16, 370, 472, 20, Muted);
            _playExplanation = Panel("PlayExplanation", detail, Color.clear, 16, 404, 472, 85);
            Panel("PlayRule", _playExplanation, Silver, 0, 0, 472, 1);
            Label("PlayTitle", _playExplanation, "플레이 해설", 13, 0, 9, 472, 22, Blue);
            _playDetail = Label("PlayDetail", _playExplanation, "", 15, 0, 36, 472, 49, Ink);
            _playDetail.fontStyle = FontStyle.Normal;
            _playExplanation.gameObject.SetActive(false);
            _decisionExplanation = Panel("DecisionExplanation", side, Color.clear, 16, 504, 472, 87);
            Panel("DecisionRule", _decisionExplanation, Silver, 0, 0, 472, 1);
            Label("DecisionTitle", _decisionExplanation, "감독의 판단", 13, 0, 9, 472, 22, Blue);
            _decisionNote = Label("DecisionNote", _decisionExplanation, "", 14, 0, 37, 472, 50, Ink);
            _decisionNote.fontStyle = FontStyle.Normal;
            _decisionExplanation.gameObject.SetActive(false);
            BuildHighlightInset(side);
            _miniLineScore = Panel("CompactLineScore", _canvas, Color.clear, 24, 622, 852, 54);
            Label("Title", _miniLineScore, "이닝별 득점", 13, 0, 0, 180, 18, Color.white);
            _miniAwayTeam = Label("AwayTeam", _miniLineScore, "", 13, 0, 18, 180, 18, Color.white);
            _miniHomeTeam = Label("HomeTeam", _miniLineScore, "", 13, 0, 36, 180, 18, Color.white);
            _miniAwayTeam.resizeTextForBestFit = _miniHomeTeam.resizeTextForBestFit = true;
            _miniAwayTeam.resizeTextMinSize = _miniHomeTeam.resizeTextMinSize = 10;
            _miniAwayTeam.resizeTextMaxSize = _miniHomeTeam.resizeTextMaxSize = 13;
        }

        private void BuildFooter()
        {
            Panel("Footer", _canvas, Paper, 0, 682, 1440, 128);
            Panel("FooterRule", _canvas, Silver, 0, 682, 1440, 2);
            Label("CommentaryTitle", _canvas, "경기 중계", 16, 20, 692, 106, 28, Blue);
            _commentary = Label("Commentary", _canvas, "잠시 후 경기가 시작됩니다.", 17, 140, 692, 900, 108, Ink);
            _commentary.alignment = TextAnchor.UpperLeft;
            _advanceButton = Control("Advance", _canvas, "다음 장면", 1070, 700, 160, HandleAdvanceRequested);
            _resultButton = Control("Result", _canvas, "경기 결과", 1242, 700, 176, () =>
            {
                _showResults = !_showResults;
                RefreshControls();
            });
            _resultToggleLabel = _resultButton.GetComponentInChildren<Text>();
            _homeButton = Control("ReturnHome", _canvas, "구단 홈으로", 1242, 752, 176, () => HomeRequested?.Invoke());
        }

        private void BuildResults()
        {
            _resultPanel = Panel("MatchResult", _canvas, Paper, 0, 62, 1440, 620);
            Panel("ResultTitleRule", _resultPanel, Silver, 20, 56, 1400, 2);
            _resultHeading = Label("Title", _resultPanel, "경기 결과", 26, 32, 8, 1376, 42, Blue);
            _resultHeading.alignment = TextAnchor.MiddleCenter;
            _resultSummary = Label("Versus", _resultPanel, "", 28, 40, 70, 1360, 55, Ink);
            _resultSummary.alignment = TextAnchor.MiddleCenter;
            _resultScoreRows = Panel("FinalLineScore", _resultPanel, Silver, 40, 140, 1360, 126);
            Control("AwayRecords", _resultPanel, "원정 기록", 40, 280, 135, () => { _showHomeRecords = false; RenderRecords(); });
            Control("HomeRecords", _resultPanel, "홈 기록", 185, 280, 135, () => { _showHomeRecords = true; RenderRecords(); });
            Control("BattingRecords", _resultPanel, "타격 성적", 340, 280, 135, () => { _showPitching = false; RenderRecords(); });
            Control("PitchingRecords", _resultPanel, "투구 성적", 485, 280, 135, () => { _showPitching = true; RenderRecords(); });
            _recordHeader = Label("RecordHeader", _resultPanel, "", 17, 650, 280, 748, 38, Blue);
            _recordHeader.alignment = TextAnchor.MiddleRight;
            var viewport = Panel("RecordViewport", _resultPanel, Silver, 40, 332, 1334, 264);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            _recordScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _recordContent = Panel("Records", viewport, Paper, 0, 0, 1334, 264);
            _recordScroll.viewport = viewport;
            _recordScroll.content = _recordContent;
            _recordScroll.horizontal = false;
            _recordScroll.movementType = ScrollRect.MovementType.Clamped;
            _recordScroll.scrollSensitivity = 30;
            _recordScrollbar = BuildRecordScrollbar();
            _recordScroll.verticalScrollbar = _recordScrollbar;
            _recordScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            _resultPanel.gameObject.SetActive(false);
        }

        private Scrollbar BuildRecordScrollbar()
        {
            RectTransform track = Panel("RecordScrollbar", _resultPanel, Silver, 1380, 332, 20, 264);
            Image trackImage = track.GetComponent<Image>();
            trackImage.raycastTarget = true;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            RectTransform slidingArea = Panel("SlidingArea", track, Color.clear, 3, 3, 14, 258);
            RectTransform handle = Panel("Handle", slidingArea, Blue, 0, 0, 14, 258);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(1f, 1f);
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            Image handleImage = handle.GetComponent<Image>();
            handleImage.raycastTarget = true;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        private static RectTransform Panel(string name, Transform parent, Color color, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Place(rect, x, y, width, height);
            go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().raycastTarget = false;
            return rect;
        }

        private static RectTransform CirclePanel(string name, Transform parent, Color color, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UICircleGraphic));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Place(rect, 0, 0, size, size);
            var graphic = go.GetComponent<UICircleGraphic>();
            graphic.color = color;
            graphic.raycastTarget = false;
            return rect;
        }

        private static RectTransform SpritePanel(string name, Transform parent, Sprite sprite,
            float x, float y, float width, float height)
        {
            if (sprite == null)
            {
                RectTransform fallback = CirclePanel(name, parent, Color.white, width);
                Place(fallback, x, y, width, height);
                return fallback;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Place(rect, x, y, width, height);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private static Text Label(string name, Transform parent, string value, int size, float x, float y,
            float width, float height, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), x, y, width, height);
            var label = go.GetComponent<Text>();
            label.font = _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static Button Control(string name, Transform parent, string text, float x, float y, float width,
            UnityEngine.Events.UnityAction action)
        {
            var rect = Panel(name, parent, Silver, x, y, width, 38);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color32(170, 215, 240, 255);
            colors.pressedColor = new Color32(105, 178, 220, 255);
            button.colors = colors;
            Label("Label", rect, text, 15, 4, 0, width - 8, 38, Ink).alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(action);
            return button;
        }
    }
}
