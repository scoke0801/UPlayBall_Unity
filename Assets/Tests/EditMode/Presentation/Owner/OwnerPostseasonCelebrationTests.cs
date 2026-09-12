using System;
using System.IO;
using System.Reflection;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Career;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    public sealed class OwnerPostseasonCelebrationTests
    {
        private static OwnerSeasonReviewSnapshot Snapshot(int wins, int losses, bool completed, bool champion = false, int season = 1)
        {
            return new OwnerSeasonReviewSnapshot(season, LeagueGrade.Rookie, null, "A", 1, 4,
                80, 60, 4, 700, 650, true, completed && champion, true, null, null,
                new[] { new OwnerPostseasonSeriesReview("series", champion ? OwnerPostseasonRound.Championship : OwnerPostseasonRound.Semifinal,
                    "A", "B", wins, losses, 3, completed) });
        }

        [Test]
        public void 결과공개전에는승리를숨기고공개후한번만전달한다()
        {
            var gate = new OwnerPostseasonCelebrationGate();
            gate.Begin(Snapshot(2, 1, false));
            Assert.That(gate.Reveal(Snapshot(3, 1, true), false), Is.Null);
            Assert.That(gate.Reveal(Snapshot(3, 1, true), true).Kind, Is.EqualTo(OwnerPostseasonCelebrationKind.SeriesVictory));
            Assert.That(gate.Reveal(Snapshot(3, 1, true), true), Is.Null);
            gate.Begin(Snapshot(2, 1, false, true));
            Assert.That(gate.Reveal(Snapshot(3, 1, true, true), true).Kind, Is.EqualTo(OwnerPostseasonCelebrationKind.Championship));
        }

        [Test]
        public void 일반승리와탈락과이전우승과시즌변경에는연출하지않는다()
        {
            Assert.That(OwnerPostseasonCelebration.Create(Snapshot(1, 1, false), Snapshot(2, 1, false)), Is.Null);
            Assert.That(OwnerPostseasonCelebration.Create(Snapshot(1, 2, false), Snapshot(1, 3, true)), Is.Null);
            Assert.That(OwnerPostseasonCelebration.Create(Snapshot(3, 1, true), Snapshot(3, 1, true)), Is.Null);
            Assert.That(OwnerPostseasonCelebration.Create(Snapshot(2, 1, false), Snapshot(3, 1, true, false, 2)), Is.Null);
            var gate = new OwnerPostseasonCelebrationGate();
            gate.Begin(Snapshot(2, 1, false));
            gate.Clear();
            Assert.That(gate.Reveal(Snapshot(3, 1, true), true), Is.Null);
        }

        [Test]
        public void 하위시드의우승도플레이어기준전적으로표시한다()
        {
            OwnerSeasonReviewSnapshot Create(int wins, bool completed) => new OwnerSeasonReviewSnapshot(
                1, LeagueGrade.Rookie, null, "B", 2, 4, 80, 60, 4, 700, 650, true, completed, true, null, null,
                new[] { new OwnerPostseasonSeriesReview("final", OwnerPostseasonRound.Championship, "A", "B", 1, wins, 3, completed) });
            var result = OwnerPostseasonCelebration.Create(Create(2, false), Create(3, true));
            Assert.That(result.Kind, Is.EqualTo(OwnerPostseasonCelebrationKind.Championship));
            Assert.That(result.TeamKey, Is.EqualTo("B"));
            Assert.That(result.OpponentKey, Is.EqualTo("A"));
            Assert.That(result.Wins, Is.EqualTo(3));
            Assert.That(result.Losses, Is.EqualTo(1));
        }

        [Test]
        public void 대진은순서대로등장하고건너뛰기후재개방은완성상태를유지한다()
        {
            var host = new GameObject("Host", typeof(RectTransform));
            var mode = CareerPresentationSettings.Mode;
            try
            {
                CareerPresentationSettings.Mode = CareerPresentationMode.Full;
                var view = UI_Popup_OwnerSeasonReview.CreateRuntime(host.GetComponent<RectTransform>());
                view.Bind(Snapshot(1, 1, false), key => "구단 " + key, 1);
                view.Show();
                Assert.That(view.IsBracketRevealing, Is.True);
                object sequence = typeof(UI_Popup_OwnerSeasonReview).GetField("_bracketSequence", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Seek(sequence, 0.15f);
                var cards = view.transform.Find("SeasonReview/ResultHero").GetComponentsInChildren<CanvasGroup>();
                Assert.That(cards[0].alpha, Is.GreaterThan(0f).And.LessThan(1f));
                view.SkipBracketReveal();
                foreach (var card in cards) Assert.That(card.alpha, Is.EqualTo(1f));
                view.Hide(); view.Show();
                Assert.That(view.IsBracketRevealing, Is.False);
            }
            finally { CareerPresentationSettings.Mode = mode; Object.DestroyImmediate(host); }
        }

        [TestCase(1280, 720, false)]
        [TestCase(1920, 1080, false)]
        [TestCase(2560, 1440, false)]
        [TestCase(3440, 1440, false)]
        [TestCase(1280, 720, true)]
        [TestCase(1920, 1080, true)]
        [TestCase(2560, 1440, true)]
        [TestCase(3440, 1440, true)]
        public void 단계별연출과최종화면은안전영역을지키고항상종료할수있다(int width, int height, bool champion)
        {
            var host = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            var previousTarget = RenderTexture.active;
            var mode = CareerPresentationSettings.Mode;
            try
            {
                CareerPresentationSettings.Mode = CareerPresentationMode.Full;
                var hostRect = host.GetComponent<RectTransform>();
                float scale = Mathf.Sqrt(width / 1920f * height / 1080f);
                hostRect.sizeDelta = new Vector2(width / scale, height / scale);
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = hostRect.rect.height / 2;
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                var view = UI_Popup_OwnerPostseasonCelebration.CreateRuntime(hostRect);
                var result = OwnerPostseasonCelebration.Create(Snapshot(2, 1, false, champion), Snapshot(3, 1, true, champion));
                view.Show(result, key => key == "A" ? "삼성 라이온즈" : "롯데 자이언츠");
                Assert.That(view.IsAnimating, Is.True);
                var art = view.GetComponentInChildren<RawImage>();
                Assert.That(art.texture, Is.Not.Null);
                object sequence = typeof(UI_Popup_OwnerPostseasonCelebration).GetField("_sequence", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                foreach (float sample in new[] { 0.3f, 1.1f, 1.9f, 5f })
                {
                    if (sample < 5f) Seek(sequence, sample); else view.Skip();
                    Canvas.ForceUpdateCanvases();
                    foreach (Text text in view.GetComponentsInChildren<Text>())
                    {
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name);
                        var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                        foreach (Vector3 corner in corners)
                            Assert.That(hostRect.rect.Contains((Vector2)hostRect.InverseTransformPoint(corner)), Is.True, text.name);
                    }
                    foreach (Button button in view.GetComponentsInChildren<Button>()) Assert.That(button.interactable, Is.True);
                    SaveRender(camera, target, width, height, champion, sample);
                }
                Assert.That(view.IsAnimating, Is.False);
                bool continued = false, records = false;
                view.ContinueRequested += () => continued = true;
                view.RecordsRequested += () => records = true;
                view.transform.Find("Celebration/Continue").GetComponent<Button>().onClick.Invoke();
                view.OnCancel(null);
                Assert.That(continued && records, Is.True);
                view.Hide();
                CareerPresentationSettings.Mode = CareerPresentationMode.ResultOnly;
                view.Show(result, key => "구단");
                Assert.That(view.IsAnimating, Is.False);
                Assert.That(art.color.a, Is.EqualTo(1f));
            }
            finally
            {
                CareerPresentationSettings.Mode = mode;
                RenderTexture.active = previousTarget;
                Object.DestroyImmediate(host); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(target);
            }
        }

        private static void Seek(object sequence, float time)
        {
            sequence.GetType().Assembly.GetType("DG.Tweening.TweenExtensions").GetMethod("Goto",
                new[] { sequence.GetType().BaseType, typeof(float), typeof(bool) }).Invoke(null, new[] { sequence, (object)time, false });
        }

        private static void SaveRender(Camera camera, RenderTexture target, int width, int height, bool champion, float sample)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-seasonReviewReport");
            if (index < 0 || index + 1 >= args.Length) return;
            Directory.CreateDirectory(args[index + 1]);
            camera.Render(); RenderTexture.active = target;
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(args[index + 1], $"ceremony-{width}x{height}-{champion}-{sample:0.0}.png"), pixels.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(pixels); }
        }
    }
}
