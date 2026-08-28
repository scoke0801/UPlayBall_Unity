using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Scene_CareerGrowth
    {
        private const float ProgramCardStripWidth = 610f;
        private const float ProgramCardGap = 10f;

        private Image[] _placementPreviewImages = Array.Empty<Image>();

        private void RenderBackgroundAccents()
        {
            CreateImage("TopGlow", _content, new Color(0.02f, 0.18f, 0.31f, 0.24f),
                new Vector2(1920f, 5f), new Vector2(0f, 456f));
            CreateImage("BottomGlow", _content, new Color(0.02f, 0.16f, 0.28f, 0.2f),
                new Vector2(1920f, 4f), new Vector2(0f, -443f));
        }

        private void RenderTopBar(CareerDashboardView dashboard, CareerGrowthView growth)
        {
            RectTransform bar = CreateImage(
                "TopBar", _content, TopBarColor, new Vector2(1920f, 80f), new Vector2(0f, 500f));
            CreateImage("TopBarBottom", bar, BorderColor, new Vector2(1920f, 2f), new Vector2(0f, -39f));

            Text logo = CreateText(
                "Logo", bar, "UPlayBall", 34, FontStyle.BoldAndItalic, TextAnchor.MiddleLeft,
                new Vector2(310f, 50f), new Vector2(-800f, 5f), PrimaryTextColor);
            AddTextOutline(logo, new Color(0.05f, 0.34f, 0.62f, 0.9f), 1.5f);
            CreateText(
                "LogoCaption", bar, "ULTIMATE BASEBALL", 9, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(230f, 18f), new Vector2(-796f, -23f), AccentColor);

            string seasonLabel = growth.IsOffseason ? "OFF-SEASON" : GetSeasonPhaseLabel(dashboard.SeasonPhase);
            CreateTopBarSegment(
                bar, "SEASON", $"{dashboard.SeasonYear}  {seasonLabel}",
                new Vector2(-365f, 0f), new Vector2(420f, 64f));
            string period = growth.IsOffseason
                ? $"남은 기간  {growth.RemainingWeeks}주"
                : "정규 시즌 · 열람 모드";
            CreateTopBarSegment(bar, "PERIOD", period, new Vector2(25f, 0f), new Vector2(330f, 64f));
            CreateTopBarSegment(
                bar, "MONEY", FormatMoney(dashboard.AvailableMoney),
                new Vector2(400f, 0f), new Vector2(390f, 64f));
            CreateText(
                "Mail", bar, "MAIL", 12, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(80f, 44f), new Vector2(760f, 0f), SecondaryTextColor);
            CreateText(
                "Settings", bar, "설정", 14, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(80f, 44f), new Vector2(855f, 0f), SecondaryTextColor);
        }

        private void RenderPlayerPanel(CareerDashboardView dashboard, CareerGrowthView growth)
        {
            RectTransform panel = CreatePanel(
                "PlayerPanel", "PLAYER INFO", "선수 정보",
                new Vector2(500f, 660f), new Vector2(-705f, 117f));

            RectTransform card = CreateSection(
                "PlayerCard", panel, new Vector2(466f, 205f), new Vector2(0f, 172f),
                new Color(0.025f, 0.15f, 0.25f, 1f));
            CreateImage("CardGlow", card, new Color(0.05f, 0.31f, 0.52f, 0.44f),
                new Vector2(190f, 191f), new Vector2(-130f, 0f));
            RectTransform portrait = CreateImage(
                "PlayerPortrait", card, Color.white,
                new Vector2(190f, 191f), new Vector2(-130f, 0f));
            Image portraitImage = portrait.GetComponent<Image>();
            portraitImage.sprite = PlayerPortraitSprites.GetDefault(dashboard.Position);
            portraitImage.preserveAspect = true;
            if (portraitImage.sprite == null)
            {
                portraitImage.color = Color.clear;
                CreateText(
                    "PlayerPortraitFallback", card, GetInitial(dashboard.PlayerName), 90, FontStyle.Bold,
                    TextAnchor.MiddleCenter, new Vector2(180f, 140f), new Vector2(-130f, 18f),
                    new Color(0.75f, 0.9f, 1f, 0.86f));
            }
            CreateText(
                "Number", card, "21", 31, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(68f, 44f), new Vector2(-190f, 72f), PrimaryTextColor);
            CreateText(
                "Name", card, dashboard.PlayerName, 28, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(225f, 44f), new Vector2(78f, 65f), PrimaryTextColor);
            CreateText(
                "Position", card, GetPositionCode(dashboard.Position), 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(62f, 31f), new Vector2(-12f, 25f),
                BrightAccentColor);
            CreateText(
                "Team", card, dashboard.TeamName, 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(180f, 30f), new Vector2(120f, 25f), SecondaryTextColor);
            CreateText(
                "Profile", card, $"{dashboard.Age}세  ·  {GetHandsLabel(dashboard)}",
                15, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(220f, 28f), new Vector2(80f, -13f), SecondaryTextColor);
            RectTransform overall = CreateImage(
                "Overall", card, new Color(0.08f, 0.09f, 0.08f, 0.92f),
                new Vector2(82f, 82f), new Vector2(178f, -53f));
            CreateText(
                "Label", overall, "OVR", 11, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(65f, 20f), new Vector2(0f, 20f), GoldColor);
            CreateText(
                "Value", overall, dashboard.Overall.ToString(), 35, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(70f, 44f), new Vector2(0f, -9f), GoldColor);

            PlayerAbility[] abilities = GetVisibleAbilities(growth.PlayerType);
            for (int index = 0; index < abilities.Length; index++)
            {
                PlayerAbility ability = abilities[index];
                RenderAttributeRow(
                    panel,
                    ability,
                    growth.StableAbilities[(int)ability],
                    growth.BoardBonuses[(int)ability],
                    new Vector2(0f, 25f - index * 31f));
            }

            RectTransform condition = CreateSection(
                "Condition", panel, new Vector2(224f, 64f), new Vector2(-117f, -237f), PanelDarkColor);
            CreateStatusValue(condition, "컨디션", dashboard.Condition);
            RectTransform evaluation = CreateSection(
                "Evaluation", panel, new Vector2(224f, 64f), new Vector2(117f, -237f), PanelDarkColor);
            CreateStatusValue(evaluation, "감독 평가", dashboard.ManagerEvaluation);
            CreateText(
                "CurrentRole", panel, "현재 역할", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(95f, 25f), new Vector2(-176f, -292f), SecondaryTextColor);
            CreateText(
                "RoleValue", panel, GetCurrentRoleLabel(dashboard), 15, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(285f, 25f), new Vector2(32f, -292f),
                dashboard.NextGame.HasValue ? GreenColor : MutedColor);
        }

        private void RenderGrowthLog(CareerGrowthView growth)
        {
            RectTransform panel = CreatePanel(
                "GrowthLog", "HISTORY", "성장 로그",
                new Vector2(500f, 190f), new Vector2(-705f, -328f));
            if (growth.RecentGrowth.Length == 0)
            {
                CreateText(
                    "Empty", panel, "아직 기록된 성장 결과가 없습니다.", 15, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(430f, 80f), new Vector2(0f, -8f),
                    SecondaryTextColor);
                return;
            }

            for (int index = 0; index < growth.RecentGrowth.Length; index++)
            {
                GrowthResultRecord record = growth.RecentGrowth[index];
                float y = 22f - index * 31f;
                CreateText(
                    "Date_" + index, panel, record.SeasonYear.ToString(), 12, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(62f, 24f), new Vector2(-202f, y), MutedColor);
                CreateText(
                    "Source_" + index, panel, GetGrowthSourceLabel(record), 13, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(120f, 24f), new Vector2(-110f, y),
                    SecondaryTextColor);
                CreateText(
                    "Change_" + index, panel, FormatGrowthChanges(record), 13, FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Vector2(250f, 24f), new Vector2(83f, y),
                    HasPositiveChange(record) ? GreenColor : WarningColor);
            }
        }

        private void RenderSkillBoard(CareerGrowthView growth)
        {
            string editGuide = !growth.CanEditBoard
                ? "정규 시즌 중에는 열람만 가능합니다."
                : _selectedOwnedBlockId > 0
                    ? "보드에 올려 미리보기 · 초록 가능 / 빨강 불가"
                    : "보유 블록을 선택한 뒤 빈 칸을 누르세요.";
            RectTransform panel = CreatePanel(
                "SkillBoard", "GROWTH BOARD", "4×4 성장 보드",
                new Vector2(700f, 735f), new Vector2(-95f, 80f));
            CreateText(
                "Guide", panel, editGuide, 13, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(500f, 20f), new Vector2(0f, 302f),
                growth.CanEditBoard ? SecondaryTextColor : WarningColor);
            Button redesign = CreateButton(
                "Redesign", panel,
                growth.IsBoardRedesignUsed
                    ? "안전 회수 사용 완료"
                    : _confirmBoardRedesign
                    ? $"안전 회수 확정"
                    : $"전체 안전 회수  {FormatMoney(growth.BoardRedesignCost)}",
                new Vector2(200f, 34f), new Vector2(235f, 338f),
                _confirmBoardRedesign
                    ? new Color(0.54f, 0.12f, 0.12f, 1f)
                    : new Color(0.03f, 0.24f, 0.42f, 1f),
                out Text redesignLabel);
            redesignLabel.fontSize = 11;
            redesign.interactable = growth.CanRedesignBoard;
            redesign.onClick.AddListener(RedesignBoard);
            Button rotateSelection = CreateButton(
                "RotateSelection", panel, $"선택 회전  {_selectedRotation * 90}°",
                new Vector2(135f, 34f), new Vector2(62f, 338f),
                new Color(0.03f, 0.24f, 0.42f, 1f), out Text rotateSelectionLabel);
            rotateSelectionLabel.fontSize = 11;
            rotateSelection.interactable = growth.CanEditBoard &&
                                           _selectedOwnedBlockId > 0 &&
                                           FindOwnedBlock(growth, _selectedOwnedBlockId).CanRotate;
            rotateSelection.onClick.AddListener(RotateSelectedBlock);

            RectTransform board = CreateSection(
                "Board", panel, new Vector2(430f, 430f), new Vector2(0f, 75f),
                new Color(0.007f, 0.025f, 0.04f, 1f));
            const float cellSize = 96f;
            const float gap = 6f;
            float boardSpan = growth.BoardWidth * cellSize + (growth.BoardWidth - 1) * gap;
            _placementPreviewImages = new Image[growth.BoardWidth * growth.BoardHeight];
            for (int index = 0; index < growth.BoardCells.Length; index++)
            {
                GrowthBoardCellView cell = growth.BoardCells[index];
                float x = -boardSpan * 0.5f + cellSize * 0.5f + cell.X * (cellSize + gap);
                float y = boardSpan * 0.5f - cellSize * 0.5f - cell.Y * (cellSize + gap);
                bool hasPlacementSelection = growth.CanEditBoard && _selectedOwnedBlockId > 0;
                bool canPlaceAtOrigin = !cell.IsOccupied && hasPlacementSelection &&
                                        _manager.GetSkillBlockPlacementPreview(
                                            _selectedOwnedBlockId,
                                            cell.X,
                                            cell.Y,
                                            _selectedRotation).CanPlace;
                Color cellColor = cell.IsOccupied
                    ? GetCategoryColor(cell.Category)
                    : canPlaceAtOrigin
                        ? new Color(0.08f, 0.30f, 0.20f, 1f)
                        : new Color(0.08f, 0.11f, 0.13f, 1f);
                if (cell.InstanceId == _selectedPlacedBlockId)
                    cellColor = Color.Lerp(cellColor, Color.white, 0.25f);
                Button button = CreateButton(
                    "Cell_" + cell.X + "_" + cell.Y,
                    board,
                    string.Empty,
                    new Vector2(cellSize, cellSize),
                    new Vector2(x, y),
                    cellColor,
                    out _);
                button.interactable = cell.IsOccupied || canPlaceAtOrigin;
                if (cell.IsOccupied)
                {
                    int instanceId = cell.InstanceId;
                    button.onClick.AddListener(() => SelectPlacedBlock(instanceId));
                    CreateText(
                        "BlockMark", button.transform, GetCategoryShortLabel(cell.Category), 12,
                        FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(80f, 24f), Vector2.zero,
                        PrimaryTextColor);
                }
                else if (canPlaceAtOrigin)
                {
                    int xValue = cell.X;
                    int yValue = cell.Y;
                    button.onClick.AddListener(() => PlaceSelectedBlock(xValue, yValue));
                    CreateText(
                        "PlaceMark", button.transform, "✓", 24, FontStyle.Bold,
                        TextAnchor.MiddleCenter, new Vector2(60f, 60f), Vector2.zero,
                        new Color(0.35f, 0.55f, 0.7f, 0.8f));
                }

                RectTransform preview = CreateImage(
                    "PlacementPreview",
                    button.transform,
                    Color.clear,
                    Vector2.zero,
                    Vector2.zero,
                    stretch: true);
                Image previewImage = preview.GetComponent<Image>();
                previewImage.enabled = false;
                _placementPreviewImages[cell.Y * growth.BoardWidth + cell.X] = previewImage;

                if (hasPlacementSelection)
                {
                    int hoverX = cell.X;
                    int hoverY = cell.Y;
                    AddPointerListener(
                        button.gameObject,
                        EventTriggerType.PointerEnter,
                        () => ShowPlacementPreview(growth, hoverX, hoverY));
                    AddPointerListener(
                        button.gameObject,
                        EventTriggerType.PointerExit,
                        ClearPlacementPreview);
                }
            }

            RectTransform bonusBand = CreateSection(
                "AppliedBonus", panel, new Vector2(650f, 58f), new Vector2(0f, -174f), PanelDarkColor);
            CreateText(
                "Title", bonusBand, "적용 중 보너스", 13, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(120f, 28f), new Vector2(-245f, 0f), SecondaryTextColor);
            CreateText(
                "Values", bonusBand, FormatBoardBonuses(growth), 16, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(455f, 36f), new Vector2(65f, 0f),
                GetDominantBonusColor(growth));

            CreateText(
                "InventoryTitle", panel, $"보유 블록  {growth.OwnedBlocks.Length} · 최신순", 13,
                FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(230f, 24f),
                new Vector2(-208f, -220f),
                SecondaryTextColor);
            if (growth.OwnedBlocks.Length == 0)
            {
                CreateText(
                    "InventoryEmpty", panel, "상점에서 블록을 구매하면 이곳에 보관됩니다.", 13,
                    FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(520f, 56f),
                    new Vector2(0f, -267f), MutedColor);
            }
            else
            {
                int pageCount = (growth.OwnedBlocks.Length + InventoryPageSize - 1) / InventoryPageSize;
                Button newer = CreateButton(
                    "InventoryNewer", panel, "‹", new Vector2(34f, 28f), new Vector2(218f, -220f),
                    new Color(0.03f, 0.24f, 0.42f, 1f), out _);
                newer.interactable = _inventoryPage > 0;
                newer.onClick.AddListener(ShowNewerInventoryPage);
                CreateText(
                    "InventoryPage", panel, $"{_inventoryPage + 1} / {pageCount}", 12,
                    FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(62f, 28f),
                    new Vector2(268f, -220f), SecondaryTextColor);
                Button older = CreateButton(
                    "InventoryOlder", panel, "›", new Vector2(34f, 28f), new Vector2(318f, -220f),
                    new Color(0.03f, 0.24f, 0.42f, 1f), out _);
                older.interactable = _inventoryPage < pageCount - 1;
                older.onClick.AddListener(ShowOlderInventoryPage);

                int pageStart = _inventoryPage * InventoryPageSize;
                int visibleCount = Math.Min(
                    InventoryPageSize,
                    growth.OwnedBlocks.Length - pageStart);
                for (int index = 0; index < visibleCount; index++)
                {
                    int sourceIndex = growth.OwnedBlocks.Length - 1 - pageStart - index;
                    RenderInventoryBlock(panel, growth.OwnedBlocks[sourceIndex], index);
                }
            }
        }

        private void ShowPlacementPreview(CareerGrowthView growth, int originX, int originY)
        {
            ClearPlacementPreview();
            if (_selectedOwnedBlockId <= 0)
                return;

            GrowthBlockPlacementPreviewView preview = _manager.GetSkillBlockPlacementPreview(
                _selectedOwnedBlockId,
                originX,
                originY,
                _selectedRotation);
            Color color = preview.CanPlace
                ? new Color(0.16f, 0.82f, 0.34f, 0.48f)
                : new Color(0.94f, 0.20f, 0.18f, 0.52f);
            for (int index = 0; index < preview.Cells.Length; index++)
            {
                BoardCell cell = preview.Cells[index];
                if (cell.X < 0 || cell.X >= growth.BoardWidth ||
                    cell.Y < 0 || cell.Y >= growth.BoardHeight)
                {
                    continue;
                }

                Image image = _placementPreviewImages[cell.Y * growth.BoardWidth + cell.X];
                if (image == null)
                    continue;
                image.color = color;
                image.enabled = true;
            }
        }

        private void ClearPlacementPreview()
        {
            for (int index = 0; index < _placementPreviewImages.Length; index++)
            {
                if (_placementPreviewImages[index] != null)
                    _placementPreviewImages[index].enabled = false;
            }
        }

        private void RenderInventoryBlock(Transform parent, GrowthSkillBlockView block, int index)
        {
            float x = -260f + index * 130f;
            bool selected = block.InstanceId == _selectedOwnedBlockId;
            Color color = GetCategoryColor(block.Category);
            Color backgroundColor = Color.Lerp(PanelDarkColor, color, selected ? 0.58f : 0.32f);
            Button button = CreateButton(
                "Owned_" + block.InstanceId,
                parent,
                string.Empty,
                new Vector2(116f, 72f),
                new Vector2(x, -270f),
                backgroundColor,
                out _);
            int instanceId = block.InstanceId;
            button.onClick.AddListener(() => SelectOwnedBlock(instanceId));
            RenderTetromino(
                button.transform,
                block.ShapeCells,
                0,
                color,
                new Vector2(-30f, 5f),
                new Vector2(50f, 48f),
                13f,
                "Shape");
            CreateText(
                "Rarity", button.transform, GetRarityCode(block.Rarity), 12,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(40f, 22f),
                new Vector2(31f, 17f), PrimaryTextColor);
            CreateText(
                "Bonus", button.transform, FormatAbilityChanges(block.AbilityBonuses), 12,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(66f, 32f),
                new Vector2(21f, -15f),
                PrimaryTextColor);
        }

        private void RenderSelectedBlockPanel(CareerGrowthView growth)
        {
            RectTransform panel = CreatePanel(
                "SelectedBlock", "BLOCK CONTROL", "선택 블록",
                new Vector2(700f, 130f), new Vector2(-95f, -358f));
            if (_selectedOwnedBlockId > 0)
            {
                GrowthSkillBlockView block = FindOwnedBlock(growth, _selectedOwnedBlockId);
                RenderTetromino(
                    panel,
                    block.ShapeCells,
                    _selectedRotation,
                    GetCategoryColor(block.Category),
                    new Vector2(-286f, -7f),
                    new Vector2(84f, 70f),
                    17f,
                    "SelectedShape");
                CreateText(
                    "Name", panel, $"{GetCategoryLabel(block.Category)} · {GetRarityLabel(block.Rarity)}",
                    16, FontStyle.Bold, TextAnchor.MiddleLeft,
                    new Vector2(265f, 29f), new Vector2(-135f, 8f), GetCategoryColor(block.Category));
                CreateText(
                    "Effect", panel,
                    $"{FormatAbilityChanges(block.AbilityBonuses)}  ·  회전 {_selectedRotation * 90}°",
                    13, FontStyle.Normal, TextAnchor.MiddleLeft,
                    new Vector2(275f, 26f), new Vector2(-130f, -24f), SecondaryTextColor);
                Button rotate = CreateButton(
                    "Rotate", panel, "회전", new Vector2(115f, 48f), new Vector2(105f, -7f),
                    new Color(0.03f, 0.24f, 0.42f, 1f), out _);
                rotate.interactable = growth.CanEditBoard && block.CanRotate;
                rotate.onClick.AddListener(RotateSelectedBlock);
                Button sell = CreateButton(
                    "Sell", panel, $"판매 {FormatMoney(block.SellValue)}", new Vector2(170f, 48f),
                    new Vector2(255f, -7f), new Color(0.24f, 0.13f, 0.08f, 1f), out _);
                sell.onClick.AddListener(SellSelectedOwnedBlock);
                return;
            }

            if (_selectedPlacedBlockId > 0)
            {
                GrowthBoardCellView placed = FindPlacedCell(growth, _selectedPlacedBlockId);
                GrowthSkillBlockView block = FindPlacedBlock(growth, _selectedPlacedBlockId);
                RenderTetromino(
                    panel,
                    block.ShapeCells,
                    block.RotationQuarterTurns,
                    GetCategoryColor(placed.Category),
                    new Vector2(-286f, -7f),
                    new Vector2(84f, 70f),
                    17f,
                    "PlacedShape");
                CreateText(
                    "Name", panel, GetCategoryLabel(placed.Category) + " 블록 장착 중", 16,
                    FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(300f, 30f),
                    new Vector2(-115f, 8f), GetCategoryColor(placed.Category));
                CreateText(
                    "Warning", panel, "개별 제거하면 블록이 소멸합니다.", 13, FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Vector2(300f, 26f), new Vector2(-115f, -24f),
                    WarningColor);
                Button remove = CreateButton(
                    "Remove", panel,
                    _confirmPlacedBlockRemoval ? "정말 제거" : "장착 제거",
                    new Vector2(190f, 50f), new Vector2(225f, -7f),
                    _confirmPlacedBlockRemoval
                        ? new Color(0.54f, 0.12f, 0.12f, 1f)
                        : new Color(0.3f, 0.16f, 0.08f, 1f),
                    out _);
                remove.interactable = growth.CanEditBoard;
                remove.onClick.AddListener(RemoveSelectedPlacedBlock);
                return;
            }

            string guide = growth.CanEditBoard
                ? "보유 블록을 선택해 회전한 뒤 성장판의 빈 칸에 배치하세요."
                : "장착 효과는 경기에 반영됩니다. 변경은 오프시즌에만 가능합니다.";
            if (growth.LastPulledBlocks.Length > 0)
            {
                GrowthSkillBlockView last = growth.LastPulledBlocks[0];
                guide = $"최근 획득: {GetCategoryLabel(last.Category)} {GetRarityLabel(last.Rarity)} · " +
                        FormatAbilityChanges(last.AbilityBonuses);
            }
            CreateText(
                "Guide", panel, guide, 14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(620f, 54f), new Vector2(0f, -7f), SecondaryTextColor);
        }

        private void RenderBlockShop(CareerGrowthView growth)
        {
            RectTransform panel = CreatePanel(
                "BlockShop", "SKILL SHOP", "블록 상점",
                new Vector2(650f, 430f), new Vector2(613f, 232f));
            GrowthGachaOfferView standard = FindGachaOffer(
                growth,
                SkillGachaPurchaseTier.Normal);
            GrowthGachaOfferView premium = FindGachaOffer(
                growth,
                SkillGachaPurchaseTier.Rare);
            GrowthGachaOfferView elite = FindGachaOffer(
                growth,
                SkillGachaPurchaseTier.Elite);
            for (int index = 0; index < growth.ShopCategories.Length; index++)
            {
                GrowthBlockShopView item = growth.ShopCategories[index];
                float y = 128f - index * 72f;
                RectTransform row = CreateSection(
                    "Shop_" + item.Category, panel, new Vector2(614f, 64f), new Vector2(0f, y),
                    index % 2 == 0 ? CardColor : PanelDarkColor);
                Color categoryColor = GetCategoryColor(item.Category);
                RenderTetromino(
                    row,
                    item.PreviewShapeCells,
                    0,
                    categoryColor,
                    new Vector2(-258f, 0f),
                    new Vector2(58f, 52f),
                    13f,
                    "ShopShape");
                CreateText(
                    "Name", row, GetCategoryLabel(item.Category) + " 블록", 17, FontStyle.Bold,
                    TextAnchor.MiddleLeft, new Vector2(190f, 29f), new Vector2(-135f, 10f),
                    categoryColor);
                CreateText(
                    "Effect", row, GetCategoryEffectLabel(item.Category) + " · 4칸", 13, FontStyle.Normal,
                    TextAnchor.MiddleLeft, new Vector2(190f, 24f), new Vector2(-135f, -16f),
                    SecondaryTextColor);
                CreateText(
                    "Owned", row, $"보유 {item.OwnedCount}", 12, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(70f, 26f), new Vector2(12f, 0f),
                    SecondaryTextColor);
                SkillBlockCategory category = item.Category;
                Button standardBuy = CreateButton(
                    "BuyStandard", row, $"일반\n{FormatMoney(standard.Price)}",
                    new Vector2(78f, 48f), new Vector2(97f, 0f),
                    new Color(0.025f, 0.31f, 0.61f, 1f), out Text standardBuyLabel);
                standardBuyLabel.fontSize = 11;
                standardBuy.interactable = standard.CanPurchase;
                standardBuy.onClick.AddListener(() => PurchaseSkillBlock(
                    category,
                    SkillGachaPurchaseTier.Normal));
                Button premiumBuy = CreateButton(
                    "BuyPremium", row, $"고급\n{FormatMoney(premium.Price)}",
                    new Vector2(78f, 48f), new Vector2(183f, 0f),
                    new Color(0.38f, 0.20f, 0.57f, 1f), out Text premiumBuyLabel);
                premiumBuyLabel.fontSize = 11;
                premiumBuy.interactable = premium.CanPurchase;
                premiumBuy.onClick.AddListener(() => PurchaseSkillBlock(
                    category,
                    SkillGachaPurchaseTier.Rare));
                Button eliteBuy = CreateButton(
                    "BuyElite", row, $"특급\n{FormatMoney(elite.Price)}",
                    new Vector2(78f, 48f), new Vector2(268f, 0f),
                    new Color(0.56f, 0.29f, 0.05f, 1f), out Text eliteBuyLabel);
                eliteBuyLabel.fontSize = 11;
                eliteBuy.interactable = elite.CanPurchase;
                eliteBuy.onClick.AddListener(() => PurchaseSkillBlock(
                    category,
                    SkillGachaPurchaseTier.Elite));
            }

            CreateText(
                "Probability", panel,
                $"일반  {FormatGachaProbability(standard)}\n" +
                $"고급  {FormatGachaProbability(premium)}\n" +
                $"특급  {FormatGachaProbability(elite)}",
                9, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(400f, 58f), new Vector2(-105f, -174f), MutedColor);
            CreateText(
                "Pity", panel,
                $"보장 E {growth.ElitePityCount}/{growth.ElitePityTarget} · " +
                $"U {growth.UniquePityCount}/{growth.UniquePityTarget}\n" +
                $"L {growth.LegendaryPityCount}/{growth.LegendaryPityTarget}",
                9, FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(200f, 38f), new Vector2(200f, -177f), GoldColor);
        }

        private void RenderOffseasonActions(CareerGrowthView growth)
        {
            RectTransform panel = CreatePanel(
                "OffseasonActions", "OFF-SEASON ACTION", "오프시즌 액션",
                new Vector2(650f, 435f), new Vector2(613f, -205f));
            string phaseText = growth.IsOffseason
                ? $"{growth.RemainingWeeks}주 남음  ·  현재 {growth.CurrentWeek}주차"
                : "정규 시즌 중 · 액션은 오프시즌에 열립니다";
            CreateText(
                "Phase", panel, phaseText, 13, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(530f, 27f), new Vector2(0f, 156f),
                growth.IsOffseason ? GoldColor : WarningColor);

            GrowthProgramView[] featuredPrograms = GetFeaturedPrograms(growth);
            for (int index = 0; index < featuredPrograms.Length; index++)
            {
                RenderProgramCard(
                    panel,
                    growth,
                    featuredPrograms[index],
                    index,
                    featuredPrograms.Length);
            }

            GrowthProgramView selected = FindSelectedProgram(growth);
            string preview = growth.PlannedActivities.Length > 0
                ? $"계획 {growth.PlannedActivities.Length}개 · {growth.PlannedWeeks}주 · " +
                  $"{FormatMoney(growth.PlannedCost)} · 예상 컨디션 {growth.ProjectedConditionAfterPlan}"
                : selected.ProgramId == null
                    ? "액션을 선택하면 기간·비용·예상 변화가 표시됩니다."
                    : BuildProgramPreview(selected);
            CreateText(
                "Preview", panel, preview, 12, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(590f, 42f), new Vector2(0f, -97f), SecondaryTextColor);

            RenderExecuteActivityButton(panel, growth, selected);
            RenderCompleteOffseasonButton(panel, growth);

            if (!string.IsNullOrEmpty(_manager.LastError))
            {
                CreateText(
                    "Error", panel, _manager.LastError, 12, FontStyle.Normal,
                    TextAnchor.MiddleCenter, new Vector2(610f, 24f), new Vector2(0f, -193f),
                    ErrorColor);
            }
        }

        private void RenderExecuteActivityButton(
            RectTransform panel,
            CareerGrowthView growth,
            GrowthProgramView selected)
        {
            string label;
            bool canExecute;
            if (!growth.IsOffseason)
            {
                label = "오프시즌 전용";
                canExecute = false;
            }
            else if (growth.RemainingWeeks == 0 && !growth.IsActivityInProgress)
            {
                label = "남은 주차 없음";
                canExecute = false;
            }
            else if (growth.PlannedActivities.Length > 0)
            {
                label = $"성장 계획 {growth.PlannedActivities.Length}개 실행  " +
                        $"({growth.PlannedWeeks}주 · {FormatMoney(growth.PlannedCost)})";
                canExecute = true;
            }
            else
            {
                int durationWeeks = growth.IsActivityInProgress
                    ? growth.ActiveActivityEndWeek - growth.CurrentWeek + 1
                    : selected.DurationWeeks;
                label = selected.ProgramId == null
                    ? "진행할 액션을 선택하세요"
                    : $"{GetProgramLabel(selected.ProgramId)} 실행  ({durationWeeks}주 진행)";
                canExecute = growth.IsActivityInProgress || selected.ProgramId != null && selected.CanSelect;
            }

            Button execute = CreateButton(
                "ExecuteActivity", panel, label, new Vector2(400f, 60f), new Vector2(-95f, -151f),
                new Color(0.025f, 0.31f, 0.61f, 1f), out Text executeLabel);
            executeLabel.fontSize = 19;
            execute.interactable = canExecute;
            execute.onClick.AddListener(() =>
            {
                if (growth.PlannedActivities.Length > 0)
                    _manager.ExecuteGrowthPlan();
                else
                    _manager.ExecuteSelectedGrowthProgram();
            });
        }

        /// <summary>
        /// 남은 주를 다 쓰지 못해도 오프시즌을 마감할 수 있게 별도 버튼으로 노출한다.
        /// 진행 중인 활동이 있을 때만 잠긴다.
        /// </summary>
        private void RenderCompleteOffseasonButton(RectTransform panel, CareerGrowthView growth)
        {
            string label = growth.RemainingWeeks > 0
                ? $"남은 {growth.RemainingWeeks}주 포기\n스프링캠프 진행"
                : "스프링캠프 진행";
            Button complete = CreateButton(
                "CompleteOffseason", panel, label, new Vector2(180f, 60f), new Vector2(205f, -151f),
                new Color(0.42f, 0.25f, 0.04f, 1f), out Text completeLabel);
            completeLabel.fontSize = 15;
            complete.interactable = growth.CanCompleteOffseason;
            complete.onClick.AddListener(CompleteOffseason);
        }

        private void CompleteOffseason()
        {
            if (!_manager.CompleteOffseasonAndAdvanceToNextSeason())
                return;

            CareerContractView contract = _manager.Contract;
            CareerTabNavigation.Show(
                contract.NegotiationStatus is ContractNegotiationStatus.CurrentTeamOfferAvailable or
                    ContractNegotiationStatus.OffersAvailable
                    ? CareerMainTab.Contract
                    : CareerMainTab.Home);
        }

        private void RenderProgramCard(
            Transform parent,
            CareerGrowthView growth,
            GrowthProgramView program,
            int index,
            int programCount)
        {
            float pitch = ProgramCardStripWidth / programCount;
            float cardWidth = pitch - ProgramCardGap;
            float x = (index - (programCount - 1) * 0.5f) * pitch;
            bool selected = string.Equals(
                growth.SelectedProgramId,
                program.ProgramId,
                StringComparison.Ordinal);
            Color baseColor = GetProgramColor(program.ActivityType);
            if (selected)
                baseColor = Color.Lerp(baseColor, Color.white, 0.22f);
            Button card = CreateButton(
                "Program_" + program.ProgramId, parent, string.Empty,
                new Vector2(cardWidth, 210f), new Vector2(x, 25f), baseColor, out _);
            bool isActiveProgram = growth.IsActivityInProgress &&
                                   string.Equals(growth.ActiveProgramId, program.ProgramId, StringComparison.Ordinal);
            card.interactable = growth.IsOffseason &&
                                (growth.IsActivityInProgress ? isActiveProgram : true);
            string programId = program.ProgramId;
            card.onClick.AddListener(() => OpenActivityConfirmation(programId));
            CreateText(
                "Type", card.transform, GetActivityShortLabel(program.ActivityType), 10,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(cardWidth - 24f, 20f),
                new Vector2(0f, 84f), selected ? PrimaryTextColor : SecondaryTextColor);
            CreateText(
                "Name", card.transform, GetProgramLabel(program.ProgramId), 15,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(cardWidth - 16f, 50f),
                new Vector2(0f, 52f), PrimaryTextColor);
            CreateText(
                "Cost", card.transform, $"{program.DurationWeeks}주\n{FormatMoney(program.MoneyCost)}",
                13, FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(cardWidth - 22f, 48f),
                new Vector2(0f, 5f), SecondaryTextColor);
            CreateText(
                "Growth", card.transform, FormatProgramAbilities(program), 12,
                FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(cardWidth - 16f, 55f),
                new Vector2(0f, -45f), GreenColor);
            CreateText(
                "Fit", card.transform, "적합도 " + GetFitLabel(program.Fit), 11,
                FontStyle.Normal, TextAnchor.MiddleCenter, new Vector2(cardWidth - 22f, 24f),
                new Vector2(0f, -82f), GetFitColor(program.Fit));
        }

        private static GrowthProgramView[] GetFeaturedPrograms(CareerGrowthView growth)
        {
            string[] programIds = growth.PlayerType == PlayerType.Batter
                ? new[]
                {
                    "personal_batting", "partner_batter_default", "japan_batting_camp",
                    "rehab_general", "rest"
                }
                : new[]
                {
                    "personal_pitching", "partner_pitcher_default", "japan_pitch_design",
                    "rehab_general", "rest"
                };
            var result = new GrowthProgramView[programIds.Length];
            for (int idIndex = 0; idIndex < programIds.Length; idIndex++)
            {
                for (int programIndex = 0; programIndex < growth.Programs.Length; programIndex++)
                {
                    if (!string.Equals(
                            growth.Programs[programIndex].ProgramId,
                            programIds[idIndex],
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    result[idIndex] = growth.Programs[programIndex];
                    break;
                }
            }
            return result;
        }

        private static void RenderAttributeRow(
            Transform parent,
            PlayerAbility ability,
            int value,
            int bonus,
            Vector2 position)
        {
            CreateText(
                ability.ToString(), parent, GetAbilityLabel(ability), 13, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(74f, 24f), new Vector2(-185f, position.y),
                SecondaryTextColor);
            CreateProgressBar(
                parent, value / 100f, new Vector2(245f, 10f), new Vector2(7f, position.y),
                GetRatingColor(value));
            string valueText = bonus > 0 ? $"{value}  (+{bonus})" : value.ToString();
            CreateText(
                ability + "Value", parent, valueText, 13, FontStyle.Bold,
                TextAnchor.MiddleRight, new Vector2(86f, 24f), new Vector2(179f, position.y),
                bonus > 0 ? GreenColor : GetRatingColor(value));
        }

        private static void CreateStatusValue(Transform parent, string label, int value)
        {
            CreateText(
                "Label", parent, label, 12, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(90f, 24f), new Vector2(-52f, 15f), SecondaryTextColor);
            CreateText(
                "Value", parent, value.ToString(), 22, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(60f, 30f), new Vector2(68f, 8f), GetRatingColor(value));
            CreateProgressBar(
                parent, value / 100f, new Vector2(180f, 8f), new Vector2(0f, -17f),
                GetRatingColor(value));
        }

        private static void CreateTopBarSegment(
            Transform parent,
            string eyebrow,
            string value,
            Vector2 position,
            Vector2 size)
        {
            RectTransform segment = CreateImage(
                eyebrow + "Segment", parent, new Color(0.02f, 0.07f, 0.12f, 0.76f), size, position);
            CreateImage(
                "LeftDivider", segment, DividerColor, new Vector2(2f, size.y - 10f),
                new Vector2(-size.x * 0.5f + 1f, 0f));
            CreateText(
                "Eyebrow", segment, eyebrow, 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 42f, 18f), new Vector2(14f, 15f), AccentColor);
            CreateText(
                "Value", segment, value, 20, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x - 42f, 31f), new Vector2(14f, -8f), PrimaryTextColor);
        }

        private RectTransform CreatePanel(
            string name,
            string eyebrow,
            string title,
            Vector2 size,
            Vector2 position)
        {
            CreateImage(
                name + "Shadow", _content, new Color(0f, 0f, 0f, 0.68f),
                size + new Vector2(8f, 8f), position + new Vector2(4f, -5f));
            RectTransform panel = CreateImage(name, _content, BorderColor, size, position);
            RectTransform surface = CreateImage(
                "Surface", panel, PanelColor, Vector2.zero, Vector2.zero, stretch: true);
            surface.offsetMin = new Vector2(3f, 3f);
            surface.offsetMax = new Vector2(-3f, -3f);

            RectTransform header = CreateImage(
                "Header", panel, new Color(0.024f, 0.11f, 0.19f, 1f),
                new Vector2(size.x - 8f, 50f), new Vector2(0f, size.y * 0.5f - 29f));
            CreateImage(
                "HeaderLine", header, AccentColor, new Vector2(size.x * 0.34f, 2f),
                new Vector2(-size.x * 0.29f, -23f));
            CreateText(
                "Eyebrow", header, eyebrow, 10, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(size.x * 0.3f, 18f), new Vector2(-size.x * 0.33f, 11f), AccentColor);
            CreateText(
                "Heading", header, title, 20, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(size.x * 0.62f, 36f), new Vector2(0f, -1f), PrimaryTextColor);
            return panel;
        }

        private static RectTransform CreateSection(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            RectTransform frame = CreateImage(name, parent, DividerColor, size, position);
            RectTransform surface = CreateImage(
                "Surface", frame, color, Vector2.zero, Vector2.zero, stretch: true);
            surface.offsetMin = new Vector2(2f, 2f);
            surface.offsetMax = new Vector2(-2f, -2f);
            return frame;
        }

        private static void RenderTetromino(
            Transform parent,
            BoardCell[] shapeCells,
            int rotationQuarterTurns,
            Color color,
            Vector2 position,
            Vector2 bounds,
            float maxCellSize,
            string namePrefix)
        {
            if (shapeCells == null || shapeCells.Length == 0)
                return;

            int rotation = ((rotationQuarterTurns % 4) + 4) % 4;
            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            int maximumX = int.MinValue;
            int maximumY = int.MinValue;
            for (int index = 0; index < shapeCells.Length; index++)
            {
                GetRotatedCoordinates(shapeCells[index], rotation, out int x, out int y);
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
            }

            const float gap = 2f;
            int widthInCells = maximumX - minimumX + 1;
            int heightInCells = maximumY - minimumY + 1;
            float widthLimitedSize = (bounds.x - gap * (widthInCells - 1)) / widthInCells;
            float heightLimitedSize = (bounds.y - gap * (heightInCells - 1)) / heightInCells;
            float cellSize = Mathf.Min(maxCellSize, widthLimitedSize, heightLimitedSize);
            float step = cellSize + gap;
            float totalWidth = widthInCells * cellSize + (widthInCells - 1) * gap;
            float totalHeight = heightInCells * cellSize + (heightInCells - 1) * gap;

            for (int index = 0; index < shapeCells.Length; index++)
            {
                GetRotatedCoordinates(shapeCells[index], rotation, out int x, out int y);
                float cellX = position.x - totalWidth * 0.5f + cellSize * 0.5f +
                              (x - minimumX) * step;
                float cellY = position.y + totalHeight * 0.5f - cellSize * 0.5f -
                              (y - minimumY) * step;
                CreateImage(
                    namePrefix + "Cell_" + index,
                    parent,
                    color,
                    new Vector2(cellSize, cellSize),
                    new Vector2(cellX, cellY));
            }
        }

        private static void GetRotatedCoordinates(
            BoardCell cell,
            int rotationQuarterTurns,
            out int x,
            out int y)
        {
            switch (rotationQuarterTurns)
            {
                case 1:
                    x = cell.Y;
                    y = -cell.X;
                    break;
                case 2:
                    x = -cell.X;
                    y = -cell.Y;
                    break;
                case 3:
                    x = -cell.Y;
                    y = cell.X;
                    break;
                default:
                    x = cell.X;
                    y = cell.Y;
                    break;
            }
        }

        private static void CreateProgressBar(
            Transform parent,
            float normalizedValue,
            Vector2 size,
            Vector2 position,
            Color fillColor)
        {
            float clamped = Mathf.Clamp01(normalizedValue);
            RectTransform track = CreateImage(
                "Track", parent, new Color(0.11f, 0.16f, 0.2f, 1f), size, position);
            float fillWidth = Mathf.Max(2f, (size.x - 4f) * clamped);
            RectTransform fill = CreateImage(
                "Fill", track, fillColor, new Vector2(fillWidth, size.y - 4f), Vector2.zero);
            fill.anchorMin = fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = new Vector2(2f, 0f);
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 size,
            Vector2 position,
            bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Vector2 size,
            Vector2 position,
            Color color,
            bool stretch = false)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            if (stretch)
                Stretch(rect);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            Vector2 position,
            Color color,
            out Text text)
        {
            RectTransform rect = CreateImage(name, parent, color, size, position);
            Button button = rect.gameObject.AddComponent<Button>();
            rect.GetComponent<Image>().raycastTarget = true;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, 0.7f);
            button.colors = colors;
            text = CreateText(
                "Label", rect, label, 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, PrimaryTextColor, stretch: true);
            return button;
        }

        private static void AddTextOutline(Text text, Color color, float distance)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddPointerListener(
            GameObject target,
            EventTriggerType eventType,
            Action action)
        {
            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = target.AddComponent<EventTrigger>();
            if (trigger.triggers == null)
                trigger.triggers = new List<EventTrigger.Entry>();

            var entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }
    }
}
