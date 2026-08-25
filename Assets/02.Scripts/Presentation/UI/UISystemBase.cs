namespace Baseball.Presentation.UI
{
    /// <summary>
    /// 로딩, 페이드처럼 모든 화면보다 위에 표시되는 System UI의 공통 기반이다.
    /// </summary>
    public abstract class UISystemBase : UIBase
    {
        public override UILayer Layer => UILayer.System;
    }
}
