using Baseball.Core.Players;
using Baseball.Presentation.Career;
using UnityEngine;

namespace Baseball.Presentation.Match.Sprites
{
    /// <summary>기존 논리 구장 좌표를 배경의 보정된 홈·베이스·외야 위치로 투영한다.</summary>
    public sealed class FieldProjection
    {
        public FieldLayoutDefinition Layout { get; }
        /// <summary>구장별 보정 정의를 주입받는다.</summary>
        public FieldProjection(FieldLayoutDefinition layout) { Layout = layout; }

        /// <summary>깊이 구간의 경계는 기존 공통 구장 좌표 계약을 따른다.</summary>
        public Vector2 Project(NormalizedFieldPoint point)
        {
            Vector2 home = Layout.GetAnchor(FieldAnchor.HomePlate);
            Vector2 first = Layout.GetAnchor(FieldAnchor.FirstBase);
            Vector2 third = Layout.GetAnchor(FieldAnchor.ThirdBase);
            Vector2 second = Layout.GetAnchor(FieldAnchor.SecondBase);
            Vector2 center = Layout.GetAnchor(FieldAnchor.CenterField);
            float y = (float)point.Y;
            float screenY = y <= 0.28f
                ? Mathf.LerpUnclamped(home.y, (first.y + third.y) * 0.5f, (y - 0.08f) / 0.20f)
                : y <= 0.49f
                    ? Mathf.LerpUnclamped((first.y + third.y) * 0.5f, second.y, (y - 0.28f) / 0.21f)
                    : Mathf.LerpUnclamped(second.y, center.y, (y - 0.49f) / 0.35f);
            float x = (float)point.X;
            float depth = Mathf.InverseLerp(0.49f, 0.84f, y);
            float rightScale = Mathf.Lerp((first.x - home.x) / 0.23f,
                (Layout.GetAnchor(FieldAnchor.RightField).x - home.x) / 0.26f, depth);
            float leftScale = Mathf.Lerp((home.x - third.x) / 0.23f,
                (home.x - Layout.GetAnchor(FieldAnchor.LeftField).x) / 0.26f, depth);
            return new Vector2(home.x + (x - 0.5f) * (x >= 0.5f ? rightScale : leftScale), screenY);
        }

        /// <summary>주루 베이스 번호에 대응하는 배경 위치다.</summary>
        public Vector2 GetBase(int number) => Layout.GetAnchor(number switch
        {
            1 => FieldAnchor.FirstBase, 2 => FieldAnchor.SecondBase, 3 => FieldAnchor.ThirdBase, _ => FieldAnchor.HomePlate
        });

        /// <summary>수비 포지션에 대응하는 보정 위치다.</summary>
        public Vector2 GetFielder(PlayerPosition position) => Layout.GetAnchor(position switch
        {
            PlayerPosition.FirstBase => FieldAnchor.FirstBaseman,
            PlayerPosition.SecondBase => FieldAnchor.SecondBaseman,
            PlayerPosition.ThirdBase => FieldAnchor.ThirdBaseman,
            PlayerPosition.Shortstop => FieldAnchor.Shortstop,
            PlayerPosition.LeftField => FieldAnchor.LeftField,
            PlayerPosition.CenterField => FieldAnchor.CenterField,
            PlayerPosition.RightField => FieldAnchor.RightField,
            PlayerPosition.Catcher => FieldAnchor.Catcher,
            _ => FieldAnchor.PitcherMound
        });

        /// <summary>전경 타자와 마운드 투수의 시안 비율을 깊이 곡선으로 유지한다.</summary>
        public float DepthScale(float imageY)
        {
            float depth = Mathf.InverseLerp(Layout.GetAnchor(FieldAnchor.CenterField).y, Layout.GetAnchor(FieldAnchor.HomePlate).y, imageY);
            return Mathf.Lerp(Layout.depthScale, 1f, depth * depth);
        }

        /// <summary>지면 위치와 독립 높이를 UI 좌표로 변환한다.</summary>
        public static Vector2 ToScreen(Vector2 normalized, Vector2 size, float height = 0) =>
            new Vector2(normalized.x * size.x, (-normalized.y + height) * size.y);
    }
}
