using System;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>특성훈련의 확정 등급을 표시하는 계약이며 카드 종류·성장 블록 희귀도와 구분한다.</summary>
    public enum PlayerTraitBadgeRank { None, C, B, A, S }

    /// <summary>카드에 남는 유학 이력과 현재 진행 상태를 구분한다.</summary>
    public enum PlayerStudyBadgeState { None, InProgress, Completed }
    /// <summary>장착 블록의 Normal부터 Legendary까지 희귀도를 일대일로 표시한다.</summary>
    public enum PlayerBoardBadgeRank { None, N, R, E, U, L }

    /// <summary>카드 표시만 담당하는 불변 성장 배지 모델이다. 경험치로 등급을 추정하지 않는다.</summary>
    public sealed class PlayerCardGrowthBadgeModel
    {
        public static readonly PlayerCardGrowthBadgeModel Empty = new PlayerCardGrowthBadgeModel();

        public PlayerCardGrowthBadgeModel(PlayerStudyBadgeState studyState = PlayerStudyBadgeState.None,
            string studyDescription = null, PlayerTraitBadgeRank traitRank = PlayerTraitBadgeRank.None,
            string traitDescription = null, int supportGames = 0, string supportDescription = "", PlayerBoardBadgeRank boardRank = PlayerBoardBadgeRank.None, string boardDescription = "")
        {
            if (!Enum.IsDefined(typeof(PlayerStudyBadgeState), studyState))
                throw new ArgumentOutOfRangeException(nameof(studyState));
            if (!Enum.IsDefined(typeof(PlayerTraitBadgeRank), traitRank))
                throw new ArgumentOutOfRangeException(nameof(traitRank));
            if (traitRank != PlayerTraitBadgeRank.None && string.IsNullOrWhiteSpace(traitDescription))
                throw new ArgumentException("특성명과 핵심 효과 설명이 필요합니다.", nameof(traitDescription));
            StudyState = studyState;
            BoardRank = boardRank;
            if (!Enum.IsDefined(typeof(PlayerBoardBadgeRank), boardRank))
                throw new ArgumentOutOfRangeException(nameof(boardRank));
            BoardDescription = boardDescription ?? "";
            TraitRank = traitRank;
            if (supportGames < 0 || supportGames > 2) throw new ArgumentOutOfRangeException(nameof(supportGames));
            SupportGames = supportGames;
            SupportDescription = supportGames == 0 ? "" : (string.IsNullOrWhiteSpace(supportDescription) ? "서포트 적용" : supportDescription) + " · " + supportGames + "경기 남음";
            StudyDescription = studyState == PlayerStudyBadgeState.None ? string.Empty :
                string.IsNullOrWhiteSpace(studyDescription)
                    ? studyState == PlayerStudyBadgeState.InProgress ? "유학 중" : "유학 완료"
                    : studyDescription.Trim();
            TraitDescription = traitRank == PlayerTraitBadgeRank.None ? string.Empty :
                FormatTraitDescription(traitDescription.Trim(), traitRank);
        }

        private static string FormatTraitDescription(string description, PlayerTraitBadgeRank rank)
        {
            int separator = description.IndexOf(" · ", StringComparison.Ordinal);
            int nameEnd = separator >= 0 ? separator : description.Length;
            return description.Insert(nameEnd, "(" + rank + "등급)");
        }

        public PlayerStudyBadgeState StudyState { get; }
        public PlayerTraitBadgeRank TraitRank { get; }
        public string StudyDescription { get; }
        public string TraitDescription { get; }
        public bool HasStudy => StudyState != PlayerStudyBadgeState.None;
        public bool HasTrait => TraitRank != PlayerTraitBadgeRank.None;
        public int SupportGames { get; }
        public string SupportDescription { get; }
        public bool HasSupport => SupportGames > 0;
        public PlayerBoardBadgeRank BoardRank { get; }
        public string BoardDescription { get; }
        public bool HasBoard => BoardRank != PlayerBoardBadgeRank.None;
        public string Description => (HasStudy && HasTrait ? TraitDescription + "\n" + StudyDescription :
            HasTrait ? TraitDescription : StudyDescription) + (HasSupport ? (HasStudy || HasTrait ? "\n" : "") + SupportDescription : "")
            + (HasBoard ? (HasStudy || HasTrait || HasSupport ? "\n" : "") + "스킬블록 " + BoardRank + " 등급 · " + BoardDescription : "");
    }
}
