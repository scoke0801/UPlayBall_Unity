using Baseball.Game.SceneFlow;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.SceneFlow
{
    /// <summary>
    /// 논리 Scene 식별자와 Build Settings 이름 계약을 검증한다.
    /// </summary>
    public sealed class SceneCatalogTests
    {
        [TestCase(SceneId.Boot, SceneCatalog.BootSceneName)]
        [TestCase(SceneId.Loading, SceneCatalog.LoadingSceneName)]
        [TestCase(SceneId.Management, SceneCatalog.ManagementSceneName)]
        [TestCase(SceneId.Match, SceneCatalog.MatchSceneName)]
        public void GetSceneName_등록된Scene이름을반환한다(SceneId sceneId, string expected)
        {
            Assert.That(SceneCatalog.GetSceneName(sceneId), Is.EqualTo(expected));
        }

        [Test]
        public void IsContentScene_Boot와Loading을콘텐츠로취급하지않는다()
        {
            Assert.That(SceneCatalog.IsContentScene(SceneId.Boot), Is.False);
            Assert.That(SceneCatalog.IsContentScene(SceneId.Loading), Is.False);
            Assert.That(SceneCatalog.IsContentScene(SceneId.Management), Is.True);
            Assert.That(SceneCatalog.IsContentScene(SceneId.Match), Is.True);
        }

        [Test]
        public void SceneLoadRequest_음수최소표시시간을0으로보정한다()
        {
            var request = new SceneLoadRequest(
                SceneId.Management,
                SceneTransitionMode.LoadingScreen,
                minimumLoadingTime: -1f);

            Assert.That(request.MinimumLoadingTime, Is.Zero);
        }
    }
}
