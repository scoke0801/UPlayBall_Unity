using Baseball.Core.Historical;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Popup_OwnerPlayerCard
    {
        private void BuildReferenceBack(RectTransform parent, OwnerCollectionCardSnapshot card, bool pitcher)
        {
            Image frame = Surface(parent, "NeutralFrame", Color.white, 0, 0, 1, 1).GetComponent<Image>();
            frame.sprite = Resources.Load<Sprite>("UI/PlayerCards/PlayerCard_Back_Neutral");
            Image edition = Surface(parent, "EditionOverlay", Color.white, 0, 0, 1, 1).GetComponent<Image>();
            edition.sprite = LoadEditionBack(card.Edition);
            edition.enabled = edition.sprite != null;

            Color paper = new Color(0.86f, 0.85f, 0.80f, 0.96f);
            Color panel = new Color(0.055f, 0.085f, 0.13f, 0.94f);
            Surface(parent, "IdentityBand", paper, 0.075f, 0.875f, 0.925f, 0.95f);
            Label(parent, "Identity", card.OriginYear + " · " + card.DisplayName,
                0.10f, 0.88f, 0.90f, 0.945f, 22, Ink);
            string roleText = pitcher && card.PitcherRole.HasValue
                ? FormatPitcherRole(card.PitcherRole.Value)
                : OwnerCollectionPresentationBuilder.FormatPosition(card.Position);
            string hands = FormatHands(card.Throws, card.Bats);
            Surface(parent, "ProfileBand", panel, 0.075f, 0.795f, 0.925f, 0.87f);
            Label(parent, "Profile", roleText + "  ·  " + hands + "  ·  COST " + card.Cost +
                "  ·  " + OwnerCollectionPresentationBuilder.FormatEdition(card.Edition) +
                "  ·  강화 +" + card.EnhancementLevel,
                0.09f, 0.80f, 0.91f, 0.865f, 14, Color.white);

            BuildSeasonRecord(parent, card, paper, panel);
            RectTransform role = Surface(parent, "RoleInformation", panel, 0.075f, 0.315f, 0.925f, 0.65f);
            if (pitcher) BuildPitchRepertoire(role, card);
            else BuildDefenseDiagram(role, card.Position);
            BuildSkillBlockBoard(parent, paper, panel);
        }

        private static void BuildDefenseDiagram(RectTransform parent, PlayerPosition position)
        {
            Label(parent, "Heading", "수비 포지션", 0, 0.87f, 1, 1, 15, Gold);
            RectTransform diamond = Surface(parent, "Infield", new Color(0.43f, 0.49f, 0.47f), 0.37f, 0.34f, 0.63f, 0.66f);
            diamond.localEulerAngles = new Vector3(0, 0, 45);
            PlayerPosition[] positions = { PlayerPosition.LeftField, PlayerPosition.CenterField, PlayerPosition.RightField,
                PlayerPosition.ThirdBase, PlayerPosition.Shortstop, PlayerPosition.SecondBase, PlayerPosition.FirstBase,
                PlayerPosition.Catcher, PlayerPosition.DesignatedHitter };
            Vector2[] points = { new Vector2(.13f,.72f), new Vector2(.5f,.79f), new Vector2(.87f,.72f),
                new Vector2(.18f,.41f), new Vector2(.36f,.58f), new Vector2(.64f,.58f), new Vector2(.82f,.41f),
                new Vector2(.5f,.14f), new Vector2(.13f,.14f) };
            string[] codes = { "LF", "CF", "RF", "3B", "SS", "2B", "1B", "C", "DH" };
            for (int index = 0; index < positions.Length; index++)
            {
                Vector2 point = points[index];
                bool selected = position == positions[index];
                Surface(parent, "Position" + index, selected ? Gold : new Color(.10f,.15f,.19f),
                    point.x-.065f, point.y-.055f, point.x+.065f, point.y+.055f);
                Label(parent, "PositionLabel" + index, codes[index],
                    point.x-.065f, point.y-.055f, point.x+.065f, point.y+.055f, 12, selected ? Ink : Color.white);
            }
            Label(parent, "PositionLegend", "금색: 주 포지션", 0, 0, 1, .07f, 10, Gold);
        }

        private static void BuildSkillBlockBoard(RectTransform parent, Color paper, Color panel)
        {
            Surface(parent, "SkillBoardTitle", paper, .075f, .255f, .925f, .31f);
            Label(parent, "SkillBoardHeading", "스킬 블록 · 4×4", .09f, .26f, .91f, .305f, 12, Ink);
            RectTransform section = Surface(parent, "SkillBoardInformation", panel, .075f, .075f, .925f, .255f);
            RectTransform grid = Surface(section, "Grid", new Color(.035f, .055f, .075f, .96f), .035f, .08f, .29f, .92f);
            SkillBoardDefinition definition = SkillBoardDefinition.CreateDefault();
            const float gap = .025f;
            float cellSize = (1f - gap * (definition.Width + 1)) / definition.Width;
            for (int y = 0; y < definition.Height; y++)
            {
                for (int x = 0; x < definition.Width; x++)
                {
                    float x0 = gap + x * (cellSize + gap);
                    float y1 = 1f - gap - y * (cellSize + gap);
                    bool traitSocket = HasTraitSocket(definition, x, y);
                    RectTransform cell = Surface(grid, "Cell_" + x + "_" + y,
                        traitSocket ? new Color(.28f, .24f, .14f, 1f) : new Color(.10f, .14f, .18f, 1f),
                        x0, y1 - cellSize, x0 + cellSize, y1);
                    if (traitSocket)
                        Label(cell, "TraitSocket", "◇", 0, 0, 1, 1, 12, Gold);
                }
            }
            Label(section, "State", "장착된 스킬 블록 정보 없음", .33f, .16f, .96f, .84f,
                12, new Color(.72f, .75f, .78f));
        }

        private static bool HasTraitSocket(SkillBoardDefinition definition, int x, int y)
        {
            for (int index = 0; index < definition.TraitSockets.Length; index++)
            {
                BoardCell socket = definition.TraitSockets[index];
                if (socket.X == x && socket.Y == y) return true;
            }
            return false;
        }

        private static void BuildPitchRepertoire(RectTransform parent, OwnerCollectionCardSnapshot card)
        {
            Label(parent, "Heading", "PITCH ARSENAL", 0, .86f, 1, 1, 15, Gold);
            int count = card.Pitches.Count;
            if (count == 0)
            {
                Label(parent, "PitchUnavailable", "Baked 구종 정보 없음", .05f, .1f, .95f, .8f, 13, Gold);
                return;
            }

            int rows = (count + 1) / 2;
            float rowHeight = Mathf.Min(.225f, .69f / rows);
            float totalHeight = rows * rowHeight;
            float top = .80f - (.69f - totalHeight) * .5f;
            for (int index = 0; index < count; index++)
            {
                int row = index / 2;
                bool centeredLast = count % 2 == 1 && index == count - 1;
                float x = centeredLast ? .27f : .04f + (index % 2) * .48f;
                float y1 = top - row * rowHeight;
                float y0 = y1 - rowHeight + .018f;
                RectTransform cell = Surface(parent, "PitchSlot" + index,
                    new Color(.16f, .20f, .25f, .98f), x, y0, x + .44f, y1);
                OwnerPitchCardSnapshot pitch = card.Pitches[index];
                Label(cell, "PitchName", pitch.DisplayName, .03f, .48f, .63f, .95f, 13, Color.white);
                Label(cell, "Grade", pitch.Grade, .64f, .40f, .97f, .97f, 21, Gold);
                Label(cell, "Velocity", Mathf.RoundToInt((float)pitch.VelocityKph) + " km/h",
                    .05f, .05f, .95f, .45f, 12, new Color(.78f, .82f, .86f));
            }
        }

        private static void BuildSeasonRecord(RectTransform parent, OwnerCollectionCardSnapshot card, Color paper, Color panel)
        {
            Surface(parent, "RecordTitle", paper, .075f, .735f, .925f, .79f);
            Label(parent, "RecordHeading", card.CurrentLeagueLabel, .09f, .74f, .91f, .785f, 12, Ink);
            int count = card.SeasonRecord.Count;
            Surface(parent, "RecordPanel", panel, .075f, .655f, .925f, .735f);
            if (count == 0)
            {
                Label(parent, "RecordUnavailable", "확정 시즌 기록 없음", .10f, .66f, .90f, .73f, 12, Gold);
                return;
            }
            for (int index = 0; index < count; index++)
            {
                float x0 = .08f + .84f * index / count;
                float x1 = .08f + .84f * (index + 1) / count;
                OwnerCardRecordFieldSnapshot field = card.SeasonRecord[index];
                Label(parent, "RecordLabel" + index, field.Label, x0, .697f, x1, .73f, 10, Gold);
                Label(parent, "RecordValue" + index, field.Value, x0, .66f, x1, .70f, 13, Color.white);
            }
        }

        private static Sprite LoadEditionBack(PlayerCardEdition edition)
        {
            string asset = edition switch
            {
                PlayerCardEdition.AllStar => "PlayerCard_AllStar_BackOverlay",
                PlayerCardEdition.GoldenGlove => "PlayerCard_GoldenGlove_BackOverlay",
                PlayerCardEdition.Mvp => "PlayerCard_MVP_BackOverlay",
                _ => null
            };
            return asset == null ? null : Resources.Load<Sprite>("UI/PlayerCards/" + asset);
        }

        private static string FormatPitcherRole(PitcherRole role)
        {
            return role switch
            {
                PitcherRole.Starter => "선발",
                PitcherRole.Swingman => "스윙맨",
                PitcherRole.LongRelief => "롱릴리프",
                PitcherRole.MiddleRelief => "중간계투",
                PitcherRole.Setup => "셋업",
                PitcherRole.Closer => "마무리",
                _ => role.ToString()
            };
        }

        private static string FormatHands(Handedness? throws, Handedness? bats)
        {
            if (!throws.HasValue || !bats.HasValue) return "투타 미확인";
            string throwing = throws.Value == Handedness.Left ? "좌투" : "우투";
            string batting = bats.Value switch
            {
                Handedness.Left => "좌타",
                Handedness.Switch => "양타",
                _ => "우타"
            };
            return throwing + batting;
        }
    }
}
