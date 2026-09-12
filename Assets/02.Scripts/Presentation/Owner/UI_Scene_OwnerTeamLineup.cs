using System;
using System.Collections.Generic;
using Baseball.Presentation.SharedUI;
using Baseball.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>경기 준비와 순위표가 공유하는 읽기 전용 구단 라인업 카드 보드다.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_Scene_OwnerTeamLineup : MonoBehaviour
    {
        private static readonly Color Paper = new Color32(246, 247, 245, 255);
        private static readonly Color Grid = new Color32(184, 190, 193, 255);
        private OwnerTeamLineupSnapshot _snapshot;
        private bool _canClose;
        private string _closeLabel;
        private string _disclosure;
        public event Action CloseRequested;
        public OwnerTeamLineupSnapshot Snapshot => _snapshot;

        /// <summary>지정한 작업 영역에 카드 보드를 만든다.</summary>
        public static UI_Scene_OwnerTeamLineup CreateRuntime(RectTransform host)
        {
            return OwnerRuntimeUiFactory.CreateRect(nameof(UI_Scene_OwnerTeamLineup), host)
                .gameObject.AddComponent<UI_Scene_OwnerTeamLineup>();
        }

        /// <summary>선택한 구단의 공개 데이터와 호출 위치에 맞는 닫기 동작을 표시한다.</summary>
        public void Bind(OwnerTeamLineupSnapshot snapshot, bool canClose = false,
            string closeLabel = "순위표로 돌아가기", string disclosure = null)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _canClose = canClose;
            _closeLabel = closeLabel;
            _disclosure = disclosure ?? "카드 우클릭: 선수 상세 · 공개 등록 기준 · 경기 중 교체에 따라 출전 선수가 달라질 수 있습니다.";
            Render();
        }

        /// <summary>셸 작업 영역 전환에 맞춰 표시한다.</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        private void Render()
        {
            OwnerRuntimeUiFactory.ClearChildren(transform);
            var root = (RectTransform)transform;
            Surface(root, "Paper", Paper, 0, 0, 1, 1);
            Label(root, "TeamName", _snapshot.TeamName + " · 라인업", .02f, .9f, .7f, .98f, 22);
            if (_canClose)
            {
                var close = OwnerRuntimeUiFactory.CreateReferenceButton("CloseLineup", root, _closeLabel);
                Place((RectTransform)close.transform, .79f, .91f, .98f, .98f);
                close.onClick.AddListener(() => CloseRequested?.Invoke());
            }
            var board = OwnerRuntimeUiFactory.CreateRect("LineupBoard", root);
            Place(board, .014f, .13f, .986f, .88f);
            var aspect = board.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 2.65f;
            // FitInParent는 부모 전체를 사용하므로 별도 여백 Host 안에서 비율을 유지한다.
            var host = OwnerRuntimeUiFactory.CreateRect("BoardHost", root);
            Place(host, .014f, .13f, .986f, .88f);
            board.SetParent(host, false);
            Surface(board, "Header", new Color32(222, 227, 229, 255), 0, .92f, 1, 1);
            Label(board, "LineupName", "라인업 명칭   " + _snapshot.LineupName, .01f, .925f, .76f, .995f, 15);
            Label(board, "Cost", _snapshot.CostLabel, .79f, .925f, .99f, .995f, 15);
            RenderRow(board, "Hitters", "야\n수", _snapshot.Hitters, _snapshot.HitterDetails, .545f, .915f, false);
            RenderRow(board, "Pitchers", "투\n수", _snapshot.Pitchers, _snapshot.PitcherDetails, .17f, .54f, true);
            RenderTeamColors(board);
            Label(root, "Disclosure", _disclosure,
                .02f, .025f, .98f, .09f, 14);
        }

        private void RenderRow(RectTransform board, string name, string label,
            IReadOnlyList<PlayerMiniCardModel> cards,
            IReadOnlyList<OwnerCollectionCardSnapshot> details,
            float bottom, float top, bool pitcher)
        {
            var row = Surface(board, name, Color.white, 0, bottom, 1, top);
            Label(row, "Role", label, 0, .1f, .028f, .9f, 14);
            for (int i = 0; i < cards.Count; i++)
            {
                // 야수는 주전 9 + 벤치 5, 투수는 선발 5 + 불펜 4 + 셋업 + 마무리의 그룹 간격을 둔다.
                float x = pitcher ? .03f + i * .083f + (i >= 5 ? .025f : 0) + (i >= 9 ? .025f : 0) :
                    .03f + i * .0685f + (i >= 9 ? .01f : 0);
                float width = pitcher ? .072f : .063f;
                var slot = Surface(row, "Slot_" + i, Grid, x, .02f, x + width, .98f);
                var inner = Surface(slot, "Inset", pitcher ? new Color32(253, 251, 228, 255) : Paper,
                    .012f, .008f, .988f, .992f);
                if (cards[i] == null)
                {
                    Label(inner, "Empty", "미등록", 0, 0, 1, 1, 12);
                    continue;
                }
                var card = PlayerMiniCardView.CreateRuntime(inner, "Player_" + i);
                OwnerCollectionCardSnapshot detail = i < details.Count ? details[i] : null;
                card.Bind(cards[i], PlayerPortraitSprites.GetDefault(
                    detail?.Position ?? (pitcher
                        ? Baseball.Core.Players.PlayerPosition.StartingPitcher
                        : Baseball.Core.Players.PlayerPosition.DesignatedHitter)));
                card.UseLineupSlotLayout();
                card.DetailRequested += selected => ShowCardDetail(selected, details);
                Place((RectTransform)card.transform, .035f, .025f, .965f, .985f);
            }
        }

        private void ShowCardDetail(
            PlayerMiniCardModel selected,
            IReadOnlyList<OwnerCollectionCardSnapshot> details)
        {
            var available = new List<OwnerCollectionCardSnapshot>(details.Count);
            int selectedIndex = -1;
            for (int index = 0; index < details.Count; index++)
            {
                OwnerCollectionCardSnapshot detail = details[index];
                if (detail == null) continue;
                if (string.Equals(detail.CardId, selected.PlayerId, StringComparison.Ordinal))
                    selectedIndex = available.Count;
                available.Add(detail);
            }
            if (selectedIndex >= 0)
                UI_Popup_OwnerPlayerCard.Show(transform, available, selectedIndex);
        }

        private void RenderTeamColors(RectTransform board)
        {
            var row = Surface(board, "TeamColors", Color.white, 0, 0, 1, .165f);
            Label(row, "Role", "팀\n컬\n러", 0, .04f, .028f, .96f, 12);
            for (int i = 0; i < _snapshot.TeamColors.Count; i++)
            {
                float x = .12f + i * .47f;
                Button button = OwnerDugoutDetailUiFactory.CreateButton(
                    row,
                    "TeamColor_" + i,
                    string.Empty,
                    x,
                    .08f,
                    x + .34f,
                    .92f,
                    null);
                button.interactable = false;
                OwnerTeamColorCandidateSnapshot candidate = i < _snapshot.TeamColorCards.Count
                    ? _snapshot.TeamColorCards[i]
                    : null;
                OwnerTeamColorCardView.Attach(button).Bind(
                    candidate,
                    candidate != null ? candidate.ProgressText : _snapshot.TeamColors[i],
                    false,
                    candidate != null);
            }
        }

        private static RectTransform Surface(RectTransform parent, string name, Color color,
            float left, float bottom, float right, float top)
        {
            var image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            Place(image.rectTransform, left, bottom, right, top);
            return image.rectTransform;
        }

        private static Text Label(RectTransform parent, string name, string value,
            float left, float bottom, float right, float top, int size)
        {
            var text = OwnerRuntimeUiFactory.CreateText(name, parent, value, size, FontStyle.Bold,
                TextAnchor.MiddleLeft, CareerUiTheme.ReferenceDataInk);
            Place(text.rectTransform, left, bottom, right, top);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = size;
            return text;
        }

        private static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
