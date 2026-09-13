using System;
using System.Linq;
using Baseball.Core.Historical;
using Baseball.Game.Historical;
using Baseball.Tools.ManagerReportValidation;

namespace Baseball.Tools.OwnerSaveLoadValidation
{
    /// <summary>에디터 없이 저장 복원 경로의 합성 이력 보존과 손상 검출을 검증한다.</summary>
    internal static class Program
    {
        private static void Main()
        {
            var fixture = RuntimeFixture.Fixture.Create(WorldRecordMode.SimulatedHistory);
            var adapter = fixture.CreateAdapter();
            int checks = 0;
            Verify(36, 0, 0, new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 0, 0 });
            Verify(36, 14, 8, new[] { 1, 2, 3, 4 }, new[] { 1, 2, 3, 4, 0 });
            Verify(35, 0, 0, null, new[] { 0, 0, 0, 0, 0 });
            Verify(35, 0, 0, Array.Empty<int>(), new[] { 0, 0, 0, 0, 0 });
            Verify(37, 15, 5, new[] { 1, 2, 3, 4, 5 }, new[] { 1, 2, 3, 4, 5 });
            Reject(37, 0, 0, new int[4]);
            Reject(37, 0, 0, null);
            Reject(36, 0, 0, new int[3]);
            Reject(36, 1, 0, new[] { 2, 0, 0, 0 });
            Reject(36, 1, 0, new[] { -1, 0, 0, 0 });
            Reject(36, 1, 2, new int[4]);
            Reject(35, 1, 0, Array.Empty<int>());
            Console.WriteLine($"구단주 저장 복원 콘솔 검증 {checks}건 통과");

            ManagerHistoricalSaveData Create(int version, int count, int points, int[] failures)
            {
                var data = adapter.CreateSaveData(fixture.State);
                data.saveVersion = version;
                data.playerGrowth.inventory.fusionCount = count;
                data.playerGrowth.inventory.fusionPoints = points;
                data.playerGrowth.inventory.fusionFailures = failures;
                return data;
            }

            void Verify(int version, int count, int points, int[] failures, int[] expected)
            {
                var data = Create(version, count, points, failures);
                int[] original = failures == null ? null : (int[])failures.Clone();
                var restored = adapter.Restore(data);
                var stock = restored.PlayerGrowth.Inventory;
                if (stock.FusionCount != count || stock.FusionPoints != points ||
                    !stock.CopyFusionFailures().SequenceEqual(expected))
                    throw new Exception("합성 이력 보존 실패");
                if (original != null && !data.playerGrowth.inventory.fusionFailures.SequenceEqual(original))
                    throw new Exception("입력 저장 DTO 변경");
                var roundTrip = adapter.Restore(adapter.CreateSaveData(restored));
                if (!roundTrip.PlayerGrowth.Inventory.CopyFusionFailures().SequenceEqual(expected))
                    throw new Exception("재저장 왕복 실패");
                checks++;
            }

            void Reject(int version, int count, int points, int[] failures)
            {
                try { adapter.Restore(Create(version, count, points, failures)); }
                catch (ArgumentException) { checks++; return; }
                throw new Exception("손상된 합성 이력을 허용함");
            }
        }
    }
}
