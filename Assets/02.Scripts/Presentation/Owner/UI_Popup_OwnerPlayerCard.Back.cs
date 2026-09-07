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
        internal static void BuildReferenceBack(RectTransform parent, OwnerCollectionCardSnapshot card, bool pitcher)
        {
            Image frame = Surface(parent, "BackFrame", Color.white, 0, 0, 1, 1).GetComponent<Image>();
            frame.sprite = Resources.Load<Sprite>("UI/PlayerCards/PlayerCard_Back_Reference");
            Color paper = new Color32(189, 184, 169, 255);
            Color panel = new Color32(39, 36, 36, 255);
            Gradient(parent, "IdentityBand", new Color32(215, 212, 201, 255), paper, .30f, .910f, .98f, .985f);
            Label(parent, "Identity", card.OriginYear + " · " + card.DisplayName,
                .32f, .943f, .96f, .982f, 21, Ink);
            string teamAndEdition = string.IsNullOrWhiteSpace(card.TeamDisplayName)
                ? OwnerCollectionPresentationBuilder.FormatEdition(card.Edition)
                : card.TeamDisplayName + " · " + OwnerCollectionPresentationBuilder.FormatEdition(card.Edition);
            Label(parent, "Edition", teamAndEdition,
                .32f, .912f, .96f, .945f, 12, Ink);
            string roleText = pitcher && card.PitcherRole.HasValue
                ? OwnerCollectionPresentationBuilder.FormatPitcherRole(card.PitcherRole.Value)
                : OwnerCollectionPresentationBuilder.FormatPosition(card.Position);
            string hands = FormatHands(card.Throws, card.Bats);
            Gradient(parent, "ProfileBand", new Color32(218, 215, 202, 255), paper, .012f, .395f, .277f, .985f);
            Image portrait = Surface(parent, "ProfilePortrait", Color.white, .025f, .695f, .265f, .97f).GetComponent<Image>();
            portrait.sprite = PlayerPortraitSprites.GetDefault(card.Position);
            portrait.preserveAspect = true;
            string enhancement = card.IsOwnedCard ? "\n강화 +" + card.EnhancementLevel : string.Empty;
            Label(parent, "Profile", hands + "\n" + roleText + "\n비용 " + card.Cost + enhancement,
                .025f, .42f, .265f, .68f, 14, Ink);

            BuildSeasonRecord(parent, card, paper, panel);
            RectTransform role = Surface(parent, "RoleInformation", new Color(0.22f, .09f, .12f, .72f), .286f, .395f, .985f, .907f);
            if (pitcher) BuildPitchRepertoire(role, card);
            else BuildDefenseDiagram(role, card.Position);
            if (card.IsOwnedCard) BuildSkillBlockBoard(parent, paper, panel, card);
            else BuildPublicLineupNotice(parent, paper, panel);
        }

        private static void BuildPublicLineupNotice(RectTransform parent, Color paper, Color panel)
        {
            Gradient(parent, "PublicInformationTitle", paper, panel, .012f, .245f, .988f, .28f);
            Label(parent, "PublicInformationHeading", "상대 구단 공개 정보", .04f, .245f, .96f, .28f,
                12, Color.white);
            RectTransform section = Surface(parent, "PublicInformation", Ink, .012f, .012f, .988f, .24f);
            Label(section, "State", "카드 기본 능력치와 공개 시즌 기록입니다.\n컨디션·훈련·스킬 블록 등 내부 정보는 공개하지 않습니다.",
                .06f, .18f, .94f, .86f, 13, new Color(.72f, .75f, .78f));
        }

        private static void BuildDefenseDiagram(RectTransform parent, PlayerPosition position)
        {
            Label(parent, "Heading", "수비 위치", 0, .88f, 1, 1, 15, Gold);
            RectTransform area = Surface(parent, "DefenseDiagram", Color.clear, .07f, .10f, .93f, .86f);
            RectTransform field = OwnerRuntimeUiFactory.CreateRect("Field", area);
            OwnerRuntimeUiFactory.Stretch(field);
            var fit = field.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            Vector2 point = position switch
            {
                PlayerPosition.LeftField => new Vector2(.23f, .70f),
                PlayerPosition.CenterField => new Vector2(.5f, .84f),
                PlayerPosition.RightField => new Vector2(.77f, .70f),
                PlayerPosition.ThirdBase => new Vector2(.25f, .38f),
                PlayerPosition.Shortstop => new Vector2(.37f, .55f),
                PlayerPosition.SecondBase => new Vector2(.63f, .55f),
                PlayerPosition.FirstBase => new Vector2(.75f, .38f),
                PlayerPosition.Catcher => new Vector2(.5f, .10f),
                _ => new Vector2(.86f, .15f)
            };
            field.gameObject.AddComponent<UICardDefenseField>().raycastTarget = false;
            point = UICardDefenseField.FitPoint(point);
            Image ball = Surface(field, "PositionBall", Color.white,
                point.x - .045f, point.y - .045f, point.x + .045f, point.y + .045f).GetComponent<Image>();
            ball.sprite = Resources.Load<Sprite>("UI/MiniGame/img_baseball_ball");
            ball.preserveAspect = true;
            string label = OwnerCollectionPresentationBuilder.FormatPosition(position);
            Text positionLabel = Label(field, "PositionLabel", label,
                Mathf.Clamp(point.x - .16f, 0, .68f), point.y - .12f,
                Mathf.Clamp(point.x - .16f, 0, .68f) + .32f, point.y - .05f, 13, Color.white);
            var shadow = positionLabel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .85f);
            shadow.effectDistance = new Vector2(1, -1);
            Label(parent, "PositionLegend", "주 포지션 · " + label, 0, .015f, 1, .09f, 12, Gold);
        }
        private static void BuildSkillBlockBoard(
            RectTransform parent, Color paper, Color panel, OwnerCollectionCardSnapshot card)
        {
            Gradient(parent, "SkillBoardTitle", paper, panel, .012f, .245f, .988f, .28f);
            Label(parent, "SkillBoardHeading", "스킬 블록 · 4×4", .04f, .245f, .96f, .28f, 12, Color.white);
            RectTransform section = Surface(parent, "SkillBoardInformation", Ink, .012f, .012f, .988f, .24f);
            RectTransform grid = Surface(section, "Grid", new Color32(131, 137, 148, 255), .015f, .06f, .32f, .96f);
            SkillBoardDefinition definition = SkillBoardDefinition.CreateDefault();
            const float gap = .012f;
            float cellSize = (1f - gap * (definition.Width + 1)) / definition.Width;
            for (int y = 0; y < definition.Height; y++)
            {
                for (int x = 0; x < definition.Width; x++)
                {
                    float x0 = gap + x * (cellSize + gap);
                    float y1 = 1f - gap - y * (cellSize + gap);
                    RectTransform cell = Surface(grid, "Cell_" + x + "_" + y,
                        new Color32(43, 49, 61, 255),
                        x0, y1 - cellSize, x0 + cellSize, y1);
                }
            }
            BuildPlacedSkillBlocks(grid, card.SkillBlockPlacements, definition.Width, definition.Height);
            Label(section, "State",
                $"장착 {card.PlacedSkillBlockCount}개\n미장착 인벤토리 {card.AvailableSkillBlockCount}개\n\n보유 선수 > 카드훈련에서 배치 변경",
                .39f, .12f, .98f, .93f,
                12, new Color(.72f, .75f, .78f));
        }

        private static void BuildPlacedSkillBlocks(
            RectTransform grid,
            System.Collections.Generic.IReadOnlyList<OwnerSkillBlockPlacementSnapshot> placements,
            int boardWidth,
            int boardHeight)
        {
            for (int index = 0; index < placements.Count; index++)
            {
                OwnerSkillBlockPlacementSnapshot placement = placements[index];
                BoardCell[] shapeCells = placement.CreateShapeCells();
                Sprite sprite = TetrominoSpriteResolver.Resolve(shapeCells);
                if (sprite == null) continue;

                GetBlockBounds(shapeCells, placement.RotationQuarterTurns, out int rotatedWidth, out int rotatedHeight);
                GetBlockBounds(shapeCells, 0, out int baseWidth, out int baseHeight);
                float centerX = (placement.OriginX + rotatedWidth * 0.5f) / boardWidth;
                float centerY = 1f - (placement.OriginY + rotatedHeight * 0.5f) / boardHeight;
                Vector2 halfSize = new Vector2(baseWidth / (boardWidth * 2f), baseHeight / (boardHeight * 2f));
                RectTransform rect = OwnerRuntimeUiFactory.CreateRect("PlacedBlock_" + index, grid);
                OwnerRuntimeUiFactory.SetAnchors(
                    rect,
                    new Vector2(centerX - halfSize.x, centerY - halfSize.y),
                    new Vector2(centerX + halfSize.x, centerY + halfSize.y),
                    new Vector2(2f, 2f),
                    new Vector2(-2f, -2f));
                Image image = rect.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                // 공용 아틀라스는 무채색이므로 정의의 등급 색상을 별도로 입힌다.
                image.color = SkillBlockVisual.GetRarityColor(placement.Rarity);
                image.preserveAspect = false;
                image.raycastTarget = false;
                rect.localEulerAngles = new Vector3(0f, 0f, placement.RotationQuarterTurns * 90f);
            }
        }

        private static void GetBlockBounds(
            BoardCell[] shapeCells,
            int rotationQuarterTurns,
            out int width,
            out int height)
        {
            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            int maximumX = int.MinValue;
            int maximumY = int.MinValue;
            for (int index = 0; index < shapeCells.Length; index++)
            {
                BoardCell cell = shapeCells[index];
                int x;
                int y;
                switch (rotationQuarterTurns)
                {
                    case 1: x = cell.Y; y = -cell.X; break;
                    case 2: x = -cell.X; y = -cell.Y; break;
                    case 3: x = -cell.Y; y = cell.X; break;
                    default: x = cell.X; y = cell.Y; break;
                }
                minimumX = Mathf.Min(minimumX, x);
                minimumY = Mathf.Min(minimumY, y);
                maximumX = Mathf.Max(maximumX, x);
                maximumY = Mathf.Max(maximumY, y);
            }
            width = maximumX - minimumX + 1;
            height = maximumY - minimumY + 1;
        }

        private static void BuildPitchRepertoire(RectTransform parent, OwnerCollectionCardSnapshot card)
        {
            Label(parent, "Heading", "보유 구종", 0, .88f, 1, 1, 14, Gold);
            int count = card.Pitches.Count;
            if (count == 0)
            {
                Label(parent, "PitchUnavailable", "구종 정보 없음", .05f, .1f, .95f, .8f, 13, Gold);
                return;
            }

            float rowHeight = Mathf.Min(.17f, .76f / count);
            float top = .47f + count * rowHeight * .5f;
            for (int index = 0; index < count; index++)
            {
                float y1 = top - index * rowHeight;
                float y0 = y1 - rowHeight + .014f;
                RectTransform cell = Gradient(parent, "PitchSlot" + index,
                    new Color32(45, 43, 46, 255), new Color32(22, 22, 25, 255), .075f, y0, .925f, y1);
                OwnerPitchCardSnapshot pitch = card.Pitches[index];
                Surface(cell, "TopRule", new Color32(140, 127, 111, 255), 0, .985f, 1, 1);
                Surface(cell, "DirectionPanel", new Color32(57, 51, 51, 255), .012f, .07f, .19f, .92f);
                Label(cell, "Direction", GetPitchDirection(pitch.PitchType, card.Throws), .012f, .07f, .19f, .92f,
                    31, new Color32(220, 205, 183, 255));
                Label(cell, "PitchName", pitch.DisplayName, .235f, .42f, .76f, .88f, 15, Color.white).alignment = TextAnchor.MiddleLeft;
                Color gradeColor = pitch.Grade.StartsWith("S", System.StringComparison.Ordinal)
                    ? new Color32(241, 161, 179, 255) : new Color32(229, 205, 147, 255);
                Surface(cell, "GradeDivider", new Color32(99, 86, 76, 255), .79f, .20f, .793f, .80f);
                Label(cell, "Grade", pitch.Grade, .81f, .10f, .98f, .90f, 29, gradeColor);
                Label(cell, "Velocity", Mathf.RoundToInt((float)pitch.VelocityKph) + " km/h",
                    .235f, .10f, .76f, .43f, 12, Gold).alignment = TextAnchor.MiddleLeft;
            }
        }

        private static string GetPitchDirection(PitchType pitch, Handedness? throws)
        {
            bool left = throws == Handedness.Left;
            // 구종의 대표 변화 방향을 읽기 위한 도식이며 실제 궤적이나 추가 능력치를 만들지 않는다.
            return pitch switch
            {
                PitchType.FourSeamFastball => "↑",
                PitchType.Cutter or PitchType.Slider or PitchType.Sweeper => left ? "→" : "←",
                PitchType.Curveball or PitchType.KnuckleCurve or PitchType.Slurve => left ? "↘" : "↙",
                PitchType.TwoSeamFastball or PitchType.Sinker or PitchType.Screwball => left ? "↙" : "↘",
                _ => "↓"
            };
        }

        private static void BuildSeasonRecord(RectTransform parent, OwnerCollectionCardSnapshot card, Color paper, Color panel)
        {
            Gradient(parent, "RecordTitle", paper, panel, .012f, .351f, .988f, .392f);
            Label(parent, "RecordHeading", card.CurrentLeagueLabel, .04f, .351f, .96f, .392f, 12, Color.white);
            int count = card.SeasonRecord.Count;
            Surface(parent, "RecordPanel", panel, .012f, .283f, .988f, .351f);
            if (count == 0)
            {
                Label(parent, "RecordUnavailable", "확정 시즌 기록 없음", .04f, .285f, .96f, .349f, 12, Gold);
                return;
            }
            for (int index = 0; index < count; index++)
            {
                float x0 = .03f + .94f * index / count;
                float x1 = .03f + .94f * (index + 1) / count;
                OwnerCardRecordFieldSnapshot field = card.SeasonRecord[index];
                Label(parent, "RecordLabel" + index, field.Label, x0, .318f, x1, .349f, 11, Gold);
                Label(parent, "RecordValue" + index, field.Value, x0, .285f, x1, .317f, 14, Color.white);
            }
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
