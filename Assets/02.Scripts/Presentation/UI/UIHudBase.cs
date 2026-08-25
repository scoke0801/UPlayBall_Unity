namespace Baseball.Presentation.UI
{
    /// <summary>
    /// 경기 스코어보드처럼 Scene 화면 위에 유지되는 HUD UI의 공통 기반이다.
    /// </summary>
    public abstract class UIHudBase : UIBase
    {
        public override UILayer Layer => UILayer.HUD;
    }
}
