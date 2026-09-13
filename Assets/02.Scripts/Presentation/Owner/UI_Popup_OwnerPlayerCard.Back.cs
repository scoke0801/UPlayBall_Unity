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
                : OwnerCollectionPresentationBuilder.FormatPosition(card.Position, card.IsPositionEvidenceMissing);
            string hands = FormatHands(card.Throws, card.Bats);
            Gradient(parent, "ProfileBand", pitcher ? PitchSteel : new Color32(218, 215, 202, 255),
                pitcher ? PitchNavy : paper, .012f, .395f, .277f, .985f);
            Image portrait = Surface(parent, "ProfilePortrait", Color.white, .025f, pitcher ? .765f : .695f, .265f, .97f).GetComponent<Image>();
            portrait.sprite = PlayerPortraitSprites.GetAssigned(card.PlayerSeasonId)
                ?? PlayerPortraitSprites.GetAssigned(card.CardId)
                ?? PlayerPortraitSprites.GetForPlayer(card.PlayerPersonId, card.Position);
            portrait.preserveAspect = true;
            string enhancement = card.IsOwnedCard || card.EnhancementLevel > 0 ? "\n강화 +" + card.EnhancementLevel : string.Empty;
            Text profile = Label(parent, "Profile", hands + "\n" + roleText + "\n비용 " + card.Cost + enhancement,
                .025f, pitcher ? .615f : .42f, .265f, pitcher ? .765f : .68f, 14, pitcher ? PitchIvory : Ink);
            if (pitcher) profile.fontStyle = FontStyle.Normal;

            BuildSeasonRecord(parent, card, paper, panel);
            RectTransform role = pitcher
                ? Gradient(parent, "RoleInformation", PitchSteel, PitchNavy, .286f, .395f, .985f, .907f)
                : Surface(parent, "RoleInformation", new Color(0.22f, .09f, .12f, .72f), .286f, .395f, .985f, .907f);
            if (pitcher)
            {
                BuildPitchRepertoire(role, card);
                RectTransform velocitySlot = ContentRect(parent, "VelocitySlot", .025f, .425f, .265f, .60f);
                BuildMaximumVelocityBadge(velocitySlot, card);
            }
            else
            {
                BuildDefenseDiagram(role, card.Position, card.IsPositionEvidenceMissing);
                BuildPreferredBattingOrderBadge(role, card.PreferredBattingOrder);
            }
            BuildSkillBlockBoard(parent, paper, panel, card);
        }

        private static void BuildPreferredBattingOrderBadge(RectTransform parent, PreferredBattingOrder preference)
        {
            if (preference == PreferredBattingOrder.None) return;
            string label = preference == PreferredBattingOrder.Upper ? "상위"
                : preference == PreferredBattingOrder.Cleanup ? "클린업" : "하위";
            string order = preference == PreferredBattingOrder.Upper ? "1·2번"
                : preference == PreferredBattingOrder.Cleanup ? "3·4·5번" : "6~9번";
            // 레퍼런스의 은색 이중 테두리 명판을 기존 프레임 위에 덧붙인다.
            RectTransform badge = Surface(parent, "PreferredBattingOrderBadge", new Color32(216, 212, 203, 255),
                .035f, .105f, .285f, .355f);
            RectTransform inset = Surface(badge, "Inset", Ink, .025f, .025f, .975f, .975f);
            Gradient(inset, "HeadingBand", new Color32(124, 119, 113, 255), new Color32(58, 55, 55, 255),
                .035f, .65f, .965f, .96f);
            Label(inset, "Heading", "선호타선", .025f, .64f, .975f, .98f, 12, Color.white);
            Surface(inset, "Divider", new Color32(194, 188, 180, 255), .03f, .63f, .97f, .65f);
            Label(inset, "Preference", label, .035f, .23f, .965f, .64f, 17, Color.white);
            Label(inset, "Order", order, .025f, .025f, .975f, .25f, 10, Gold);
        }

        private static void BuildMaximumVelocityBadge(RectTransform parent, OwnerCollectionCardSnapshot card)
        {
            // 경기 중 일시적인 구속 변동 대신 기존 구종별 안정 프로필의 최댓값을 표시한다.
            double maximumVelocity = 0;
            for (int index = 0; index < card.Pitches.Count; index++)
            {
                double velocity = card.Pitches[index].VelocityKph;
                if (!double.IsNaN(velocity) && !double.IsInfinity(velocity) && velocity > maximumVelocity)
                    maximumVelocity = velocity;
            }
            RectTransform badge = ContentRect(parent, "MaximumVelocityBadge",
                .04f, .04f, .96f, .96f);
            RectTransform inset = ContentRect(badge, "Inset", .025f, .025f, .975f, .975f);
            Label(inset, "Heading", "최대구속", .025f, .70f, .975f, .98f, 11, PitchSilver);
            Surface(inset, "Divider", PitchBrass, .18f, .67f, .82f, .68f);
            Label(inset, "Velocity", maximumVelocity > 0
                ? Mathf.RoundToInt((float)maximumVelocity).ToString() : "—",
                .035f, .19f, .965f, .64f, 30, PitchIvory);
            Label(inset, "Unit", "km/h", .025f, .025f, .975f, .21f, 10, PitchSilver).fontStyle = FontStyle.Normal;
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

        private static void BuildDefenseDiagram(RectTransform parent, PlayerPosition position, bool isPositionEvidenceMissing)
        {
            if (isPositionEvidenceMissing)
                position = PlayerPosition.DesignatedHitter;
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
            if (card.SkillBlockPlacements.Count == 0)
            {
                Label(section, "State", "스킬 블록 없음", .36f, .16f, .96f, .86f, 13, PitchSilver);
                return;
            }

            // 4×4 보드의 네 칸짜리 블록마다 한 행을 배정한다.
            for (int index = 0; index < card.SkillBlockPlacements.Count; index++)
            {
                OwnerSkillBlockPlacementSnapshot placement = card.SkillBlockPlacements[index];
                float top = .90f - index * .21f;
                Text row = Label(section, "EquippedSkillBlock_" + index,
                    FormatSkillBlockCategory(placement.Category) + " · " +
                    SkillBlockGradeCatalog.GetLabel(placement.Rarity) + " · " + placement.DisplayName,
                    .36f, top - .17f, .96f, top, 13, PitchSilver);
                row.alignment = TextAnchor.MiddleLeft;
                row.fontStyle = FontStyle.Normal;
            }
        }

        private static string FormatSkillBlockCategory(SkillBlockCategory category) => category switch
        {
            SkillBlockCategory.Contact => "교타력",
            SkillBlockCategory.Power => "장타력",
            SkillBlockCategory.Baserunning => "주력",
            SkillBlockCategory.Defense => "수비력",
            SkillBlockCategory.BatterMental => "타자 정신력",
            SkillBlockCategory.Velocity => "구속",
            SkillBlockCategory.Control => "제구력",
            SkillBlockCategory.Breaking => "변화구",
            SkillBlockCategory.PitcherPhysical => "체력",
            SkillBlockCategory.PitcherMental => "투수 정신력",
            SkillBlockCategory.Bunt => "번트",
            SkillBlockCategory.Stuff => "구위",
            _ => "분류 없음"
        };

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
                if (shapeCells.Length == 0) continue;

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
                rect.localEulerAngles = new Vector3(0f, 0f, placement.RotationQuarterTurns * 90f);
                int minimumX = int.MaxValue, minimumY = int.MaxValue;
                foreach (BoardCell cell in shapeCells)
                {
                    minimumX = Mathf.Min(minimumX, cell.X);
                    minimumY = Mathf.Min(minimumY, cell.Y);
                }
                for (int cellIndex = 0; cellIndex < shapeCells.Length; cellIndex++)
                {
                    BoardCell cell = shapeCells[cellIndex];
                    float x0 = (cell.X - minimumX) / (float)baseWidth;
                    float y1 = 1f - (cell.Y - minimumY) / (float)baseHeight;
                    RectTransform tile = OwnerRuntimeUiFactory.CreateRect("SkillTile_" + cellIndex, rect);
                    OwnerRuntimeUiFactory.SetAnchors(tile, new Vector2(x0, y1 - 1f / baseHeight),
                        new Vector2(x0 + 1f / baseWidth, y1), Vector2.zero, Vector2.zero);
                    // 배치 모양만 회전하고 문양과 광원은 두 화면에서 항상 정방향을 유지한다.
                    tile.localEulerAngles = new Vector3(0, 0, -placement.RotationQuarterTurns * 90f);
                    SkillBlockVisual.ApplyDirectionalTile(tile.gameObject.AddComponent<RawImage>(), placement.Rarity,
                        shapeCells, cellIndex, placement.RotationQuarterTurns,
                        SkillBlockVisual.GetCategoryColor(placement.Category));
                }
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

        private static readonly Color PitchNavy = new Color32(19, 26, 37, 255);
        private static readonly Color PitchSteel = new Color32(52, 63, 76, 255);
        private static readonly Color PitchSilver = new Color32(169, 180, 193, 255);
        private static readonly Color PitchIvory = new Color32(245, 238, 215, 255);
        private static readonly Color PitchBrass = new Color32(179, 150, 94, 255);

        private static void BuildPitchRepertoire(RectTransform parent, OwnerCollectionCardSnapshot card)
        {
            Label(parent, "Heading", "보유 구종", .08f, .90f, .92f, .98f, 15, PitchIvory);
            Surface(parent, "HeadingRule", PitchBrass, .40f, .88f, .60f, .885f);
            int count = card.Pitches.Count;
            if (count == 0)
            {
                Label(parent, "PitchUnavailable", "구종 정보 없음", .04f, .1f, .96f, .8f, 13, Gold);
                return;
            }

            RectTransform diagram = ContentRect(parent, "PitchCompass", .06f, .14f, .94f, .84f);
            var compass = diagram.gameObject.AddComponent<UICardPitchCompass>();
            int directions = 0;
            for (int i = 0; i < count; i++)
                directions |= 1 << GetPitchDirectionIndex(card.Pitches[i].PitchType, card.Throws);
            compass.SetDirections(directions);
            Image ball = Surface(parent, "CompassBall", Color.white, .453f, .455f, .547f, .525f).GetComponent<Image>();
            ball.sprite = Resources.Load<Sprite>("UI/MiniGame/img_baseball_ball");
            ball.preserveAspect = true;

            // 명판은 중앙의 공과 방향 도식을 둘러싸며, 적은 구종도 같은 간격으로 중앙에 모은다.
            int rows = (count + 1) / 2;
            float rowHeight = Mathf.Min(.25f, .72f / rows);
            float top = .49f + (rows - 1) * rowHeight * .5f + .065f;
            for (int index = 0; index < count; index++)
            {
                float y1 = top - (index / 2) * rowHeight;
                float y0 = y1 - .13f;
                float x0 = index % 2 == 0 ? .025f : .58f;
                RectTransform cell = ContentRect(parent, "PitchSlot" + index, x0, y0, x0 + .395f, y1);
                OwnerPitchCardSnapshot pitch = card.Pitches[index];
                Gradient(cell, "MetalPlate", PitchIvory, PitchSilver, 0, .36f, 1, 1);
                Surface(cell, "TopRule", Color.white, .015f, .98f, .985f, 1);
                Gradient(cell, "VelocityPlate", PitchSteel, PitchNavy, 0, 0, 1, .36f);
                Label(cell, "PitchName", pitch.DisplayName, .035f, .41f, .76f, .94f, 12, Ink).fontStyle = FontStyle.Normal;
                Label(cell, "Direction", GetPitchDirection(pitch.PitchType, card.Throws), .03f, .015f, .20f, .35f,
                    14, PitchBrass);
                RectTransform medallion = ContentRect(cell, "GradeBadge", .77f, .41f, .98f, .95f);
                medallion.gameObject.AddComponent<UICardPitchGradeBadge>().SetGrade(pitch.Grade);
                Text grade = Label(cell, "Grade", pitch.Grade, .77f, .41f, .98f, .95f,
                    pitch.Grade.Length > 1 ? 10 : 12, Color.white);
                Shadow gradeShadow = grade.gameObject.AddComponent<Shadow>();
                gradeShadow.effectColor = new Color(0, 0, 0, .75f);
                gradeShadow.effectDistance = new Vector2(.6f, -.6f);
                Label(cell, "Velocity", Mathf.RoundToInt((float)pitch.VelocityKph) + " km/h",
                    .22f, .015f, .75f, .35f, 10, PitchSilver).fontStyle = FontStyle.Normal;
            }
            Label(parent, "DirectionLegend", "변화 방향 · 투수 시점", .04f, .025f, .96f, .09f, 10, PitchSilver)
                .fontStyle = FontStyle.Normal;
        }

        private static int GetPitchDirectionIndex(PitchType pitch, Handedness? throws)
        {
            return GetPitchDirection(pitch, throws) switch
            {
                "↑" => 2,
                "←" => 4,
                "→" => 0,
                "↙" => 5,
                "↘" => 7,
                _ => 6
            };
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
