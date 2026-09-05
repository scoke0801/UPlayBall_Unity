using System;
using Baseball.Core.Balance;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>한 Seed의 Bake 결과와, 그것을 만드는 데 실제로 돌린 경기 수를 함께 돌려준다.</summary>
    public sealed class WorldHistoryBakeResult
    {
        public WorldHistoryBakeResult(BakedWorldHistoryPayload payload, int totalGameCount, int statisticsRowCount)
        {
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            TotalGameCount = totalGameCount;
            StatisticsRowCount = statisticsRowCount;
        }

        public BakedWorldHistoryPayload Payload { get; }
        public int TotalGameCount { get; }
        public int StatisticsRowCount { get; }
    }

    /// <summary>
    /// 44시즌을 실제로 시뮬레이션해 Bake 산출물을 만든다.
    /// Editor 저작 도구가 Simulation 어셈블리를 직접 참조하지 않도록 Game 경계에 두는 진입점이다.
    /// </summary>
    public static class WorldHistoryBakeService
    {
        public static WorldHistoryBakeResult Create(
            HistoricalBakedContent content,
            BalanceTable balance,
            ulong worldHistorySeed)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));

            // Bake Source를 주지 않으므로 기존 산출물을 재사용하지 않고 반드시 새로 시뮬레이션한다.
            var builder = new HistoricalWorldRuntimeBuilder(balance);
            HistoricalWorldRuntimeContent world = builder.Build(
                content,
                WorldRecordMode.SimulatedHistory,
                worldHistorySeed);
            WorldHistorySnapshot history = world.WorldHistory;
            var payload = new BakedWorldHistoryPayload(
                builder.CreateBakeKey(content, worldHistorySeed),
                new WorldHistorySaveMapper().CreateSaveData(history));
            return new WorldHistoryBakeResult(payload, world.Metrics.TotalGameCount, history.Statistics.Count);
        }

        public static byte[] Encode(BakedWorldHistoryPayload payload)
        {
            return WorldHistoryBakeCodec.Encode(payload);
        }
    }
}
