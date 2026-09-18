using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClassicGamePlugin.Features.RichTown.Application;
using ClassicGamePlugin.Features.RichTown.Domain;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using static ClassicGamePlugin.RichTown.Tests.RichTownScenarios;

namespace ClassicGamePlugin.RichTown.Tests;

/// <summary>完整确定性对局门禁。所有指令通过正式会话，逐步核对金额流水、归属、回合和恢复后的事件序列。</summary>
public sealed class RichTownSimulationTests(ITestOutputHelper output)
{
    [Fact]
    public void 一千个固定种子完整对局在上限内结束且逐步满足不变量()
    {
        const int games = 1000;
        const int commandLimit = 2000;
        var commands = 0;
        var maxCommands = 0;
        var rolls = 0;
        var bankruptcies = 0;
        var debts = 0;
        var restoredCommands = 0;
        var roundLimitGames = 0;
        var lastSurvivorGames = 0;
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (ulong seed = 0; seed < games; seed++)
        {
            var history = new List<RichTownRequest>();
            var session = new RichTownSession(RichTownSnapshot.Create(seed, allComputer: true));
            RichTownSession? restored = null;
            var acted = new HashSet<(int Round, int Player)>();
            try
            {
                while (session.Snapshot.Phase != RichTownPhase.Finished)
                {
                    Assert.True(history.Count < commandLimit, "对局超过命令上限。");
                    var before = session.Snapshot;
                    if (history.Count == 100)
                        restored = new RichTownSession(before with { Players = [.. before.Players], Properties = [.. before.Properties], Random = new(before.Random.Version, before.Random.State) });
                    var request = Assert.IsType<RichTownRequest>(RichTownComputerPlayer.Decide(before));
                    history.Add(request);
                    var result = session.Submit(request);
                    var after = result.Accepted();
                    CheckInvariants(before, after, result.Events, request);
                    if (request.Command.Kind == RichTownCommandKind.Roll)
                    {
                        Assert.True(acted.Add((before.Round, before.CurrentPlayerId)), "同一轮同一玩家重复掷骰。");
                        rolls++;
                    }
                    Assert.True(acted.Count <= 90, "超过 30 轮 × 3 人的掷骰上限。");
                    bankruptcies += result.Events.OfType<RichTownBankrupt>().Count();
                    debts += result.Events.OfType<RichTownDebtOpened>().Count();
                    if (restored is not null)
                    {
                        Assert.Equal(request, RichTownComputerPlayer.Decide(restored.Snapshot));
                        var replay = restored.Submit(request);
                        Assert.Equal(StateText(after), StateText(replay.Accepted()));
                        Assert.Equal(EventText(result.Events), EventText(replay.Events));
                        restoredCommands++;
                    }
                }
                var final = session.Snapshot;
                Assert.NotEmpty(final.GetStandings().Where(standing => standing.IsWinner));
                Assert.Null(RichTownComputerPlayer.Decide(final));
                if (final.FinishReason == RichTownFinishReason.RoundLimit)
                {
                    Assert.Equal(30, final.Round);
                    roundLimitGames++;
                }
                else
                {
                    Assert.Single(final.Players, player => !player.IsEliminated);
                    lastSurvivorGames++;
                }
                digest.AppendData(Encoding.UTF8.GetBytes(StateText(final) + "\n"));
                commands += history.Count;
                maxCommands = Math.Max(maxCommands, history.Count);
            }
            catch (Exception error)
            {
                throw new XunitException($"固定种子 {seed}，已提交 {history.Count} 条命令。\n" +
                    $"复现：RichTownSnapshot.Create({seed}UL, allComputer: true)，依次提交以下完整记录。\n" +
                    string.Join('\n', history) + $"\n当前快照：{StateText(session.Snapshot)}\n{error}");
            }
        }
        var report = JsonSerializer.Serialize(new
        {
            rulesVersion = RichTownBoard.RulesVersion,
            randomAlgorithm = "SplitMix64-v1",
            seedStart = 0,
            seedEnd = games - 1,
            games,
            commandLimit,
            commands,
            maxCommands,
            rolls,
            debts,
            bankruptcies,
            roundLimitGames,
            lastSurvivorGames,
            restoredCommands,
            finalSnapshotsSha256 = Convert.ToHexString(digest.GetHashAndReset())
        }, new JsonSerializerOptions { WriteIndented = true });
        output.WriteLine(report);
        // 只有本地开发脚本显式提供输出路径时才写额外证据；普通 dotnet test 仍在 TRX 中保留同一 JSON。
        var reportPath = Environment.GetEnvironmentVariable("RICH_TOWN_SIMULATION_REPORT");
        if (!string.IsNullOrWhiteSpace(reportPath)) File.WriteAllText(reportPath, report, Encoding.UTF8);
    }

    private static void CheckInvariants(RichTownSnapshot before, RichTownSnapshot after, IReadOnlyList<RichTownEvent> events, RichTownRequest request)
    {
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.EventSequence + events.Count, after.EventSequence);
        Assert.Equal(Enumerable.Range(1, events.Count).Select(offset => before.EventSequence + offset), events.Select(item => item.Sequence));
        Assert.InRange(after.Round, before.Round, Math.Min(30, before.Round + 1));
        Assert.InRange(after.Round, 1, 30);
        Assert.Equal(3, after.Players.Length);
        Assert.Equal(18, after.Properties.Length);
        Assert.Equal(18, after.Properties.Select(property => property.Index).Distinct().Count());
        Assert.Equal(after.Phase == RichTownPhase.AwaitingDebt, after.Debt is not null);
        Assert.False(after.Players[after.CurrentPlayerId].IsEliminated);
        if (request.Command.Kind != RichTownCommandKind.Roll) Assert.Equal(before.Random, after.Random);

        // 独立按对外流水核对实际现金增减，不调用生产转账函数作为测试的预期值。
        var ledger = before.Players.Select(player => (long)player.Cash).ToArray();
        foreach (var transfer in events.OfType<RichTownMoneyTransferred>())
        {
            Assert.True(transfer.Amount > 0);
            Assert.NotEqual(transfer.From, transfer.To);
            if (transfer.From is { } payer) ledger[payer] -= transfer.Amount;
            if (transfer.To is { } payee) ledger[payee] += transfer.Amount;
            Assert.All(ledger, cash => Assert.True(cash >= 0, "事件流水出现中间负余额。"));
        }
        foreach (var player in after.Players)
        {
            Assert.Equal(ledger[player.Id], player.Cash);
            Assert.InRange(player.Position, 0, 23);
            Assert.True(player.Cash >= 0);
            if (before.Players[player.Id].IsEliminated) Assert.True(player.IsEliminated);
            if (player.IsEliminated)
            {
                Assert.Equal(0, player.Cash);
                Assert.DoesNotContain(after.Properties, property => property.OwnerId == player.Id);
            }
        }
        foreach (var property in after.Properties)
        {
            Assert.InRange(property.Level, 0, 2);
            if (property.OwnerId is { } owner) Assert.False(after.Players[owner].IsEliminated);
            else Assert.Equal(0, property.Level);
        }
        if (after.Debt is { } debt)
        {
            Assert.Equal(after.CurrentPlayerId, debt.DebtorId);
            Assert.True(after.Players[debt.DebtorId].Cash < debt.Amount);
        }
    }
}
