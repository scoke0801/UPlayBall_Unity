using System;
using System.Collections.Generic;

namespace Baseball.Game.Career.Narrative
{
    /// <summary>중요 경기 뒤 짧은 반응이 발생한 이유를 저장한다.</summary>
    public enum CareerReactionTrigger
    {
        CareerDebut,
        FirstCareerHit,
        FirstCareerHomeRun,
        ImportantWin,
        ImportantLoss,
        SlumpStarted,
        SlumpEnded,
        RoleAtRisk,
        TradeRumor,
        ContractOffer
    }

    /// <summary>반응 이벤트에서 질문하거나 메시지를 보내는 주체다.</summary>
    public enum CareerReactionSpeaker
    {
        Manager,
        Teammate,
        Reporter
    }

    /// <summary>선수가 선택할 수 있는 일관된 답변 태도다.</summary>
    public enum CareerResponseStyle
    {
        Confident,
        Accountable,
        TeamFirst
    }

    /// <summary>답변 하나가 커리어 관계 지표에 주는 작은 변화를 고정한다.</summary>
    public readonly struct CareerReactionEffect
    {
        public CareerReactionEffect(
            int managerTrust,
            int confidence,
            int mediaStanding,
            int fanSupport,
            int teamChemistry)
        {
            ManagerTrust = managerTrust;
            Confidence = confidence;
            MediaStanding = mediaStanding;
            FanSupport = fanSupport;
            TeamChemistry = teamChemistry;
        }

        public int ManagerTrust { get; }
        public int Confidence { get; }
        public int MediaStanding { get; }
        public int FanSupport { get; }
        public int TeamChemistry { get; }
    }

    /// <summary>질문에 제시되는 답변 문구와 효과를 함께 저장한다.</summary>
    public sealed class CareerReactionOptionState
    {
        public CareerReactionOptionState(
            CareerResponseStyle style,
            string text,
            CareerReactionEffect effect)
        {
            Style = style;
            Text = text ?? string.Empty;
            Effect = effect;
        }

        public CareerResponseStyle Style { get; }
        public string Text { get; }
        public CareerReactionEffect Effect { get; }
    }

    /// <summary>중요 사건 하나와 선택 결과를 다시 열어도 바뀌지 않게 보관한다.</summary>
    public sealed class CareerReactionEventState
    {
        private readonly CareerReactionOptionState[] _options;

        public CareerReactionEventState(
            string reactionId,
            int seasonId,
            int round,
            int gameId,
            CareerReactionTrigger trigger,
            CareerReactionSpeaker speaker,
            string speakerName,
            string prompt,
            CareerReactionOptionState[] options)
        {
            if (string.IsNullOrWhiteSpace(reactionId))
                throw new ArgumentException("ReactionId가 비어 있습니다.", nameof(reactionId));
            if (seasonId <= 0 || round < 0 || gameId < 0)
                throw new ArgumentOutOfRangeException(nameof(seasonId));
            if (options == null || options.Length != 3)
                throw new ArgumentException("반응 선택지는 정확히 3개여야 합니다.", nameof(options));
            ReactionId = reactionId;
            SeasonId = seasonId;
            Round = round;
            GameId = gameId;
            Trigger = trigger;
            Speaker = speaker;
            SpeakerName = speakerName ?? string.Empty;
            Prompt = prompt ?? string.Empty;
            _options = (CareerReactionOptionState[])options.Clone();
            SelectedOptionIndex = -1;
        }

        public string ReactionId { get; }
        public int SeasonId { get; }
        public int Round { get; }
        public int GameId { get; }
        public CareerReactionTrigger Trigger { get; }
        public CareerReactionSpeaker Speaker { get; }
        public string SpeakerName { get; }
        public string Prompt { get; }
        public IReadOnlyList<CareerReactionOptionState> Options => _options;
        public int SelectedOptionIndex { get; private set; }
        public bool IsResolved => SelectedOptionIndex >= 0;

        internal CareerReactionEffect Resolve(int optionIndex)
        {
            if (IsResolved)
                throw new InvalidOperationException("이미 답변한 반응 이벤트입니다.");
            if (optionIndex < 0 || optionIndex >= _options.Length)
                throw new ArgumentOutOfRangeException(nameof(optionIndex));
            SelectedOptionIndex = optionIndex;
            return _options[optionIndex].Effect;
        }
    }

    /// <summary>인터뷰 선택 이력과 서사 전용 관계 지표를 커리어 세이브에서 소유한다.</summary>
    public sealed class CareerNarrativeState
    {
        private const int InitialRating = 50;
        private readonly List<CareerReactionEventState> _reactionHistory = new();

        public CareerNarrativeState(int saveVersion)
        {
            SaveVersion = saveVersion;
            Confidence = InitialRating;
            MediaStanding = InitialRating;
            FanSupport = InitialRating;
            TeamChemistry = InitialRating;
        }

        public int SaveVersion { get; private set; }
        public int Confidence { get; private set; }
        public int MediaStanding { get; private set; }
        public int FanSupport { get; private set; }
        public int TeamChemistry { get; private set; }
        public IReadOnlyList<CareerReactionEventState> ReactionHistory => _reactionHistory;
        public CareerReactionEventState PendingReaction { get; private set; }

        public bool TryQueue(CareerReactionEventState reaction)
        {
            if (reaction == null) throw new ArgumentNullException(nameof(reaction));
            if (PendingReaction != null || Contains(reaction.ReactionId))
                return false;
            PendingReaction = reaction;
            _reactionHistory.Add(reaction);
            return true;
        }

        internal CareerReactionEffect ResolvePending(int optionIndex)
        {
            if (PendingReaction == null)
                throw new InvalidOperationException("답변할 반응 이벤트가 없습니다.");
            CareerReactionEffect effect = PendingReaction.Resolve(optionIndex);
            Confidence = Clamp(Confidence + effect.Confidence);
            MediaStanding = Clamp(MediaStanding + effect.MediaStanding);
            FanSupport = Clamp(FanSupport + effect.FanSupport);
            TeamChemistry = Clamp(TeamChemistry + effect.TeamChemistry);
            PendingReaction = null;
            return effect;
        }

        public bool HasRecentReaction(int seasonId, int round, int cooldownRounds)
        {
            for (int index = _reactionHistory.Count - 1; index >= 0; index--)
            {
                CareerReactionEventState reaction = _reactionHistory[index];
                if (reaction.SeasonId != seasonId)
                    continue;
                return round - reaction.Round < cooldownRounds;
            }
            return false;
        }

        public void UpgradeSaveVersion(int saveVersion)
        {
            if (saveVersion <= SaveVersion)
                throw new ArgumentOutOfRangeException(nameof(saveVersion));
            SaveVersion = saveVersion;
        }

        private bool Contains(string reactionId)
        {
            for (int index = 0; index < _reactionHistory.Count; index++)
            {
                if (string.Equals(_reactionHistory[index].ReactionId, reactionId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static int Clamp(int value) => value < 0 ? 0 : value > 100 ? 100 : value;
    }
}
