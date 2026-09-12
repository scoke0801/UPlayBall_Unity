using UnityEngine;

namespace Baseball.Presentation.UI
{
    /// <summary>선수·구단주 UI가 공유하는 프로젝트 기본 폰트 정의다.</summary>
    public sealed class UIProjectFonts : ScriptableObject
    {
        [SerializeField] private Font _medium;
        private static UIProjectFonts _instance;

        /// <summary>플랫폼의 설치 폰트에 의존하지 않는 Medium 폰트를 반환한다.</summary>
        public static Font Default
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<UIProjectFonts>("Fonts/DefaultFonts");
                if (_instance == null || _instance._medium == null)
                    throw new System.InvalidOperationException("프로젝트 기본 Medium 폰트 에셋이 누락되었습니다.");
                return _instance._medium;
            }
        }
    }
}
