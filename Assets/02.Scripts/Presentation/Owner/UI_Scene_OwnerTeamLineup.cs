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
        private static readonly Color Paper = OwnerDashboardStyle.TableSurface;
        private OwnerTeamLineupSnapshot _snapshot;
        private bool _canClose;
        private string _closeLabel;
        private string _disclosure;
        private Button _closeButton;
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
            _disclosure = disclosure ?? "카드 선택: 선수 상세 · 공개 등록 기준 · 경기 중 교체에 따라 출전 선수가 달라질 수 있습니다.";
            Render();
        }

        /// <summary>셸 작업 영역 전환에 맞춰 표시한다.</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        /// <summary>내부 배치와 무관하게 현재 라인업의 돌아가기 버튼에 포커스를 둔다.</summary>
        public void FocusClose()
        {
            if (_closeButton != null && _closeButton.IsActive() && _closeButton.IsInteractable())
                _closeButton.Select();
        }

        private void Render()
        {
            _closeButton = null;
            OwnerRuntimeUiFactory.ClearChildren(transform);
            var root = (RectTransform)transform;
            UIOwnerFrontOfficePanel.ApplyWorkspace(root);
            var content = OwnerRuntimeUiFactory.CreateRect("LineupContent", root);
            OwnerRuntimeUiFactory.Stretch(content, new Vector2(20, 16), new Vector2(-20, -16));
            root = content;
            Label(root, "TeamName", _snapshot.TeamName + " · 라인업", .01f, .925f, .74f, 1, 22);
            if (_canClose)
            {
                var close = OwnerRuntimeUiFactory.CreateReferenceButton("CloseLineup", root, _closeLabel);
                _closeButton = close;
                Place((RectTransform)close.transform, .79f, .925f, .99f, 1);
                OwnerUiButtonSkin.Apply(close, OwnerButtonRole.Quiet);
                close.onClick.AddListener(() => CloseRequested?.Invoke());
            }
            var board = OwnerRuntimeUiFactory.CreateRect("LineupBoard", root);
            var aspect = board.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 2.4f;
            // FitInParent는 부모 전체를 사용하므로 별도 여백 Host 안에서 비율을 유지한다.
            var host = OwnerRuntimeUiFactory.CreateRect("BoardHost", root);
            Place(host, 0, .08f, 1, .91f);
            board.SetParent(host, false);
            var header = Surface(board, "Header", Paper, 0, .93f, 1, 1);
            UIOwnerFrontOfficePanel.Apply(header, "CompactStrip");
            Label(header, "LineupName", _snapshot.LineupName, .015f, .1f, .76f, .9f, 15);
            Label(header, "Cost", _snapshot.CostLabel, .79f, .1f, .985f, .9f, 15).alignment = TextAnchor.MiddleRight;
            RenderRow(board, "Hitters", "야수", _snapshot.Hitters, _snapshot.HitterDetails, .55f, .915f, false);
            RenderRow(board, "Pitchers", "투수", _snapshot.Pitchers, _snapshot.PitcherDetails, .17f, .535f, true);
            RenderTeamColors(board);
            Label(root, "Disclosure", _disclosure,
                .01f, 0, .99f, .065f, 13);
        }

        private void RenderRow(RectTransform board, string name, string label,
            IReadOnlyList<PlayerMiniCardModel> cards,
            IReadOnlyList<OwnerCollectionCardSnapshot> details,
            float bottom, float top, bool pitcher)
        {
            var row = Surface(board, name, Paper, 0, bottom, 1, top);
            OwnerDashboardStyle.ApplyInset(row.GetComponent<Image>());
            Label(row, "Role", label, .012f, .87f, .09f, .99f, 15);
            if (pitcher)
            {
                Label(row, "Starters", "선발 로테이션", .10f, .87f, .43f, .99f, 12);
                Label(row, "Relievers", "불펜", .47f, .87f, .78f, .99f, 12);
                Label(row, "Finishers", "셋업 · 마무리", .84f, .87f, .99f, .99f, 12);
            }
            else
            {
                Label(row, "BattingOrder", "선발 타순", .10f, .87f, .63f, .99f, 12);
                Label(row, "Bench", "벤치", .66f, .87f, .99f, .99f, 12);
            }
            if (cards.Count == 0)
                Label(row, "EmptyRoster", "공개 등록된 선수가 없습니다", .02f, .15f, .98f, .75f, 16);
            for (int i = 0; i < cards.Count; i++)
            {
                // 야수는 주전 9 + 벤치 5, 투수는 선발 5 + 불펜 4 + 셋업 + 마무리의 그룹 간격을 둔다.
                float x = pitcher ? .03f + i * .083f + (i >= 5 ? .025f : 0) + (i >= 9 ? .025f : 0) :
                    .03f + i * .0685f + (i >= 9 ? .01f : 0);
                float width = pitcher ? .072f : .063f;
                var inner = OwnerRuntimeUiFactory.CreateRect("Slot_" + i, row);
                Place(inner, x, .035f, x + width, .85f);
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
                card.SetPrimaryClickForDetail(detail != null);
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
                UI_Popup_OwnerPlayerCard.ShowLineup(transform, available, selectedIndex);
        }

        private void RenderTeamColors(RectTransform board)
        {
            var row = Surface(board, "TeamColors", Paper, 0, 0, 1, .155f);
            OwnerDashboardStyle.ApplyInset(row.GetComponent<Image>());
            Label(row, "Role", "적용 팀컬러", .015f, .66f, .98f, .96f, 14);
            if (_snapshot.TeamColors.Count == 0)
                Label(row, "Empty", "적용된 팀컬러 없음", .02f, .1f, .98f, .6f, 14);
            for (int i = 0; i < _snapshot.TeamColors.Count; i++)
            {
                float x = .015f + i * .49f;
                var card = Surface(row, "TeamColor_" + i, Paper, x, .07f, x + .475f, .64f);
                UIOwnerFrontOfficePanel.Apply(card, "CompactStrip");
                OwnerTeamColorCandidateSnapshot candidate = i < _snapshot.TeamColorCards.Count
                    ? _snapshot.TeamColorCards[i]
                    : null;
                Label(card, "Name", candidate?.Name ?? _snapshot.TeamColors[i], .025f, .35f, .77f, .90f, 14);
                Label(card, "Progress", candidate?.ProgressText ?? string.Empty, .025f, .08f, .77f, .40f, 12);
                Label(card, "Grade", candidate?.Grade ?? string.Empty, .80f, .2f, .96f, .8f, 20)
                    .alignment = TextAnchor.MiddleCenter;
            }
        }

        private static RectTransform Surface(RectTransform parent, string name, Color color,
            float left, float bottom, float right, float top)
        {
            var image = OwnerRuntimeUiFactory.CreateImage(name, parent, color);
            OwnerDashboardStyle.SetDataSurface(image, color);
            Place(image.rectTransform, left, bottom, right, top);
            return image.rectTransform;
        }

        private static Text Label(RectTransform parent, string name, string value,
            float left, float bottom, float right, float top, int size)
        {
            var text = OwnerRuntimeUiFactory.CreateText(name, parent, value, size, FontStyle.Normal,
                TextAnchor.MiddleLeft, OwnerDashboardStyle.Ivory);
            OwnerDashboardStyle.SetDataText(text, size >= 14);
            Place(text.rectTransform, left, bottom, right, top);
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
