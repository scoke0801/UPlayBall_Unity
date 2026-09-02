namespace Baseball.Game.Input
{
    /// <summary>
    /// 현재 화면이 허용하는 입력 action map 조합을 나타낸다.
    /// </summary>
    public enum InputContext
    {
        Management,
        Match,
        Modal,
        Disabled
    }

    /// <summary>
    /// 마지막으로 입력한 장치 계열을 UI 표현에 전달한다.
    /// </summary>
    public enum InputDeviceKind
    {
        KeyboardMouse,
        Gamepad
    }
}
