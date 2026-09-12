using Baseball.Game.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.UI
{
    /// <summary>
    /// 텍스트 입력창에 포커스가 있을 때만 IME 조합을 허용하고, 그 사실을 InputManager에 알린다.
    /// </summary>
    /// <remarks>
    /// Q/E 탭 단축키를 한글 자판 상태에서 누르면 입력창이 없어도 OS IME가 'ㅂ'/'ㄷ' 조합을 시작한다.
    /// 그 조합은 확정되지 않은 채 남아 있다가 다음에 선택된 InputField가 compositionString으로 그려,
    /// 클릭만 해도 글자가 보이고 text에 들어간 값이 아니라서 Backspace로도 지워지지 않는다.
    /// IMECompositionMode.Auto는 Windows에서 이 조합을 막지 못하므로 포커스가 없으면 명시적으로 끈다.
    /// InputField는 활성화될 때 스스로 On으로 바꾸므로 입력창 쪽 코드는 손대지 않는다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UITextInputImeGate : MonoBehaviour
    {
        private bool _wasTextInputFocused;

        private void Update()
        {
            EventSystem eventSystem = EventSystem.current;
            bool isTextInputFocused = IsTextInputFocused(eventSystem);
            if (isTextInputFocused != _wasTextInputFocused)
            {
                _wasTextInputFocused = isTextInputFocused;
                InputManager.Instance?.SetTextInputFocused(isTextInputFocused);
            }

            if (isTextInputFocused) return;
            BaseInput input = eventSystem?.currentInputModule?.input;
            if (input == null || input.imeCompositionMode == IMECompositionMode.Off) return;
            input.imeCompositionMode = IMECompositionMode.Off;
        }

        private void OnDisable()
        {
            if (!_wasTextInputFocused) return;
            _wasTextInputFocused = false;
            InputManager.Instance?.SetTextInputFocused(false);
        }

        private static bool IsTextInputFocused(EventSystem eventSystem)
        {
            GameObject selected = eventSystem?.currentSelectedGameObject;
            if (selected == null) return false;
            InputField inputField = selected.GetComponent<InputField>();
            return inputField != null && inputField.isFocused;
        }
    }
}
