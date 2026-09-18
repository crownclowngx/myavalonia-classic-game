using System.Text.Json;
using System.Text.Json.Nodes;
using ClassicGamePlugin.Features.RichTown.Domain;
using ClassicGamePlugin.Features.RichTown.Persistence;
using MyAvaloniaManagement.PluginSdk;
using Xunit;
using static ClassicGamePlugin.RichTown.Tests.RichTownScenarios;

namespace ClassicGamePlugin.RichTown.Tests;

public sealed class RichTownPersistenceTests
{
    [Theory]
    [InlineData("roll")]
    [InlineData("purchase")]
    [InlineData("debt")]
    [InlineData("actions")]
    [InlineData("finished")]
    public void 所有阶段完整往返且待购买债务与随机续序保持一致(string phase)
    {
        var snapshot = phase switch
        {
            "purchase" => At().Act(RichTownCommandKind.Roll).Accepted(),
            "debt" => At(11, 0).Own(1, level: 2).Act(RichTownCommandKind.Roll).Accepted(),
            "actions" => At(2).Act(RichTownCommandKind.Roll).Accepted(),
            "finished" => At(11, 0).Player(1, p => p with { IsEliminated = true, Cash = 0 }).Act(RichTownCommandKind.Roll).Accepted(),
            _ => At(seed: ulong.MaxValue)
        };
        var source = new RichTownSaveState(snapshot, 6, true);
        var content = RichTownContentCodec.Encode(source);
        var restored = RichTownContentCodec.Decode(content);
        Assert.Equal(StateText(snapshot), StateText(restored.Snapshot));
        Assert.Equal((6, true), (restored.LastDie, restored.HasRolled));
        var first = snapshot.Random;
        var second = restored.Snapshot.Random;
        for (var i = 0; i < 100; i++)
        {
            var a = RichTownRandom.NextUInt64(first);
            var b = RichTownRandom.NextUInt64(second);
            Assert.Equal(a, b);
            first = a.State; second = b.State;
        }
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("missing")]
    [InlineData("null-string")]
    [InlineData("null-array")]
    [InlineData("null-player")]
    [InlineData("null-property")]
    [InlineData("player-count")]
    [InlineData("property-count")]
    [InlineData("missing-player-field")]
    [InlineData("unknown-player-field")]
    [InlineData("number-as-string")]
    [InlineData("phase-number")]
    [InlineData("phase-unknown")]
    [InlineData("map")]
    [InlineData("algorithm")]
    [InlineData("random-length")]
    [InlineData("random-hex")]
    [InlineData("rules")]
    [InlineData("negative-cash")]
    [InlineData("eliminated-owner")]
    [InlineData("human-role")]
    [InlineData("computer-role")]
    [InlineData("revision-limit")]
    [InlineData("event-limit")]
    [InlineData("last-die-low")]
    [InlineData("last-die-high")]
    [InlineData("unrolled-die")]
    [InlineData("bad-debt")]
    public void 坏字段不能进入领域或绕过当前内容协议(string kind)
    {
        var json = JsonNode.Parse(RichTownContentCodec.Encode(new(At())).Payload.GetRawText())!.AsObject();
        switch (kind)
        {
            case "unknown": json["camera"] = 123; break;
            case "missing": json.Remove("round"); break;
            case "null-string": json["randomState"] = null; break;
            case "null-array": json["players"] = null; break;
            case "null-player": json["players"]![0] = null; break;
            case "null-property": json["properties"]![0] = null; break;
            case "player-count": json["players"]!.AsArray().RemoveAt(0); break;
            case "property-count": json["properties"]!.AsArray().RemoveAt(0); break;
            case "missing-player-field": json["players"]![0]!.AsObject().Remove("cash"); break;
            case "unknown-player-field": json["players"]![0]!["bonus"] = 1; break;
            case "number-as-string": json["round"] = "1"; break;
            case "phase-number": json["phase"] = 0; break;
            case "phase-unknown": json["phase"] = "Other"; break;
            case "map": json["mapId"] = "other"; break;
            case "algorithm": json["randomAlgorithm"] = "Random"; break;
            case "random-length": json["randomState"] = "123"; break;
            case "random-hex": json["randomState"] = "ZZZZZZZZZZZZZZZZ"; break;
            case "rules": json["rulesVersion"] = 999; break;
            case "negative-cash": json["players"]![0]!["cash"] = -1; break;
            case "eliminated-owner": json["players"]![1]!["cash"] = 0; json["players"]![1]!["isEliminated"] = true; json["properties"]![0]!["ownerId"] = 1; break;
            case "human-role": json["players"]![0]!["isComputer"] = true; break;
            case "computer-role": json["players"]![2]!["isComputer"] = false; break;
            case "revision-limit": json["revision"] = long.MaxValue; json["eventSequence"] = long.MaxValue; break;
            case "event-limit": json["eventSequence"] = long.MaxValue; break;
            case "last-die-low": json["lastDie"] = 0; break;
            case "last-die-high": json["lastDie"] = 7; break;
            case "unrolled-die": json["lastDie"] = 2; break;
            case "bad-debt": json["debt"] = new JsonObject { ["debtorId"] = 0, ["creditorId"] = null, ["amount"] = 100, ["reason"] = "Tax" }; break;
        }
        Assert.Throws<InvalidDataException>(() => RichTownContentCodec.Decode(Content(json.ToJsonString())));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{}")]
    public void 非对象或空内容拒绝(string json) => Assert.Throws<InvalidDataException>(() => RichTownContentCodec.Decode(Content(json)));

    [Fact]
    public void 重复属性未知版本超大内容及空引用均拒绝()
    {
        var valid = RichTownContentCodec.Encode(new(At()));
        Assert.Throws<InvalidDataException>(() => RichTownContentCodec.Decode(new(2, valid.Payload)));
        Assert.Throws<InvalidDataException>(() => RichTownContentCodec.Decode(Content(valid.Payload.GetRawText().Replace("\"round\":1", "\"round\":1,\"round\":2", StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() => RichTownContentCodec.Decode(Content("{\"oversized\":\"" + new string('x', RichTownContentCodec.MaximumJsonCharacters) + "\"}")));
        Assert.Throws<ArgumentNullException>(() => RichTownContentCodec.Decode(null!));
        Assert.Throws<ArgumentNullException>(() => RichTownContentCodec.Encode(null!));
        using var original = JsonDocument.Parse(valid.Payload.GetRawText());
        var frozen = new DocumentContent(1, original.RootElement);
        original.Dispose();
        Assert.Equal(StateText(At()), StateText(RichTownContentCodec.Decode(frozen).Snapshot));
    }

    internal static DocumentContent Content(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new(1, document.RootElement);
    }
}
