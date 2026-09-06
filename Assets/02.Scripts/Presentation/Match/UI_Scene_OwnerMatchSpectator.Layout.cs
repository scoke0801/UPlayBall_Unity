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
        private Text _announcement, _commentary, _resultHeading, _resultSummary, _managerDecisionSummary, _recordHeader;
        private Text _scoreCaption, _resultToggleLabel;
        private Button _pauseButton, _advanceButton, _revealAllButton, _homeButton, _resultButton;
        private Button[] _speedButtons;
        private Button[] _viewingModeButtons;
        private readonly Image[] _balls = new Image[4];
        private readonly Image[] _strikes = new Image[3];
        private readonly Image[] _outs = new Image[3];
        private readonly Image[] _bases = new Image[3];
        private RawImage _stadiumBackground, _actors, _actorsBlend;
        private Texture2D _pitchBackground;
        private readonly Texture2D[] _rightBatterOverlays = new Texture2D[8];
        private readonly Texture2D[] _leftBatterOverlays = new Texture2D[8];
        private Material _overlayMaterial;
        private RectTransform _scorePanel, _scoreRows, _resultPanel, _recordContent;
        private RectTransform _resultScoreRows;
        private ScrollRect _recordScroll;

        private void Build()
        {
            _canvas = Panel("BroadcastCanvas", _root, Ink, 0, 0, 1440, 810);
            _canvas.anchorMin = _canvas.anchorMax = new Vector2(0.5f, 0.5f);
            _canvas.pivot = new Vector2(0.5f, 0.5f);
            _canvas.anchoredPosition = Vector2.zero;
            _pitchBackground = LoadStadiumTexture("UI/OwnerMatch/stadium_pitch_background");
            LoadOverlaySet(_rightBatterOverlays, "rr");
            LoadOverlaySet(_leftBatterOverlays, "rl");
            Shader overlayShader = Resources.Load<Shader>("UI/OwnerMatch/OwnerMatchOverlayKey");
            if (overlayShader != null)
                _overlayMaterial = new Material(overlayShader) { name = "OwnerMatchOverlayMaterial" };
            RectTransform field = Panel("Field", _canvas, new Color32(44, 75, 47, 255), 0, 62, 1440, 748);
            field.gameObject.AddComponent<RectMask2D>();
            var backdrop = new GameObject("StadiumBackground", typeof(RectTransform), typeof(RawImage));
            backdrop.transform.SetParent(field, false);
            _stadiumBackground = backdrop.GetComponent<RawImage>();
            Place(_stadiumBackground.rectTransform, 0, 0, 1440, 748);
            _stadiumBackground.raycastTarget = false;
            SetLayerTexture(_stadiumBackground, _pitchBackground, false);
            _actors = CreateActorLayer("StadiumActors", field);
            _actorsBlend = CreateActorLayer("StadiumActorsBlend", field);
            HideBlendLayer();
            SetActorTexture(_rightBatterOverlays[0], false);
            BuildHeader();
            BuildFieldOverlay();
            BuildFooter();
            BuildResults();
            FitWorkspace();
        }

        private RawImage CreateActorLayer(string name, Transform parent)
        {
            var actorObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            actorObject.transform.SetParent(parent, false);
            RawImage actor = actorObject.GetComponent<RawImage>();
            Place(actor.rectTransform, 0, 0, 1440, 748);
            actor.raycastTarget = false;
            actor.material = _overlayMaterial;
            return actor;
        }

        private static void LoadOverlaySet(Texture2D[] target, string suffix)
        {
            string[] names = { "set", "windup", "pitch1", "pitch2", "flight", "hit", "miss", "take" };
            for (int index = 0; index < names.Length; index++)
                target[index] = LoadStadiumTexture("UI/OwnerMatch/stadium_overlay_" + names[index] + "_" + suffix);
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
            var speeds = new[] { OwnerMatchPlaybackSpeed.Normal, OwnerMatchPlaybackSpeed.Fast, OwnerMatchPlaybackSpeed.VeryFast };
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
            var badge = Panel("LiveBadge", _canvas, new Color(0.08f, 0.12f, 0.15f, 0.88f), 20, 80, 250, 36);
            _statusLabel = Label("LiveStatus", badge, "경기 중계", 17, 12, 0, 226, 36, Color.white);
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
                    282 + index * 112,
                    79,
                    106,
                    () => HandleViewingModeRequested(mode));
            }
            var runners = Panel("BaseOccupancy", _canvas, new Color(0.08f, 0.12f, 0.15f, 0.85f), 1300, 80, 120, 118);
            Label("BaseTitle", runners, "주자 상황", 13, 0, 2, 120, 25, Color.white).alignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < 3; i++)
            {
                float x = i == 0 ? 83 : i == 1 ? 52 : 21;
                float y = i == 1 ? 38 : 67;
                var rect = Panel("Base" + (i + 1), runners, Muted, x, y, 16, 16);
                rect.localEulerAngles = new Vector3(0, 0, 45);
                _bases[i] = rect.GetComponent<Image>();
            }
            _announcement = Label("PlayAnnouncement", _canvas, "", 58, 300, 280, 840, 92, Color.white);
            _announcement.alignment = TextAnchor.MiddleCenter;
            var shadow = _announcement.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(3, -4);
            var pitcher = ParticipantCard("PitcherCard", 20, PlayerPosition.StartingPitcher);
            _pitcherLabel = Label("Name", pitcher, "투수", 22, 94, 30, 286, 36, Ink);
            _pitcherDetail = Label("Detail", pitcher, "등판 대기", 15, 94, 69, 286, 27, Muted);
            var batter = ParticipantCard("BatterCard", 1020, PlayerPosition.DesignatedHitter);
            _batterLabel = Label("Name", batter, "타자", 22, 94, 30, 286, 36, Ink);
            _batterDetail = Label("Detail", batter, "타석 대기", 15, 94, 69, 286, 27, Muted);
            _scorePanel = Panel("InningOverlay", _canvas, new Color(0.06f, 0.09f, 0.13f, 0.83f), 140, 233, 1160, 226);
            _scoreCaption = Label("Caption", _scorePanel, "공수 교대", 31, 20, 8, 1120, 54, Color.white);
            _scoreCaption.alignment = TextAnchor.MiddleCenter;
            _scoreRows = Panel("LineScore", _scorePanel, Paper, 20, 78, 1120, 126);
            _scorePanel.gameObject.SetActive(false);
        }

        private RectTransform ParticipantCard(string name, float x, PlayerPosition position)
        {
            var card = Panel(name, _canvas, Paper, x, 552, 400, 112);
            Panel("Accent", card, Blue, 0, 0, 400, 3);
            var portrait = Panel("Portrait", card, Silver, 8, 12, 76, 92).GetComponent<Image>();
            portrait.sprite = PlayerPortraitSprites.GetDefault(position);
            portrait.preserveAspect = true;
            Label("Role", card, position == PlayerPosition.StartingPitcher ? "마운드 · 투수" : "타석 · 타자", 13,
                94, 7, 286, 24, Blue);
            return card;
        }

        private void BuildFooter()
        {
            Panel("Footer", _canvas, Paper, 0, 682, 1440, 128);
            Panel("FooterRule", _canvas, Silver, 0, 682, 1440, 2);
            Label("CommentaryTitle", _canvas, "경기 중계", 16, 20, 692, 106, 28, Blue);
            _commentary = Label("Commentary", _canvas, "잠시 후 경기가 시작됩니다.", 17, 140, 692, 900, 108, Ink);
            _commentary.alignment = TextAnchor.UpperLeft;
            _advanceButton = Control("Advance", _canvas, "다음 타석", 1070, 700, 160, HandleAdvanceRequested);
            _resultButton = Control("Result", _canvas, "경기 결과", 1242, 700, 176, () =>
            {
                _showResults = !_showResults;
                RefreshControls();
            });
            _resultToggleLabel = _resultButton.GetComponentInChildren<Text>();
            _homeButton = Control("ReturnHome", _canvas, "구단 홈으로", 1242, 752, 176, () => HomeRequested?.Invoke());
            Label("AiNote", _canvas, "감독 AI 자동 운영", 13, 1070, 753, 168, 32, Muted);
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
            _managerDecisionSummary = Label("ManagerDecisions", _resultPanel, "", 14, 40, 270, 1360, 62, Muted);
            _managerDecisionSummary.alignment = TextAnchor.MiddleLeft;
            Control("AwayRecords", _resultPanel, "원정 기록", 40, 342, 135, () => { _showHomeRecords = false; RenderRecords(); });
            Control("HomeRecords", _resultPanel, "홈 기록", 185, 342, 135, () => { _showHomeRecords = true; RenderRecords(); });
            Control("BattingRecords", _resultPanel, "타격 성적", 340, 342, 135, () => { _showPitching = false; RenderRecords(); });
            Control("PitchingRecords", _resultPanel, "투구 성적", 485, 342, 135, () => { _showPitching = true; RenderRecords(); });
            _recordHeader = Label("RecordHeader", _resultPanel, "", 17, 650, 342, 748, 38, Blue);
            _recordHeader.alignment = TextAnchor.MiddleRight;
            var viewport = Panel("RecordViewport", _resultPanel, Silver, 40, 394, 1360, 202);
            viewport.gameObject.AddComponent<RectMask2D>();
            _recordScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _recordContent = Panel("Records", viewport, Paper, 0, 0, 1360, 202);
            _recordScroll.viewport = viewport;
            _recordScroll.content = _recordContent;
            _recordScroll.horizontal = false;
            _recordScroll.movementType = ScrollRect.MovementType.Clamped;
            _recordScroll.scrollSensitivity = 30;
            _resultPanel.gameObject.SetActive(false);
        }

        private static Texture2D LoadStadiumTexture(string path)
        {
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture != null) return texture;

            Sprite[] sprites = Resources.LoadAll<Sprite>(path);
            if (sprites.Length == 0) return null;
            Sprite largest = sprites[0];
            for (int i = 1; i < sprites.Length; i++)
            {
                if (sprites[i].rect.width * sprites[i].rect.height > largest.rect.width * largest.rect.height)
                    largest = sprites[i];
            }
            return largest.texture;
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
