namespace Baseball.Presentation.UI
{
    /// <summary>
    /// Home, Team, League처럼 한 화면을 점유하는 Scene UI의 공통 기반이다.
    /// </summary>
    public abstract class UISceneBase : UIBase
    {
        public override UILayer Layer => UILayer.Scene;

        // Scene UI는 다른 화면 위에 뜬 오버레이가 아니라 해당 화면 자체이므로,
        // ESC(Cancel)로 닫으면 그 아래에 아무것도 없어 화면이 비어버린다.
        public override bool CanCloseWithCancel => false;
    }
}
