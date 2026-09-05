using UnityEngine;

namespace Baseball.Presentation.Player
{
    /// <summary>
    /// 선수 모드에서 요청할 수 있는 모드 전용 시각 자산을 구분한다.
    /// </summary>
    public enum PlayerUiAssetId
    {
        HomeClubhouseBackground = 0
    }

    /// <summary>
    /// 배경의 안정적인 ID, 프로젝트 원본 위치와 Runtime Resource 경로를 정의한다.
    /// </summary>
    public static class PlayerUiAssetCatalog
    {
        public const string HomeClubhouseBackgroundKey = "ui.player.home.clubhouse-background";
        public const string HomeClubhouseBackgroundAssetPath =
            "Assets/Resources/UI/Generated/bg_player_clubhouse_v1.png";
        public const string HomeClubhouseBackgroundResourcePath =
            "UI/Generated/bg_player_clubhouse_v1";

        /// <summary>Asset 관리 계층이 사용할 안정적인 논리 Key를 반환한다.</summary>
        public static string GetKey(PlayerUiAssetId assetId)
        {
            return assetId switch
            {
                PlayerUiAssetId.HomeClubhouseBackground => HomeClubhouseBackgroundKey,
                _ => string.Empty
            };
        }
    }

    /// <summary>
    /// Workspace가 실제 저장 위치를 알지 않고 직렬화 또는 Asset 관리 계층에서 Sprite를 받는 계약이다.
    /// </summary>
    public interface IPlayerUiAssetProvider
    {
        bool TryGetSprite(PlayerUiAssetId assetId, out Sprite sprite);
    }
}
