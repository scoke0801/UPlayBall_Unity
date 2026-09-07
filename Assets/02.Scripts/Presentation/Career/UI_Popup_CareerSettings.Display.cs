using Baseball.Game.Historical;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Popup_CareerSettings
    {
        private void RenderDisplaySettings(RectTransform body)
        {
            if (!DevelopmentRealIdentitySettings.IsAvailable)
            {
                RenderPlaceholder(body, "화면");
                return;
            }

            CreateHeading(body, "개발용 실제 Identity", 260f);
            bool isEnabled = DevelopmentRealIdentitySettings.IsEnabled;
            Button toggle = CreateButton(
                "RealIdentityToggle",
                body,
                isEnabled ? "ON  선수·구단 실명 + 실제 엠블렘" : "OFF  가상 이름 + 가상 엠블렘",
                new Vector2(520f, 62f),
                new Vector2(0f, 155f),
                isEnabled ? SelectedColor : CardColor,
                out _);
            toggle.onClick.AddListener(() =>
            {
                DevelopmentRealIdentitySettings.SetEnabled(!DevelopmentRealIdentitySettings.IsEnabled);
                Render();
            });

            CreateText(
                "RealIdentityGuide",
                body,
                "표시 이름과 엠블렘만 바뀝니다. 선수·구단 Stable ID, 경기 결과, 기록과 세이브 데이터는 유지됩니다.\n현재 화면과 이후에 여는 화면에 즉시 적용됩니다.",
                17,
                FontStyle.Normal,
                TextAnchor.MiddleCenter,
                new Vector2(760f, 100f),
                new Vector2(0f, 50f),
                SecondaryTextColor);
        }
    }
}
