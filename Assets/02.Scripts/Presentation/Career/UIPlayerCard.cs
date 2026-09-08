using System;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Presentation.Owner;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>선수 카드에 표시할 대표 수상 프레임을 구분한다.</summary>
    public enum PlayerCardSpecialType { None, AllStar, Mvp, GoldenGlove }

    /// <summary>선수 커리어 읽기 모델을 현행 공용 카드 앞면·뒷면으로 연결한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UIPlayerCard : MonoBehaviour
    {
        private RectTransform _front;
        private RectTransform _back;
        private PlayerProfileView _profile;
        private string _roleLabel;

        public bool IsShowingBack { get; private set; }
        public PlayerCardSpecialType SpecialType { get; private set; }

        /// <summary>현행 카드 렌더러를 사용하는 클릭 가능한 선수 카드를 생성한다.</summary>
        public static UIPlayerCard CreateRuntime(Transform parent, Vector2 size, Vector2 position)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            RectTransform root = OwnerRuntimeUiFactory.CreateRect("Card", parent);
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
            root.sizeDelta = size;
            root.anchoredPosition = position;
            Image input = root.gameObject.AddComponent<Image>();
            input.color = Color.clear;
            input.raycastTarget = true;
            var card = root.gameObject.AddComponent<UIPlayerCard>();
            card._front = OwnerRuntimeUiFactory.CreateRect("Front", root);
            card._back = OwnerRuntimeUiFactory.CreateRect("Back", root);
            OwnerRuntimeUiFactory.Stretch(card._front);
            OwnerRuntimeUiFactory.Stretch(card._back);
            Button flip = root.gameObject.AddComponent<Button>();
            flip.targetGraphic = input;
            flip.transition = Selectable.Transition.None;
            flip.onClick.AddListener(() => card.SetShowingBack(!card.IsShowingBack));
            card.SetShowingBack(false);
            return card;
        }

        /// <summary>현재 선수의 실제 능력치·시즌 기록·투타를 카드에 표시한다.</summary>
        public void Bind(PlayerProfileView view, string roleLabel)
        {
            _profile = view ?? throw new ArgumentNullException(nameof(view));
            _roleLabel = roleLabel ?? string.Empty;
            Render();
            SetShowingBack(false);
        }

        /// <summary>별도 장식 없이 현행 수상 Edition 프레임을 선택한다.</summary>
        public void SetSpecialType(PlayerCardSpecialType specialType)
        {
            if (!Enum.IsDefined(typeof(PlayerCardSpecialType), specialType))
                throw new ArgumentOutOfRangeException(nameof(specialType));
            SpecialType = specialType;
            if (_profile != null) Render();
        }

        /// <summary>카드 앞면과 뒷면 중 하나를 표시한다.</summary>
        public void SetShowingBack(bool isShowingBack)
        {
            IsShowingBack = isShowingBack;
            _front.gameObject.SetActive(!isShowingBack);
            _back.gameObject.SetActive(isShowingBack);
        }

        private void Render()
        {
            OwnerRuntimeUiFactory.ClearChildren(_front);
            OwnerRuntimeUiFactory.ClearChildren(_back);
            bool pitcher = _profile.PlayerType == PlayerType.Pitcher;
            OwnerCollectionCardSnapshot snapshot = CreateSnapshot(_profile, pitcher);
            UI_Popup_OwnerPlayerCard.BuildFrontCard(_front, snapshot);
            UI_Popup_OwnerPlayerCard.BuildReferenceBack(_back, snapshot, pitcher);
            // 선수 커리어에는 카드 Cost가 없으므로 같은 푸터에 실제 종합 능력치를 표시한다.
            _front.Find("CostStars")?.gameObject.SetActive(false);
            SetText(_front, "CostLabel", "OVR");
            SetText(_front, "Cost", _profile.Overall.ToString());
            SetText(_back, "Profile", GetHands(_profile) + "\n" + _roleLabel + "\nOVR " + _profile.Overall);
            SetText(_back, "PublicInformationHeading", "선수 커리어");
            SetText(_back, "PublicInformation/State", _profile.TeamName + "\n" + _roleLabel +
                "\n성장 계획에서 훈련과 스킬 블록을 확인할 수 있습니다.");
            SetShowingBack(IsShowingBack);
        }

        private OwnerCollectionCardSnapshot CreateSnapshot(PlayerProfileView view, bool pitcher)
        {
            var values = new int[PlayerAbilityCatalog.AbilityCount];
            for (int index = 0; index < values.Length; index++) values[index] = AbilityRatings.Minimum;
            if (view.Abilities != null)
                for (int index = 0; index < view.Abilities.Length; index++)
                {
                    PlayerProfileAbilityView ability = view.Abilities[index];
                    values[(int)ability.Ability] = Mathf.Clamp(ability.StableValue, AbilityRatings.Minimum, AbilityRatings.Maximum);
                }
            string playerId = view.PlayerId.ToString();
            return new OwnerCollectionCardSnapshot(
                "Career:" + playerId, playerId, view.PlayerName, view.SeasonYear, view.Position,
                0, GetEdition(), 0, 0, false, false, new AbilityRatings(values),
                currentLeagueLabel: view.SeasonYear + " 시즌 기록",
                throws: view.ThrowingHand, bats: view.BattingHand,
                seasonRecord: CreateSeasonRecord(view.SeasonStatistics, pitcher),
                teamDisplayName: view.TeamName, isOwnedCard: false);
        }

        private PlayerCardEdition GetEdition()
        {
            return SpecialType switch
            {
                PlayerCardSpecialType.AllStar => PlayerCardEdition.AllStar,
                PlayerCardSpecialType.Mvp => PlayerCardEdition.Mvp,
                PlayerCardSpecialType.GoldenGlove => PlayerCardEdition.GoldenGlove,
                _ => PlayerCardEdition.Normal
            };
        }

        private static OwnerCardRecordFieldSnapshot[] CreateSeasonRecord(PlayerProfileStatisticsView stats, bool pitcher)
        {
            return pitcher
                ? new[] {
                    new OwnerCardRecordFieldSnapshot("등판", stats.PitchingAppearances.ToString()),
                    new OwnerCardRecordFieldSnapshot("승", stats.Wins.ToString()),
                    new OwnerCardRecordFieldSnapshot("패", stats.Losses.ToString()),
                    new OwnerCardRecordFieldSnapshot("ERA", stats.EarnedRunAverage.ToString("0.00")),
                    new OwnerCardRecordFieldSnapshot("삼진", stats.PitchingStrikeouts.ToString()) }
                : new[] {
                    new OwnerCardRecordFieldSnapshot("경기", stats.GamesPlayed.ToString()),
                    new OwnerCardRecordFieldSnapshot("타율", stats.BattingAverage.ToString("0.000")),
                    new OwnerCardRecordFieldSnapshot("홈런", stats.HomeRuns.ToString()),
                    new OwnerCardRecordFieldSnapshot("타점", stats.RunsBattedIn.ToString()),
                    new OwnerCardRecordFieldSnapshot("OPS", stats.OnBasePlusSlugging.ToString("0.000")) };
        }

        private static string GetHands(PlayerProfileView view)
        {
            string throwing = view.ThrowingHand == Handedness.Left ? "좌투" : "우투";
            string batting = view.BattingHand == Handedness.Left ? "좌타" :
                view.BattingHand == Handedness.Right ? "우타" : "양타";
            return throwing + " " + batting;
        }

        private static void SetText(Transform parent, string path, string value)
        {
            Text text = parent.Find(path)?.GetComponent<Text>();
            if (text != null) text.text = value;
        }
    }
}
