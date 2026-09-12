using System;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    public sealed partial class UI_Popup_OwnerPlayerCard
    {
        public static event Action<string> TraitTrainingRequested;
        private Button _traitTrainingButton;
        private Button _growthHistoryButton;
        private RectTransform _growthHistoryRoot;
        private Text _growthHistoryText;
        private Text _growthHistoryPage;
        private Button _historyPrevious;
        private Button _historyNext;
        private string[] _growthHistoryEntries = Array.Empty<string>();
        private int _growthHistoryIndex;
        private const int HistoryEntriesPerPage = 2;

        private void BuildGrowthHistory(RectTransform root)
        {
            _growthHistoryButton = CreateNavigationButton(root, "GrowthHistory", "성장 이력", .5f, 0, .5f, 0, ToggleGrowthHistory);
            _growthHistoryRoot = Surface(_cardRoot, "GrowthHistoryPage", new Color32(237, 239, 240, 255), 0, 0, 1, 1);
            _growthHistoryRoot.GetComponent<Image>().raycastTarget = true;
            // 카드 뒤집기 이벤트가 이력 화면까지 전파되지 않게 별도 선택 대상을 둔다.
            _growthHistoryRoot.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            OwnerDashboardStyle.SetDataSurface(_growthHistoryRoot.GetComponent<Image>(), OwnerDashboardStyle.TableSurface, true);
            var title = Label(_growthHistoryRoot, "Title", "성장 이력", .07f, .88f, .52f, .97f, 24, Ink);
            OwnerDashboardStyle.SetDataText(title,true);
            _traitTrainingButton = CreateNavigationButton(_growthHistoryRoot, "TraitTraining", "특성훈련", .56f, .88f, .93f, .97f,
                () => { string id = _cards[_cardIndex].CardId; Close(); TraitTrainingRequested?.Invoke(id); });
            _growthHistoryText = Label(_growthHistoryRoot, "Entries", "", .07f, .19f, .93f, .84f, 17, Ink);
            _growthHistoryText.alignment = TextAnchor.UpperLeft;
            OwnerDashboardStyle.SetDataText(_growthHistoryText);
            _growthHistoryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _growthHistoryPage = Label(_growthHistoryRoot, "Page", "", .35f, .05f, .65f, .13f, 16, Ink);
            OwnerDashboardStyle.SetDataText(_growthHistoryPage);
            _historyPrevious = CreateNavigationButton(_growthHistoryRoot, "PreviousPage", "이전", .07f, .05f, .30f, .13f,
                () => { _growthHistoryIndex--; RefreshGrowthHistory(); });
            _historyNext = CreateNavigationButton(_growthHistoryRoot, "NextPage", "다음", .70f, .05f, .93f, .13f,
                () => { _growthHistoryIndex++; RefreshGrowthHistory(); });
            _growthHistoryRoot.gameObject.SetActive(false);
        }

        private void BindGrowthHistory(OwnerCollectionCardSnapshot card)
        {
            _growthHistoryEntries = (string.IsNullOrEmpty(card.GrowthHistory) ? "아직 완료한 성장 과정이 없습니다." : card.GrowthHistory)
                .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            _growthHistoryIndex = 0;
            if (card.GrowthBadges.HasTrait)
            {
                var entries = new string[_growthHistoryEntries.Length + 1];
                entries[0] = "현재 특성\n" + card.GrowthBadges.TraitDescription;
                Array.Copy(_growthHistoryEntries,0,entries,1,_growthHistoryEntries.Length); _growthHistoryEntries = entries;
            }
            _traitTrainingButton.interactable = card.IsOwnedCard && TraitTrainingRequested != null;
            _growthHistoryButton.interactable = card.IsOwnedCard;
            RefreshGrowthHistory();
        }

        private void ToggleGrowthHistory()
        {
            _growthHistoryRoot.gameObject.SetActive(!_growthHistoryRoot.gameObject.activeSelf);
            _growthHistoryButton.GetComponentInChildren<Text>().text = _growthHistoryRoot.gameObject.activeSelf ? "선수 카드로" : "성장 이력";
        }

        private void RefreshGrowthHistory()
        {
            int pages = Math.Max(1, (_growthHistoryEntries.Length + HistoryEntriesPerPage - 1) / HistoryEntriesPerPage);
            _growthHistoryIndex = Math.Max(0, Math.Min(pages - 1, _growthHistoryIndex));
            int start = _growthHistoryIndex * HistoryEntriesPerPage;
            _growthHistoryText.text = string.Join("\n\n", _growthHistoryEntries, start,
                Math.Min(HistoryEntriesPerPage, _growthHistoryEntries.Length - start));
            _growthHistoryPage.text = (_growthHistoryIndex + 1) + " / " + pages;
            _historyPrevious.interactable = _growthHistoryIndex > 0;
            _historyNext.interactable = _growthHistoryIndex + 1 < pages;
        }
    }
}
