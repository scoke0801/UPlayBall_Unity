using Baseball.Core.Players;
using Baseball.Presentation.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match.Sprites
{
    /// <summary>두 경기 모드가 공유하는 고정 수 선수·공·카메라의 2.5D 무대다.</summary>
    public sealed class SpriteMatchStage
    {
        private static readonly PlayerPosition[] Positions =
        {
            PlayerPosition.StartingPitcher, PlayerPosition.Catcher, PlayerPosition.FirstBase,
            PlayerPosition.SecondBase, PlayerPosition.ThirdBase, PlayerPosition.Shortstop,
            PlayerPosition.LeftField, PlayerPosition.CenterField, PlayerPosition.RightField
        };
        private readonly SpriteAnimationCatalog _catalog;
        private readonly RectTransform _viewport;
        private readonly RectTransform _content;
        private readonly FieldProjection _projection;
        private readonly SpriteActor[] _fielders = new SpriteActor[9];
        private readonly SpriteActor[] _runners = new SpriteActor[4];
        private readonly Image[] _runnerMarkers = new Image[4];
        private readonly Image _catcherMarker;
        private readonly SpriteActor _batter;
        private readonly SpriteActor[] _sorted = new SpriteActor[14];
        private readonly BallVisualController _ball;
        private readonly BaseballCameraDirector _camera;
        private SpriteClipDefinition _pitch, _swing, _ground, _fly, _run, _runnerIdle, _catcher;
        private Handedness _batting;
        private bool _canPresent;
        private int _receivingFielder = -1;
        private Vector2 _receiverOrigin;
        public bool IsAvailable => _canPresent;
        public FieldProjection Projection => _projection;

        public float PitchDuration => _canPresent ? Mathf.Max(_pitch.DurationSeconds, _swing.DurationSeconds) : 0f;

        /// <summary>승인 카탈로그를 표시할 고정 수의 무대 부품을 생성한다.</summary>
        public SpriteMatchStage(RectTransform parent, SpriteAnimationCatalog catalog, Sprite ballSprite)
        {
            _catalog = catalog;
            _projection = new FieldProjection(catalog.fieldLayout);
            _viewport = new GameObject("SpriteMatchStage", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            _viewport.SetParent(parent, false);
            _viewport.anchorMin = Vector2.zero;
            _viewport.anchorMax = Vector2.one;
            _viewport.offsetMin = _viewport.offsetMax = Vector2.zero;
            _content = new GameObject("FieldCamera", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
            _content.SetParent(_viewport, false);
            _content.GetComponent<RawImage>().texture = catalog.fieldLayout.background;
            _content.GetComponent<RawImage>().raycastTarget = false;
            for (int i = 0; i < _fielders.Length; i++)
                _sorted[i] = _fielders[i] = new SpriteActor(_content, _projection, "Fielder" + Positions[i],
                    i == 1 ? catalog.fieldLayout.catcherScale : 1f);
            _sorted[9] = _batter = new SpriteActor(_content, _projection, "Batter");
            _catcherMarker = SpriteActor.CreateImage(_content, "CatcherMarker", true);
            _catcherMarker.color = new Color32(33, 77, 106, 255);
            _catcherMarker.rectTransform.anchorMin = _catcherMarker.rectTransform.anchorMax = new Vector2(0, 1);
            _catcherMarker.rectTransform.sizeDelta = new Vector2(14, 14);
            for (int i = 0; i < _runners.Length; i++)
            {
                _sorted[10 + i] = _runners[i] = new SpriteActor(_content, _projection, "Runner" + i, catalog.fieldLayout.runnerScale);
                _runnerMarkers[i] = SpriteActor.CreateImage(_content, "RunnerMarker" + i, true);
                _runnerMarkers[i].color = new Color32(255, 195, 72, 255);
                _runnerMarkers[i].rectTransform.anchorMin = _runnerMarkers[i].rectTransform.anchorMax = new Vector2(0, 1);
                _runnerMarkers[i].rectTransform.sizeDelta = new Vector2(14, 14);
            }
            _ball = new BallVisualController(_content, ballSprite, _projection);
            _camera = new BaseballCameraDirector(_content, catalog.fieldLayout);
            SetHands(Handedness.Right, Handedness.Right);
            SetVisible(false);
        }

        /// <summary>투타 방향별 승인 시트가 모두 있을 때에만 무대를 활성화할 수 있다.</summary>
        public bool SetHands(Handedness throwing, Handedness batting)
        {
            _canPresent = BaseballVisualSequenceResolver.CanPresent(_catalog, throwing, batting);
            if (!_canPresent) { SetVisible(false); return false; }
            _batting = batting;
            _catalog.TryGetClip(BaseballVisualSequenceResolver.PitchClip(throwing), out _pitch);
            _catalog.TryGetClip(BaseballVisualSequenceResolver.SwingClip(batting), out _swing);
            _catalog.TryGetClip("Fielder.InfieldGrounder", out _ground);
            _catalog.TryGetClip("Fielder.OutfieldFlyCatch", out _fly);
            _catalog.TryGetClip("Runner.Run", out _run);
            _catalog.TryGetClip("Runner.Idle", out _runnerIdle);
            _catalog.TryGetClip("Catcher.Idle", out _catcher);
            return true;
        }

        /// <summary>미검수 모션 조합은 요청과 관계없이 숨긴다.</summary>
        public void SetVisible(bool visible) => _viewport.gameObject.SetActive(visible && _canPresent);

        /// <summary>정본 구장 비율을 보존하고 다음 투구의 초기 자세로 돌아간다.</summary>
        public void Reset()
        {
            if (!_canPresent) return;
            SetVisible(true);
            _receivingFielder = -1;
            FitBackground();
            _camera.Render(BaseballCameraShot.Duel, new Vector2(0.5f, 0.5f), 1f);
            for (int i = 0; i < _fielders.Length; i++)
                _fielders[i].Render(i == 0 ? _pitch : i == 1 ? _catcher : i >= 6 ? _fly : _ground,
                    0, _projection.GetFielder(Positions[i]));
            // 포수 모션 대신 내야수 시트를 확대하면 타석과 접점을 가리므로 위치 표식만 사용한다.
            _catcherMarker.gameObject.SetActive(_catcher == null);
            _catcherMarker.rectTransform.anchoredPosition = FieldProjection.ToScreen(
                _projection.GetFielder(PlayerPosition.Catcher), _content.rect.size);
            _batter.Render(_swing, 0, BatterPoint());
            ClearRunners();
            _ball.Hide();
            SortActors();
        }

        /// <summary>투구·스윙 시트의 사건 위치에 맞춰 외부 공의 시작과 도착을 연결한다.</summary>
        public void RenderPitch(float progress, bool didSwing, bool didContact = false)
        {
            if (!_canPresent) return;
            FitBackground();
            float t = Mathf.Clamp01(progress);
            _pitch.TryGetEventTime(SpriteAnimationEvent.BallRelease, out float release);
            _swing.TryGetEventTime(SpriteAnimationEvent.BatContact, out float contact);
            // 투구 끝에서 판정을 공개하므로 타격 접점도 정확히 그 경계에 둔다.
            float pitchTime = t * _pitch.DurationSeconds;
            float releaseProgress = release / _pitch.DurationSeconds;
            _fielders[0].Render(_pitch, pitchTime, _projection.GetFielder(PlayerPosition.StartingPitcher));
            _fielders[1].Render(_catcher, t * PitchDuration, _projection.GetFielder(PlayerPosition.Catcher));
            // 헛스윙을 투수 준비 동작부터 재생하면 공이 출발할 때 이미 배트가 지나간다.
            // 투구 후반에 원래 길이의 스윙 전체를 배치해 비행 중 스윙과 후속 자세를 보여준다.
            float swingTime = didContact
                ? Mathf.Max(0, contact - (1f - t) * _swing.DurationSeconds)
                : Mathf.Max(0, t * PitchDuration - (PitchDuration - _swing.DurationSeconds));
            _batter.Render(_swing, didSwing ? swingTime : 0, BatterPoint());
            if (t < releaseProgress) _ball.Hide();
            else _ball.Render(ReleasePoint(), didSwing && didContact ? ContactPoint() : PlatePoint(), Mathf.InverseLerp(releaseProgress, 1, t), 0);
            if (t >= 1f && !didContact) _ball.Hide();
            _camera.Render(BaseballCameraShot.Duel, new Vector2(0.5f, 0.5f), 1);
            SortActors();
        }

        /// <summary>타구 종류와 수비 실패 기록으로 이동·포구만 표현한다.</summary>
        public void RenderContact(in BallInPlayEventData play, float progress)
        {
            if (!_canPresent || !play.HasValue) return;
            float t = Mathf.Clamp01(progress);
            Vector2 target = _projection.Project(PlayResolutionFieldLayout.GetBattedBallTarget(play.BattedBall));
            SpriteClipDefinition defense = play.BattedBall.Type is BattedBallType.FlyBall or BattedBallType.PopUp or BattedBallType.LineDrive ? _fly : _ground;
            defense.TryGetEventTime(SpriteAnimationEvent.GloveContact, out float catchTime);
            _swing.TryGetEventTime(SpriteAnimationEvent.BatContact, out float contactTime);
            _batter.Render(_swing, Mathf.Lerp(contactTime, _swing.DurationSeconds, t), BatterPoint());
            int index = FindFielder(play.Fielding.FielderPosition);
            bool hasFielder = play.Fielding.HasValue && !play.BattedBall.IsHomeRun;
            bool caught = hasFielder && play.Fielding.FailureType is FieldingFailureType.None or FieldingFailureType.ThrowingError;
            if (hasFielder)
            {
                float reach = play.Fielding.FailureType == FieldingFailureType.Reach ? 0.76f : 1f;
                float frameTime = caught ? t * catchTime : Mathf.Min(t * catchTime, Mathf.Max(0, catchTime - 0.001f));
                _fielders[index].Render(defense, frameTime,
                    Vector2.Lerp(_projection.GetFielder(Positions[index]), target, t * reach));
            }
            Vector2 ballTarget = target;
            if (caught && _fielders[index].TryProjectEvent(defense, SpriteAnimationEvent.GloveContact, target, out Vector2 glovePoint))
                ballTarget = glovePoint;
            if (caught && t >= 1f) _ball.Hide();
            else _ball.Render(ContactPoint(), ballTarget, t,
                BaseballVisualSequenceResolver.GetBallHeight(play.BattedBall.Type, _catalog.fieldLayout.ballHeightScale));
            float peak = Mathf.Clamp(_catalog.fieldLayout.contactCameraPeakProgress, 0.01f, 0.9f);
            if (t <= peak)
                _camera.Render(BaseballCameraShot.Contact, ContactPoint(), t / peak);
            else
                _camera.RenderTransition(BaseballCameraShot.Contact, ContactPoint(),
                    play.BattedBall.IsHomeRun ? BaseballCameraShot.Highlight : BaseballCameraShot.Field,
                    target, Mathf.InverseLerp(peak, 1f, t));
            SortActors();
        }

        /// <summary>주자의 실제 시작·도착 베이스만 이동에 사용한다.</summary>
        public void RenderRunner(int slot, int fromBase, int toBase, float progress)
        {
            if (!_canPresent || slot < 0 || slot >= _runners.Length) return;
            if (fromBase == 0 && toBase > 0 && progress > 0) _batter.SetVisible(false);
            int from = Mathf.Clamp(fromBase, 0, 3), to = Mathf.Clamp(toBase, from, 4);
            float position = Mathf.Lerp(from, to, Mathf.Clamp01(progress));
            int segment = Mathf.Min(3, (int)position);
            Vector2 origin = fromBase == 0 && segment == 0 ? BatterPoint() : _projection.GetBase(segment);
            Vector2 point = Vector2.Lerp(origin, _projection.GetBase(segment + 1), position - segment);
            // 주루 전용 모션이 없으면 배트를 든 타격 그림을 주자로 재활용하지 않는다.
            if (_run != null)
            {
                float elapsed = progress * (to - from) * _catalog.fieldLayout.runnerSecondsPerBase;
                bool isWaiting = from == to || progress >= 1f;
                _runners[slot].Render(isWaiting && _runnerIdle != null ? _runnerIdle : _run, elapsed, point);
                _runners[slot].SetFacingLeft(_projection.GetBase(segment + 1).x < _projection.GetBase(segment).x);
            }
            else
            {
                _runnerMarkers[slot].gameObject.SetActive(true);
                _runnerMarkers[slot].rectTransform.anchoredPosition = FieldProjection.ToScreen(point, _content.rect.size);
            }
            SortActors();
        }

        /// <summary>기록의 송구 목적지로 던지고 시트의 손 이탈 사건까지 외부 공을 숨긴다.</summary>
        public bool RenderFieldThrow(in BallInPlayEventData play, Vector2 destination, float progress)
        {
            if (!_canPresent || !play.Fielding.HasValue) return false;
            SpriteClipDefinition clip = play.BattedBall.Type is BattedBallType.GroundBall or BattedBallType.Bunt ? _ground : _fly;
            Vector2 start = _projection.Project(PlayResolutionFieldLayout.GetBattedBallTarget(play.BattedBall));
            return RenderThrowFromActor(FindFielder(play.Fielding.FielderPosition), clip, start, destination, progress);
        }

        private bool RenderThrowFromActor(int index, SpriteClipDefinition clip, Vector2 start, Vector2 destination, float progress)
        {
            if (!clip.TryGetEventTime(SpriteAnimationEvent.GloveContact, out float catchTime) ||
                !clip.TryGetEventTime(SpriteAnimationEvent.ThrowRelease, out float releaseTime))
            {
                // 손 이탈 시점이 없는 시트로 송구를 지어내지 않고 공식 결과만 기존 공개 경계에서 보여준다.
                _ball.Hide();
                return false;
            }
            float time = Mathf.Lerp(catchTime, clip.DurationSeconds, Mathf.Clamp01(progress));
            _fielders[index].Render(clip, time, start);
            if (time < releaseTime || progress >= 1f) _ball.Hide();
            else
            {
                Vector2 release = _fielders[index].TryProjectEvent(
                    clip, SpriteAnimationEvent.ThrowRelease, start, out Vector2 handPoint) ? handPoint : start;
                _ball.Render(release, destination, Mathf.InverseLerp(releaseTime, clip.DurationSeconds, time), 0.025f);
            }
            SortActors();
            return true;
        }

        /// <summary>공식 주자 아웃 사건의 목적 베이스로 송구한다.</summary>
        public bool RenderFieldThrowToBase(in BallInPlayEventData play, int toBase, float progress)
        {
            if (!_canPresent || !play.Fielding.HasValue || toBase < 1 || toBase > 4) return false;
            int receiver = GetBaseReceiverIndex(toBase);
            Vector2 destination = _projection.GetBase(toBase);
            float t = Mathf.Clamp01(progress);
            _ground.TryGetEventTime(SpriteAnimationEvent.GloveContact, out float receiveTime);
            if (receiver == FindFielder(play.Fielding.FielderPosition))
            {
                // 자기 자신에게 공을 던지는 대신 공을 가진 야수가 확정 목적 베이스로 이동한다.
                Vector2 pickup = _projection.Project(PlayResolutionFieldLayout.GetBattedBallTarget(play.BattedBall));
                _fielders[receiver].Render(_ground, receiveTime, Vector2.Lerp(pickup, destination, t));
                _ball.Hide();
                SortActors();
                return true;
            }
            return RenderFieldThrow(play, RenderBaseReceiver(toBase, t), t);
        }

        /// <summary>직전 베이스에서 공을 받은 야수의 손 이탈부터 다음 베이스의 포구까지 잇는다.</summary>
        public bool RenderRelayThrow(int fromBase, int toBase, float progress)
        {
            if (!_canPresent || fromBase < 1 || fromBase > 3 || toBase < 1 || toBase > 4 || fromBase == toBase)
            {
                _ball.Hide();
                return false;
            }
            int sender = GetBaseReceiverIndex(fromBase);
            Vector2 start = _fielders[sender].Position;
            return RenderThrowFromActor(sender, _ground, start, RenderBaseReceiver(toBase, progress), progress);
        }

        private static int GetBaseReceiverIndex(int baseNumber) => baseNumber switch { 1 => 2, 2 => 3, 3 => 4, _ => 1 };

        private Vector2 RenderBaseReceiver(int toBase, float progress)
        {
            int receiver = GetBaseReceiverIndex(toBase);
            Vector2 destination = _projection.GetBase(toBase);
            float t = Mathf.Clamp01(progress);
            _ground.TryGetEventTime(SpriteAnimationEvent.GloveContact, out float receiveTime);
            // 송구 도착 전에 수신 야수가 베이스를 커버한다. 포수는 전용 장비 시트를 유지한다.
            if (_receivingFielder != receiver || t <= 0)
            {
                _receivingFielder = receiver;
                _receiverOrigin = _fielders[receiver].Position;
            }
            SpriteClipDefinition receiveClip = receiver == 1 ? _catcher : _ground;
            Vector2 offset = _catalog.fieldLayout.baseReceiverOffset;
            Vector2 receivePosition = receiver == 1 ? destination : destination +
                new Vector2(toBase == 1 ? -offset.x : offset.x, -offset.y);
            Vector2 receiverPoint = Vector2.Lerp(_receiverOrigin, receivePosition,
                Mathf.SmoothStep(0, 1, Mathf.Clamp01(t * 2f)));
            _fielders[receiver].Render(receiveClip, receiveTime * t, receiverPoint);
            Vector2 glove = destination;
            if (receiveClip != null)
                _fielders[receiver].TryProjectEvent(receiveClip, SpriteAnimationEvent.GloveContact, receivePosition, out glove);
            return glove;
        }

        /// <summary>공식 주자 아웃 사건의 목적 베이스로 송구한다.</summary>
        public void RenderThrow(Vector2 start, int toBase, float progress) =>
            _ball.Render(start, _projection.GetBase(toBase), progress, 0.025f);

        /// <summary>파울과 병살 중계의 추가 송구처럼 명시된 경로를 재생한다.</summary>
        public void RenderBallFlight(Vector2 start, Vector2 end, float progress, float height) => _ball.Render(start, end, progress, height);

        /// <summary>공식 판정 이후 남아 있는 공 표시를 닫는다.</summary>
        public void HideBall() => _ball.Hide();

        /// <summary>타석 결과 표시 구간의 카메라 복귀 출발점을 저장한다.</summary>
        public void BeginReturnToDuel() => _camera.BeginReturnToDuel();

        /// <summary>다음 투구 전에 기본 구도로 부드럽게 돌아간다.</summary>
        public void RenderReturnToDuel(float progress) => _camera.RenderReturnToDuel(progress);

        /// <summary>이미 공개된 새 베이스 상태를 그리기 전에 이전 주자를 숨긴다.</summary>
        public void ClearRunners()
        {
            for (int i = 0; i < _runners.Length; i++)
            {
                _runners[i].SetVisible(false);
                _runnerMarkers[i].gameObject.SetActive(false);
            }
        }

        private Vector2 BatterPoint() => _catalog.fieldLayout.GetAnchor(_batting == Handedness.Left ? FieldAnchor.BatterBoxL : FieldAnchor.BatterBoxR);
        private Vector2 ReleasePoint()
        {
            Vector2 mound = _projection.GetFielder(PlayerPosition.StartingPitcher);
            return _fielders[0].TryProjectEvent(_pitch, SpriteAnimationEvent.BallRelease, mound, out Vector2 point)
                ? point : mound - _catalog.fieldLayout.pitcherReleaseOffset;
        }

        private Vector2 ContactPoint() => _batter.TryProjectEvent(_swing, SpriteAnimationEvent.BatContact, BatterPoint(), out Vector2 point)
            ? point : PlatePoint();

        private Vector2 PlatePoint() => _catalog.fieldLayout.GetAnchor(FieldAnchor.HomePlate) - _catalog.fieldLayout.batContactOffset;

        private void FitBackground()
        {
            Texture2D background = _catalog.fieldLayout.background;
            float aspect = (float)background.width / background.height;
            Vector2 size = _viewport.rect.size;
            float width = Mathf.Min(size.x, size.y * aspect);
            _content.sizeDelta = new Vector2(width, width / aspect);
        }

        private static int FindFielder(PlayerPosition position)
        {
            for (int i = 0; i < Positions.Length; i++) if (Positions[i] == position) return i;
            return 0;
        }

        private void SortActors()
        {
            // 고정 배열의 삽입 정렬로 프레임 할당 없이 깊이가 같은 선수 순서를 보존한다.
            for (int i = 1; i < _sorted.Length; i++)
            {
                SpriteActor actor = _sorted[i];
                int j = i - 1;
                while (j >= 0 && _sorted[j].Position.y > actor.Position.y) { _sorted[j + 1] = _sorted[j]; j--; }
                _sorted[j + 1] = actor;
            }
            for (int i = 0; i < _sorted.Length; i++) _sorted[i].SetSiblingIndex(i);
        }
    }
}
