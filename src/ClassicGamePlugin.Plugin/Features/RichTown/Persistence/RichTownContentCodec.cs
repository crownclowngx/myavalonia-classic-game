using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassicGamePlugin.Features.RichTown.Domain;
using MyAvaloniaManagement.PluginSdk;

namespace ClassicGamePlugin.Features.RichTown.Persistence;

/// <summary>可持久化内容只含规则和最后骰面；不保存 View、选中格、相机、动画进度或引擎对象。</summary>
internal sealed record RichTownSaveState(RichTownSnapshot Snapshot, int LastDie = 1, bool HasRolled = false);

/// <summary>
/// 小镇独占的 schema 1 编解码器。严格字段、枚举、数量和大小限制先于会话替换，坏文件不会产生半恢复。
/// DTO 与领域分开，未来更改 C# 领域类型不会无意改变磁盘协议；随机状态用 16 位十六进制保存全部 64 位。
/// Host 拥有路径、文件事务、信封和备份；此类只处理 SDK 已冻结的 JSON，不读写文件。
/// </summary>
internal static class RichTownContentCodec
{
    public const int SchemaVersion = 1;
    public const string MapId = "rich-town-ring24-v1";
    public const string RandomAlgorithm = "SplitMix64-v1";
    public const int MaximumJsonCharacters = 32768;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
        MaxDepth = 8,
        Converters = { new JsonStringEnumConverter<RichTownPhase>(allowIntegerValues: false),
            new JsonStringEnumConverter<RichTownFinishReason>(allowIntegerValues: false),
            new JsonStringEnumConverter<RichTownPaymentReason>(allowIntegerValues: false) }
    };

    public static DocumentContent Encode(RichTownSaveState state)
    {
        Validate(state);
        var s = state.Snapshot;
        var payload = new Payload(MapId, s.RulesVersion, RandomAlgorithm, s.Random.State.ToString("X16", CultureInfo.InvariantCulture),
            s.Players.Select(p => new PlayerData(p.Id, p.Position, p.Cash, p.IsComputer, p.IsEliminated)).ToArray(),
            s.Properties.Select(p => new PropertyData(p.Index, p.OwnerId, p.Level)).ToArray(),
            s.Round, s.CurrentPlayerId, s.Phase, s.HasUpgraded,
            s.Debt is { } d ? new DebtData(d.DebtorId, d.CreditorId, d.Amount, d.Reason) : null,
            s.FinishReason, s.Revision, s.EventSequence, state.LastDie, state.HasRolled);
        return new(SchemaVersion, JsonSerializer.SerializeToElement(payload, Options));
    }

    public static RichTownSaveState Decode(DocumentContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            if (content.SchemaVersion != SchemaVersion) throw new InvalidDataException("小镇存档版本不受支持。");
            var json = content.Payload.GetRawText();
            if (json.Length > MaximumJsonCharacters) throw new InvalidDataException("小镇存档超过 32K 字符上限。");
            var p = JsonSerializer.Deserialize<Payload>(json, Options) ?? throw new InvalidDataException("小镇存档不能为空。");
            if (p.MapId != MapId || p.RandomAlgorithm != RandomAlgorithm || p.RandomState.Length != 16 ||
                !ulong.TryParse(p.RandomState, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var random))
                throw new InvalidDataException("小镇地图或随机算法状态无效。");
            if (p.Players.Length != 3 || p.Players.Any(item => item is null) ||
                p.Properties.Length != 18 || p.Properties.Any(item => item is null))
                throw new InvalidDataException("小镇玩家或地产集合无效。");
            var snapshot = new RichTownSnapshot(p.RulesVersion,
                p.Players.Select(item => new RichTownPlayer(item.Id, item.Position, item.Cash, item.IsComputer, item.IsEliminated)).ToImmutableArray(),
                p.Properties.Select(item => new RichTownProperty(item.Index, item.OwnerId, item.Level)).ToImmutableArray(),
                new(RichTownRandom.AlgorithmVersion, random), p.Round, p.CurrentPlayerId, p.Phase, p.HasUpgraded,
                p.Debt is { } d ? new(d.DebtorId, d.CreditorId, d.Amount, d.Reason) : null,
                p.FinishReason, p.Revision, p.EventSequence);
            var state = new RichTownSaveState(snapshot, p.LastDie, p.HasRolled);
            Validate(state);
            return state;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException("小镇存档字段、格式或规则状态无效。", error);
        }
    }

    private static void Validate(RichTownSaveState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        RichTownSnapshotValidator.Validate(state.Snapshot);
        if (state.LastDie is < 1 or > 6 || (!state.HasRolled && state.LastDie != 1))
            throw new InvalidDataException("小镇最后骰面无效。");
        // schema 1 的产品入口固定一真人两电脑；全电脑仅用于规则模拟，不能从文件偷偷更改玩家控制权。
        if (state.Snapshot.Players[0].IsComputer || !state.Snapshot.Players[1].IsComputer || !state.Snapshot.Players[2].IsComputer)
            throw new InvalidDataException("小镇存档必须是一真人两电脑。");
        // 30 轮正常对局远小于此上限，拒绝接近 long 溢出的伪造计数；这不是新的经济/回合规则。
        if (state.Snapshot.Revision > 1_000_000 || state.Snapshot.EventSequence > 10_000_000)
            throw new InvalidDataException("小镇存档计数超过当前版本上限。");
    }

    private sealed record Payload(string MapId, int RulesVersion, string RandomAlgorithm, string RandomState,
        PlayerData[] Players, PropertyData[] Properties, int Round, int CurrentPlayerId, RichTownPhase Phase,
        bool HasUpgraded, DebtData? Debt, RichTownFinishReason? FinishReason, long Revision, long EventSequence, int LastDie, bool HasRolled);
    private sealed record PlayerData(int Id, int Position, int Cash, bool IsComputer, bool IsEliminated);
    private sealed record PropertyData(int Index, int? OwnerId, int Level);
    private sealed record DebtData(int DebtorId, int? CreditorId, int Amount, RichTownPaymentReason Reason);
}
