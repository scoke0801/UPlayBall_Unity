using System;
using Baseball.Presentation.Career;
using Baseball.Simulation.Match;
using UnityEngine;

namespace Baseball.Presentation.Match
{
    /// <summary>구장 이미지의 좌표 보정과 관전 연출 시간을 판정 데이터와 분리한다.</summary>
    [Serializable]
    public sealed class MatchGameCastConfig
    {
        private readonly PlayResolutionPresentationConfig _playTiming = new PlayResolutionPresentationConfig();
        public string fieldTexture = "UI/OwnerMatch/gamecast_field_v1";
        public string baseballSprite = "UI/MiniGame/img_baseball_ball";
        public float homeY = 0.882f;
        public float baseY = 0.665f;
        public float secondY = 0.474f;
        public float baseSpread = 0.173f;
        public float outfieldY = 0.17f;
        public float moundY = 0.667f;
        public float catcherY = 0.925f;
        public float fieldBallSize = 18f;
        public float strikeZoneBallSize = 16f;
        public float pitchSeconds = 0.30f;
        public float runnerSecondsPerBase = 0.24f;
        public float resultSeconds = 0.32f;
        public float inningSeconds = 0.8f;
        public float decisionSeconds = 0.6f;
        public int highlightLateInning = 7;
        public int highlightCloseRunMargin = 2;
        public int highlightMultiRunThreshold = 2;
        public float highlightWinExpectancySwing = 0.10f;
        public float highlightLateWinExpectancySwing = 0.055f;

        /// <summary>Resources의 미술·연출 설정을 읽는다.</summary>
        public static MatchGameCastConfig Load()
        {
            TextAsset asset = Resources.Load<TextAsset>("UI/OwnerMatch/GameCastPresentation");
            return asset == null ? new MatchGameCastConfig() : JsonUtility.FromJson<MatchGameCastConfig>(asset.text);
        }

        /// <summary>구장 비행과 스트라이크 존이 공유하는 실제 야구공 스프라이트를 읽는다.</summary>
        public Sprite LoadBaseballSprite()
        {
            return Resources.Load<Sprite>(baseballSprite);
        }

        /// <summary>커리어와 같은 의미 좌표를 생성된 구장 원본의 베이스 위치에 맞춘다.</summary>
        public Vector2 ToTexturePoint(NormalizedFieldPoint point)
        {
            float y = (float)point.Y;
            float imageY = y <= 0.28f
                ? Mathf.LerpUnclamped(homeY, baseY, (y - 0.08f) / 0.20f)
                : y <= 0.49f
                    ? Mathf.LerpUnclamped(baseY, secondY, (y - 0.28f) / 0.21f)
                    : Mathf.LerpUnclamped(secondY, outfieldY, (y - 0.49f) / 0.35f);
            return new Vector2(0.5f + ((float)point.X - 0.5f) * baseSpread / 0.23f,
                Mathf.Clamp(imageY, 0.035f, 0.97f));
        }

        /// <summary>구장 그림의 마운드와 홈 뒤 포수 위치를 별도로 보정한다.</summary>
        public Vector2 GetFielderTexturePoint(Baseball.Core.Players.PlayerPosition position)
        {
            return position switch
            {
                Baseball.Core.Players.PlayerPosition.StartingPitcher => new Vector2(0.5f, moundY),
                Baseball.Core.Players.PlayerPosition.Catcher => new Vector2(0.5f, catcherY),
                _ => ToTexturePoint(PlayResolutionFieldLayout.GetFielderPoint(position))
            };
        }

        /// <summary>공식 사건 한 건의 읽을 수 있는 재생 시간을 반환한다.</summary>
        public float GetDuration(in MatchEvent value)
        {
            return value.EventType switch
            {
                MatchEventType.Pitch => pitchSeconds,
                MatchEventType.Contact => value.BallInPlayData.HasValue
                    ? (float)_playTiming.ResolveBattedBallFlightSeconds(value.BallInPlayData.BattedBall)
                    : resultSeconds,
                MatchEventType.RunnerAdvance or MatchEventType.RunnerThrownOut =>
                    runnerSecondsPerBase * Math.Max(1, value.ToBase - value.FromBase),
                MatchEventType.HalfInningEnded => inningSeconds,
                MatchEventType.PlateAppearanceEnded or MatchEventType.Score or MatchEventType.Out or
                    MatchEventType.Hit or MatchEventType.DoublePlay => resultSeconds,
                MatchEventType.PitcherEntered or MatchEventType.PinchHitterEntered or
                    MatchEventType.PinchRunnerEntered or MatchEventType.DefensiveReplacement or
                    MatchEventType.HighLeverageSituationStarted => decisionSeconds,
                _ => 0f
            };
        }
    }
}
