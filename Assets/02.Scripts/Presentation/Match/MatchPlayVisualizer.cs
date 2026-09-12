using System;
using Baseball.Core.Players;
using Baseball.Presentation.Career;
using Baseball.Presentation.UI;
using Baseball.Simulation.Match;
using Baseball.Presentation.Match.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match
{
    /// <summary>모드와 무관하게 공식 사건을 상공 구장의 공·야수·주자에 투영한다.</summary>
    public sealed partial class MatchPlayVisualizer
    {
        private static readonly PlayerPosition[] Positions =
        {
            PlayerPosition.StartingPitcher, PlayerPosition.Catcher, PlayerPosition.FirstBase,
            PlayerPosition.SecondBase, PlayerPosition.ThirdBase, PlayerPosition.Shortstop,
            PlayerPosition.LeftField, PlayerPosition.CenterField, PlayerPosition.RightField
        };
        private static readonly string[] PositionLabels = { "투", "포", "1", "2", "3", "유", "좌", "중", "우" };
        private static readonly Color RunnerColor = new Color32(255, 195, 72, 255);
        private static readonly Color FielderColor = new Color32(33, 77, 106, 255);
        private readonly MatchGameCastConfig _config;
        private readonly RectTransform _field;
        private readonly RectTransform _ball;
        private readonly Image[] _fielders = new Image[9];
        private readonly Image[] _runners = new Image[4];
        private readonly Text[] _runnerNames = new Text[4];
        private readonly int[] _runnerIds = new int[4];
        private readonly int[] _runnerBases = new int[4];
        private readonly RectTransform[] _trail = new RectTransform[24];
        private readonly Func<int, string> _getName;
        private readonly Func<int, int, OwnerMatchHandedness> _getHands;
        private readonly SpriteMatchStage _spriteStage;
        private Material _awayUniform, _homeUniform;

        /// <summary>현재 경기 참가 구단의 유니폼을 카드와 같은 발급 규칙으로 고정한다.</summary>
        public void SetTeamUniforms(string awayFranchiseId, string homeFranchiseId)
        {
            _awayUniform = MatchUniformMaterials.GetForFranchise(awayFranchiseId);
            _homeUniform = MatchUniformMaterials.GetForFranchise(homeFranchiseId);
        }
        private MatchEvent _event;
        private BallInPlayEventData _play;
        private Vector2 _ballStart, _ballEnd, _fielderStart;
        private int _activeFielder = -1, _activeRunner = -1;
        private int _batterId, _firstVisibleId, _secondVisibleId, _thirdVisibleId;
        private int _throwDestination, _throwOriginBase, _ballHeldAtBase, _runnerFromBase, _runnerToBase;
        private float _batterApproachProgress;
        private bool _isBatterApproaching;

        /// <summary>고정 수의 공·야수·주자 표시 부품을 한 번 생성한다.</summary>
        public MatchPlayVisualizer(RectTransform field, MatchGameCastConfig config, Font font, Func<int, string> getName,
            Func<int, int, OwnerMatchHandedness> getHands = null)
        {
            _field = new GameObject("GameCastMarkers", typeof(RectTransform)).GetComponent<RectTransform>();
            _field.SetParent(field, false);
            _field.anchorMin = Vector2.zero;
            _field.anchorMax = Vector2.one;
            _field.offsetMin = _field.offsetMax = Vector2.zero;
            _config = config;
            _getName = getName;
            _getHands = getHands;
            var background = new GameObject("StadiumBackground", typeof(RectTransform), typeof(RawImage));
            background.transform.SetParent(_field, false);
            RectTransform backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            RawImage image = background.GetComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>(config.fieldTexture);
            image.raycastTarget = false;
            for (int index = 0; index < _trail.Length; index++)
                _trail[index] = CreateMarker("BallTrail" + index, 5, new Color(1f, 0.9f, 0.5f, 0.8f)).rectTransform;
            for (int index = 0; index < _fielders.Length; index++)
            {
                _fielders[index] = CreateMarker("Fielder" + PositionLabels[index], 27, FielderColor);
                AddText(_fielders[index].transform, PositionLabels[index], font, 14, 0, 0, 27, 27);
            }
            for (int index = 0; index < _runners.Length; index++)
            {
                _runners[index] = CreateMarker("Runner" + index, 19, RunnerColor);
                _runnerNames[index] = AddText(_runners[index].transform, "", font, 12, -43, 19, 105, 22);
                _runnerNames[index].gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
            }
            _ball = CreateBaseballMarker("Ball", config.fieldBallSize, config.LoadBaseballSprite()).rectTransform;
            SpriteAnimationCatalog catalog = Resources.Load<SpriteAnimationCatalog>("UI/SpriteMatch/AnimationCatalog");
            if (catalog != null && catalog.fieldLayout != null && catalog.fieldLayout.background != null)
                _spriteStage = new SpriteMatchStage(field, catalog, config.LoadBaseballSprite());
            Reset();
        }

        /// <summary>다음 경기 또는 이닝의 초기 위치로 돌아간다.</summary>
        public void Reset()
        {
            _play = default;
            _runnerRouteCount = 0;
            _batterApproachProgress = 0;
            _isBatterApproaching = false;
            _batterId = _firstVisibleId = _secondVisibleId = _thirdVisibleId = _ballHeldAtBase = _throwDestination = 0;
            _spriteStage?.SetVisible(false);
            _field.gameObject.SetActive(true);
            _activeFielder = _activeRunner = -1;
            ResetFielders();
            _ball.gameObject.SetActive(false);
            foreach (RectTransform dot in _trail) dot.gameObject.SetActive(false);
            for (int index = 0; index < _runners.Length; index++)
            {
                _runnerIds[index] = 0;
                _runners[index].gameObject.SetActive(false);
            }
        }

        /// <summary>이미 공개된 베이스 상태만 주자 이름과 위치에 반영한다.</summary>
        public void PresentBases(MatchHudPresentationModel hud)
        {
            _firstVisibleId = hud.Bases.First.PlayerId;
            _secondVisibleId = hud.Bases.Second.PlayerId;
            _thirdVisibleId = hud.Bases.Third.PlayerId;
            _spriteStage?.ClearRunners();
            for (int index = 0; index < _runners.Length; index++)
            {
                _runnerIds[index] = 0;
                _runners[index].gameObject.SetActive(false);
            }
            Register(hud.Bases.First.PlayerId, 1);
            Register(hud.Bases.Second.PlayerId, 2);
            Register(hud.Bases.Third.PlayerId, 3);
            if (_isBatterApproaching && _batterId != _firstVisibleId && _batterId != _secondVisibleId && _batterId != _thirdVisibleId)
            {
                int slot = Register(_batterId, 0);
                _spriteStage?.RenderRunner(slot, 0, 1, _batterApproachProgress);
            }
            RestoreUpcomingRunners();
        }

        /// <summary>같은 투구의 공식 수비 데이터로 다음 사건의 이동만 준비한다.</summary>
        public void Begin(in MatchEvent value, in BallInPlayEventData play)
        {
            _event = value;
            _spriteStage?.SetUniforms(value.Half == InningHalf.Top ? _awayUniform : _homeUniform,
                value.Half == InningHalf.Top ? _homeUniform : _awayUniform);
            if (value.EventType == MatchEventType.PlateAppearanceEnded) _spriteStage?.BeginReturnToDuel();
            _batterEventStart = _batterApproachProgress;
            for (int index = 0; index < _runnerRouteCount; index++) _routeEventStart[index] = _routeProgress[index];
            if (value.EventType == MatchEventType.Pitch || play.HasValue) _play = play;
            _activeRunner = -1;
            _runnerFromBase = value.FromBase;
            _runnerToBase = value.ToBase;
            _throwDestination = OwnerMatchPlaybackGroup.ResolveOutBase(value, _batterId, _firstVisibleId, _secondVisibleId, _thirdVisibleId);
            _throwOriginBase = _ballHeldAtBase;
            if (value.EventType == MatchEventType.Pitch)
            {
                _batterId = value.BatterId;
                _runnerRouteCount = 0;
                _batterApproachProgress = 0;
                _isBatterApproaching = false;
                _ballHeldAtBase = 0;
                if (_spriteStage != null && _getHands != null)
                {
                    OwnerMatchHandedness hands = _getHands(value.PitcherId, value.BatterId);
                    bool available = _spriteStage.SetHands(hands.ThrowingHand, hands.BattingHand);
                    _field.gameObject.SetActive(!available);
                    if (available)
                    {
                        _spriteStage.Reset();
                        for (int i = 0; i < _runnerIds.Length; i++)
                            if (_runnerIds[i] > 0) _spriteStage.RenderRunner(i, _runnerBases[i], _runnerBases[i], 1);
                    }
                }
                ResetFielders();
                foreach (RectTransform dot in _trail) dot.gameObject.SetActive(false);
                _ballStart = _fielders[0].rectTransform.anchoredPosition;
                _ballEnd = Point(PlayResolutionFieldLayout.Home);
            }
            else if (value.EventType == MatchEventType.Contact && play.HasValue)
            {
                _ballHeldAtBase = 0;
                _ballStart = Point(PlayResolutionFieldLayout.Home);
                _ballEnd = Point(PlayResolutionFieldLayout.GetBattedBallTarget(play.BattedBall));
                _activeFielder = Array.IndexOf(Positions, NormalizePosition(play.Fielding.FielderPosition));
                if (!play.Fielding.HasValue || play.BattedBall.IsHomeRun) _activeFielder = -1;
                if (_activeFielder >= 0)
                {
                    _fielderStart = _fielders[_activeFielder].rectTransform.anchoredPosition;
                    _fielders[_activeFielder].color = new Color32(48, 173, 160, 255);
                }
            }
            else if (value.EventType is MatchEventType.RunnerAdvance or MatchEventType.RunnerThrownOut)
            {
                _activeRunner = Register(value.PlayerId, value.FromBase);
                if (value.EventType == MatchEventType.RunnerThrownOut)
                {
                    _ballStart = _ball.anchoredPosition;
                    _ballEnd = BasePoint(value.ToBase);
                }
            }
            else if (value.EventType == MatchEventType.Out && _throwDestination > 0)
            {
                _runnerFromBase = value.PlayerId == _batterId ? 0 : _throwDestination - 1;
                _runnerToBase = _throwDestination;
                _activeRunner = Register(value.PlayerId, _runnerFromBase);
                _ballStart = _ball.anchoredPosition;
                _ballEnd = BasePoint(_throwDestination);
            }
            Render(0f);
        }

        /// <summary>모션의 프레임 체류 시간을 보존하도록 투구의 최소 재생 시간을 정한다.</summary>
        public float GetDuration(in MatchEvent value, float fallback)
        {
            if (_spriteStage == null || !_spriteStage.IsAvailable || _field.gameObject.activeSelf) return fallback;
            // Contact의 완성된 타구 데이터는 같은 타석의 종료 사건에 있다.
            // 세션이 제공한 데이터를 사용해 뜬공이 짧은 판정 시간으로 압축되지 않게 한다.
            return value.EventType switch
            {
                MatchEventType.Pitch => Mathf.Max(fallback, _spriteStage.PitchDuration),
                MatchEventType.Contact => Mathf.Max(fallback, _config.GetContactDuration(_play)),
                MatchEventType.RunnerAdvance or MatchEventType.RunnerThrownOut => Mathf.Max(fallback,
                    _spriteStage.Projection.Layout.runnerSecondsPerBase * Mathf.Max(1, value.ToBase - value.FromBase)),
                MatchEventType.Out when _throwDestination > 0 => Mathf.Max(fallback, _spriteStage.Projection.Layout.runnerSecondsPerBase),
                _ => fallback
            };
        }

        /// <summary>공식 사건의 진행률에 맞춰 이동 경로를 그린다.</summary>
        public void Render(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (_spriteStage != null && _spriteStage.IsAvailable && !_field.gameObject.activeSelf)
            {
                switch (_event.EventType)
                {
                    case MatchEventType.Pitch:
                        _spriteStage.RenderPitch(progress, _event.PitchPlayData.HasValue
                            ? _event.PitchPlayData.Swing.DidSwing
                            : _event.PitchResult is Baseball.Simulation.PlateAppearance.PitchResult.InPlay or
                                Baseball.Simulation.PlateAppearance.PitchResult.SwingingStrike or
                                Baseball.Simulation.PlateAppearance.PitchResult.Foul,
                            _event.PitchResult is Baseball.Simulation.PlateAppearance.PitchResult.InPlay or
                                Baseball.Simulation.PlateAppearance.PitchResult.Foul);
                        // 파울에는 별도의 Contact 사건이 없다. 다음 투구까지 접점에 공을 남기지 않는다.
                        if (progress >= 1f && _event.PitchResult == Baseball.Simulation.PlateAppearance.PitchResult.Foul)
                            _spriteStage.HideBall();
                        break;
                    case MatchEventType.Contact:
                        _spriteStage.RenderContact(_play, progress);
                        if (_play.HasValue)
                        {
                            // 페어 타구 뒤 출발은 세이프·아웃 판정과 무관하다. 결과 공개 전에는 1루에 도착시키지 않는다.
                            FieldLayoutDefinition layout = _spriteStage.Projection.Layout;
                            _batterApproachProgress = Mathf.Clamp(layout.batterRunLeadProgress, 0f, 0.95f) *
                                Mathf.InverseLerp(layout.batterRunStartProgress, 1f, progress);
                            _isBatterApproaching = _batterApproachProgress > 0;
                            if (_isBatterApproaching)
                                _spriteStage.RenderRunner(Register(_batterId, 0), 0, 1, _batterApproachProgress);
                        }
                        break;
                    case MatchEventType.RunnerAdvance:
                        _spriteStage.RenderRunner(_activeRunner, _event.FromBase, _event.ToBase,
                            ContinueRunnerRun(_event.PlayerId, _event.FromBase, _event.ToBase, progress));
                        if (progress >= 1 && _event.PlayerId == _batterId) _isBatterApproaching = false;
                        break;
                    case MatchEventType.RunnerThrownOut:
                    case MatchEventType.Out:
                        if (_activeRunner >= 0)
                            _spriteStage.RenderRunner(_activeRunner, _runnerFromBase, _runnerToBase,
                                ContinueRunnerRun(_event.PlayerId, _runnerFromBase, _runnerToBase, progress));
                        if (_throwDestination > 0 && _play.HasValue && _play.Fielding.HasValue)
                        {
                            bool hasThrowMotion = true;
                            if (_throwOriginBase > 0)
                                hasThrowMotion = _spriteStage.RenderRelayThrow(_throwOriginBase, _throwDestination, progress);
                            else
                                hasThrowMotion = _spriteStage.RenderFieldThrowToBase(_play, _throwDestination, progress);
                            if (progress >= 1 && hasThrowMotion)
                            {
                                _ballHeldAtBase = _throwDestination;
                                _spriteStage.HideBall();
                            }
                        }
                        if (progress >= 1 && _event.PlayerId == _batterId) _isBatterApproaching = false;
                        break;
                    case MatchEventType.PlateAppearanceEnded:
                        _spriteStage.RenderReturnToDuel(progress);
                        _spriteStage.HideBall();
                        _isBatterApproaching = false;
                        _runnerRouteCount = 0;
                        break;
                    case MatchEventType.HalfInningEnded:
                    case MatchEventType.MatchEnded:
                    case MatchEventType.MatchEndedAsDraw:
                        _spriteStage.HideBall();
                        _isBatterApproaching = false;
                        _runnerRouteCount = 0;
                        break;
                }
                RenderUpcomingRunners(progress);
                return;
            }
            if (_event.EventType == MatchEventType.Pitch || _event.EventType == MatchEventType.RunnerThrownOut ||
                (_event.EventType == MatchEventType.Out && _throwDestination > 0))
                MoveBall(Vector2.Lerp(_ballStart, _ballEnd, progress));
            if (_throwDestination > 0 && progress >= 1) _ballHeldAtBase = _throwDestination;
            if (_event.EventType == MatchEventType.Contact && _play.HasValue)
            {
                Vector2 point = FlightPoint(progress);
                MoveBall(point);
                for (int index = 0; index < _trail.Length; index++)
                {
                    float portion = (index + 1f) / _trail.Length;
                    _trail[index].gameObject.SetActive(portion <= progress);
                    _trail[index].anchoredPosition = FlightPoint(portion);
                }
                if (_activeFielder >= 0)
                {
                    // 도달 실패는 포구로 오인되지 않도록 공과 야수 사이에 간격을 남긴다.
                    float reach = _play.Fielding.FailureType == FieldingFailureType.Reach ? 0.76f : 1f;
                    _fielders[_activeFielder].rectTransform.anchoredPosition =
                        Vector2.Lerp(_fielderStart, _ballEnd, progress * reach);
                }
            }
            if (_activeRunner >= 0)
            {
                int from = Mathf.Clamp(_runnerFromBase, 0, 3);
                int to = Mathf.Clamp(_runnerToBase, from + 1, 4);
                float position = Mathf.Lerp(from, to, progress);
                int segment = Math.Min(to - 1, (int)position);
                _runners[_activeRunner].rectTransform.anchoredPosition =
                    Vector2.Lerp(BasePoint(segment), BasePoint(segment + 1), position - segment);
            }
        }

        private float ContinueRunnerRun(int playerId, int fromBase, int toBase, float progress)
        {
            for (int index = 0; index < _runnerRouteCount; index++)
                if (_runnerRoutes[index].PlayerId == playerId && _runnerRoutes[index].FromBase == fromBase && toBase > fromBase)
                    return Mathf.Lerp(_routeProgress[index] / (toBase - fromBase), 1f, progress);
            if (playerId != _batterId || fromBase != 0 || toBase <= 0) return progress;
            return Mathf.Lerp(_batterApproachProgress / toBase, 1f, progress);
        }

        private Vector2 FlightPoint(float progress)
        {
            Vector2 point = Vector2.Lerp(_ballStart, _ballEnd, progress);
            float lift = _play.BattedBall.Type switch
            {
                BattedBallType.FlyBall or BattedBallType.PopUp => 30f,
                BattedBallType.LineDrive => 9f,
                _ => 0f
            };
            point.y += Mathf.Sin(progress * Mathf.PI) * lift;
            return point;
        }

        private void ResetFielders()
        {
            for (int index = 0; index < _fielders.Length; index++)
            {
                Vector2 uv = _config.GetFielderTexturePoint(Positions[index]);
                _fielders[index].rectTransform.anchoredPosition =
                    new Vector2(uv.x * _field.rect.width, -uv.y * _field.rect.height);
                _fielders[index].color = FielderColor;
            }
        }

        private int Register(int playerId, int baseNumber)
        {
            if (playerId <= 0) return -1;
            int slot = Array.IndexOf(_runnerIds, playerId);
            if (slot < 0) slot = Array.IndexOf(_runnerIds, 0);
            if (slot < 0) return -1;
            _runnerIds[slot] = playerId;
            _runnerBases[slot] = baseNumber;
            _runners[slot].gameObject.SetActive(true);
            _runners[slot].rectTransform.anchoredPosition = BasePoint(baseNumber);
            _runnerNames[slot].text = _getName(playerId);
            if (_spriteStage != null && !_field.gameObject.activeSelf)
                _spriteStage.RenderRunner(slot, baseNumber, baseNumber, 1);
            return slot;
        }

        private Vector2 BasePoint(int value) => Point(PlayResolutionFieldLayout.GetBasePoint(value));

        private Vector2 Point(NormalizedFieldPoint value)
        {
            Vector2 uv = _config.ToTexturePoint(value);
            return new Vector2(uv.x * _field.rect.width, -uv.y * _field.rect.height);
        }

        private void MoveBall(Vector2 point)
        {
            _ball.gameObject.SetActive(true);
            _ball.anchoredPosition = point;
        }

        private Image CreateMarker(string name, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UICircleGraphic));
            go.transform.SetParent(_field, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0, 1);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }

        private Image CreateBaseballMarker(string name, float size, Sprite sprite)
        {
            if (sprite == null)
                return CreateMarker(name, size, Color.white);

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_field, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0, 1);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }

        private static Text AddText(Transform parent, string value, Font font, int size, float x, float y, float width, float height)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Baseball.Presentation.UI.UIProjectText));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0, 1);
            text.rectTransform.pivot = new Vector2(0, 1);
            text.rectTransform.anchoredPosition = new Vector2(x, -y);
            text.rectTransform.sizeDelta = new Vector2(width, height);
            return text;
        }

        private static PlayerPosition NormalizePosition(PlayerPosition value) =>
            value == PlayerPosition.ReliefPitcher ? PlayerPosition.StartingPitcher : value;
    }
}
