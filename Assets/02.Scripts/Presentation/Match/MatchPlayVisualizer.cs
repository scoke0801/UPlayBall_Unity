using System;
using Baseball.Core.Players;
using Baseball.Presentation.Career;
using Baseball.Presentation.UI;
using Baseball.Simulation.Match;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match
{
    /// <summary>모드와 무관하게 공식 사건을 상공 구장의 공·야수·주자에 투영한다.</summary>
    public sealed class MatchPlayVisualizer
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
        private readonly RectTransform[] _trail = new RectTransform[24];
        private readonly Func<int, string> _getName;
        private MatchEvent _event;
        private BallInPlayEventData _play;
        private Vector2 _ballStart, _ballEnd, _fielderStart;
        private int _activeFielder = -1, _activeRunner = -1;

        /// <summary>고정 수의 공·야수·주자 표시 부품을 한 번 생성한다.</summary>
        public MatchPlayVisualizer(RectTransform field, MatchGameCastConfig config, Font font, Func<int, string> getName)
        {
            _field = field;
            _config = config;
            _getName = getName;
            var background = new GameObject("StadiumBackground", typeof(RectTransform), typeof(RawImage));
            background.transform.SetParent(field, false);
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
            Reset();
        }

        /// <summary>다음 경기 또는 이닝의 초기 위치로 돌아간다.</summary>
        public void Reset()
        {
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
            for (int index = 0; index < _runners.Length; index++)
            {
                _runnerIds[index] = 0;
                _runners[index].gameObject.SetActive(false);
            }
            Register(hud.Bases.First.PlayerId, 1);
            Register(hud.Bases.Second.PlayerId, 2);
            Register(hud.Bases.Third.PlayerId, 3);
        }

        /// <summary>같은 투구의 공식 수비 데이터로 다음 사건의 이동만 준비한다.</summary>
        public void Begin(in MatchEvent value, in BallInPlayEventData play)
        {
            _event = value;
            _play = play;
            _activeRunner = -1;
            if (value.EventType == MatchEventType.Pitch)
            {
                ResetFielders();
                foreach (RectTransform dot in _trail) dot.gameObject.SetActive(false);
                _ballStart = _fielders[0].rectTransform.anchoredPosition;
                _ballEnd = Point(PlayResolutionFieldLayout.Home);
            }
            else if (value.EventType == MatchEventType.Contact && play.HasValue)
            {
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
            Render(0f);
        }

        /// <summary>공식 사건의 진행률에 맞춰 이동 경로를 그린다.</summary>
        public void Render(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (_event.EventType == MatchEventType.Pitch || _event.EventType == MatchEventType.RunnerThrownOut)
                MoveBall(Vector2.Lerp(_ballStart, _ballEnd, progress));
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
                int from = Mathf.Clamp(_event.FromBase, 0, 3);
                int to = Mathf.Clamp(_event.ToBase, from + 1, 4);
                float position = Mathf.Lerp(from, to, progress);
                int segment = Math.Min(to - 1, (int)position);
                _runners[_activeRunner].rectTransform.anchoredPosition =
                    Vector2.Lerp(BasePoint(segment), BasePoint(segment + 1), position - segment);
            }
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
            _runners[slot].gameObject.SetActive(true);
            _runners[slot].rectTransform.anchoredPosition = BasePoint(baseNumber);
            _runnerNames[slot].text = _getName(playerId);
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
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
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
