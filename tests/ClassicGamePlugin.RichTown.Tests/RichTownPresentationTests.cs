using ClassicGamePlugin.Features.RichTown.Domain;
using ClassicGamePlugin.Features.RichTown.Presentation;
using ClassicGamePlugin.Features.RichTown.Rendering;
using Xunit;
using static ClassicGamePlugin.RichTown.Tests.RichTownScenarios;

namespace ClassicGamePlugin.RichTown.Tests;

/// <summary>G3 使用手动时间推进，验证动画与规则提交分离；不依赖 GPU、消息泵或真实等待。</summary>
public sealed class RichTownPresentationTests
{
    [Fact]
    public void 掷骰先提交规则而动画逐格移动且结束不再次结算()
    {
        using var play = Active(At(23));
        var old = play.Stamp;
        Assert.True(play.SubmitHuman(old, new(RichTownCommandKind.Roll)));
        Assert.Equal(1700, play.Snapshot.Players[0].Cash);
        Assert.Equal(0, play.Snapshot.Players[0].Position);
        Assert.Equal(RichTownBoardLayout.Cell(23) + RichTownBoardLayout.TokenOffset(0), play.Frame.Tokens[0].Position);
        Assert.True(play.IsAnimating);
        Assert.False(play.SubmitHuman(old, new(RichTownCommandKind.Roll)));
        for (var i = 0; i < 5; i++) play.Tick(TimeSpan.FromMilliseconds(100));
        var middle = play.Frame.Tokens[0].Position - RichTownBoardLayout.TokenOffset(0);
        Assert.Equal(RichTownBoardLayout.Cell(23).X, middle.X);
        Assert.InRange(middle.Y, RichTownBoardLayout.Cell(23).Y + 0.01f, RichTownBoardLayout.Cell(0).Y - 0.01f);
        var committed = play.Snapshot;
        FinishAnimation(play);
        Assert.Same(committed, play.Snapshot);
        Assert.Equal(0, play.Frame.DieSpin);
        Assert.Equal(1, play.Frame.DieValue);
        Assert.Equal(RichTownBoardLayout.Cell(0) + RichTownBoardLayout.TokenOffset(0), play.Frame.Tokens[0].Position);
        Assert.Equal(1, play.Snapshot.Revision);
    }

    [Fact]
    public void 六步路径在转角处逐段移动而非直线穿越棋盘()
    {
        var before = At(20, seed: 1);
        var result = before.Act(RichTownCommandKind.Roll);
        var playback = new RichTownPlayback(before, result);
        playback.Advance(0.4);
        var path = Assert.Single(result.Events.OfType<RichTownMoved>()).Path;
        foreach (var index in path)
        {
            playback.Advance(0.16);
            var actual = playback.Pose(result.Snapshot.Players[0]).Position - RichTownBoardLayout.TokenOffset(0);
            Assert.True(System.Numerics.Vector2.Distance(RichTownBoardLayout.Cell(index), actual) < 0.0001f);
        }
        playback.Advance(1);
        Assert.True(playback.IsComplete);
        // 终点之后仍有短暂停留帧；即使时间超过动画长度，也必须停在最后一格，不外插到下一格。
        var finalPose = playback.Pose(result.Snapshot.Players[0]);
        Assert.Equal(RichTownBoardLayout.Cell(path[^1]) + RichTownBoardLayout.TokenOffset(0), finalPose.Position);
        playback.Advance(100);
        Assert.Equal(finalPose, playback.Pose(result.Snapshot.Players[0]));
    }

    [Fact]
    public void 跳过动画仅对齐画面不重复发奖收租或推进电脑()
    {
        using var play = Active(At().Own(1, 1, 2));
        play.SubmitHuman(play.Stamp, new(RichTownCommandKind.Roll));
        var settled = play.Snapshot;
        play.SkipAnimation(play.Stamp);
        play.SkipAnimation(play.Stamp);
        Assert.Same(settled, play.Snapshot);
        Assert.Equal((1400, 1600), (settled.Players[0].Cash, settled.Players[1].Cash));
        Assert.False(play.IsAnimating);
        Assert.True(play.CanInteract);
    }

    [Fact]
    public void 隐藏冻结动画和电脑恢复时显式继续且不追赶墙钟()
    {
        using var play = Active(At());
        play.SubmitHuman(play.Stamp, new(RichTownCommandKind.Roll));
        play.Tick(TimeSpan.FromMilliseconds(100));
        var before = play.Frame;
        play.SetActive(false);
        play.Tick(TimeSpan.FromHours(2));
        Assert.Equal(before.DieSpin, play.Frame.DieSpin);
        play.SetActive(true);
        Assert.True(play.IsPaused);
        play.Tick(TimeSpan.FromHours(1));
        Assert.Equal(before.DieSpin, play.Frame.DieSpin);
        play.TogglePause();
        play.Tick(TimeSpan.FromHours(1));
        Assert.True(play.IsAnimating);
        Assert.NotEqual(before.DieSpin, play.Frame.DieSpin);
        Assert.Equal(1, play.Snapshot.Revision);
    }

    [Fact]
    public void 电脑按延迟每次只提一个命令动画期间不会继续提交()
    {
        using var play = Active(RichTownSnapshot.Create(19, allComputer: true));
        Assert.False(play.CanInteract);
        Assert.Contains("正在行动", RichTownPresentation.Status(play));
        for (var i = 0; i < 3; i++) play.Tick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0, play.Snapshot.Revision);
        play.Tick(TimeSpan.FromHours(1));
        Assert.Equal(1, play.Snapshot.Revision);
        Assert.True(play.IsAnimating);
        var snapshot = play.Snapshot;
        FinishAnimation(play);
        Assert.Same(snapshot, play.Snapshot);
        for (var i = 0; i < 4; i++) play.Tick(TimeSpan.FromMilliseconds(100));
        Assert.Equal(2, play.Snapshot.Revision);
        Assert.Equal(0, play.Snapshot.Properties[0].OwnerId);
    }

    [Fact]
    public void 重开使同修订旧请求和旧跳过通知失效并清空有界日志()
    {
        using var play = Active(At());
        var old = play.Stamp;
        play.SubmitHuman(old, new(RichTownCommandKind.Roll));
        Assert.NotEmpty(play.Log);
        play.Restart(At(seed: 0));
        Assert.Equal(1, play.Generation);
        Assert.Empty(play.Log);
        Assert.False(play.IsAnimating);
        Assert.False(play.HasRolled);
        Assert.False(play.SubmitHuman(old, new(RichTownCommandKind.Roll)));
        Assert.True(play.SubmitHuman(play.Stamp, new(RichTownCommandKind.Roll)));
        play.SkipAnimation(new(0, play.Snapshot.Revision));
        Assert.True(play.IsAnimating);
        Assert.Equal(2, play.Snapshot.Players[0].Position);
        var current = play.Snapshot;
        Assert.Throws<ArgumentException>(() => play.Restart(At() with { Round = 0 }));
        Assert.Same(current, play.Snapshot);
        Assert.Equal(1, play.Generation);
    }

    [Fact]
    public void 关闭后所有迟到入口均无效且重复关闭安全()
    {
        using var play = Active(At());
        play.SubmitHuman(play.Stamp, new(RichTownCommandKind.Roll));
        var current = play.Snapshot;
        var notifications = 0;
        play.Changed += () => notifications++;
        play.Dispose(); play.Dispose();
        play.Tick(TimeSpan.FromHours(1));
        play.SetActive(true); play.TogglePause(); play.SelectCell(8); play.SkipAnimation(play.Stamp); play.Restart(At(seed: 2));
        Assert.False(play.SubmitHuman(play.Stamp, new(RichTownCommandKind.Roll)));
        Assert.Equal(1, notifications);
        Assert.Same(current, play.Snapshot);
        Assert.False(play.IsAnimating);
        Assert.False(play.IsActive);
        Assert.Equal("对局已关闭", RichTownPresentation.Status(play));
    }

    [Fact]
    public void 不可见或暂停时不能操作非法命令不建立动画()
    {
        using var play = new RichTownPlayController(At());
        Assert.False(play.CanInteract);
        Assert.Contains("准备", RichTownPresentation.Status(play));
        play.TogglePause();
        Assert.False(play.IsPaused);
        Assert.False(play.SubmitHuman(play.Stamp, new(RichTownCommandKind.Roll)));
        play.SetActive(true); play.SetActive(true);
        Assert.True(play.CanInteract);
        Assert.Contains("掷骰", RichTownPresentation.Status(play));
        Assert.False(RichTownPresentation.Can(play, new(RichTownCommandKind.Roll, 1)));
        Assert.False(play.SubmitHuman(play.Stamp, new(RichTownCommandKind.Buy)));
        Assert.False(play.IsAnimating);
        play.TogglePause();
        Assert.False(play.CanInteract);
        Assert.Throws<ArgumentOutOfRangeException>(() => play.Tick(TimeSpan.FromMilliseconds(-1)));
        var selected = play.SelectedCell;
        play.SelectCell(-1); play.SelectCell(24); play.SelectCell(selected);
        Assert.Equal(selected, play.SelectedCell);
        play.SelectCell(5);
        Assert.Equal(5, play.Frame.SelectedCell);
    }

    [Fact]
    public void 购买升级出售的投影及按钮状态与规则一致()
    {
        using var play = Active(At(1, phase: RichTownPhase.AwaitingPurchase));
        Assert.True(RichTownPresentation.Can(play, new(RichTownCommandKind.Buy)));
        Assert.True(RichTownPresentation.Can(play, new(RichTownCommandKind.Decline)));
        Assert.False(RichTownPresentation.Can(play, new(RichTownCommandKind.Roll)));
        Assert.True(play.SubmitHuman(play.Stamp, new(RichTownCommandKind.Buy)));
        Assert.False(RichTownPresentation.Can(play, new(RichTownCommandKind.Upgrade, 1)));
        play.SkipAnimation(play.Stamp);
        play.SelectCell(1);
        Assert.Equal(0, play.Frame.Tiles[1].OwnerId);
        Assert.True(RichTownPresentation.Can(play, new(RichTownCommandKind.Upgrade, 1)));
        play.SubmitHuman(play.Stamp, new(RichTownCommandKind.Upgrade, 1));
        Assert.Equal(1, play.Frame.Tiles[1].Level);
        play.SkipAnimation(play.Stamp);
        Assert.False(RichTownPresentation.Can(play, new(RichTownCommandKind.Upgrade, 1)));
        Assert.True(RichTownPresentation.Can(play, new(RichTownCommandKind.Sell, 1)));
        play.SubmitHuman(play.Stamp, new(RichTownCommandKind.Sell, 1));
        Assert.Null(play.Frame.Tiles[1].OwnerId);
        Assert.Equal(0, play.Frame.Tiles[1].Level);
    }

    [Fact]
    public void 中文状态覆盖债务破产终局以及各类地块()
    {
        using var debt = Active(At(11, 0).Own(1, level: 2).Act(RichTownCommandKind.Roll).Accepted());
        Assert.Contains("请选择地产出售", RichTownPresentation.Status(debt));
        Assert.False(RichTownPresentation.Can(debt, new(RichTownCommandKind.EndTurn)));
        Assert.True(RichTownPresentation.Can(debt, new(RichTownCommandKind.Sell, 1)));
        debt.TogglePause();
        Assert.Contains("暂停", RichTownPresentation.Status(debt));
        foreach (var index in Enumerable.Range(0, 24))
        {
            Assert.NotEmpty(RichTownPresentation.Cell(index));
            Assert.NotEmpty(RichTownPresentation.Details(debt.Snapshot, index));
        }
        Assert.Contains("1 号车", RichTownPresentation.Details(debt.Snapshot, 1));
        using var winner = Active(At(11, 0).Player(1, p => p with { Cash = 0, IsEliminated = true }).Act(RichTownCommandKind.Roll).Accepted());
        Assert.Contains("获胜", RichTownPresentation.Status(winner));
        winner.Tick(TimeSpan.FromHours(1));
        Assert.False(winner.CanInteract);
        Assert.False(winner.IsAnimating);
        Assert.Equal(RichTownPhase.Finished, winner.Snapshot.Phase);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(19UL)]
    [InlineData(999UL)]
    public void 一真人两电脑通过展示控制器完成完整对局且帧与规则一致(ulong seed)
    {
        using var play = Active(RichTownSnapshot.Create(seed));
        var humanCommands = 0;
        for (var tick = 0; tick < 20000 && play.Snapshot.Phase != RichTownPhase.Finished; tick++)
        {
            Assert.True(play.Log.Count <= 10);
            if (play.CanInteract)
            {
                // 只用电脑策略选择测试中的真人按钮；实际快照仍是一个真人，必须经过 SubmitHuman 的校验。
                var planning = play.Snapshot.Player(0, player => player with { IsComputer = true });
                var command = RichTownComputerPlayer.Decide(planning)!.Command;
                Assert.True(RichTownPresentation.Can(play, command));
                Assert.True(play.SubmitHuman(play.Stamp, command));
                humanCommands++;
            }
            play.Tick(TimeSpan.FromMilliseconds(100));
        }
        Assert.Equal(RichTownPhase.Finished, play.Snapshot.Phase);
        Assert.True(humanCommands > 0);
        FinishAnimation(play);
        Assert.Equal(play.Snapshot.Revision, play.Frame.Revision);
        Assert.Equal(24, play.Frame.Tiles.Length);
        Assert.Equal(3, play.Frame.Tokens.Length);
        foreach (var player in play.Snapshot.Players) Assert.Equal(player.IsEliminated, play.Frame.Tokens[player.Id].IsEliminated);
        Assert.NotEmpty(play.Snapshot.GetStandings().Where(item => item.IsWinner));
        Assert.Contains(play.Log, message => message.Contains("对局结束", StringComparison.Ordinal));
    }

    private static RichTownPlayController Active(RichTownSnapshot snapshot)
    {
        var play = new RichTownPlayController(snapshot);
        play.SetActive(true);
        return play;
    }
    private static void FinishAnimation(RichTownPlayController play)
    {
        for (var i = 0; i < 30 && play.IsAnimating; i++) play.Tick(TimeSpan.FromMilliseconds(100));
        Assert.False(play.IsAnimating);
    }
}
