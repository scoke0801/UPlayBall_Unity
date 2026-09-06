using Baseball.Core.Historical;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Popup_OwnerPlayerCard
    {
        private void BuildReferenceBack(RectTransform parent, OwnerCollectionCardSnapshot card, bool pitcher)
        {
            BuildCardBorder(parent);
            Color paper = new Color32(107, 122, 143, 255);
            Color panel = new Color32(26, 44, 65, 255);
            Gradient(parent, "IdentityBand", GetEditionColor(card.Edition), Ink, .30f, .865f, .98f, .98f);
            Label(parent, "Identity", card.OriginYear + " · " + card.DisplayName,
                .32f, .895f, .96f, .97f, 21, Color.white);
            Label(parent, "Edition", OwnerCollectionPresentationBuilder.FormatEdition(card.Edition),
                .32f, .865f, .96f, .90f, 12, Gold);
            string roleText = pitcher && card.PitcherRole.HasValue
                ? FormatPitcherRole(card.PitcherRole.Value)
                : OwnerCollectionPresentationBuilder.FormatPosition(card.Position);
            string hands = FormatHands(card.Throws, card.Bats);
            Gradient(parent, "ProfileBand", panel, Ink, .02f, .485f, .295f, .98f);
            Image portrait = Surface(parent, "ProfilePortrait", Color.white, .035f, .735f, .28f, .97f).GetComponent<Image>();
            portrait.sprite = PlayerPortraitSprites.GetDefault(card.Position);
            portrait.preserveAspect = true;
            Label(parent, "Profile", hands + "\n" + roleText + "\n비용 " + card.Cost + "\n강화 +" + card.EnhancementLevel,
                .03f, .505f, .285f, .72f, 14, Color.white);

            BuildSeasonRecord(parent, card, paper, panel);
            RectTransform role = Gradient(parent, "RoleInformation", new Color32(71, 105, 143, 255), panel, .30f, .485f, .98f, .86f);
            if (pitcher) BuildPitchRepertoire(role, card);
            else BuildDefenseDiagram(role, card.Position);
            BuildSkillBlockBoard(parent, paper, panel, card);
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
            Label(parent, "PositionLegend", "밝은 칸: 주 포지션", 0, 0, 1, .09f, 10, Gold);
        }

        private static void BuildSkillBlockBoard(
            RectTransform parent, Color paper, Color panel, OwnerCollectionCardSnapshot card)
        {
            Gradient(parent, "SkillBoardTitle", paper, panel, .02f, .29f, .98f, .33f);
            Label(parent, "SkillBoardHeading", "스킬 블록 · 4×4", .04f, .29f, .96f, .33f, 12, Color.white);
            RectTransform section = Surface(parent, "SkillBoardInformation", Ink, .02f, .02f, .98f, .285f);
            RectTransform grid = Surface(section, "Grid", new Color32(131, 137, 148, 255), .015f, .06f, .355f, .96f);
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
                        traitSocket ? new Color32(178, 191, 209, 255) : new Color32(43, 49, 61, 255),
                        x0, y1 - cellSize, x0 + cellSize, y1);
                    if (traitSocket)
                        Label(cell, "TraitSocket", "◇", 0, 0, 1, 1, 12, Ink);
                }
            }
            Label(section, "State",
                $"장착 {card.PlacedSkillBlockCount}개\n미장착 인벤토리 {card.AvailableSkillBlockCount}개\n\n보유 선수 > 카드훈련에서 배치 변경",
                .39f, .12f, .98f, .93f,
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
            Label(parent, "Heading", "보유 구종", 0, .86f, 1, 1, 14, Gold);
            int count = card.Pitches.Count;
            if (count == 0)
            {
                Label(parent, "PitchUnavailable", "구종 정보 없음", .05f, .1f, .95f, .8f, 13, Gold);
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
                RectTransform cell = Gradient(parent, "PitchSlot" + index,
                    new Color32(107, 134, 163, 255), new Color32(49, 77, 107, 255), x, y0, x + .44f, y1);
                OwnerPitchCardSnapshot pitch = card.Pitches[index];
                Label(cell, "PitchName", pitch.DisplayName, .03f, .48f, .63f, .95f, 13, Color.white);
                Label(cell, "Grade", pitch.Grade, .64f, .40f, .97f, .97f, 21, Gold);
                Label(cell, "Velocity", Mathf.RoundToInt((float)pitch.VelocityKph) + " km/h",
                    .05f, .05f, .95f, .45f, 12, new Color(.78f, .82f, .86f));
            }
        }

        private static void BuildSeasonRecord(RectTransform parent, OwnerCollectionCardSnapshot card, Color paper, Color panel)
        {
            Gradient(parent, "RecordTitle", paper, panel, .02f, .44f, .98f, .48f);
            Label(parent, "RecordHeading", card.CurrentLeagueLabel, .04f, .44f, .96f, .48f, 12, Color.white);
            int count = card.SeasonRecord.Count;
            Surface(parent, "RecordPanel", panel, .02f, .335f, .98f, .44f);
            if (count == 0)
            {
                Label(parent, "RecordUnavailable", "확정 시즌 기록 없음", .04f, .34f, .96f, .435f, 12, Gold);
                return;
            }
            for (int index = 0; index < count; index++)
            {
                float x0 = .03f + .94f * index / count;
                float x1 = .03f + .94f * (index + 1) / count;
                OwnerCardRecordFieldSnapshot field = card.SeasonRecord[index];
                Label(parent, "RecordLabel" + index, field.Label, x0, .39f, x1, .435f, 11, Gold);
                Label(parent, "RecordValue" + index, field.Value, x0, .34f, x1, .39f, 14, Color.white);
            }
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
                _ => "역할 미정"
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
