using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Baseball.Core.Players;
using Baseball.Editor.SpriteSheets;
using Baseball.Presentation.Match.Sprites;
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

        private static void Capture(SpriteAnimationCatalog catalog, string directory)
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
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
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
