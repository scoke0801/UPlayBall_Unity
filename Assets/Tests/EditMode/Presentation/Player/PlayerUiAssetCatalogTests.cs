using Baseball.Presentation.Player;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Presentation.Player
{
    /// <summary>
    /// 생성 배경을 코드 위치가 아니라 안정적인 Asset ID로 요청하는지 검증한다.
    /// </summary>
    public sealed class PlayerUiAssetCatalogTests
    {
        [Test]
        public void HomeBackground_생성Asset의안정적인Key와경로를제공한다()
        {
            Assert.That(
                PlayerUiAssetCatalog.GetKey(PlayerUiAssetId.HomeClubhouseBackground),
                Is.EqualTo(PlayerUiAssetCatalog.HomeClubhouseBackgroundKey));
            Assert.That(
                PlayerUiAssetCatalog.HomeClubhouseBackgroundAssetPath,
                Does.EndWith("bg_player_clubhouse_v1.png"));
            Assert.That(
                PlayerUiAssetCatalog.HomeClubhouseBackgroundResourcePath,
                Is.EqualTo("UI/Generated/bg_player_clubhouse_v1"));
        }
    }
}
