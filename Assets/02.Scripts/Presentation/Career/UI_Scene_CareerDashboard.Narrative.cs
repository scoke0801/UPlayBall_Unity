using Baseball.Game.Career;
using Baseball.Game.Career.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerDashboard
    {
        /// <summary>중요 경기에서만 발생한 질문을 다른 홈 입력보다 먼저 해결하게 한다.</summary>
        private void RenderCareerReactionOverlay(CareerDashboardView view)
        {
            CareerReactionEventState reaction = view.PendingReaction;
            CreateImage(
                "ReactionDim",
                _content,
                new Color(0f, 0.01f, 0.02f, 0.93f),
                Vector2.zero,
                Vector2.zero,
                stretch: true);
            RectTransform panel = CreateImage(
                "ReactionPanel",
                _content,
                PanelColor,
                new Vector2(1180f, 760f),
                new Vector2(0f, -18f));
            CreateImage(
                "TopLine", panel, AccentColor,
                new Vector2(1140f, 4f), new Vector2(0f, 356f));
            CreateText(
                "Eyebrow", panel, GetReactionSpeakerLabel(reaction.Speaker), 14,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(520f, 28f), new Vector2(0f, 305f), AccentColor);
            CreateText(
                "Speaker", panel, reaction.SpeakerName, 20,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(520f, 32f), new Vector2(0f, 266f), SecondaryTextColor);
            CreateText(
                "Prompt", panel, reaction.Prompt, 27,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(1000f, 86f), new Vector2(0f, 190f), PrimaryTextColor);

            for (int index = 0; index < reaction.Options.Count; index++)
            {
                int selectedIndex = index;
                CareerReactionOptionState option = reaction.Options[index];
                Button button = CreateButton(
                    $"ReactionOption_{index}",
                    panel,
                    $"{index + 1}   {option.Text}",
                    new Vector2(960f, 86f),
                    new Vector2(0f, 75f - index * 108f),
                    index == 0
                        ? new Color(0.025f, 0.24f, 0.43f, 1f)
                        : new Color(0.025f, 0.12f, 0.2f, 1f),
                    out Text optionText);
                optionText.fontSize = 18;
                button.onClick.AddListener(() => ResolveCareerReaction(selectedIndex));
            }

            CreateText(
                "RatingsLabel", panel, "현재 커리어 반응 지표", 12,
                FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(400f, 22f), new Vector2(0f, -275f), MutedColor);
            CreateText(
                "Ratings", panel,
                $"자신감 {view.NarrativeConfidence}   ·   미디어 {view.MediaStanding}   ·   " +
                $"팬 반응 {view.FanSupport}   ·   팀워크 {view.TeamChemistry}",
                15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(880f, 30f), new Vector2(0f, -310f), SecondaryTextColor);
        }

        private void ResolveCareerReaction(int optionIndex)
        {
            _manager.ResolveCareerReaction(optionIndex);
        }

        private static string GetReactionSpeakerLabel(CareerReactionSpeaker speaker)
        {
            return speaker switch
            {
                CareerReactionSpeaker.Manager => "MANAGER MESSAGE",
                CareerReactionSpeaker.Teammate => "TEAMMATE MESSAGE",
                _ => "POSTGAME INTERVIEW"
            };
        }
    }
}
