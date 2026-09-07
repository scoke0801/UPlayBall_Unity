using System;
using System.Text;
using Baseball.Presentation.Owner;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Encyclopedia
{
    public sealed partial class UI_Scene_PlayerEncyclopedia
    {
        private void BuildInspector(RectTransform host)
        {
            _inspector = OwnerWorkspaceUiFactory.CreateRoot(host, "EncyclopediaInspector", false);
            var panel = OwnerWorkspaceUiFactory.CreatePanel(_inspector, "SelectedEntry", "선택 상세");
            OwnerRuntimeUiFactory.Stretch(panel.Root);
            _preview = SharedUI.PlayerMiniCardView.CreateRuntime(panel.Content, "SelectedCard");
            OwnerRuntimeUiFactory.SetAnchors(_preview.GetComponent<RectTransform>(), new Vector2(.23f, 1), new Vector2(.77f, 1), new Vector2(0, -190), Vector2.zero);
            _preview.DetailRequested += _ => OpenDetail();
            _preview.Selected += _ => OpenDetail();
            RectTransform actions = Row(panel.Content, "DetailActions", 194, 30);
            _openDetail = OwnerWorkspaceUiFactory.CreateButton(actions, "OpenCardDetail", "카드 앞 / 뒷면", OpenDetail);
            _otherYears = OwnerWorkspaceUiFactory.CreateButton(actions, "OtherYears", "다른 연도", () => NavigateRelated(true));
            _otherEditions = OwnerWorkspaceUiFactory.CreateButton(actions, "OtherEditions", "다른 Edition", () => NavigateRelated(false));
            _detailTabs = Row(panel.Content, "DetailTabs", 230, 30);
            string[] labels = { "카드 정보", "능력치", "월드 기록", "획득 경로" };
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                Button button = OwnerWorkspaceUiFactory.CreateButton(_detailTabs, "DetailTab" + i, labels[i], () => { _detailTab = index; ShowInspector(); });
                button.GetComponent<LayoutElement>().minWidth = 48;
            }
            var detailSurface = OwnerRuntimeUiFactory.CreateRect("DetailScroll", panel.Content);
            Image scrollInput = detailSurface.gameObject.AddComponent<Image>();
            scrollInput.color = new Color(1, 1, 1, 0.01f);
            scrollInput.raycastTarget = true;
            OwnerRuntimeUiFactory.SetAnchors(detailSurface, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -268));
            var scroll = detailSurface.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
            var viewport = OwnerRuntimeUiFactory.CreateRect("Viewport", detailSurface);
            OwnerRuntimeUiFactory.Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            _detail = OwnerWorkspaceUiFactory.CreateText(viewport, "Details", string.Empty, 13, FontStyle.Normal, TextAnchor.UpperLeft);
            var rect = _detail.rectTransform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _detail.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = rect;
        }

        private void NavigateRelated(bool person)
        {
            if (_selected == null) return;
            string id = person ? _selected.PlayerPersonId : _selected.PlayerSeasonId;
            _filter = new EncyclopediaScreenFilter();
            if (person) _filter.PlayerPersonId = id; else _filter.PlayerSeasonId = id;
            _tab = person ? 0 : 1;
            BuildFilters();
            Refresh();
        }

        private void ShowInspector()
        {
            bool hasSelection = _selected != null;
            bool readOnly = _snapshot == null || _snapshot.IsReadOnly;
            bool exactCard = hasSelection && !string.IsNullOrEmpty(_selected.CardId);
            _preview.gameObject.SetActive(hasSelection);
            _openDetail.interactable = hasSelection;
            _otherYears.interactable = hasSelection;
            _otherEditions.interactable = hasSelection;
            _otherYears.gameObject.SetActive(!_wishlistOnly);
            _otherEditions.gameObject.SetActive(!_wishlistOnly);
            _wish.gameObject.SetActive(!readOnly);
            _scout.gameObject.SetActive(!readOnly);
            _wish.interactable = exactCard && (_selected.IsWishlisted || !_selected.IsCurrentlyOwned);
            _scout.interactable = exactCard && _selected.HasScoutRoute;
            _detailTabs.GetChild(3).gameObject.SetActive(!readOnly);
            if (readOnly && _detailTab == 3) _detailTab = 0;
            for (int i = 0; i < _detailTabs.childCount; i++) OwnerUiButtonSkin.SetSelected(_detailTabs.GetChild(i).GetComponent<Button>(), i == _detailTab);
            if (!hasSelection) { _detail.text = "선수 또는 카드를 선택해 주세요.\n\n좌클릭: 선택\n우클릭: 카드 상세"; return; }
            _preview.Bind(CreateMiniCard(_selected, true));
            _preview.SetTeamIdentity(_selected.FranchiseDisplayName);
            _wish.transform.Find("Label").GetComponent<Text>().text = _selected.IsWishlisted ? "★ 위시 해제" : "☆ 위시 등록";
            switch (_detailTab)
            {
                case 1: _detail.text = EmptyFallback(_selected.AbilityInformation, "표시 가능한 능력치 정보가 없습니다."); break;
                case 2: _detail.text = EmptyFallback(_selected.WorldRecordInformation, "현재 월드에 이 시즌의 기록이 없습니다."); break;
                case 3: _detail.text = EmptyFallback(_selected.AcquisitionInformation, "현재 사용 가능한 스카우트 대상이 아닙니다."); break;
                default: _detail.text = BuildInformation(readOnly); break;
            }
        }

        private string BuildInformation(bool readOnly)
        {
            var text = new StringBuilder();
            text.Append(_selected.DisplayName).Append('\n').Append(_selected.OriginYear).Append(" / ").Append(_selected.FranchiseDisplayName).Append('\n');
            text.Append(_selected.Position).Append(" / ").Append(_selected.PitcherRole).Append("  COST ").Append(_selected.Cost).Append('\n');
            text.Append(_selected.Bats).Append(" / ").Append(_selected.Throws).Append('\n');
            if (!readOnly)
            {
                text.Append(_selected.IsCurrentlyOwned ? "✓ 현재 보유 " + _selected.OwnedCount + "장" : _selected.WasEverAcquired ? "✓ 획득 이력 있음 · 현재 미보유" : "미획득").Append('\n');
                if (_selected.IsWishlisted) text.Append("★ 위시리스트에 등록됨\n");
            }
            text.Append('\n').Append(_selected.CardInformation);
            text.Append("\n\n현재 시즌 카드\n");
            bool any = false;
            foreach (var entry in _snapshot.Cards)
            {
                if (entry.PlayerSeasonId != _selected.PlayerSeasonId) continue;
                if (any) text.Append(" · ");
                text.Append(entry.EditionDisplayName); any = true;
            }
            if (!any) text.Append("현재 발급된 카드가 없습니다.");
            text.Append("\n\n다른 연도\n");
            int displayed = 0;
            foreach (var entry in _snapshot.Seasons)
            {
                if (entry.PlayerPersonId != _selected.PlayerPersonId) continue;
                text.Append(entry.OriginYear).Append(" · ").Append(entry.FranchiseDisplayName).Append('\n');
                if (++displayed == 24) { text.Append("‘다른 연도’에서 전체 보기"); break; }
            }
            return text.ToString();
        }

        private static string EmptyFallback(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
