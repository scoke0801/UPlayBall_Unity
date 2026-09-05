using System;
using Baseball.Core.Growth;
using Baseball.Game.Career;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_Player
    {
        private void RenderPlayerCard(PlayerProfileView view)
        {
            RectTransform card = CreatePanel("PlayerCard", _content, "MY PLAYER",
                new Vector2(420f, 750f), new Vector2(-735f, -21f));
            UIPlayerCard playerCard = UIPlayerCard.CreateRuntime(
                card,
                new Vector2(360f, 540f),
                new Vector2(0f, 28f));
            playerCard.Bind(view, GetRoleLabel(view));

            CreateText("FlipGuide", card, "카드를 선택하면 앞면과 뒷면을 전환합니다.", 12,
                FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(356f, 28f),
                new Vector2(0f, -264f), SecondaryTextColor);

            Button growthButton = CreateButton("OpenGrowth", card, "성장 계획 보기",
                new Vector2(356f, 52f), new Vector2(0f, -340f),
                new Color(0.025f, 0.22f, 0.43f, 1f), out Text growthLabel);
            growthLabel.fontSize = 17;
            growthButton.onClick.AddListener(() => CareerTabNavigation.Show(CareerMainTab.Growth));
        }

        private void RenderProfilePage(PlayerProfileView view)
        {
            var page = CreateRect("ProfilePage", _content, new Vector2(1390f, 720f), new Vector2(205f, -20f));
            RenderBasicInformation(page, view);
            RenderKeyAbilities(page, view);
            RenderSeasonRecord(page, view);
            RenderBoardPreview(page, view);
            RenderOwnedSkillsPreview(page, view);
            RenderRecentForm(page, view);
        }

        private static void RenderBasicInformation(Transform parent, PlayerProfileView view)
        {
            RectTransform panel = CreatePanel("BasicInfo", parent, "기본 정보",
                new Vector2(880f, 210f), new Vector2(-245f, 252f));
            RenderInformationCell(panel, "소속", view.TeamName, -265f, 34f);
            RenderInformationCell(panel, "포지션", GetPositionCode(view.Position), -265f, -5f);
            RenderInformationCell(panel, "투타", GetHandednessLabel(view.BattingHand, view.ThrowingHand), -265f, -44f);
            RenderInformationCell(panel, "입단 연도", $"{view.JoinedYear}년", -265f, -83f);
            RenderInformationCell(panel, "프로 연차", $"{view.ProfessionalYears}년차", 155f, 34f);
            RenderInformationCell(panel, "계약 기간", $"{view.JoinedYear} ~ {view.ContractEndYear}", 155f, -5f);
            RenderInformationCell(panel, "연봉", FormatMoney(view.AnnualSalary), 155f, -44f);
            RenderInformationCell(panel, "성장 단계", GetCareerPhaseLabel(view.CareerPhase), 155f, -83f);
            CreateImage("ColumnDivider", panel, DividerColor,
                new Vector2(1f, 138f), new Vector2(0f, -29f));
        }

        private static void RenderInformationCell(
            Transform parent, string label, string value, float x, float y)
        {
            CreateText("Label_" + label, parent, label, 14, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(120f, 29f), new Vector2(x - 85f, y), SecondaryTextColor);
            CreateText("Value_" + label, parent, value, 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(265f, 29f), new Vector2(x + 110f, y), PrimaryTextColor);
            CreateImage("Line_" + label, parent, new Color(0.12f, 0.24f, 0.34f, 0.72f),
                new Vector2(395f, 1f), new Vector2(x + 55f, y - 17f));
        }

        private static void RenderKeyAbilities(Transform parent, PlayerProfileView view)
        {
            RectTransform panel = CreatePanel("KeyAbilities", parent,
                view.PlayerType == Baseball.Core.Players.PlayerType.Pitcher ? "주요 능력치 · 투수" : "주요 능력치 · 타자",
                new Vector2(880f, 258f), new Vector2(-245f, 10f));
            for (int index = 0; index < view.Abilities.Length; index++)
            {
                PlayerProfileAbilityView ability = view.Abilities[index];
                int column = index % 2;
                int row = index / 2;
                float x = column == 0 ? -220f : 220f;
                float y = 67f - row * 64f;
                CreateText("AbilityLabel_" + ability.Ability, panel, GetAbilityLabel(ability.Ability),
                    14, FontStyle.Bold, TextAnchor.MiddleLeft,
                    new Vector2(86f, 28f), new Vector2(x - 145f, y), SecondaryTextColor);
                CreateProgressBar(panel, ability.StableValue / 100f,
                    new Vector2(244f, 14f), new Vector2(x + 22f, y), GetRatingColor(ability.StableValue));
                string value = ability.BoardBonus > 0
                    ? $"{ability.StableValue}  +{ability.BoardBonus}"
                    : ability.StableValue.ToString();
                CreateText("AbilityValue_" + ability.Ability, panel, value, 15, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(82f, 28f), new Vector2(x + 173f, y),
                    ability.BoardBonus > 0 ? RoleColor : PrimaryTextColor);
            }
        }

        private static void RenderSeasonRecord(Transform parent, PlayerProfileView view)
        {
            RectTransform panel = CreatePanel("SeasonRecord", parent, $"{view.SeasonYear} 시즌 성적",
                new Vector2(880f, 210f), new Vector2(-245f, -252f));
            PlayerProfileStatisticsView stats = view.SeasonStatistics;
            if (view.PlayerType == Baseball.Core.Players.PlayerType.Pitcher)
            {
                RenderStatCells(panel,
                    new[] { "경기", "선발", "승-패", "세이브", "평균자책", "이닝당출루", "탈삼진" },
                    new[]
                    {
                        stats.PitchingAppearances.ToString(), stats.PitchingStarts.ToString(),
                        $"{stats.Wins}-{stats.Losses}", stats.Saves.ToString(),
                        stats.EarnedRunAverage.ToString("0.00"), stats.WalksHitsPerInningPitched.ToString("0.00"),
                        stats.PitchingStrikeouts.ToString()
                    });
            }
            else
            {
                RenderStatCells(panel,
                    new[] { "경기", "타수", "안타", "홈런", "타점", "타율", "출루+장타" },
                    new[]
                    {
                        stats.GamesPlayed.ToString(), stats.AtBats.ToString(), stats.Hits.ToString(),
                        stats.HomeRuns.ToString(), stats.RunsBattedIn.ToString(),
                        stats.BattingAverage.ToString(".000"), stats.OnBasePlusSlugging.ToString(".000")
                    });
            }
            CreateText("Note", panel, "정규 시즌 기준 · 상세 순위와 연도별 기록은 기록 메뉴에서 확인",
                11, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(820f, 24f),
                new Vector2(0f, -77f), MutedColor);
        }

        private static void RenderStatCells(Transform parent, string[] labels, string[] values)
        {
            const float cellWidth = 116f;
            for (int index = 0; index < labels.Length; index++)
            {
                float x = -348f + index * cellWidth;
                RectTransform cell = CreateImage("Stat_" + labels[index], parent, PanelDarkColor,
                    new Vector2(cellWidth - 4f, 94f), new Vector2(x, 3f));
                CreateText("Label", cell, labels[index], 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(cellWidth - 12f, 26f), new Vector2(0f, 24f), SecondaryTextColor);
                CreateText("Value", cell, values[index], 21, FontStyle.Bold, TextAnchor.MiddleCenter,
                    new Vector2(cellWidth - 12f, 38f), new Vector2(0f, -12f), PrimaryTextColor);
            }
        }

        private void RenderBoardPreview(Transform parent, PlayerProfileView view)
        {
            RectTransform panel = CreatePanel("BoardPreview", parent, "성장판 · 현재 적용",
                new Vector2(480f, 322f), new Vector2(450f, 196f));
            const float cellSize = 48f;
            const float gap = 4f;
            const float boardSpan = cellSize * 4f + gap * 3f;
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    GrowthBoardCellView cell = FindBoardCell(view.BoardCells, x, y, out bool found);
                    RectTransform slot = CreateImage($"Cell_{x}_{y}", panel, DividerColor,
                        new Vector2(cellSize, cellSize),
                        new Vector2(
                            -boardSpan * 0.5f + cellSize * 0.5f + x * (cellSize + gap),
                            boardSpan * 0.5f - cellSize * 0.5f - y * (cellSize + gap)));
                    CreateImage(
                        "Fill",
                        slot,
                        found && cell.IsOccupied ? Color.clear : PanelDarkColor,
                        new Vector2(cellSize - 4f, cellSize - 4f),
                        Vector2.zero);
                }
            }
            RenderAppliedBoardBlocks(panel, view, Vector2.zero, boardSpan, cellSize, gap, "Preview");
            Button button = CreateButton("OpenBoard", panel, "성장판 관리",
                new Vector2(320f, 42f), new Vector2(0f, -132f),
                new Color(0.025f, 0.22f, 0.43f, 1f), out Text label);
            label.fontSize = 15;
            button.onClick.AddListener(() => CareerTabNavigation.Show(CareerMainTab.Growth));
        }

        private static void RenderOwnedSkillsPreview(Transform parent, PlayerProfileView view)
        {
            RectTransform panel = CreatePanel("OwnedSkills", parent, $"보유 스킬 블록 · {view.OwnedBlocks.Length}",
                new Vector2(480f, 222f), new Vector2(450f, -88f));
            int count = Math.Min(3, view.OwnedBlocks.Length);
            if (count == 0)
            {
                CreateText("Empty", panel, "보유한 스킬 블록이 없습니다.\n성장 메뉴의 상점과 활동 보상에서 획득할 수 있습니다.",
                    14, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(420f, 100f),
                    new Vector2(0f, -10f), SecondaryTextColor);
                return;
            }
            for (int index = 0; index < count; index++)
            {
                GrowthSkillBlockView block = view.OwnedBlocks[index];
                float y = 42f - index * 48f;
                RectTransform row = CreateImage("Skill_" + block.InstanceId, panel, PanelDarkColor,
                    new Vector2(430f, 42f), new Vector2(0f, y));
                CreateImage("Category", row, GetSkillCategoryColor(block.Category),
                    new Vector2(5f, 34f), new Vector2(-208f, 0f));
                RectTransform shapeVisual = SkillBlockVisual.Create(
                    row,
                    block.ShapeCells,
                    0,
                    GetSkillCategoryColor(block.Category),
                    new Vector2(-167f, 0f),
                    new Vector2(70f, 34f),
                    17f,
                    "Shape");
                const float skillNameRight = 88f;
                float skillNameLeft = shapeVisual != null
                    ? shapeVisual.anchoredPosition.x + shapeVisual.rect.width * 0.5f + 12f
                    : -130f;
                float skillNameWidth = skillNameRight - skillNameLeft;
                CreateText("Name", row, GetSkillCategoryLabel(block.Category) + " 블록", 14, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(skillNameWidth, 30f),
                    new Vector2(skillNameLeft + skillNameWidth * 0.5f, 0f), PrimaryTextColor);
                CreateText("Rarity", row, GetRarityLabel(block.Rarity), 12, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(110f, 30f), new Vector2(150f, 0f),
                    GetRarityColor(block.Rarity));
            }
        }

        private static void RenderRecentForm(Transform parent, PlayerProfileView view)
        {
            RectTransform panel = CreatePanel("RecentForm", parent, "최근 경기 및 메모",
                new Vector2(480f, 150f), new Vector2(450f, -284f));
            string result = BuildRecentFormText(view);
            CreateText("Summary", panel, result, 15, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(425f, 34f), new Vector2(0f, 12f), PrimaryTextColor);
            string note = BuildPlayerNote(view);
            CreateText("Note", panel, note, 12, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(425f, 40f), new Vector2(0f, -32f), SecondaryTextColor);
        }

        private void RenderAttributesPage(PlayerProfileView view)
        {
            RectTransform page = CreatePanel("AttributesPage", _content, "능력치 · 현재 적용값과 성장 여지",
                new Vector2(1390f, 720f), new Vector2(205f, -20f));
            CreateText("Guide", page,
                "막대는 경기 시뮬레이션에 들어가는 현재 적용값입니다. Potential은 정확한 수치 대신 성장 여지로만 표시합니다.",
                13, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(1320f, 34f),
                new Vector2(0f, 278f), SecondaryTextColor);
            for (int index = 0; index < view.Abilities.Length; index++)
            {
                PlayerProfileAbilityView ability = view.Abilities[index];
                int column = index % 2;
                int row = index / 2;
                float x = column == 0 ? -330f : 330f;
                float y = 185f - row * 145f;
                RectTransform card = CreateImage("Ability_" + ability.Ability, page, PanelDarkColor,
                    new Vector2(610f, 124f), new Vector2(x, y));
                CreateText("Name", card, GetAbilityLabel(ability.Ability), 20, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(170f, 34f), new Vector2(-205f, 30f), PrimaryTextColor);
                CreateText("Value", card, ability.StableValue.ToString(), 27, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(80f, 40f), new Vector2(250f, 30f),
                    GetRatingColor(ability.StableValue));
                CreateProgressBar(card, ability.StableValue / 100f, new Vector2(540f, 16f),
                    new Vector2(0f, -7f), GetRatingColor(ability.StableValue));
                string detail = ability.BoardBonus > 0
                    ? $"기초 {ability.BaseValue}  ·  성장판 +{ability.BoardBonus}  ·  성장 여지 {GetGrowthRoomLabel(ability.GrowthRoom)}"
                    : $"기초 {ability.BaseValue}  ·  성장판 보너스 없음  ·  성장 여지 {GetGrowthRoomLabel(ability.GrowthRoom)}";
                CreateText("Detail", card, detail, 12, FontStyle.Normal, TextAnchor.MiddleLeft,
                    new Vector2(540f, 28f), new Vector2(0f, -40f), SecondaryTextColor);
            }
        }

        private void RenderBoardPage(PlayerProfileView view)
        {
            RectTransform page = CreateRect("BoardPage", _content, new Vector2(1390f, 720f), new Vector2(205f, -20f));
            RectTransform board = CreatePanel("Board", page, "4×4 성장판 · 읽기 전용",
                new Vector2(820f, 720f), new Vector2(-285f, 0f));
            const float cellSize = 112f;
            const float gap = 14f;
            const float boardSpan = cellSize * 4f + gap * 3f;
            Vector2 boardCenter = new Vector2(0f, 5f);
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    GrowthBoardCellView cell = FindBoardCell(view.BoardCells, x, y, out bool found);
                    RectTransform slot = CreateImage($"Cell_{x}_{y}", board, DividerColor,
                        new Vector2(cellSize, cellSize),
                        new Vector2(
                            boardCenter.x - boardSpan * 0.5f + cellSize * 0.5f + x * (cellSize + gap),
                            boardCenter.y + boardSpan * 0.5f - cellSize * 0.5f - y * (cellSize + gap)));
                    CreateImage(
                        "Fill",
                        slot,
                        found && cell.IsOccupied ? Color.clear : PanelDarkColor,
                        new Vector2(cellSize - 8f, cellSize - 8f),
                        Vector2.zero);
                }
            }
            RenderAppliedBoardBlocks(board, view, boardCenter, boardSpan, cellSize, gap, "Applied");
            CreateText("Guide", board, "정규 시즌에는 현재 배치를 열람합니다. 배치 변경은 오프시즌 성장 메뉴에서만 가능합니다.",
                13, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(740f, 42f),
                new Vector2(0f, -310f), SecondaryTextColor);

            RectTransform insight = CreatePanel("BoardEffects", page, "현재 성장판 효과",
                new Vector2(540f, 520f), new Vector2(430f, 100f));
            int effectCount = 0;
            for (int index = 0; index < view.Abilities.Length; index++)
            {
                PlayerProfileAbilityView ability = view.Abilities[index];
                if (ability.BoardBonus <= 0)
                    continue;
                float y = 170f - effectCount * 55f;
                CreateText("Effect_" + ability.Ability, insight,
                    $"{GetAbilityLabel(ability.Ability)}  +{ability.BoardBonus}", 17, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(430f, 38f), new Vector2(0f, y), RoleColor);
                effectCount++;
            }
            if (effectCount == 0)
            {
                CreateText("Empty", insight, "현재 적용 중인 능력치 보너스가 없습니다.", 15,
                    FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(450f, 80f),
                    new Vector2(0f, 60f), SecondaryTextColor);
            }
            Button button = CreateButton("OpenGrowth", page, "성장 메뉴에서 관리",
                new Vector2(540f, 70f), new Vector2(430f, -260f),
                new Color(0.025f, 0.22f, 0.43f, 1f), out Text label);
            label.fontSize = 19;
            button.onClick.AddListener(() => CareerTabNavigation.Show(CareerMainTab.Growth));
        }

        private void RenderSkillsPage(PlayerProfileView view)
        {
            RectTransform page = CreatePanel("SkillsPage", _content, $"보유 스킬 블록 · {view.OwnedBlocks.Length}",
                new Vector2(1390f, 720f), new Vector2(205f, -20f));
            if (view.OwnedBlocks.Length == 0)
            {
                CreateText("Empty", page,
                    "아직 보유한 스킬 블록이 없습니다.\n오프시즌 성장 활동과 스킬 상점에서 획득한 블록이 이곳에 표시됩니다.",
                    17, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(1000f, 100f),
                    new Vector2(0f, 40f), SecondaryTextColor);
                return;
            }

            int visibleCount = Math.Min(8, view.OwnedBlocks.Length);
            for (int index = 0; index < visibleCount; index++)
            {
                GrowthSkillBlockView block = view.OwnedBlocks[index];
                int column = index % 2;
                int row = index / 2;
                float x = column == 0 ? -330f : 330f;
                float y = 220f - row * 132f;
                RectTransform card = CreateImage("Skill_" + block.InstanceId, page, PanelDarkColor,
                    new Vector2(610f, 112f), new Vector2(x, y));
                CreateImage("Category", card, GetSkillCategoryColor(block.Category),
                    new Vector2(8f, 96f), new Vector2(-295f, 0f));
                SkillBlockVisual.Create(
                    card,
                    block.ShapeCells,
                    0,
                    GetSkillCategoryColor(block.Category),
                    new Vector2(-242f, 0f),
                    new Vector2(94f, 82f),
                    23f,
                    "Shape");
                CreateText("Name", card, GetSkillCategoryLabel(block.Category) + " 블록", 18, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(300f, 34f), new Vector2(-50f, 25f), PrimaryTextColor);
                CreateText("Rarity", card, GetRarityLabel(block.Rarity), 14, FontStyle.Bold,
                    TextAnchor.MiddleRight, new Vector2(140f, 30f), new Vector2(215f, 25f),
                    GetRarityColor(block.Rarity));
                CreateText("Effect", card, FormatAbilityBonuses(block.AbilityBonuses), 13, FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Vector2(450f, 40f), new Vector2(10f, -25f), SecondaryTextColor);
            }
        }

        private static void RenderAppliedBoardBlocks(
            Transform parent,
            PlayerProfileView view,
            Vector2 boardCenter,
            float boardSpan,
            float cellSize,
            float gap,
            string namePrefix)
        {
            if (view.AppliedLayout == null || view.PlacedBlocks == null)
                return;

            float cellPitch = cellSize + gap;
            for (int index = 0; index < view.AppliedLayout.Length; index++)
            {
                GrowthBoardLayoutPlacement placement = view.AppliedLayout[index];
                GrowthSkillBlockView block = FindPlacedBlock(view.PlacedBlocks, placement.InstanceId);
                if (block.InstanceId == 0)
                    continue;

                GetRotatedShapeSize(
                    block.ShapeCells,
                    placement.RotationQuarterTurns,
                    out int widthInCells,
                    out int heightInCells);
                float centerX = boardCenter.x - boardSpan * 0.5f + cellSize * 0.5f +
                                (placement.OriginX + (widthInCells - 1) * 0.5f) * cellPitch;
                float centerY = boardCenter.y + boardSpan * 0.5f - cellSize * 0.5f -
                                (placement.OriginY + (heightInCells - 1) * 0.5f) * cellPitch;
                RectTransform visual = SkillBlockVisual.Create(
                    parent,
                    block.ShapeCells,
                    placement.RotationQuarterTurns,
                    GetSkillCategoryColor(block.Category),
                    new Vector2(centerX, centerY),
                    new Vector2(widthInCells * cellPitch, heightInCells * cellPitch),
                    cellPitch,
                    namePrefix + "Block_" + block.InstanceId);
                Image image = visual != null ? visual.GetComponent<Image>() : null;
                if (image == null)
                    continue;
                Outline outline = image.gameObject.AddComponent<Outline>();
                outline.effectColor = GetRarityColor(block.Rarity);
                outline.effectDistance = new Vector2(2f, -2f);
                outline.useGraphicAlpha = true;
            }
        }

        private static GrowthSkillBlockView FindPlacedBlock(
            GrowthSkillBlockView[] blocks,
            int instanceId)
        {
            for (int index = 0; index < blocks.Length; index++)
            {
                if (blocks[index].InstanceId == instanceId)
                    return blocks[index];
            }
            return default;
        }

        private static void GetRotatedShapeSize(
            BoardCell[] cells,
            int rotationQuarterTurns,
            out int width,
            out int height)
        {
            int rotation = ((rotationQuarterTurns % 4) + 4) % 4;
            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            int maximumX = int.MinValue;
            int maximumY = int.MinValue;
            for (int index = 0; index < cells.Length; index++)
            {
                int x;
                int y;
                switch (rotation)
                {
                    case 1:
                        x = cells[index].Y;
                        y = -cells[index].X;
                        break;
                    case 2:
                        x = -cells[index].X;
                        y = -cells[index].Y;
                        break;
                    case 3:
                        x = -cells[index].Y;
                        y = cells[index].X;
                        break;
                    default:
                        x = cells[index].X;
                        y = cells[index].Y;
                        break;
                }
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
            }
            width = maximumX - minimumX + 1;
            height = maximumY - minimumY + 1;
        }

        private void RenderCareerPage(PlayerProfileView view)
        {
            RectTransform page = CreateRect("CareerPage", _content, new Vector2(1390f, 720f), new Vector2(205f, -20f));
            RectTransform summary = CreatePanel("CareerSummary", page, "커리어 요약",
                new Vector2(650f, 300f), new Vector2(-360f, 210f));
            RenderCareerSummary(summary, view);
            RectTransform totals = CreatePanel("CareerTotals", page, "통산 주요 기록",
                new Vector2(710f, 300f), new Vector2(340f, 210f));
            RenderCareerTotals(totals, view.CareerTotals);
            RectTransform recent = CreatePanel("CareerRecentGames", page, "최근 5경기",
                new Vector2(980f, 390f), new Vector2(-195f, -160f));
            RenderRecentGames(recent, view);
            Button button = CreateButton("OpenRecords", page, "기록 메뉴에서 전체 경력 보기",
                new Vector2(360f, 390f), new Vector2(490f, -160f),
                new Color(0.025f, 0.22f, 0.43f, 1f), out Text label);
            label.fontSize = 18;
            button.onClick.AddListener(() => CareerTabNavigation.Show(CareerMainTab.Records));
        }

        private static void RenderCareerSummary(Transform parent, PlayerProfileView view)
        {
            string[] labels = { "프로 경력", "현재 리그", "현재 구단", "계약", "성장 단계", "부상 이력" };
            string[] values =
            {
                $"{view.ProfessionalYears}년차", GetLeagueLabel(view.LeagueLevel), view.TeamName,
                $"{view.ContractEndYear}년까지", GetCareerPhaseLabel(view.CareerPhase),
                view.InjuryHistoryCount == 0 ? "기록 없음" : $"{view.InjuryHistoryCount}회"
            };
            for (int index = 0; index < labels.Length; index++)
            {
                int column = index % 2;
                int row = index / 2;
                float x = column == 0 ? -155f : 155f;
                float y = 72f - row * 70f;
                CreateText("Label_" + labels[index], parent, labels[index], 12, FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Vector2(125f, 26f), new Vector2(x - 80f, y + 14f),
                    SecondaryTextColor);
                CreateText("Value_" + labels[index], parent, values[index], 17, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(255f, 32f), new Vector2(x - 15f, y - 14f),
                    PrimaryTextColor);
            }
        }

        private static void RenderCareerTotals(Transform parent, CareerRecordMetricValue[] totals)
        {
            int visibleCount = Math.Min(7, totals.Length);
            for (int index = 0; index < visibleCount; index++)
            {
                CareerRecordMetricValue metric = totals[index];
                float x = -285f + index * 95f;
                RectTransform cell = CreateImage("Metric_" + metric.Metric, parent, PanelDarkColor,
                    new Vector2(91f, 160f), new Vector2(x, 5f));
                CreateText("Label", cell, GetMetricLabel(metric.Metric), 11, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(82f, 45f), new Vector2(0f, 44f), SecondaryTextColor);
                CreateText("Value", cell, FormatMetricValue(metric), 19, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(84f, 45f), new Vector2(0f, -6f), PrimaryTextColor);
            }
        }

        private static void RenderRecentGames(Transform parent, PlayerProfileView view)
        {
            if (view.RecentGames.Length == 0)
            {
                CreateText("Empty", parent, "아직 진행한 경기가 없습니다.", 16, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(820f, 80f), Vector2.zero, SecondaryTextColor);
                return;
            }
            int count = Math.Min(5, view.RecentGames.Length);
            for (int index = 0; index < count; index++)
            {
                PlayerGameLogState game = view.RecentGames[index];
                float y = 118f - index * 58f;
                RectTransform row = CreateImage("Game_" + game.GameId, parent, PanelDarkColor,
                    new Vector2(920f, 50f), new Vector2(0f, y));
                CreateText("Result", row, game.DidWin ? "승" : "패", 16, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(50f, 36f), new Vector2(-420f, 0f),
                    game.DidWin ? RoleColor : WarningColor);
                CreateText("Score", row, $"{game.TeamRuns} : {game.OpponentRuns}", 15, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(100f, 36f), new Vector2(-340f, 0f), PrimaryTextColor);
                CreateText("Line", row, FormatGameLine(game, view.PlayerType), 14, FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Vector2(690f, 36f), new Vector2(65f, 0f), SecondaryTextColor);
            }
        }

        private static GrowthBoardCellView FindBoardCell(
            GrowthBoardCellView[] cells, int x, int y, out bool found)
        {
            for (int index = 0; index < cells.Length; index++)
            {
                if (cells[index].X == x && cells[index].Y == y)
                {
                    found = true;
                    return cells[index];
                }
            }
            found = false;
            return default;
        }
    }
}
