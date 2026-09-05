using System;
using Baseball.Presentation.UI;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    /// <summary>선수 커리어 화면의 고정된 하단 8개 탭을 식별한다.</summary>
    public enum CareerMainTab
    {
        Home,
        Player,
        Growth,
        Schedule,
        League,
        Team,
        Records,
        Contract
    }

    /// <summary>각 메뉴 화면이 중앙 라우터에 자신이 담당하는 탭을 알리는 계약이다.</summary>
    public interface ICareerTabScreen
    {
        CareerMainTab MainTab { get; }
    }

    /// <summary>SharedGameShell 이관 중인 화면에서 Legacy 하단 Chrome 생성을 차단한다.</summary>
    public static class CareerNavigationChrome
    {
        /// <summary>Navigation은 단일 PlayerCareerShellCoordinator가 소유하므로 화면별 복제 생성을 하지 않는다.</summary>
        public static void Create(Transform parent, CareerMainTab activeTab)
        {
            // 기존 화면의 Render 호출 계약을 깨지 않고 중앙 셸로 이동하기 위한 무동작 호환점이다.
        }
    }

    /// <summary>등록된 커리어 화면을 찾아 현재 탭만 보이게 전환한다.</summary>
    public static class CareerTabNavigation
    {
        /// <summary>중앙 셸이 프로그램 기반 화면 전환도 같은 선택 상태로 반영하도록 알린다.</summary>
        public static event Action<CareerMainTab> TabChanged;

        /// <summary>마지막으로 성공적으로 표시한 선수 커리어 탭이다.</summary>
        public static CareerMainTab CurrentTab { get; private set; } = CareerMainTab.Home;

        public static bool Show(CareerMainTab tab)
        {
            UIBase[] screens = UnityEngine.Object.FindObjectsByType<UIBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            UIBase target = null;
            for (int index = 0; index < screens.Length; index++)
            {
                if (screens[index] is ICareerTabScreen careerScreen && careerScreen.MainTab == tab)
                {
                    target = screens[index];
                    break;
                }
            }
            if (target == null)
                return false;

            for (int index = 0; index < screens.Length; index++)
            {
                if (screens[index] != target && screens[index] is ICareerTabScreen)
                    screens[index].Hide();
            }
            target.Show();
            CurrentTab = tab;
            TabChanged?.Invoke(tab);
            return true;
        }
    }
}
