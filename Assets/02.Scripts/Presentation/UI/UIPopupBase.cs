namespace Baseball.Presentation.UI
{
    /// <summary>
    /// 확인, 선수 상세, 감독 명령처럼 기존 화면 위에 쌓이는 Popup UI의 공통 기반이다.
    /// </summary>
    public abstract class UIPopupBase : UIBase
    {
        public override UILayer Layer => UILayer.Popup;
        public override bool BlocksLowerInput => true;
    }
}
