using System.Linq;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    public sealed partial class OwnerNavigationRefreshTests
    {
        [Test]
        public void 구단기록실진입은실제Runtime기록을바인딩하고리그경로와화면을공유한다()
        {
            var club = OwnerModeUiProfileFactory.Create().Navigation.FindEntry(OwnerNavigationRoutes.Club);
            Assert.That(club.Children.Any(entry => entry.RouteId == OwnerNavigationRoutes.ClubHistory && entry.DisplayName == "기록실"), Is.True);
            Invoke(_coordinator, "HandleNavigationRequested", OwnerNavigationRoutes.ClubHistory);
            Assert.That(_root.GetComponentInChildren<OwnerSharedInformationWorkspaceCoordinator>().ActiveRouteId,
                Is.EqualTo(OwnerNavigationRoutes.ClubHistory));
            var view = _root.GetComponentInChildren<UI_Scene_OwnerClubHistory>();
            Assert.That(view, Is.Not.Null);
            view.transform.Find("Tab3").GetComponent<Button>().onClick.Invoke();
            Assert.That(view.transform.Find("TrophyRoom").gameObject.activeInHierarchy, Is.True);
            Invoke(_coordinator, "HandleNavigationRequested", OwnerSharedInformationWorkspaceCoordinator.RecordsRouteId);
            Assert.That(_root.GetComponentInChildren<UI_Scene_OwnerClubHistory>(), Is.SameAs(view));
            Invoke(_coordinator, "HandleNavigationRequested", OwnerNavigationRoutes.ClubHistory);
            Assert.That(view.transform.Find("TrophyRoom").gameObject.activeInHierarchy, Is.True);
            CaptureClubHistory();
        }

        private void CaptureClubHistory()
        {
            var canvas = _root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)_root.transform).sizeDelta = new Vector2(1920, 1080);
            var cameraObject = new GameObject("HistoryCamera", typeof(Camera));
            var target = new RenderTexture(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
                camera.orthographicSize = 540; camera.transform.position = new Vector3(0, 0, -100);
                camera.nearClipPlane = .1f; camera.farClipPlane = 500; camera.targetTexture = target;
                canvas.worldCamera = camera; Canvas.ForceUpdateCanvases(); camera.Render();
                foreach (var text in _root.GetComponentsInChildren<Text>()) text.SetVerticesDirty();
                Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                string directory = System.Environment.GetEnvironmentVariable("BASEBALL_HISTORY_VISUAL_OUTPUT");
                if (!string.IsNullOrEmpty(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, "club-history-navigation.png"), pixels.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
