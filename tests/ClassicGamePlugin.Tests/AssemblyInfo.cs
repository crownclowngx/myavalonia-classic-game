using Xunit;

// Avalonia Dispatcher 是进程级单例；本测试程序集中的普通 Fact 会共同投递绑定任务。
// 串行化只约束测试基础设施，不改变游戏生产并发行为。它保证一个用例排空的正是自己
// 创建的 View/Binding，避免另一个并行用例偶然执行这些任务而造成覆盖率证据漂移。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
