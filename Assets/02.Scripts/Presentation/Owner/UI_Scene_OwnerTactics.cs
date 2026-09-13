using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>보유 작전카드의 조건·대상·지속시간과 두 장착 슬롯을 함께 편집하는 화면이다.</summary>
    [DisallowMultipleComponent]
    public sealed class UI_Scene_OwnerTactics : MonoBehaviour, IUiCancelHandler
    {
        private RectTransform _root;
        private RectTransform _scheduleContent;
        private RectTransform _editorScrim;
        private RectTransform _editor;
        private RectTransform _cardContent;
        private readonly Text[] _slotLabels = new Text[2];
        private readonly RawImage[] _slotArtworks = new RawImage[2];
        private Text _editorTitle;
        private Text _selectedGameText;
        private Text _detail;
        private ScrollRect _detailScroll;
        private Text _status;
        private Text _editorStatus;
        private OwnerTacticsSnapshot _snapshot;
        private OwnerTacticCardSnapshot _selectedCard;
        private readonly string[] _draftIds = new string[2];
        private int _draftCount;
        private int _selectedSlot;
        private int _editingGameId;
        private TacticCardCategory? _category;

        private static readonly float[] ScheduleColumnEdges =
            { 0f, 0.08f, 0.19f, 0.31f, 0.43f, 0.67f, 0.76f, 0.88f, 1f };
        private static readonly string[] ScheduleColumnLabels =
            { "구분", "경기", "일정", "점수 및 결과", "상대 구단", "장소", "작전 카드", "설정" };

        public event Action<int, string[]> SelectionConfirmed;
        public RectTransform GuideTarget => _root != null && _root.gameObject.activeInHierarchy && _snapshot != null ? _root : null;
        public bool HasGuideEditor => _editor != null && _editor.gameObject.activeInHierarchy;

        public static UI_Scene_OwnerTactics CreateRuntime(Transform parent)
        {
            var host = new GameObject(nameof(UI_Scene_OwnerTactics), typeof(RectTransform));
            host.transform.SetParent(parent, false);
            OwnerWorkspaceUiFactory.Stretch(host.GetComponent<RectTransform>());
            var view = host.AddComponent<UI_Scene_OwnerTactics>();
            view.Build();
            return view;
        }

        public void Bind(OwnerTacticsSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _editingGameId = 0;
            Restore();
            _category = null;
            _selectedCard = null;
            RebuildCards();
            RebuildSchedule();
            if (_snapshot.Cards.Count > 0)
                SelectCard(_snapshot.Cards[0]);
            else
            {
                _detail.text = "보유 중인 작전카드가 없습니다.";
            }
            _editorScrim.gameObject.SetActive(false);
            _editor.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        /// <summary>열려 있는 작전카드 편집기를 저장하지 않고 닫는다.</summary>
        public bool TryHandleCancel()
        {
            if (_editor == null || !_editor.gameObject.activeSelf)
                return false;
            CloseEditor();
            return true;
        }

        public void SetFeedback(string message, bool isError)
        {
            Color color = isError ? CareerUiTheme.Error : CareerUiTheme.Success;
            if (_status != null)
            {
                _status.text = message ?? string.Empty;
                _status.color = color;
            }
            if (_editorStatus != null)
            {
                _editorStatus.text = message ?? string.Empty;
                _editorStatus.color = color;
            }
        }

        private void Build()
        {
            _root = OwnerWorkspaceUiFactory.CreateRoot(transform, "OwnerTacticsWorkspace", true);
            RectTransform title = OwnerDugoutDetailUiFactory.CreatePanel(_root, "ScheduleTitle", 0.015f, 0.88f, 0.985f, 0.975f);
            OwnerDugoutDetailUiFactory.CreateLabel(title, "Title", "작전 / 시즌 경기", 0.025f, 0.15f, 0.34f, 0.9f, 22, FontStyle.Bold);
            OwnerDugoutDetailUiFactory.CreateLabel(title, "Guide", "앞으로 열릴 최대 10경기에 작전카드를 미리 배치합니다.", 0.35f, 0.15f, 0.78f, 0.9f, 13);
            OwnerDugoutDetailUiFactory.CreateButton(title, "NextGame", "다음 경기", 0.82f, 0.18f, 0.975f, 0.84f, OpenEditor);

            RectTransform table = OwnerDugoutDetailUiFactory.CreatePanel(_root, "ScheduleTable", 0.015f, 0.105f, 0.985f, 0.865f);
            CreateScheduleHeader(table);
            _scheduleContent = OwnerDugoutDetailUiFactory.CreateScrollContent(table, "Rows", 0.012f, 0.035f, 0.988f, 0.88f, out _);

            _editorScrim = OwnerDugoutDetailUiFactory.CreateRect(_root, "CardSettingScrim", 0f, 0f, 1f, 1f);
            Image scrimImage = _editorScrim.gameObject.AddComponent<Image>();
            scrimImage.color = new Color(0.02f, 0.03f, 0.03f, 0.48f);
            scrimImage.raycastTarget = true;

            _editor = OwnerDugoutDetailUiFactory.CreatePanel(_root, "CardSettingOverlay", 0.12f, 0.14f, 0.88f, 0.88f);
            _editor.GetComponent<UIOwnerFrontOfficePanel>().Refresh();
            Shadow editorShadow = _editor.gameObject.AddComponent<Shadow>();
            editorShadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            editorShadow.effectDistance = new Vector2(8f, -8f);
            editorShadow.useGraphicAlpha = false;
            _editorTitle = OwnerDugoutDetailUiFactory.CreateLabel(
                _editor, "OverlayTitle", "경기 작전 카드 설정", 0.03f, 0.90f, 0.50f, 0.985f, 20, FontStyle.Bold);
            OwnerDugoutDetailUiFactory.CreateLabel(_editor, "OverlayRule", "최대 2장 · 중복 불가 · 방해카드 최대 1장", 0.49f, 0.90f, 0.84f, 0.985f, 11);
            OwnerDugoutDetailUiFactory.CreateButton(_editor, "Close", "닫기", 0.865f, 0.915f, 0.975f, 0.975f, CloseEditor);

            RectTransform catalog = OwnerDugoutDetailUiFactory.CreatePanel(_editor, "CardCatalog", 0.025f, 0.14f, 0.58f, 0.89f);
            RectTransform loadout = OwnerDugoutDetailUiFactory.CreatePanel(_editor, "Loadout", 0.60f, 0.45f, 0.975f, 0.89f);
            RectTransform detail = OwnerDugoutDetailUiFactory.CreatePanel(_editor, "Detail", 0.60f, 0.14f, 0.975f, 0.43f);

            OwnerDugoutDetailUiFactory.CreateLabel(catalog, "Title", "보유 작전카드", 0.03f, 0.91f, 0.33f, 0.98f, 20, FontStyle.Bold);
            CreateCategoryButton(catalog, "전체", null, 0.34f, 0.46f);
            CreateCategoryButton(catalog, "야수", TacticCardCategory.Batting, 0.47f, 0.59f);
            CreateCategoryButton(catalog, "투수", TacticCardCategory.Pitching, 0.60f, 0.72f);
            CreateCategoryButton(catalog, "공통", TacticCardCategory.Common, 0.73f, 0.85f);
            CreateCategoryButton(catalog, "분석", TacticCardCategory.Analysis, 0.86f, 0.98f);
            _cardContent = OwnerDugoutDetailUiFactory.CreateScrollContent(catalog, "Scroll", 0.025f, 0.035f, 0.975f, 0.89f, out _);

            OwnerDugoutDetailUiFactory.CreateLabel(loadout, "Title", "선택 프리셋", 0.07f, 0.90f, 0.93f, 0.98f, 18, FontStyle.Bold);
            _selectedGameText = OwnerDugoutDetailUiFactory.CreateLabel(
                loadout, "Preset", "카드 클릭: 장착 · 장착 카드 재클릭: 해제", 0.07f, 0.79f, 0.93f, 0.89f, 12);
            for (int index = 0; index < _slotLabels.Length; index++)
            {
                int slotIndex = index;
                Button button = OwnerDugoutDetailUiFactory.CreateButton(loadout, "Slot" + index, "슬롯 " + (index + 1),
                    0.07f, 0.46f - index * 0.34f, 0.93f, 0.75f - index * 0.34f, () => SelectSlot(slotIndex));
                _slotLabels[index] = button.transform.Find("Label").GetComponent<Text>();
                _slotLabels[index].fontSize = 12;
                OwnerDugoutDetailUiFactory.Place(_slotLabels[index].rectTransform, 0.32f, 0f, 0.96f, 1f);
                _slotLabels[index].alignment = TextAnchor.MiddleLeft;
                _slotArtworks[index] = CreateCardArtwork(button.transform, "CardArtwork", TacticCardArtwork.CommonKey,
                    0.025f, 0.06f, 0.29f, 0.94f, true);
            }

            OwnerDugoutDetailUiFactory.CreateLabel(detail, "Title", "작전 상세", 0.06f, 0.90f, 0.94f, 0.98f, 20, FontStyle.Bold);
            RectTransform detailContent = OwnerDugoutDetailUiFactory.CreateScrollContent(
                detail, "DescriptionScroll", 0.06f, 0.08f, 0.94f, 0.86f, out _detailScroll);
            var detailLayout = detailContent.gameObject.AddComponent<VerticalLayoutGroup>();
            detailLayout.childControlWidth = true;
            detailLayout.childControlHeight = true;
            detailLayout.childForceExpandWidth = true;
            detailLayout.childForceExpandHeight = false;
            var detailSize = detailContent.gameObject.AddComponent<ContentSizeFitter>();
            detailSize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _detail = OwnerDugoutDetailUiFactory.CreateLabel(detailContent, "Description", "카드를 선택하세요.",
                0f, 0f, 1f, 1f, 13, FontStyle.Normal, TextAnchor.UpperLeft);
            _detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detail.verticalOverflow = VerticalWrapMode.Truncate;
            _status = OwnerDugoutDetailUiFactory.CreateLabel(_root, "Status", string.Empty, 0.02f, 0.025f, 0.98f, 0.085f, 13);
            _editorStatus = OwnerDugoutDetailUiFactory.CreateLabel(_editor, "Status", string.Empty, 0.03f, 0.035f, 0.62f, 0.115f, 12);
            OwnerDugoutDetailUiFactory.CreateButton(_editor, "Restore", "되돌리기", 0.65f, 0.035f, 0.80f, 0.115f, Restore);
            var confirm = OwnerDugoutDetailUiFactory.CreateButton(_editor, "Confirm", "작전 적용", 0.82f, 0.035f, 0.975f, 0.115f, Confirm);
            OwnerUiButtonSkin.Apply(confirm, OwnerButtonRole.Primary);
            _editorScrim.gameObject.SetActive(false);
            _editor.gameObject.SetActive(false);
        }

        private static void CreateScheduleHeader(Transform parent)
        {
            RectTransform header = OwnerDugoutDetailUiFactory.CreateRect(parent, "Header", 0.012f, 0.89f, 0.988f, 0.975f);
            Image surface = header.gameObject.AddComponent<Image>();
            OwnerDashboardStyle.SetDataSurface(surface, OwnerDashboardStyle.TableHeader);
            surface.raycastTarget = false;
            for (int index = 0; index < ScheduleColumnLabels.Length; index++)
                CreateScheduleCell(header, "Header" + index, ScheduleColumnLabels[index],
                    ScheduleColumnEdges[index], ScheduleColumnEdges[index + 1], 13, FontStyle.Bold);
        }

        private void RebuildSchedule()
        {
            if (_snapshot == null || _scheduleContent == null) return;
            OwnerDugoutDetailUiFactory.ClearChildren(_scheduleContent);
            float height = Mathf.Max(112f, _snapshot.ScheduleRows.Count * 58f);
            _scheduleContent.sizeDelta = new Vector2(0f, height);
            for (int index = 0; index < _snapshot.ScheduleRows.Count; index++)
                CreateScheduleRow(_snapshot.ScheduleRows[index], index, height);
            if (_snapshot.ScheduleRows.Count == 0)
                CreateScheduleCell(_scheduleContent, "Empty", "표시할 시즌 경기가 없습니다.", 0f, 1f, 15, FontStyle.Normal);
        }

        private void CreateScheduleRow(OwnerTacticScheduleRowSnapshot row, int index, float contentHeight)
        {
            float top = 1f - index * 58f / contentHeight;
            float bottom = 1f - (index + 1) * 58f / contentHeight + 2f / contentHeight;
            RectTransform root = OwnerDugoutDetailUiFactory.CreateRect(_scheduleContent, "GameRow" + index, 0f, bottom, 1f, top);
            Image background = root.gameObject.AddComponent<Image>();
            OwnerDashboardStyle.SetDataSurface(background,
                index % 2 == 0 ? OwnerDashboardStyle.TableSurface : OwnerDashboardStyle.TableAlternate);
            background.raycastTarget = false;

            CreateScheduleCell(root, "Type", "정규", ScheduleColumnEdges[0], ScheduleColumnEdges[1], 12, FontStyle.Normal);
            CreateScheduleCell(root, "Round", "제 " + row.Round + "경기", ScheduleColumnEdges[1], ScheduleColumnEdges[2], 12, FontStyle.Bold);
            CreateScheduleCell(root, "Schedule", row.IsCompleted ? "경기 종료" : index == 0 ? "다음 경기" : "예정",
                ScheduleColumnEdges[2], ScheduleColumnEdges[3], 12, FontStyle.Normal);
            Text result = CreateScheduleCell(root, "Result", FormatResult(row), ScheduleColumnEdges[3], ScheduleColumnEdges[4], 12, FontStyle.Bold);
            if (row.IsCompleted)
                result.color = row.TeamRuns > row.OpponentRuns ? CareerUiTheme.Success : OwnerDashboardStyle.TableSecondary;
            CreateScheduleCell(root, "Opponent", row.OpponentName, ScheduleColumnEdges[4], ScheduleColumnEdges[5], 12, FontStyle.Bold);
            CreateScheduleCell(root, "Venue", row.IsHome ? "홈" : "원정", ScheduleColumnEdges[5], ScheduleColumnEdges[6], 12, FontStyle.Normal);
            if (row.IsConfigurable || row.EquippedIds.Count > 0)
                CreateScheduleTokens(root, ScheduleColumnEdges[6], ScheduleColumnEdges[7], row);
            else CreateScheduleCell(root, "Tactics", row.IsCompleted ? "사용 완료" : "-", ScheduleColumnEdges[6], ScheduleColumnEdges[7], 11, FontStyle.Normal);

            Button setting = OwnerDugoutDetailUiFactory.CreateButton(root, "CardSetting",
                row.IsConfigurable ? "카드설정" : "완료",
                ScheduleColumnEdges[7] + 0.012f, 0.16f, ScheduleColumnEdges[8] - 0.012f, 0.84f,
                row.IsConfigurable ? () => OpenEditor(row) : null);
            setting.interactable = row.IsConfigurable;
            setting.transform.Find("Label").GetComponent<Text>().fontSize = 11;
        }

        private void CreateScheduleTokens(
            Transform parent,
            float left,
            float right,
            OwnerTacticScheduleRowSnapshot row)
        {
            IReadOnlyList<string> ids = row.GameId == _editingGameId && _editor.gameObject.activeSelf
                ? GetDraftIds()
                : row.EquippedIds;
            if (ids.Count == 0)
            {
                CreateScheduleCell(parent, "TacticsEmpty", "미설정", left, right, 11, FontStyle.Normal);
                return;
            }
            float width = right - left;
            for (int index = 0; index < ids.Count; index++)
            {
                float tokenLeft = left + width * (index == 0 ? 0.26f : 0.56f);
                OwnerTacticCardSnapshot card = Find(ids[index]);
                CreateCardArtwork(parent, "TacticToken" + index, card?.ArtworkKey ?? TacticCardArtwork.CommonKey,
                    tokenLeft, 0.08f, tokenLeft + width * 0.18f, 0.92f);
            }
        }

        private static RawImage CreateCardArtwork(
            Transform parent,
            string name,
            string artworkKey,
            float left,
            float bottom,
            float right,
            float top,
            bool showFrame = false)
        {
            RectTransform holder = OwnerDugoutDetailUiFactory.CreateRect(parent, name + "Holder", left, bottom, right, top);
            if (showFrame)
            {
                Image frame = holder.gameObject.AddComponent<Image>();
                frame.color = new Color(0.82f, 0.86f, 0.88f, 0.62f);
                frame.raycastTarget = false;
                Outline outline = holder.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.28f, 0.35f, 0.40f, 0.55f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
            RawImage token = TacticCardArtwork.Create(holder, name, artworkKey, Color.white);
            OwnerWorkspaceUiFactory.Stretch(token.rectTransform);
            AspectRatioFitter fitter = token.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = token.texture != null
                ? token.texture.width / (float)token.texture.height
                : 2f / 3f;
            // 버튼 스킨의 불투명 배경보다 나중에 그려야 장착 카드가 가려지지 않는다.
            holder.SetAsLastSibling();
            return token;
        }

        private static string FormatResult(OwnerTacticScheduleRowSnapshot row)
        {
            if (!row.IsCompleted) return "-";
            string result = row.TeamRuns > row.OpponentRuns ? "승" : row.TeamRuns < row.OpponentRuns ? "패" : "무";
            return result + "  " + row.TeamRuns + " : " + row.OpponentRuns;
        }

        private static Text CreateScheduleCell(
            Transform parent,
            string name,
            string value,
            float left,
            float right,
            int fontSize,
            FontStyle fontStyle)
        {
            Text text = OwnerDugoutDetailUiFactory.CreateLabel(parent, name, value, left, 0f, right, 1f,
                fontSize, fontStyle, TextAnchor.MiddleCenter);
            OwnerDashboardStyle.SetDataText(text, fontStyle == FontStyle.Bold);
            if (name == "Opponent" || name == "Header4") text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        private void OpenEditor()
        {
            if (_snapshot == null) return;
            for (int index = 0; index < _snapshot.ScheduleRows.Count; index++)
            {
                if (!_snapshot.ScheduleRows[index].IsConfigurable) continue;
                OpenEditor(_snapshot.ScheduleRows[index]);
                return;
            }
            SetFeedback("설정할 다음 경기가 없습니다.", true);
        }

        private void OpenEditor(OwnerTacticScheduleRowSnapshot row)
        {
            if (row == null || !row.IsConfigurable) return;
            _editingGameId = row.GameId;
            _editorTitle.text = $"제 {row.Round}경기 작전 카드 설정";
            _selectedGameText.text = $"제 {row.Round}경기 · 카드 클릭: 장착 · 재클릭: 해제";
            _editorScrim.gameObject.SetActive(true);
            _editor.gameObject.SetActive(true);
            _editorScrim.SetAsLastSibling();
            _editor.SetAsLastSibling();
            Restore();
        }

        private void CloseEditor()
        {
            Restore();
            _editorScrim.gameObject.SetActive(false);
            _editor.gameObject.SetActive(false);
            RebuildSchedule();
        }

        private void CreateCategoryButton(Transform parent, string label, TacticCardCategory? category, float left, float right)
        {
            OwnerDugoutDetailUiFactory.CreateButton(parent, "Category" + label, label, left, 0.91f, right, 0.975f,
                () => SetCategory(category));
        }

        private void SetCategory(TacticCardCategory? category)
        {
            _category = category;
            RebuildCards();
        }

        private void RebuildCards()
        {
            if (_snapshot == null) return;
            var surface = _cardContent.parent.GetComponent<Image>();
            OwnerDashboardStyle.SetDataSurface(surface, OwnerDashboardStyle.TableSurface, true);
            _cardContent.parent.GetComponent<Mask>().showMaskGraphic = true;
            OwnerDugoutDetailUiFactory.ClearChildren(_cardContent);
            var visible = new List<OwnerTacticCardSnapshot>();
            for (int index = 0; index < _snapshot.Cards.Count; index++)
                if (!_category.HasValue || _snapshot.Cards[index].Definition.Category == _category.Value) visible.Add(_snapshot.Cards[index]);
            const int columnCount = 2;
            const float rowHeight = 112f;
            int rowCount = Mathf.CeilToInt(visible.Count / (float)columnCount);
            float height = Mathf.Max(112f, rowCount * rowHeight);
            _cardContent.sizeDelta = new Vector2(0f, height);
            if (visible.Count == 0)
            {
                var empty = OwnerDugoutDetailUiFactory.CreateLabel(_cardContent, "EmptyCards",
                    _snapshot.Cards.Count == 0 ? "보유 작전카드가 없습니다.\n상점에서 작전카드를 획득한 뒤 설정하세요."
                        : "이 분류의 작전카드가 없습니다.\n전체 탭에서 보유 카드를 확인하세요.",
                    .04f, .08f, .96f, .92f, 16, FontStyle.Normal, TextAnchor.MiddleCenter);
                OwnerDashboardStyle.SetDataText(empty);
            }
            for (int index = 0; index < visible.Count; index++)
            {
                OwnerTacticCardSnapshot card = visible[index];
                int row = index / columnCount;
                int column = index % columnCount;
                float top = 1f - row * rowHeight / height;
                float bottom = 1f - (row + 1) * rowHeight / height + 6f / height;
                float left = 0.01f + column * 0.50f;
                float right = 0.49f + column * 0.50f;
                Button button = OwnerDugoutDetailUiFactory.CreateButton(_cardContent, "Card" + index,
                    card.Name + "  ×" + card.OwnedCount + "\n" + GetCategoryName(card.Definition.Category) + " · " + GetTierName(card.Definition.TacticTier),
                    left, bottom, right, top, () => SelectAndEquipCard(card));
                Text label = button.transform.Find("Label").GetComponent<Text>();
                OwnerDugoutDetailUiFactory.Place(label.rectTransform, 0.32f, 0.04f, 0.96f, 0.96f);
                label.fontSize = 12;
                label.alignment = TextAnchor.MiddleLeft;
                RawImage artwork = TacticCardArtwork.Create(button.transform, "Artwork", card.ArtworkKey, Color.white);
                OwnerDugoutDetailUiFactory.Place(artwork.rectTransform, 0.025f, 0.10f, 0.29f, 0.90f);
            }
        }

        private void SelectAndEquipCard(OwnerTacticCardSnapshot card)
        {
            SelectCard(card);
            int equippedSlot = FindDraftSlot(card.Id);
            if (equippedSlot >= 0)
            {
                _selectedSlot = equippedSlot;
                ClearSelectedSlot();
                return;
            }
            EquipSelected();
        }

        private int FindDraftSlot(string cardId)
        {
            for (int index = 0; index < _draftCount; index++)
                if (string.Equals(_draftIds[index], cardId, StringComparison.Ordinal)) return index;
            return -1;
        }

        private void SelectCard(OwnerTacticCardSnapshot card)
        {
            _selectedCard = card;
            _detail.text = card.Name + "\n" + GetTierName(card.Definition.TacticTier) + " · 보유 " + card.OwnedCount + "장\n\n" +
                           "발동 조건  " + card.TriggerText + "\n\n" +
                           "효과  " + card.EffectText;
            if (card.Definition.CounterCardIds.Count > 0)
            {
                var counterNames = new string[card.Definition.CounterCardIds.Count];
                for (int index = 0; index < counterNames.Length; index++)
                    counterNames[index] = Find(card.Definition.CounterCardIds[index])?.Name ?? "미보유 작전카드";
                _detail.text += "\n\n상쇄 대상  " + string.Join(", ", counterNames);
            }
            _detailScroll.verticalNormalizedPosition = 1f;
        }

        private void SelectSlot(int slotIndex)
        {
            _selectedSlot = slotIndex;
            RefreshSlots();
        }

        private void EquipSelected()
        {
            if (_selectedCard == null || _selectedCard.OwnedCount <= 0) return;
            if (CountReservedOutsideSelectedSlot(_selectedCard.Id) >= _selectedCard.OwnedCount)
            {
                SetFeedback($"보유 {_selectedCard.OwnedCount}장이 다른 경기 또는 슬롯에 이미 배치되어 있습니다.", true);
                return;
            }
            int other = 1 - _selectedSlot;
            if (other < _draftCount && string.Equals(_draftIds[other], _selectedCard.Id, StringComparison.Ordinal))
            {
                SetFeedback("같은 작전카드는 중복 장착할 수 없습니다.", true);
                return;
            }
            OwnerTacticCardSnapshot otherCard = other < _draftCount ? Find(_draftIds[other]) : null;
            if (_selectedCard.Definition.IsDisruption && otherCard?.Definition.IsDisruption == true)
            {
                SetFeedback("방해 작전카드는 경기당 한 장만 장착할 수 있습니다.", true);
                return;
            }
            _draftIds[_selectedSlot] = _selectedCard.Id;
            _draftCount = Mathf.Max(_draftCount, _selectedSlot + 1);
            CompactDraft();
            int equippedSlot = _selectedSlot;
            if (_draftCount == 1) _selectedSlot = 1;
            else _selectedSlot = equippedSlot;
            RefreshSlots();
            RebuildSchedule();
            SetFeedback("장착했습니다. 결정하면 선택한 경기에 저장됩니다.", false);
        }

        private int CountReservedOutsideSelectedSlot(string cardId)
        {
            int count = 0;
            if (_snapshot != null)
            {
                for (int rowIndex = 0; rowIndex < _snapshot.ScheduleRows.Count; rowIndex++)
                {
                    OwnerTacticScheduleRowSnapshot row = _snapshot.ScheduleRows[rowIndex];
                    if (row.IsCompleted || row.GameId == _editingGameId) continue;
                    for (int cardIndex = 0; cardIndex < row.EquippedIds.Count; cardIndex++)
                        if (string.Equals(row.EquippedIds[cardIndex], cardId, StringComparison.Ordinal)) count++;
                }
            }
            for (int slotIndex = 0; slotIndex < _draftCount; slotIndex++)
            {
                if (slotIndex == _selectedSlot) continue;
                if (string.Equals(_draftIds[slotIndex], cardId, StringComparison.Ordinal)) count++;
            }
            return count;
        }

        private void ClearSelectedSlot()
        {
            if (_selectedSlot < _draftCount) _draftIds[_selectedSlot] = null;
            CompactDraft();
            RefreshSlots();
            RebuildSchedule();
            SetFeedback("선택 슬롯을 비웠습니다. 결정 전에는 저장되지 않습니다.", false);
        }

        private void CompactDraft()
        {
            if (string.IsNullOrEmpty(_draftIds[0]) && !string.IsNullOrEmpty(_draftIds[1]))
            {
                _draftIds[0] = _draftIds[1];
                _draftIds[1] = null;
            }
            _draftCount = string.IsNullOrEmpty(_draftIds[0]) ? 0 : string.IsNullOrEmpty(_draftIds[1]) ? 1 : 2;
            if (_selectedSlot >= _draftCount && _draftCount < 2) _selectedSlot = _draftCount;
        }

        private void RefreshSlots()
        {
            for (int index = 0; index < _slotLabels.Length; index++)
            {
                OwnerTacticCardSnapshot card = index < _draftCount ? Find(_draftIds[index]) : null;
                string marker = index == _selectedSlot ? "▶ " : string.Empty;
                _slotLabels[index].text = marker + "슬롯 " + (index + 1) + "\n" + (card?.Name ?? "비어 있음");
                _slotArtworks[index].texture = card == null ? null : TacticCardArtwork.Load(card.ArtworkKey);
                _slotArtworks[index].transform.parent.gameObject.SetActive(card != null);
            }
        }

        private void Restore()
        {
            if (_snapshot == null) return;
            Array.Clear(_draftIds, 0, _draftIds.Length);
            OwnerTacticScheduleRowSnapshot row = FindScheduleRow(_editingGameId);
            IReadOnlyList<string> equippedIds = row?.EquippedIds ?? _snapshot.EquippedIds;
            _draftCount = Math.Min(equippedIds.Count, _draftIds.Length);
            for (int index = 0; index < _draftCount; index++) _draftIds[index] = equippedIds[index];
            _selectedSlot = 0;
            RefreshSlots();
            if (_editor == null || !_editor.gameObject.activeSelf) RebuildSchedule();
            SetFeedback(row == null
                ? $"{_snapshot.PresetName} 프리셋 · 저장된 작전 구성입니다."
                : $"제 {row.Round}경기 · 저장된 작전 구성입니다.", false);
        }

        private void Confirm()
        {
            var result = new string[_draftCount];
            for (int index = 0; index < result.Length; index++) result[index] = _draftIds[index];
            SelectionConfirmed?.Invoke(_editingGameId, result);
            _editorScrim.gameObject.SetActive(false);
            _editor.gameObject.SetActive(false);
        }

        private IReadOnlyList<string> GetDraftIds()
        {
            var result = new string[_draftCount];
            for (int index = 0; index < result.Length; index++) result[index] = _draftIds[index];
            return result;
        }

        private OwnerTacticScheduleRowSnapshot FindScheduleRow(int gameId)
        {
            if (_snapshot == null || gameId <= 0) return null;
            for (int index = 0; index < _snapshot.ScheduleRows.Count; index++)
                if (_snapshot.ScheduleRows[index].GameId == gameId) return _snapshot.ScheduleRows[index];
            return null;
        }

        private OwnerTacticCardSnapshot Find(string id)
        {
            if (string.IsNullOrEmpty(id) || _snapshot == null) return null;
            for (int index = 0; index < _snapshot.Cards.Count; index++)
                if (string.Equals(_snapshot.Cards[index].Id, id, StringComparison.Ordinal)) return _snapshot.Cards[index];
            return null;
        }

        private static string GetCategoryName(TacticCardCategory value) => value switch
        {
            TacticCardCategory.Batting => "야수",
            TacticCardCategory.Pitching => "투수",
            TacticCardCategory.Analysis => "분석",
            TacticCardCategory.Common => "공통",
            _ => "분류 없음"
        };

        private static string GetTierName(TacticTier value) => value switch
        {
            TacticTier.Normal => "일반",
            TacticTier.Rare => "레어",
            TacticTier.Special => "스페셜",
            TacticTier.Signature => "시그니처",
            _ => "등급 없음"
        };

        private void OnDestroy()
        {
            SelectionConfirmed = null;
            OwnerWorkspaceUiFactory.DestroyOwnedRoot(_root);
        }
    }
}
