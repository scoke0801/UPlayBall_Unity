namespace Baseball.Presentation.UI
{
    /// <summary>
    /// Home, Team, League처럼 한 화면을 점유하는 Scene UI의 공통 기반이다.
    /// </summary>
    public abstract class UISceneBase : UIBase
    {
        public override UILayer Layer => UILayer.Scene;
    }
}
