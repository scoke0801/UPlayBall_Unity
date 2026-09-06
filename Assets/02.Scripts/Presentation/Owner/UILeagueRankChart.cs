using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Owner
{
    /// <summary>크기 변경에도 좌표가 일치하는 순위 추이 선과 점을 그린다.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class UILeagueRankChart : MaskableGraphic
    {
        private OwnerLeaguePresentationModel _model;
        private int _start;

        /// <summary>선택한 여섯 라운드의 순위를 연결한다.</summary>
        public void Bind(OwnerLeaguePresentationModel model, int start)
        {
            _model = model;
            _start = Mathf.Max(0, start);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (_model == null || _model.Standings.Count == 0) return;
            Rect rect = rectTransform.rect;
            float rowHeight = rect.height / Mathf.Max(11, _model.Standings.Count + 1);
            Vector2 Point(int column, int rank) => new Vector2(rect.xMin + (column + .5f) * rect.width / 6,
                rect.yMax - (rank + .5f) * rowHeight);
            Color grid = new Color32(211, 214, 218, 255);
            for (int rank = 1; rank <= _model.Standings.Count; rank++)
                Line(mesh, Point(0, rank), Point(5, rank), 1, grid);
            for (int column = 0; column < 6; column++)
                Line(mesh, Point(column, 1), Point(column, _model.Standings.Count), 1, grid);
            // 포커스 선을 마지막에 그려 회색 선과 겹쳐도 내 구단을 추적할 수 있게 한다.
            for (int pass = 0; pass < 2; pass++)
            foreach (var team in _model.Standings)
            {
                bool focus = team.Id == _model.FocusTeamId;
                if (focus != (pass == 1)) continue;
                Color color = focus ? new Color32(246, 57, 69, 255) : new Color32(162, 167, 173, 255);
                int count = Mathf.Clamp(team.RankHistory.Count - _start, 0, 6);
                for (int i = 0; i < count; i++)
                {
                    Vector2 point = Point(i, team.RankHistory[_start + i]);
                    if (i > 0) Line(mesh, Point(i - 1, team.RankHistory[_start + i - 1]), point, focus ? 3 : 1, color);
                    Dot(mesh, point, focus ? 4 : 3, focus ? new Color32(56, 190, 250, 255) : color);
                }
            }
        }

        private static void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 direction = to - from;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * width * .5f;
            int start = mesh.currentVertCount;
            mesh.AddVert(from - normal, color, Vector2.zero);
            mesh.AddVert(from + normal, color, Vector2.zero);
            mesh.AddVert(to + normal, color, Vector2.zero);
            mesh.AddVert(to - normal, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Dot(VertexHelper mesh, Vector2 point, float radius, Color color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(point, color, Vector2.zero);
            for (int i = 0; i <= 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                mesh.AddVert(point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
