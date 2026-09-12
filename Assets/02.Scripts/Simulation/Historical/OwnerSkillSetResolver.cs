using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Simulation.Growth;

namespace Baseball.Simulation.Historical
{
    /// <summary>변으로 연결된 같은 계열 세 블록을 한 세트로 계산한다.</summary>
    public static class OwnerSkillSetResolver
    {
        public static int GetBonus(IReadOnlyList<PlacedSkillBlock> placements, SkillBoardService board,
            SkillBlockDefinition[] definitions, PlayerAbility ability)
        {
            if (placements.Count < 3) return 0;
            var visited = new bool[placements.Count];
            var queue = new int[placements.Count];
            int total = 0;
            for (int root = 0; root < placements.Count; root++)
            {
                if (visited[root]) continue;
                var definition = Find(definitions, placements[root].Instance.DefinitionId);
                if (definition.AdjacencySetBonus == 0) continue;
                int read = 0, write = 1; queue[0] = root; visited[root] = true;
                while (read < write)
                {
                    var cells = board.GetOccupiedCells(placements[queue[read++]]);
                    for (int next = 0; next < placements.Count; next++)
                    {
                        if (visited[next] || Find(definitions, placements[next].Instance.DefinitionId).Category != definition.Category) continue;
                        if (!Touches(cells, board.GetOccupiedCells(placements[next]))) continue;
                        visited[next] = true; queue[write++] = next;
                    }
                }
                bool hasAbility = false; int setBonus = 0;
                for (int member = 0; member < write; member++)
                {
                    var block = Find(definitions, placements[queue[member]].Instance.DefinitionId);
                    setBonus = Math.Max(setBonus, block.AdjacencySetBonus);
                    foreach (var bonus in block.AbilityBonuses) if (bonus.Ability == ability) hasAbility = true;
                }
                if (hasAbility) total += write / 3 * setBonus;
            }
            return total;
        }
        private static bool Touches(BoardCell[] first, BoardCell[] second)
        {
            foreach (var a in first) foreach (var b in second)
                if (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1) return true;
            return false;
        }
        private static SkillBlockDefinition Find(SkillBlockDefinition[] definitions, string id)
        {
            foreach (var definition in definitions) if (definition.BlockId == id) return definition;
            throw new InvalidOperationException("스킬 블록 정의가 없습니다.");
        }
    }
}
