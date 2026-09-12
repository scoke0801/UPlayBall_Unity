using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using Baseball.Editor.SpriteSheets;
using Baseball.Presentation.Match.Sprites;
using Baseball.Presentation.Match;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tools.SpriteMatchValidation
{
    /// <summary>격리 Unity 프로젝트에서 가져오기 재현성과 검수용 16:9 합성을 검증한다.</summary>
    public static class ValidationRunner
    {
        /// <summary>명시한 처리 폴더를 두 번 가져오며 원본 경기 카탈로그의 승인은 변경하지 않는다.</summary>
        public static void Run()
        {
            string processed = Argument("-spriteProcessedRoot");
            string report = Argument("-spriteReportRoot");
            Directory.CreateDirectory(report);
            SpriteAnimationCatalog review = SpriteSheetBatchImporter.Import(processed);
            Dictionary<string, string> first = Snapshot();
            SpriteSheetBatchImporter.Import(processed);
            Dictionary<string, string> second = Snapshot();
            if (first.Count != second.Count || first.Any(pair => !second.TryGetValue(pair.Key, out string hash) || hash != pair.Value))
                throw new InvalidOperationException("재가져오기에서 생성 에셋 또는 GUID가 변경됐습니다.");
            SpriteAnimationCatalog production = AssetDatabase.LoadAssetAtPath<SpriteAnimationCatalog>(SpriteSheetBatchImporter.ProductionPath);
            if (production.clips.Any(clip => !clip.IsProductionReady))
                throw new InvalidOperationException("미검수 모션이 경기 카탈로그에 들어갔습니다.");
            File.WriteAllText(Path.Combine(report, "unity-import-qa.json"), JsonUtility.ToJson(new ImportReport
            {
                reviewClips = review.clips.Length, productionClips = production.clips.Length,
                comparedFiles = first.Count, isIdempotent = true,
                isRuntimeReady = BaseballVisualSequenceResolver.CanPresent(production, Handedness.Right, Handedness.Right)
            }, true));
            if (BaseballVisualSequenceResolver.CanPresent(production, Handedness.Right, Handedness.Right))
            {
                Capture(production, report);
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                    new Vector2Int(2560, 1440), new Vector2Int(3440, 1440) })
                    CaptureOwnerLayout(size, report);
                return;
            }
            // 검수 합성은 비저장 복사본만 사용한다. NeedsReview 원본을 경기용으로 승인하지 않는다.
            SpriteAnimationCatalog preview = UnityEngine.Object.Instantiate(review);
            try
            {
                foreach (SpriteClipDefinition clip in preview.clips)
                {
                    // 시안처럼 홈 쪽으로 배트가 향하는 배치 후보다. 손잡이 확정이나 승인으로 저장하지 않는다.
                    if (clip.clipId == "Batter.DuelSwing.A") clip.clipId = "Batter.DuelSwing.L";
                    if (clip.clipId == "Batter.DuelSwing.B") clip.clipId = "Batter.DuelSwing.R";
                    if (clip.clipId == "Batter.DuelSwing.R") clip.handedness = SpriteHandedness.Right;
                    if (clip.clipId == "Batter.DuelSwing.L") clip.handedness = SpriteHandedness.Left;
                    clip.approved = clip.handedness != SpriteHandedness.NeedsReview;
                }
                Capture(preview, report);
            }
            finally { UnityEngine.Object.DestroyImmediate(preview); }
        }

        /// <summary>모션 원본을 다시 가져오지 않고 실제 경기 재생과 네 해상도의 UI를 검증한다.</summary>
        public static void RunOwnerUi()
        {
            string report = Argument("-spriteReportRoot");
            Directory.CreateDirectory(report);
            Capture(null, report, ownerUiOnly: true);
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                         new Vector2Int(2560, 1440), new Vector2Int(3440, 1440) })
                CaptureOwnerLayout(size, report);
        }

        private static void Capture(SpriteAnimationCatalog catalog, string directory, bool ownerUiOnly = false)
        {
            var root = new GameObject("검수 전용 Canvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("검수 전용 Camera", typeof(Camera));
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(1280, 720);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = 360;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                if (ownerUiOnly)
                {
                    CaptureMatchReplay(rect, camera, target, pixels, directory, ownerUiOnly: true);
                    return;
                }
                var stage = new SpriteMatchStage(rect, catalog, null);
                foreach (Handedness hand in new[] { Handedness.Right, Handedness.Left })
                {
                    if (!stage.SetHands(hand, hand)) throw new InvalidOperationException("검수용 필수 모션 누락: " + hand);
                    stage.Reset();
                    foreach (float progress in new[] { 0f, 0.7f, 1f })
                    {
                        stage.RenderPitch(progress, true, true);
                        Save(camera, target, pixels, Path.Combine(directory, "review-" + hand + "-pitch-" + Mathf.RoundToInt(progress * 100) + ".png"));
                    }
                    string frames = Path.Combine(directory, "motion-" + hand);
                    Directory.CreateDirectory(frames);
                    for (int frame = 0; frame < 36; frame++)
                    {
                        stage.RenderPitch(frame / 35f, true, true);
                        Save(camera, target, pixels, Path.Combine(frames, $"{frame:D3}.png"));
                    }
                }
                foreach (BattedBallType type in new[] { BattedBallType.GroundBall, BattedBallType.FlyBall })
                {
                    var ball = new BattedBallDescriptor(type, BattedBallDirection.Center,
                        type == BattedBallType.GroundBall ? FieldZone.Shortstop : FieldZone.CenterField,
                        0.5, BallFlightBand.Medium, BallPaceBand.Medium, false);
                    var fielding = new FieldingPlayOutcome(type == BattedBallType.GroundBall ? PlateAppearanceResult.GroundOut : PlateAppearanceResult.FlyOut,
                        type == BattedBallType.GroundBall ? PlayerPosition.Shortstop : PlayerPosition.CenterField,
                        1, FieldingFailureType.None, true, false, 1);
                    var play = new BallInPlayEventData(ball, fielding);
                    stage.Reset();
                    stage.RenderContact(play, 0.6f);
                    stage.RenderRunner(0, 0, 1, 0.5f);
                    Save(camera, target, pixels, Path.Combine(directory, "review-" + type + ".png"));
                    stage.RenderContact(play, 1f);
                    stage.RenderRunner(0, 0, 1, 0.5f);
                    Save(camera, target, pixels, Path.Combine(directory, "review-" + type + "-catch.png"));
                    if (type == BattedBallType.GroundBall)
                    {
                        foreach (float progress in new[] { 0f, 0.7f, 1f })
                        {
                            stage.RenderFieldThrow(play, stage.Projection.GetBase(1), progress);
                            Save(camera, target, pixels, Path.Combine(directory,
                                "review-ground-throw-" + Mathf.RoundToInt(progress * 100) + ".png"));
                        }
                    }
                }
                MeasureUpdates(stage, directory);
                stage.SetVisible(false);
                CaptureMatchReplay(rect, camera, target, pixels, directory);
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void CaptureMatchReplay(RectTransform parent, Camera camera, RenderTexture target, Texture2D pixels,
            string directory, bool ownerUiOnly = false)
        {
            const ulong seed = 20260911;
            var players = new Dictionary<int, Player>();
            Team away = CreateReplayTeam(1, players), home = CreateReplayTeam(2, players);
            var input = new MatchInput(1, 1, seed, away, home);
            var buffer = new MatchEventBuffer();
            MatchResult match = new MatchSimulator(BalanceTable.CreateDefault(), MatchRandomStreams.Create(seed)).Simulate(input, buffer);
            var repeated = new MatchEventBuffer();
            new MatchSimulator(BalanceTable.CreateDefault(), MatchRandomStreams.Create(seed)).Simulate(input, repeated);
            if (!buffer.ToArray().SequenceEqual(repeated.ToArray())) throw new InvalidOperationException("재생 검증 경기의 결정론 불일치");
            if (ownerUiOnly)
            {
                CaptureOwnerSession(parent, camera, target, pixels, directory, match, buffer.ToArray());
                return;
            }
            var host = new GameObject("실제 경기 이벤트 재생", typeof(RectTransform));
            var rect = (RectTransform)host.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = parent.rect.size;
            var config = MatchGameCastConfig.Load();
            var visualizer = new MatchPlayVisualizer(rect, config, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                id => players.TryGetValue(id, out Player player) ? player.Name : "선수",
                (pitcher, batter) => new OwnerMatchHandedness(players[pitcher].ThrowingHand, players[batter].BattingHand));
            var samples = new HashSet<string>();
            MatchEvent[] replayEvents = buffer.ToArray();
            var runnerRoutes = new OwnerMatchRunnerRoute[3];
            var report = new System.Text.StringBuilder("speed,sequence,type,result,sample,frame,ballVisible,groupCount\n");
            foreach (int speed in new[] { 1, 2, 4 })
            {
                visualizer.Reset();
                var bases = new int[4];
                string sample = null;
                int frame = 0;
                for (int i = 0; i < buffer.Count; i++)
                {
                    OwnerMatchPlaybackGroup group = OwnerMatchPlaybackGroup.Resolve(buffer[i], i + 1 < buffer.Count ? buffer[i + 1] : default);
                    MatchEvent value = group.VisualEvent;
                    BallInPlayEventData play = value.BallInPlayData;
                    for (int next = i + 1; next < buffer.Count; next++)
                    {
                        if (buffer[next].EventType == MatchEventType.Pitch) break;
                        if (buffer[next].BallInPlayData.HasValue) play = buffer[next].BallInPlayData;
                        if (buffer[next].EventType == MatchEventType.PlateAppearanceEnded) break;
                    }
                    if (speed == 1 && value.EventType == MatchEventType.Pitch && play.HasValue)
                    {
                        string kind = play.Fielding.IsDoublePlay ? "DoublePlay" :
                            play.BattedBall.IsHomeRun ? "HomeRun" : play.BattedBall.Type.ToString();
                        if (samples.Add(kind))
                        {
                            sample = "replay-" + kind;
                            frame = 0;
                            Directory.CreateDirectory(Path.Combine(directory, sample));
                        }
                    }
                    if (value.EventType == MatchEventType.Contact)
                    {
                        int routeCount = OwnerMatchRunnerRoute.Collect(replayEvents, i, value.BatterId,
                            bases[1], bases[2], bases[3], runnerRoutes);
                        visualizer.PrepareRunnerRoutes(runnerRoutes, routeCount);
                    }
                    visualizer.Begin(value, play);
                    float duration = visualizer.GetDuration(value, config.GetDuration(value));
                    int steps = Math.Max(1, Mathf.CeilToInt(duration * 24 / speed));
                    for (int step = 1; step <= steps; step++)
                    {
                        visualizer.Render((float)step / steps);
                        if (sample != null)
                            Save(camera, target, pixels, Path.Combine(directory, sample, $"{frame++:D4}.png"));
                    }
                    Transform ball = rect.Find("SpriteMatchStage/FieldCamera/BallVisual");
                    bool visible = ball != null && ball.gameObject.activeInHierarchy;
                    if (value.EventType is MatchEventType.PlateAppearanceEnded or MatchEventType.HalfInningEnded or MatchEventType.MatchEnded)
                    {
                        if (visible) throw new InvalidOperationException("사건 종료 뒤 공이 남았습니다: " + value.Sequence);
                    }
                    report.AppendLine($"{speed},{value.Sequence},{value.EventType},{value.PlateAppearanceResult},{sample},{frame},{visible},{group.EventCount}");
                    // 기록이 공개된 뒤에만 베이스 상태를 갱신한다. 검증용 HUD는 화면에 표시하지 않는다.
                    if (value.EventType is MatchEventType.RunnerAdvance or MatchEventType.RunnerThrownOut or MatchEventType.Out or MatchEventType.Score)
                    {
                        for (int b = 1; b <= 3; b++) if (bases[b] == value.PlayerId) bases[b] = 0;
                        if (value.EventType == MatchEventType.RunnerAdvance && value.ToBase is >= 1 and <= 3)
                            bases[value.ToBase] = value.PlayerId;
                    }
                    if (value.EventType == MatchEventType.HalfInningEnded) Array.Clear(bases, 0, bases.Length);
                    MatchHudParticipantModel Runner(int b) => bases[b] == 0 ? null : new MatchHudParticipantModel(bases[b], "주자");
                    visualizer.PresentBases(new MatchHudPresentationModelBuilder().Build(value.Inning, (MatchHudHalf)value.Half,
                        new MatchHudTeamModel(away.Name, value.AwayScore, value.Half == InningHalf.Top),
                        new MatchHudTeamModel(home.Name, value.HomeScore, value.Half == InningHalf.Bottom),
                        new MatchHudCountModel(value.Balls, value.Strikes, value.Outs),
                        new MatchHudBaseStateModel(Runner(1), Runner(2), Runner(3)), null, null, false));
                    if (value.EventType == MatchEventType.PlateAppearanceEnded) sample = null;
                    i += group.EventCount - 1;
                }
            }
            File.WriteAllText(Path.Combine(directory, "real-match-replay.csv"), report.ToString());
            File.WriteAllText(Path.Combine(directory, "real-match-replay.txt"),
                $"Seed={seed}\nEvents={buffer.Count}\nSpeeds=1,2,4\nDeterministic=True\nSamples={string.Join(",", samples)}\n검증 범위: 실제 시뮬레이션 이벤트와 MatchPlayVisualizer. 관전 세션 입력 및 결과 UI 검증은 별도.\n");
            UnityEngine.Object.DestroyImmediate(host);
            CaptureOwnerSession(parent, camera, target, pixels, directory, match, replayEvents);
        }

        private static void CaptureOwnerSession(RectTransform parent, Camera camera, RenderTexture target, Texture2D pixels,
            string directory, MatchResult match, MatchEvent[] events)
        {
            string[] Ids(string prefix, int count) => Enumerable.Range(1, count).Select(index => prefix + index).ToArray();
            string[] batting = Ids("batter-", 9);
            var lineup = batting.Select((id, index) => new LineupPresetSlot(id, (PlayerPosition)(index + 1))).ToArray();
            var preset = new LineupPresetState("replay", "관전 검증", lineup, batting,
                Ids("bench-", ActiveRosterCompositionRule.BenchHitterCount), Ids("starter-", ActiveRosterCompositionRule.StartingPitcherCount),
                Ids("bullpen-", ActiveRosterCompositionRule.BullpenPitcherCount), "setup", "closer", new string[2], Array.Empty<string>());
            var plan = new PreGamePlanSnapshot(1, "replay-team", preset,
                new LineupPresetValidationResult("replay", Array.Empty<LineupPresetValidationIssue>()));
            var result = new ManagerModeMatchResult(match, plan,
                new LineupChemistryResult(Array.Empty<LineupChemistryEdge>(), Array.Empty<LineupChemistryPlayerResult>()),
                HomeGameFinanceResult.CreateNotHomeGame("replay-game"), default, "manager", "coach",
                ManagerTacticalProfile.Balanced, "감독", "수석 코치");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            ConstructorInfo constructor = typeof(OwnerMatchSpectatorSession).GetConstructors(flags).Single(value => value.GetParameters().Length == 4);
            MethodInfo update = typeof(UI_Scene_OwnerMatchSpectator).GetMethod("UpdateGameCast", flags);
            MethodInfo refresh = typeof(UI_Scene_OwnerMatchSpectator).GetMethod("RefreshControls", flags);
            FieldInfo eventElapsed = typeof(UI_Scene_OwnerMatchSpectator).GetField("_eventElapsed", flags);
            FieldInfo insetKind = typeof(UI_Scene_OwnerMatchSpectator).GetField("_highlightKind", flags);
            FieldInfo insetElapsed = typeof(UI_Scene_OwnerMatchSpectator).GetField("_highlightElapsed", flags);
            var insetCaptures = new HashSet<OwnerMatchHighlightKind>();
            var insetReport = new System.Text.StringBuilder("mode,speed,kind,visibleEvent,type\n");
            var summary = new System.Text.StringBuilder("mode,speed,frames,visibleEvents,pauseVerified,awayScore,homeScore\n");
            foreach (OwnerMatchViewingMode mode in Enum.GetValues(typeof(OwnerMatchViewingMode)))
            foreach (int speed in new[] { 1, 2, 4 })
            {
                UI_Scene_OwnerMatchSpectator view = UI_Scene_OwnerMatchSpectator.CreateRuntime(parent);
                try
                {
                    var session = (OwnerMatchSpectatorSession)constructor.Invoke(new object[] { result, events, view, 1 });
                    typeof(UI_Scene_OwnerMatchSpectator).GetField("_session", flags).SetValue(view, session);
                    session.TrySetPlaybackSpeed((OwnerMatchPlaybackSpeed)speed);
                    session.TrySetViewingMode(mode);
                    view.SetVisible(true);
                    Canvas.ForceUpdateCanvases();
                    typeof(UI_Scene_OwnerMatchSpectator).GetMethod("FitWorkspace", flags).Invoke(view, null);
                    bool pauseVerified = false, contactCaptured = false;
                    int frames = 0;
                    var routes = new OwnerMatchRunnerRoute[3];
                    while (!session.State.IsComplete && frames++ < 50000)
                    {
                        int visible = session.State.VisibleEventCount;
                        MatchHudPresentationModel hud = session.CurrentHud;
                        session.CopyUpcomingRunnerRoutes(routes);
                        session.PeekBallInPlay();
                        if (visible != session.State.VisibleEventCount || !ReferenceEquals(hud, session.CurrentHud))
                            throw new InvalidOperationException("연출 경로 조회가 HUD 공개 상태를 변경했습니다.");
                        if (!pauseVerified && (OwnerMatchHighlightKind)insetKind.GetValue(view) != OwnerMatchHighlightKind.None)
                        {
                            float elapsedBeforePause = (float)eventElapsed.GetValue(view);
                            float insetBeforePause = (float)insetElapsed.GetValue(view);
                            session.TryTogglePause();
                            update.Invoke(view, new object[] { 1f });
                            if (visible != session.State.VisibleEventCount) throw new InvalidOperationException("일시정지 중 사건 공개");
                            if ((float)eventElapsed.GetValue(view) != elapsedBeforePause)
                                throw new InvalidOperationException("일시정지 중 스프라이트 재생 시간이 진행했습니다.");
                            if ((float)insetElapsed.GetValue(view) != insetBeforePause)
                                throw new InvalidOperationException("일시정지 중 삽입 컷 표시 시간이 진행했습니다.");
                            session.TryTogglePause();
                            pauseVerified = true;
                        }
                        update.Invoke(view, new object[] { 1f / 24f });
                        int last = session.State.VisibleEventCount - 1;
                        var kind = (OwnerMatchHighlightKind)insetKind.GetValue(view);
                        if (kind != OwnerMatchHighlightKind.None)
                        {
                            if (last < 0 || OwnerMatchHighlightCue.Resolve(events[last]) != kind)
                                throw new InvalidOperationException("삽입 컷이 공개 사건보다 앞서거나 다른 결과를 표시합니다.");
                            if (view.transform.Find("BroadcastCanvas/GameCastSidebar/PitchContext").gameObject.activeSelf)
                                throw new InvalidOperationException("삽입 컷과 투구 상세가 겹쳐 표시됩니다.");
                            if (mode == OwnerMatchViewingMode.EveryMoment && speed == 1 &&
                                (float)insetElapsed.GetValue(view) >= 0.15f && insetCaptures.Add(kind))
                            {
                                Save(camera, target, pixels, Path.Combine(directory, "inset-replay-" + kind + ".png"));
                                insetReport.AppendLine($"{mode},{speed},{kind},{last},{events[last].EventType}");
                            }
                        }
                        if (last >= 0 && (session.CurrentHud.AwayTeam.Score != events[last].AwayScore ||
                            session.CurrentHud.HomeTeam.Score != events[last].HomeScore))
                            throw new InvalidOperationException("HUD 점수가 공개 사건 경계와 다릅니다.");
                        var pending = (MatchEvent)typeof(UI_Scene_OwnerMatchSpectator).GetField("_pendingEvent", flags).GetValue(view);
                        float elapsed = (float)typeof(UI_Scene_OwnerMatchSpectator).GetField("_eventElapsed", flags).GetValue(view);
                        if (!contactCaptured && mode == OwnerMatchViewingMode.EveryMoment && speed == 1 &&
                            pending.EventType == MatchEventType.Contact && elapsed > 0.08f)
                        {
                            Save(camera, target, pixels, Path.Combine(directory, "owner-session-contact.png"));
                            contactCaptured = true;
                        }
                    }
                    if (!session.State.IsComplete || session.State.VisibleEventCount != events.Length)
                        throw new InvalidOperationException("관전 화면이 경기 종료에 도달하지 못했습니다: " + mode);
                    refresh.Invoke(view, null);
                    if ((OwnerMatchHighlightKind)insetKind.GetValue(view) != OwnerMatchHighlightKind.None)
                        throw new InvalidOperationException("경기 종료 후 삽입 컷이 남았습니다.");
                    if (session.CurrentHud.AwayTeam.Score != match.AwayBoxScore.Runs ||
                        session.CurrentHud.HomeTeam.Score != match.HomeBoxScore.Runs)
                        throw new InvalidOperationException("관전 최종 점수가 공식 BoxScore와 다릅니다.");
                    Transform broadcast = view.transform.Find("BroadcastCanvas");
                    if (!broadcast.Find("MatchResult").gameObject.activeSelf ||
                        !broadcast.Find("ReturnHome").gameObject.activeSelf)
                        throw new InvalidOperationException("경기 종료 후 결과와 홈 복귀 경로가 표시되지 않았습니다.");
                    if (speed == 1) Save(camera, target, pixels, Path.Combine(directory, "owner-session-result-" + mode + ".png"));
                    summary.AppendLine($"{mode},{speed},{frames},{session.State.VisibleEventCount},{pauseVerified},{session.CurrentHud.AwayTeam.Score},{session.CurrentHud.HomeTeam.Score}");
                }
                finally { UnityEngine.Object.DestroyImmediate(view.gameObject); }
            }
            File.WriteAllText(Path.Combine(directory, "owner-session-replay.csv"), summary.ToString());
            File.WriteAllText(Path.Combine(directory, "highlight-inset-replay.csv"), insetReport.ToString());
            VerifyInsetCancellation(parent, result, events);
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                         new Vector2Int(2560, 1440), new Vector2Int(3440, 1440) })
                CaptureOwnerLayout(size, directory, result, events);
        }

        private static void VerifyInsetCancellation(RectTransform parent, ManagerModeMatchResult result, MatchEvent[] events)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var view = UI_Scene_OwnerMatchSpectator.CreateRuntime(parent);
            try
            {
                var session = (OwnerMatchSpectatorSession)typeof(OwnerMatchSpectatorSession)
                    .GetConstructors(flags).Single(value => value.GetParameters().Length == 4).Invoke(new object[] { result, events, view, 1 });
                typeof(UI_Scene_OwnerMatchSpectator).GetField("_session", flags).SetValue(view, session);
                view.SetVisible(true);
                var update = typeof(UI_Scene_OwnerMatchSpectator).GetMethod("UpdateGameCast", flags);
                var kind = typeof(UI_Scene_OwnerMatchSpectator).GetField("_highlightKind", flags);
                int frames = 0;
                while ((OwnerMatchHighlightKind)kind.GetValue(view) == OwnerMatchHighlightKind.None && frames++ < 5000)
                    update.Invoke(view, new object[] { 1f / 24f });
                if ((OwnerMatchHighlightKind)kind.GetValue(view) == OwnerMatchHighlightKind.None)
                    throw new InvalidOperationException("즉시 결과 취소 검증용 삽입 컷이 시작되지 않았습니다.");
                view.transform.Find("BroadcastCanvas/RevealAll").GetComponent<Button>().onClick.Invoke();
                if (!session.State.IsComplete || (OwnerMatchHighlightKind)kind.GetValue(view) != OwnerMatchHighlightKind.None ||
                    !view.transform.Find("BroadcastCanvas/MatchResult").gameObject.activeSelf)
                    throw new InvalidOperationException("삽입 컷 중 즉시 결과 전환 실패");
            }
            finally { UnityEngine.Object.DestroyImmediate(view.gameObject); }
        }

        private static Team CreateReplayTeam(int id, Dictionary<int, Player> players)
        {
            var slots = new LineupSlot[9];
            for (int index = 0; index < slots.Length; index++)
            {
                var player = new Player(id * 100 + index + 1, "검증 타자 " + (index + 1), (PlayerPosition)(index + 1),
                    index % 2 == 0 ? Handedness.Right : Handedness.Left, Handedness.Right,
                    new BatterAttributes(60, 60, 60, 60, 60, 60), new PitcherAttributes(20, 20, 20, 20, 20, 20));
                players.Add(player.PlayerId, player);
                slots[index] = new LineupSlot(player, player.PrimaryPosition);
            }
            var pitcher = new Player(id * 100 + 99, "검증 투수", PlayerPosition.StartingPitcher, Handedness.Right,
                id == 1 ? Handedness.Right : Handedness.Left, new BatterAttributes(20, 20, 20, 20, 20, 20),
                new PitcherAttributes(50, 50, 50, 50, 50, 50));
            players.Add(pitcher.PlayerId, pitcher);
            return new Team(id, "검증 " + id + "팀", new Lineup(slots), pitcher);
        }

        private static void CaptureOwnerLayout(Vector2Int size, string directory,
            ManagerModeMatchResult result = null, MatchEvent[] events = null)
        {
            var root = new GameObject("관전 화면 검증", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("관전 Camera", typeof(Camera));
            var target = new RenderTexture(size.x, size.y, 24);
            var pixels = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = size;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = size.y * 0.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                UI_Scene_OwnerMatchSpectator view = UI_Scene_OwnerMatchSpectator.CreateRuntime(rect);
                view.SetVisible(true);
                if (result != null)
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    ConstructorInfo constructor = typeof(OwnerMatchSpectatorSession).GetConstructors(flags)
                        .Single(value => value.GetParameters().Length == 4);
                    var session = (OwnerMatchSpectatorSession)constructor.Invoke(new object[] { result, events, view, 1 });
                    typeof(UI_Scene_OwnerMatchSpectator).GetField("_session", flags).SetValue(view, session);
                    session.TrySetViewingMode(OwnerMatchViewingMode.ResultOnly);
                    typeof(UI_Scene_OwnerMatchSpectator).GetMethod("RefreshControls", flags).Invoke(view, null);
                    Canvas.ForceUpdateCanvases();
                    typeof(UI_Scene_OwnerMatchSpectator).GetMethod("FitWorkspace", flags).Invoke(view, null);
                    view.transform.Find("BroadcastCanvas/MatchResult/RecordViewport")
                        .GetComponent<ScrollRect>().Rebuild(CanvasUpdate.PostLayout);
                    Save(camera, target, pixels, Path.Combine(directory, $"owner-result-{size.x}x{size.y}.png"));
                    return;
                }
                view.Present(new MatchHudPresentationModelBuilder().Build(1, MatchHudHalf.Top,
                    new MatchHudTeamModel("LG 트윈스", 0, true), new MatchHudTeamModel("한화 이글스", 0, false),
                    new MatchHudCountModel(1, 1, 0), MatchHudBaseStateModel.Empty,
                    new MatchHudParticipantModel(1, "김원준"), new MatchHudParticipantModel(2, "김성찬"), false));
                typeof(UI_Scene_OwnerMatchSpectator).GetMethod("RenderCompactLineScore", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(view, new object[] { Baseball.Game.Career.MatchLineScore.Create(Array.Empty<MatchEvent>(), 0) });
                Canvas.ForceUpdateCanvases();
                typeof(UI_Scene_OwnerMatchSpectator).GetMethod("FitWorkspace", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
                // 화면 자체의 무대를 사용한다. 입력·경기 진행 검증과 구분되는 정적 합성이다.
                var visualizer = (MatchPlayVisualizer)typeof(UI_Scene_OwnerMatchSpectator)
                    .GetField("_playVisualizer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
                var stage = (SpriteMatchStage)typeof(MatchPlayVisualizer)
                    .GetField("_spriteStage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(visualizer);
                if (stage == null || !stage.SetHands(Handedness.Right, Handedness.Right))
                    throw new InvalidOperationException("실제 관전 화면의 경기용 무대가 연결되지 않았습니다.");
                view.transform.Find("BroadcastCanvas/Field/Ground/GameCastMarkers").gameObject.SetActive(false);
                stage.Reset();
                stage.RenderPitch(1, true, true);
                Save(camera, target, pixels, Path.Combine(directory, $"owner-{size.x}x{size.y}.png"));
                var contact = new BallInPlayEventData(new BattedBallDescriptor(BattedBallType.GroundBall,
                    BattedBallDirection.Center, FieldZone.Shortstop, 0.5, BallFlightBand.Medium, BallPaceBand.Medium, false), default);
                stage.RenderContact(contact, stage.Projection.Layout.contactCameraPeakProgress);
                Save(camera, target, pixels, Path.Combine(directory, $"owner-contact-{size.x}x{size.y}.png"));
                CaptureHighlightInsets(view, camera, target, pixels, directory, size);
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static void CaptureHighlightInsets(UI_Scene_OwnerMatchSpectator view, Camera camera,
            RenderTexture target, Texture2D pixels, string directory, Vector2Int size)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo show = typeof(UI_Scene_OwnerMatchSpectator).GetMethod("TryPresentHighlightInset", flags);
            MethodInfo advance = typeof(UI_Scene_OwnerMatchSpectator).GetMethod("AdvanceHighlightInset", flags);
            var sidebar = (RectTransform)view.transform.Find("BroadcastCanvas/GameCastSidebar");
            var inset = (RectTransform)sidebar.Find("HighlightInset");
            var corners = new Vector3[4];
            foreach (OwnerMatchHighlightKind kind in Enum.GetValues(typeof(OwnerMatchHighlightKind)))
            {
                if (kind == OwnerMatchHighlightKind.None) continue;
                if (!(bool)show.Invoke(view, new object[] { kind, OwnerMatchPlaybackSpeed.Normal }))
                    throw new InvalidOperationException("삽입 컷 리소스 누락: " + kind);
                advance.Invoke(view, new object[] { 0.15f });
                Canvas.ForceUpdateCanvases();
                inset.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                    if (!sidebar.rect.Contains(sidebar.InverseTransformPoint(corner)))
                        throw new InvalidOperationException("삽입 컷이 상세 영역을 침범했습니다: " + size);
                if (sidebar.Find("PitchContext").gameObject.activeSelf)
                    throw new InvalidOperationException("투구 상세와 삽입 컷 중첩");
                if (inset.GetComponent<CanvasGroup>().blocksRaycasts || inset.Find("Picture").GetComponent<Image>().raycastTarget)
                    throw new InvalidOperationException("삽입 컷이 조작 입력을 가로챕니다.");
                Save(camera, target, pixels, Path.Combine(directory, $"inset-{kind}-{size.x}x{size.y}.png"));
                advance.Invoke(view, new object[] { 2f });
                if (inset.gameObject.activeSelf || !sidebar.Find("PitchContext").gameObject.activeSelf)
                    throw new InvalidOperationException("삽입 컷 종료 후 투구 상세 복귀 실패");
            }
        }

        private static void MeasureUpdates(SpriteMatchStage stage, string directory)
        {
            const int iterations = 600;
            var watch = new System.Diagnostics.Stopwatch();
            // 최초 활성화와 Unity 캐시 준비를 제외하고 커리어의 절대 시각 재생 경로를 측정한다.
            for (int i = 0; i < 120; i++) RenderUpdate(stage, i);
            long before = GC.GetAllocatedBytesForCurrentThread();
            watch.Start();
            for (int i = 0; i < iterations; i++) RenderUpdate(stage, i);
            watch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            File.WriteAllText(Path.Combine(directory, "unity-update-profile.json"), JsonUtility.ToJson(new UpdateReport
            {
                iterations = iterations, managedBytes = allocated,
                millisecondsPerUpdate = watch.Elapsed.TotalMilliseconds / iterations,
                includesCanvasRendering = false
            }, true));
        }

        private static void RenderUpdate(SpriteMatchStage stage, int index)
        {
            stage.Reset();
            float progress = (index % 60) / 59f;
            stage.RenderPitch(progress, true, true);
            stage.RenderRunner(0, 0, 1, progress);
        }

        [Serializable]
        private sealed class UpdateReport
        {
            public int iterations;
            public long managedBytes;
            public double millisecondsPerUpdate;
            public bool includesCanvasRendering;
        }

        private static void Save(Camera camera, RenderTexture target, Texture2D pixels, string path)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }

        private static Dictionary<string, string> Snapshot()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            using SHA256 hash = SHA256.Create();
            foreach (string root in new[] { "Assets/04.Images/SpriteMatch", "Assets/10.Datas/SpriteMatch", "Assets/Resources/UI/SpriteMatch" })
                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    result[file] = Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(file)));
            return result;
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("필수 인자 누락: " + name);
            return Path.GetFullPath(args[index + 1]);
        }

        [Serializable]
        private sealed class ImportReport
        {
            public int reviewClips, productionClips, comparedFiles;
            public bool isIdempotent, isRuntimeReady;
        }
    }
}
