using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using Baseball.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    public sealed class OwnerSeasonReviewPresentationTests
    {
        [Test]
        public void 미진출은남은리그마감으로안내하고결산잠금과취소포커스를유지한다()
        {
            var eventsObject = new GameObject("EventSystem", typeof(EventSystem));
            var host = new GameObject("PopupHost", typeof(RectTransform));
            var previous = new GameObject("Previous", typeof(RectTransform), typeof(Button));
            try
            {
                var events = eventsObject.GetComponent<EventSystem>();
                events.SetSelectedGameObject(previous);
                var view = UI_Popup_OwnerSeasonReview.CreateRuntime(host.GetComponent<RectTransform>());
                view.Bind(new OwnerSeasonReviewSnapshot(1, LeagueGrade.Rookie, null, "TEAM-A",
                    10, 10, 40, 104, 0, 400, 1000, false, false, false, 0, 37, false,
                    OwnerTeamPostseasonResult.DidNotQualify, null,
                    new OwnerPostseasonSeriesReview[0]), key => null, 1);
                view.Show();
                Transform modal = view.transform.Find("SeasonReview");
                var primary = modal.Find("Primary").GetComponent<Button>();
                Assert.That(primary.GetComponentInChildren<Text>().text, Is.EqualTo("남은 리그 마감"));
                Assert.That(modal.Find("Tab2").GetComponent<Button>().interactable, Is.False);
                Assert.That(modal.Find("Hint").GetComponent<Text>().text, Does.Contain("모든 조"));
                foreach (Button button in view.GetComponentsInChildren<Button>())
                {
                    if (!button.interactable) continue;
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
                    foreach (Selectable next in new[] { button.navigation.selectOnLeft, button.navigation.selectOnRight,
                        button.navigation.selectOnUp, button.navigation.selectOnDown })
                    {
                        Assert.That(next.transform.IsChildOf(view.transform), Is.True);
                        Assert.That(next.interactable, Is.True);
                    }
                }
                bool closed = false;
                view.CloseRequested += () => { closed = true; view.Hide(); };
                view.OnCancel(new BaseEventData(events));
                Assert.That(closed, Is.True);
                Assert.That(events.currentSelectedGameObject, Is.EqualTo(previous));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(previous);
                Object.DestroyImmediate(eventsObject);
            }
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void 결과화면은각해상도에서기록과안내를분리하고버튼대비를유지한다(int width, int height)
        {
            var host = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("Camera", typeof(Camera));
            var target = new RenderTexture(width, height, 24);
            RenderTexture previous = RenderTexture.active;
            try
            {
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                // 실제 셸의 CanvasScaler와 같은 논리 크기를 사용한다.
                float scale = Mathf.Sqrt(width / 1920f * height / 1080f);
                var hostRect = host.GetComponent<RectTransform>();
                hostRect.sizeDelta = new Vector2(width / scale, height / scale);
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = hostRect.rect.height / 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = CareerUiTheme.Background;
                camera.targetTexture = target;
                canvas.worldCamera = camera;
                var view = UI_Popup_OwnerSeasonReview.CreateRuntime(hostRect);
                var snapshot = new OwnerSeasonReviewSnapshot(
                    20, LeagueGrade.Rookie, LeagueGrade.Minor, "TEAM-A",
                    1, 10, 100, 44, 0, 1234, 567, true, true, true,
                    OwnerTeamPostseasonResult.Champion, "TEAM-A",
                    new[] {
                        new OwnerPostseasonSeriesReview("semi-a", OwnerPostseasonRound.Semifinal,
                            "TEAM-A", "TEAM-B", 3, 2, 3, true),
                        new OwnerPostseasonSeriesReview("semi-b", OwnerPostseasonRound.Semifinal,
                            "TEAM-C", "TEAM-D", 3, 1, 3, true),
                        new OwnerPostseasonSeriesReview("final", OwnerPostseasonRound.Championship,
                            "TEAM-A", "TEAM-C", 3, 2, 3, true) });
                for (int page = 0; page < 3; page++)
                {
                    view.Bind(snapshot, key => key == "TEAM-A" ? "서울 챔피언스 베이스볼" : "부산 인터내셔널 마리너스", page);
                    view.Show();
                    CareerUiSkin.Apply(view.transform);
                    Canvas.ForceUpdateCanvases();
                    RectTransform modal = (RectTransform)view.transform.Find("SeasonReview");
                    AssertContained(hostRect, modal);
                    RectTransform insight = (RectTransform)modal.Find("NextStep");
                    for (int i = 0; i < 3; i++)
                    {
                        RectTransform metric = (RectTransform)modal.Find("Metric" + i);
                        Assert.That(GetBounds(modal, metric).Overlaps(GetBounds(modal, insight)), Is.False);
                    }
                    foreach (Text text in view.GetComponentsInChildren<Text>())
                    {
                        AssertContained((RectTransform)text.transform.parent, text.rectTransform);
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f), text.name + " 세로 잘림");
                    }
                    Button primary = modal.Find("Primary").GetComponent<Button>();
                    Assert.That(primary.GetComponent<OwnerUiButtonSkin>(), Is.Not.Null);
                    Assert.That(((Image)primary.targetGraphic).sprite, Is.Not.Null);
                    Assert.That(primary.transform.Find("Label").GetComponent<Text>().color.grayscale, Is.GreaterThan(0.85f));
                    string[] args = System.Environment.GetCommandLineArgs();
                    int reportIndex = System.Array.IndexOf(args, "-seasonReviewReport");
                    if (reportIndex < 0 || reportIndex + 1 >= args.Length) continue;
                    System.IO.Directory.CreateDirectory(args[reportIndex + 1]);
                    camera.Render();
                    RenderTexture.active = target;
                    var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    try
                    {
                        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        pixels.Apply();
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(args[reportIndex + 1], $"season-{width}x{height}-page{page}.png"), pixels.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(pixels); }
                }
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
            }
        }

        private static void AssertContained(RectTransform parent, RectTransform child)
        {
            Rect bounds = GetBounds(parent, child);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - 1f), child.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - 1f), child.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + 1f), child.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + 1f), child.name);
        }

        private static Rect GetBounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3 min = parent.InverseTransformPoint(corners[0]);
            Vector3 max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [Test]
        public void Bind_페넌트레이스부터포스트시즌과결산까지한팝업에서탐색한다()
        {
            var hostObject = new GameObject("PopupHost", typeof(RectTransform));
            UI_Popup_OwnerSeasonReview view = UI_Popup_OwnerSeasonReview.CreateRuntime(
                hostObject.GetComponent<RectTransform>());
            var snapshot = new OwnerSeasonReviewSnapshot(
                3, LeagueGrade.Rookie, LeagueGrade.Minor, "TEAM-A",
                2, 10, 87, 2, 55, 633, 588,
                true, true, true, OwnerTeamPostseasonResult.RunnerUp, "TEAM-B",
                new[]
                {
                    new OwnerPostseasonSeriesReview("championship", OwnerPostseasonRound.Championship,
                        "TEAM-A", "TEAM-B", 2, 3, 3, true)
                });

            view.Bind(snapshot, key => key == "TEAM-A" ? "서울 베어스" : "부산 마리너스", 0);
            view.Show();

            Text summary = view.transform.Find("SeasonReview/ResultHero/Summary").GetComponent<Text>();
            Button primary = view.transform.Find("SeasonReview/Primary").GetComponent<Button>();
            Assert.That(summary.text, Does.Contain("2위"));

            primary.onClick.Invoke();
            Assert.That(summary.text, Is.EqualTo("포스트시즌 준우승"));
            primary.onClick.Invoke();
            Assert.That(summary.text, Does.Contain("정규시즌 2위"));

            Object.DestroyImmediate(hostObject);
        }

        [Test]
        public void Bind_우리조만완료된경우남은월드포스트시즌을다시진행할수있다()
        {
            var hostObject = new GameObject("PopupHost", typeof(RectTransform));
            UI_Popup_OwnerSeasonReview view = UI_Popup_OwnerSeasonReview.CreateRuntime(
                hostObject.GetComponent<RectTransform>());
            var snapshot = new OwnerSeasonReviewSnapshot(
                1, LeagueGrade.Rookie, null, "TEAM-A",
                2, 10, 80, 62, 2, 615, 537,
                true, true, false, 1, 4, true,
                OwnerTeamPostseasonResult.Champion, "TEAM-A",
                new[]
                {
                    new OwnerPostseasonSeriesReview("championship", OwnerPostseasonRound.Championship,
                        "TEAM-A", "TEAM-B", 3, 1, 3, true)
                });
            bool requested = false;
            view.PostseasonRequested += () => requested = true;

            view.Bind(snapshot, key => key, 1);
            Button primary = view.transform.Find("SeasonReview/Primary").GetComponent<Button>();
            Text label = primary.transform.Find("Label").GetComponent<Text>();

            Assert.That(label.text, Is.EqualTo("남은 리그 마감"));
            primary.onClick.Invoke();
            Assert.That(requested, Is.True);

            Object.DestroyImmediate(hostObject);
        }
    }
}
