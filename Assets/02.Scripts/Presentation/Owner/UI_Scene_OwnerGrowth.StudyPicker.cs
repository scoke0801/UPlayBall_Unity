using System;
using System.Collections.Generic;
using Baseball.Core.Players;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Scene_OwnerGrowth
    {
        private readonly OwnerCardFilters _studyOrigins = new OwnerCardFilters();
        private string _studyDraftCardId = string.Empty;
        private string _studyQuery = string.Empty;
        private int _studyPosition;
        private int _studyRegistration;
        private int _studySort;
        private int _studyPickerPage;
        private RectTransform _studyPickerBody;
        private RectTransform _studyPickerSafe;
        private InputField _studySearch;
        private readonly List<PlayerPosition> _studyPositions = new List<PlayerPosition>();

        private void RenderStudyPlayerPicker()
        {
            // 배경 버튼의 포커스와 입력을 함께 차단한다. 선택창은 독립된 형제 영역이다.
            foreach (Transform child in _content)
            {
                CanvasGroup group = child.GetComponent<CanvasGroup>();
                if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            Image dim = Surface(_content, "StudyPickerDim", 0, 0, 1100, 560, new Color32(12, 22, 35, 185));
            dim.raycastTarget = true;
            Image panel = Surface(_content, "StudyPlayerPicker", 32, 16, 1036, 512, new Color32(244, 246, 248, 255));
            panel.raycastTarget = true;
            UIOwnerFrontOfficePanel.ApplyFramedSurface(panel.rectTransform);
            _studyPickerSafe = OwnerRuntimeUiFactory.CreateRect("ContentSafeRect", panel.transform);
            Place(_studyPickerSafe, 16, 16, 1004, 480);
            Label(_studyPickerSafe, "PickerHeading", "유학 선수 선택", 22, 0, 0, 700, 32);
            Label(_studyPickerSafe, "PickerSubtitle", "시즌 중에는 1군 해제 후 신청 · 포스트시즌 종료 후에는 1군도 참가 가능", 12, 0, 34, 900, 24);
            Tab(_studyPickerSafe, "CancelStudyPickerTop", "닫기", () => CloseStudyPicker(false), false, 928, 0, 76, 32);
            Surface(_studyPickerSafe, "HeaderRule", 0, 64, 1004, 1, Border);
            BuildStudyPickerFilters();
            _studyPickerBody = OwnerRuntimeUiFactory.CreateRect("StudyPickerBody", _studyPickerSafe);
            Place(_studyPickerBody, 0, 152, 1004, 328);
            RefreshStudyPickerBody();
        }

        private void BuildStudyPickerFilters()
        {
            Image field = Surface(_studyPickerSafe, "StudySearch", 0, 76, 272, 32, Color.white);
            field.raycastTarget = true;
            _studySearch = field.gameObject.AddComponent<InputField>();
            _studySearch.targetGraphic = field;
            _studySearch.characterLimit = 40;
            _studySearch.textComponent = Label(field.transform, "Text", "", 13, 12, 0, 248, 32);
            Text hint = Label(field.transform, "Placeholder", "선수 이름 검색", 13, 12, 0, 248, 32);
            hint.color = new Color32(110, 121, 135, 255);
            _studySearch.placeholder = hint;
            _studySearch.lineType = InputField.LineType.SingleLine;
            _studySearch.textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;
            OwnerDashboardStyle.SetDataInput(_studySearch);
            _studySearch.SetTextWithoutNotify(_studyQuery);
            _studySearch.onValueChanged.AddListener(value => { _studyQuery = value; ChangeStudyPickerFilter(); });
            _studyPositions.Clear();
            var positions = new List<string> { _isPitcher ? "전체 투수 포지션" : "전체 야수 포지션" };
            foreach (PlayerPosition position in Enum.GetValues(typeof(PlayerPosition)))
            {
                bool pitcher = position == PlayerPosition.StartingPitcher || position == PlayerPosition.ReliefPitcher;
                if (pitcher != _isPitcher || !HasStudyPosition(position)) continue;
                _studyPositions.Add(position);
                positions.Add(OwnerCollectionPresentationBuilder.FormatPlayerRole(position, null, false));
            }
            _studyPosition = Mathf.Clamp(_studyPosition, 0, positions.Count - 1);
            AddStudyDropdown("StudyPosition", positions, _studyPosition, 280, 76, 180,
                value => { _studyPosition = value; ChangeStudyPickerFilter(); });
            AddStudyDropdown("StudyRegistration", new List<string> { "전체 등록 상태", "1군 미등록", "1군 등록" },
                _studyRegistration, 468, 76, 180, value => { _studyRegistration = value; ChangeStudyPickerFilter(); });
            AddStudyDropdown("StudySort", new List<string> { "코스트 높은 순", "코스트 낮은 순", "이름순", "최신 연도순" },
                _studySort, 656, 76, 228, value => { _studySort = value; ChangeStudyPickerFilter(); });
            Tab(_studyPickerSafe, "ResetStudyFilters", "초기화", ResetStudyPickerFilters, false, 892, 76, 112, 32);
            RectTransform origins = OwnerRuntimeUiFactory.CreateRect("StudyOriginFilters", _studyPickerSafe);
            Place(origins, 0, 116, 648, 28);
            OwnerWorkspaceUiFactory.AddHorizontalLayout(origins, 8);
            var cards = new List<OwnerCollectionCardSnapshot>();
            foreach (OwnerGrowthCardSnapshot card in _snapshot.Cards)
                if (IsPitcher(card.Card) == _isPitcher) cards.Add(card.Card);
            _studyOrigins.Build(origins, cards, ChangeStudyPickerFilter);
            Label(_studyPickerSafe, "StudyPickerGuide", "카드 선택 → 조건 확인 → 선수 선택 완료", 12, 664, 116, 340, 28);
        }

        private bool HasStudyPosition(PlayerPosition position)
        {
            foreach (OwnerGrowthCardSnapshot card in _snapshot.Cards)
                if (card.Card.Position == position) return true;
            return false;
        }

        private void AddStudyDropdown(string name, List<string> labels, int selected, float x, float y, float width, Action<int> changed)
        {
            Dropdown dropdown = OwnerCardFilters.CreateDropdown(_studyPickerSafe, name, labels, selected);
            Place(dropdown.GetComponent<RectTransform>(), x, y, width, 32);
            dropdown.onValueChanged.AddListener(value => changed(value));
        }

        private void ChangeStudyPickerFilter()
        {
            _studyPickerPage = 0;
            RefreshStudyPickerBody();
        }

        private void ResetStudyPickerFilters()
        {
            _studyQuery = string.Empty;
            _studyPosition = _studyRegistration = _studySort = _studyPickerPage = 0;
            _studyOrigins.Reset();
            Render();
            FocusRosterControl("StudySearch");
        }

        private List<OwnerGrowthCardSnapshot> GetStudyPickerCards()
        {
            var result = new List<OwnerGrowthCardSnapshot>();
            string query = _studyQuery.Trim();
            foreach (OwnerGrowthCardSnapshot item in _snapshot.Cards)
            {
                OwnerCollectionCardSnapshot card = item.Card;
                if (IsPitcher(card) != _isPitcher || !_studyOrigins.Matches(card)) continue;
                if (_studyPosition > 0 && card.Position != _studyPositions[_studyPosition - 1]) continue;
                if (_studyRegistration == 1 && card.IsActiveRoster || _studyRegistration == 2 && !card.IsActiveRoster) continue;
                if (query.Length > 0 && card.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                result.Add(item);
            }
            // 목록 탐색은 요약만 읽는다. 상세·유학 Preview는 오른쪽 선택 선수에만 요청한다.
            result.Sort((left, right) =>
            {
                int comparison = _studySort switch
                {
                    1 => left.Card.Cost.CompareTo(right.Card.Cost),
                    2 => StringComparer.CurrentCulture.Compare(left.Card.DisplayName, right.Card.DisplayName),
                    3 => right.Card.OriginYear.CompareTo(left.Card.OriginYear),
                    _ => right.Card.Cost.CompareTo(left.Card.Cost)
                };
                return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left.Card.CardId, right.Card.CardId);
            });
            return result;
        }

        private void RefreshStudyPickerBody()
        {
            OwnerRuntimeUiFactory.ClearChildren(_studyPickerBody);
            List<OwnerGrowthCardSnapshot> cards = GetStudyPickerCards();
            const int pageSize = 14;
            int pages = Math.Max(1, (cards.Count + pageSize - 1) / pageSize);
            _studyPickerPage = Mathf.Clamp(_studyPickerPage, 0, pages - 1);
            Label(_studyPickerBody, "StudyResultCount", $"{(_isPitcher ? "투수" : "야수")}  {cards.Count:N0}명", 13, 0, 0, 380, 24);
            Label(_studyPickerBody, "StudyPageCount", $"{_studyPickerPage + 1} / {pages} 페이지", 12, 500, 0, 200, 24).alignment = TextAnchor.MiddleRight;
            Surface(_studyPickerBody, "StudyCardSurface", 0, 28, 704, 248, new Color32(224, 230, 237, 255));
            int start = _studyPickerPage * pageSize;
            for (int index = start; index < Math.Min(cards.Count, start + pageSize); index++)
            {
                OwnerGrowthCardSnapshot candidate = cards[index];
                int slot = index - start;
                var card = PlayerMiniCardView.CreateRuntime(_studyPickerBody, "StudyCandidate_" + candidate.Card.CardId);
                card.UseLineupSlotLayout();
                card.UseRosterPresentation();
                Place(card.GetComponent<RectTransform>(), 8 + slot % 7 * 99, 34 + slot / 7 * 120, 94, 116);
                card.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(candidate.Card, candidate.Card.CardId == _studyDraftCardId));
                card.SetPortrait(PlayerPortraitSprites.GetDefault(candidate.Card.Position));
                card.Selected += _ =>
                {
                    _studyDraftCardId = candidate.Card.CardId;
                    RefreshStudyPickerBody();
                    FocusRosterControl("StudyCandidate_" + _studyDraftCardId);
                };
            }
            if (cards.Count == 0)
            {
                Text empty = Label(_studyPickerBody, "StudyEmpty", "조건에 맞는 선수가 없습니다.\n검색어나 필터를 바꿔 다시 찾아보세요.", 16, 32, 84, 640, 76);
                empty.alignment = TextAnchor.MiddleCenter;
                Tab(_studyPickerBody, "StudyEmptyReset", "필터 초기화", ResetStudyPickerFilters, false, 264, 180, 176, 36);
            }
            Button previous = Tab(_studyPickerBody, "StudyPreviousPage", "이전", () => ChangeStudyPickerPage(-1), false, 0, 288, 80, 36);
            previous.interactable = _studyPickerPage > 0;
            Button next = Tab(_studyPickerBody, "StudyNextPage", "다음", () => ChangeStudyPickerPage(1), false, 624, 288, 80, 36);
            next.interactable = _studyPickerPage + 1 < pages;
            bool visible = cards.Exists(card => card.Card.CardId == _studyDraftCardId);
            Label(_studyPickerBody, "StudySelectionHint", visible ? "선택한 선수는 오른쪽에서 확인할 수 있습니다."
                : "선택 선수는 현재 목록 밖에 있습니다.", 12, 92, 288, 520, 36).alignment = TextAnchor.MiddleCenter;
            RenderStudyPickerInspector();
        }

        private void RenderStudyPickerInspector()
        {
            Surface(_studyPickerBody, "InspectorRule", 720, 0, 1, 324, Border);
            OwnerGrowthCardSnapshot selected = null;
            foreach (OwnerGrowthCardSnapshot card in _snapshot.Cards)
                if (card.Card.CardId == _studyDraftCardId && IsPitcher(card.Card) == _isPitcher) selected = card;
            Label(_studyPickerBody, "StudyPreviewTitle", "선택 선수", 14, 736, 0, 268, 24);
            if (selected != null)
            {
                PlayerMiniCardView preview = PlayerMiniCardView.CreateRuntime(_studyPickerBody, "StudyPreviewCard");
                preview.UseLineupSlotLayout();
                Place(preview.GetComponent<RectTransform>(), 736, 32, 96, 132);
                preview.Bind(OwnerCollectionPresentationBuilder.CreateMiniCard(selected.DetailCard, false));
                preview.SetPortrait(PlayerPortraitSprites.GetDefault(selected.Card.Position));
                preview.GetComponent<CanvasGroup>().blocksRaycasts = false;
                preview.GetComponent<CanvasGroup>().interactable = false;
                Label(_studyPickerBody, "StudyPreviewName", selected.Card.DisplayName, 18, 844, 32, 160, 28);
                Label(_studyPickerBody, "StudyPreviewDetails", $"{selected.Card.OriginYear}년 · 코스트 {selected.Card.Cost}\n" +
                    OwnerCollectionPresentationBuilder.FormatPlayerRole(selected.Card.Position, selected.Card.PitcherRole, false) + "\n" +
                    (selected.Card.IsActiveRoster ? "1군 등록" : "1군 미등록"), 12, 844, 64, 160, 76);
                Label(_studyPickerBody, "StudyPreviewStatus", string.IsNullOrEmpty(selected.Card.StudyStatus)
                    ? "유학 이력 없음" : selected.Card.StudyStatus, 11, 844, 140, 160, 24);
                OwnerStudyOption option = Array.Find(selected.Studies, study => study.Program.ProgramId == _programId);
                string description = option == null ? "선수 선택 후 지도에서 유학지를 골라\n성장 효과와 비용을 확인하세요."
                    : option.Program.DisplayName + "\n" + option.RewardText + $"\n{option.Program.DurationWeeks}주 · {option.CostText}";
                Label(_studyPickerBody, "StudyPreviewProgram", description, 12, 736, 168, 268, 72);
                string reason = selected.Card.IsActiveRoster ? "신청 전 선수단에서 1군 등록을 해제하세요."
                    : option != null ? (option.CanStart ? "신청 가능한 과정입니다." : option.BlockedReason.Length > 0 ? option.BlockedReason : option.UnlockText)
                    : "신청은 포스트시즌 종료 후 가능합니다.";
                Label(_studyPickerBody, "StudyPreviewReason", reason, 11, 736, 240, 268, 40);
            }
            else Label(_studyPickerBody, "StudyNoSelection", "목록에서 선수를 선택하세요.", 14, 736, 56, 268, 80);
            Tab(_studyPickerBody, "CancelStudyPicker", "취소", () => CloseStudyPicker(false), false, 736, 288, 76, 36);
            Button confirm = Tab(_studyPickerBody, "CloseStudyPlayerPicker", "선수 선택 완료", () => CloseStudyPicker(true), true, 820, 288, 184, 36);
            confirm.interactable = selected != null;
        }

        private void ChangeStudyPickerPage(int delta)
        {
            _studyPickerPage += delta;
            RefreshStudyPickerBody();
            string name = delta > 0 ? "StudyNextPage" : "StudyPreviousPage";
            Button button = _studyPickerBody.Find(name).GetComponent<Button>();
            if (button.interactable) button.Select();
            else FocusRosterControl(delta > 0 ? "StudyPreviousPage" : "StudyNextPage");
        }

        private void CloseStudyPicker(bool apply)
        {
            if (apply) _cardId = _studyDraftCardId;
            _isChoosingStudyPlayer = false;
            _pendingStudy = string.Empty;
            Render();
            FocusRosterControl("ChooseStudyPlayer");
        }
    }
}
