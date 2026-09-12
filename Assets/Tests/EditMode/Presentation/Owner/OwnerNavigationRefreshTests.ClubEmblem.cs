using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Presentation.Owner;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Tests.EditMode.Presentation.Owner
{
    public sealed partial class OwnerNavigationRefreshTests
    {
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3440, 1440)]
        public void ClubEmblem_사용자구단명으로저장복원후두탭에서원본엠블렘을유지한다(int width, int height)
        {
            var provider = (IHistoricalContentProvider)GetField(_manager, "_contentProvider");
            var adapter = new ManagerHistoricalSaveAdapter(provider, CardEditionBalanceTable.CreateInitial());
            var save = adapter.CreateSaveData(_manager.Runtime);
            save.ownerProfile.clubName = "하마킹";
            typeof(OwnerModeManager).GetProperty("Runtime").SetValue(_manager, adapter.Restore(save));
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            _coordinator.Refresh();

            string identityName = _manager.GetTeamIdentityDisplayName(_manager.Runtime.PlayerTeamSeasonKey);
            Assert.That(identityName, Is.Not.EqualTo("하마킹"));
            foreach (string route in new[] { OwnerNavigationRoutes.ClubOwner,
                         OwnerNavigationRoutes.ClubInformation, OwnerNavigationRoutes.ClubOwner })
            {
                Navigate(route);
                Canvas.ForceUpdateCanvases();
                object workspace = GetField(_coordinator, "_sharedInformationWorkspace");
                var model = (OwnerClubInformationPresentationModel)GetField(workspace, "_clubInformationModel");
                var view = (UI_Scene_OwnerClubInformation)GetField(workspace, "_clubInformationView");
                Assert.That(model.TeamName, Is.EqualTo("하마킹"));
                Assert.That(model.EmblemTeamName, Is.EqualTo(identityName));
                var emblem = view.transform.Find("Identity/EmblemBox/Emblem").GetComponent<Image>();
                Assert.That(emblem.sprite, Is.Not.Null, identityName);
                Assert.That(emblem.color.a, Is.EqualTo(1f));
                Assert.That(emblem.preserveAspect, Is.True);
                Assert.That(emblem.raycastTarget, Is.False);
                Assert.That(emblem.rectTransform.rect.width, Is.GreaterThan(0));
                Assert.That(emblem.rectTransform.rect.height, Is.GreaterThan(0));
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    emblem.transform.parent, emblem.rectTransform);
                Rect box = ((RectTransform)emblem.transform.parent).rect;
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(box.xMin));
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(box.xMax));
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(box.yMin));
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(box.yMax));
            }
        }
    }
}
