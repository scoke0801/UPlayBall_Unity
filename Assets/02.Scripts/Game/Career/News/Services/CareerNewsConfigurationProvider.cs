using System;

namespace Baseball.Game.Career.News
{
    /// <summary>
    /// 뉴스 설정을 명시적으로 주입받지 않은 호출자에게 기본 설정을 제공한다.
    /// </summary>
    /// <remarks>
    /// Unity 런타임에서는 Resources 저작 정의를, EditMode 테스트와 Headless 러너에서는
    /// 코드 기본값을 쓰도록 Game 레이어가 Unity를 참조하지 않은 채로 갈라지게 한다.
    /// </remarks>
    public static class CareerNewsConfigurationProvider
    {
        private static Func<CareerNewsConfiguration> _loader;

        /// <summary>Unity 부트스트랩이 Resources 기반 로더를 등록한다.</summary>
        public static void SetLoader(Func<CareerNewsConfiguration> loader)
        {
            _loader = loader;
        }

        /// <summary>등록된 로더가 없거나 결과가 없으면 테스트된 코드 기본값으로 대체한다.</summary>
        public static CareerNewsConfiguration Load()
        {
            return _loader?.Invoke() ?? CareerNewsConfiguration.CreateDefault();
        }
    }
}
