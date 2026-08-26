# 扫雷 Document 设计与开发说明

## 功能边界

扫雷以普通、不可持久化的 `IPluginDocument` 接入 Host，稳定身份为
`myavalonia.plugin.classic.game.document.minesweeper`。每次打开 Document 都创建一局独立游戏；关闭后释放计时资源，
不保存对局，也不提供排行榜、统计、自定义棋盘、问号标记、音效或动画。

首版固定提供经典三级：

| 难度 | 行×列 | 雷数 |
| --- | ---: | ---: |
| 初级 | 9×9 | 10 |
| 中级 | 16×16 | 40 |
| 高级 | 16×30 | 99 |

玩家左键翻格、右键插旗或取消旗帜。再次左键点击已翻开的数字格，或者在该数字格上同时按住左右键时，
如果相邻旗帜数等于数字，就翻开其余相邻格；旗帜位置错误时可能由此踩雷。所有安全格翻开即获胜，
旗帜是否正确不参与胜利判定。

## SOLID 职责划分

SOLID 是该功能代码评审的首要约束，但不等于为每个类型创建接口。当前职责按真实变化原因划分：

```text
Features/Minesweeper/
├─ Domain/                    # 纯领域规则与布雷策略
├─ ViewModels/                # 页面、格子投影与可测试计时
├─ Views/                     # AXAML 布局和输入转发
└─ MinesweeperDocument.cs     # Plugin SDK 生命周期适配器
```

| 组件 | 单一职责 | 明确不负责 |
| --- | --- | --- |
| `MinesweeperGame` | 编排布雷、翻格、插旗、区域展开、快速展开与胜负转换 | Avalonia、Document、计时和指针输入 |
| 棋盘与格子模型 | 保存坐标、雷位、相邻雷数和覆盖状态 | 命令、颜色、文案和计时 |
| `IMinePlacementStrategy` | 按约束返回无重复雷位 | 修改棋盘或判断胜负 |
| `GameTimer` | 基于 `TimeProvider` 累计有效游戏时间 | 主动调度线程或刷新 UI |
| `MinesweeperDocument` | SDK 标题、初始化、释放，并拥有一个 ViewModel | 玩家命令、可观察属性、棋盘规则和鼠标输入 |
| `MinesweeperViewModel` | 用户命令、计时调度、顶部摘要和棋盘集合投影 | Plugin SDK、指针设备和领域规则细节 |
| `MinesweeperCellViewModel` | 把单个领域格子投影为文本与颜色 | 修改格子或判断胜负 |
| `MinesweeperDocumentView` | 接收 Host 的 Document，并单向绑定其 ViewModel 到游戏 View | 游戏布局、玩家命令和领域状态 |
| `MinesweeperView` | 直接绑定 ViewModel，负责布局、样式和左右键事件转发 | Plugin SDK、Document 和领域状态修改 |

- **单一职责：** 规则、随机、时间、Document 协议、ViewModel 展示状态和 View 输入各自只有一种变化原因。
- **开闭原则：** 新小游戏建立新的同级功能域和 Document；不修改扫雷来容纳俄罗斯方块、贪吃蛇等规则。
- **里氏替换：** `MinesweeperDocument` 只按 SDK 的 `IPluginDocument` 生命周期工作，不派生自定义 Document 基类，
  也不改变 Host 对普通 Document 的创建和释放语义。
- **接口隔离：** 只保留确有两个实现的窄布雷策略接口；计时直接使用 BCL `TimeProvider`，不再包装空洞接口。
- **依赖倒置：** 游戏引擎依赖雷位生成能力而非随机数实现，测试可注入确定雷位而不改变生产规则。

使用的设计模式只有朴素的 Strategy：随机布雷与确定性测试布雷实现同一个最小接口。游戏阶段使用枚举和显式方法，
不使用 State 类层次；界面通知沿用 `ObservableObject`，不额外引入事件总线、Mediator、仓储或抽象工厂。

## 状态与数据流

对局状态只允许按以下方向变化：

```text
Ready --首次有效翻格--> Running --全部安全格翻开--> Won
                                 \--翻到地雷--------> Lost

任意状态 --重新开始/切换难度--> Ready
```

一次左键操作的数据流如下：

1. Host 把 `MinesweeperDocument` 设置为 `MinesweeperDocumentView` 的 DataContext；包装 View 通过 XAML 单向绑定，
   在游戏 View 创建时就为其提供 `Document.ViewModel`，不依赖 DataContext 变更事件的执行顺序。
2. 格子使用 `Button.Click` 接收主要操作，再由 `MinesweeperView` 把对应的 `MinesweeperCellViewModel` 交给
   `MinesweeperViewModel`。不能在 Button 上只监听普通 `PointerPressed`：Avalonia Button 会优先处理左键按下，
   后注册的普通路由处理器可能收不到该事件。
3. ViewModel 用行列坐标调用 `MinesweeperGame.Reveal`。
4. 游戏若仍为 `Ready`，先排除首格及相邻八格，再调用布雷策略并计算全部相邻雷数。
5. 游戏使用队列展开零雷区域；队列避免展开规模与调用栈深度耦合。
6. 游戏完成胜负判断后，ViewModel 启停计时器并刷新格子与顶部摘要属性；View 仅重新读取绑定值。

插旗不会触发布雷或启动计时。计时从首次有效翻格开始，在获胜、失败、重新开始、切换难度或 Document 释放时停止。
ViewModel 拥有的 `DispatcherTimer` 只定期通知界面读取 `GameTimer`，真实耗时由 `TimeProvider` 计算，因此 UI 卡顿不会导致计时漂移。

## 关键规则与失败处理

- 首击安全区是首格与棋盘范围内的所有相邻格，因此首击相邻雷数必为零。
- 布雷策略返回的雷数、重复坐标、越界坐标和安全区坐标都会由游戏引擎再次校验；非法结果直接拒绝，不能发布半初始化棋盘。
- 零格展开不会翻开旗帜格；玩家需要先取消旗帜才能翻开该格。
- 快速展开要求旗帜数量与中心数字相等，但不会假定旗帜位置正确；错误旗帜导致翻雷属于经典规则。
- 快速展开同时支持再次左键数字格和经典左右键组合。View 只识别按键组合并复用同一个 `Reveal` 意图，
  不复制旗帜计数、邻格展开或失败判断。
- 左右键组合按住期间，中心数字周围最多八个“已覆盖且未插旗”格子会先显示缩进、变暗的下压预览；
  松开任一按键后才提交快速展开。旗帜数不匹配时只清除预览，不改变棋盘。
- 获胜只要求全部非雷格已翻开。失败后投影显示全部雷、直接引爆格和错误旗帜，领域状态不再接受输入。
- 高级棋盘保持固定 30 像素格子；Document 空间不足时由滚动区域承载，不缩小点击目标。
- 顶部面板和棋盘边框使用插件内 Light/Dark 主题字典，不依赖 Host 私有颜色资源；在黑色背景下仍保持文字、
  边框和状态提示的对比度，并可随 Host 或系统主题动态切换。

## 测试矩阵与开发门禁

领域测试使用确定性布雷策略，计时测试使用可手工推进的 `TimeProvider`，禁止依赖随机结果、`Sleep` 或真实墙钟。

| 范围 | 必测场景 |
| --- | --- |
| 难度与布雷 | 经典三级、中心/角落首击安全、雷数、重复和越界约束 |
| 数字与展开 | 八邻域数字、零格队列展开、数字边界、旗帜阻挡 |
| 旗帜与快速展开 | 插旗/取消、剩余雷数、数量不匹配、正确展开、错误旗帜踩雷 |
| 胜负与重置 | 胜利、失败、终局输入无效、重开、切换难度、多实例隔离 |
| ViewModel | 开始/停止/重置、终局停止、难度命令、多个 ViewModel 隔离 |
| View 输入 | 左键主要操作转发、纯右键插旗、组合键邻域预览/取消/松开提交、数字格快速展开 |
| Document | 默认/Host 标题、只暴露独立 ViewModel、释放时级联停止计时 |
| 插件组合 | 普通 Document 注册、包装 View 类型、Document→ViewModel 单向绑定、稳定 ID、菜单名称和分类 |

开发阶段执行以下本地门禁：

```powershell
dotnet restore
dotnet build -c Debug -warnaserror --no-restore
dotnet test -c Debug --no-build
```

本阶段不采集覆盖率阈值，不配置 Windows CI，不执行 Release 构建、ZIP 打包或发布验收。发布门禁仍按
`deployment-and-release.md` 在真正发布时单独执行。

## 手工体验检查

1. 启动 Standalone，分别切换初级、中级和高级，确认棋盘尺寸、雷数和滚动行为正确。
2. 首次点击棋盘中心与角落，确认都会展开空白区域且不会踩雷。
3. 验证左键可以稳定翻格、右键可以插旗/取消；再次左键数字格及左右键组合都能快速展开，并验证错误旗帜导致失败。
4. 验证计时只从首次翻格开始，重新开始和切换难度清零。
5. 分别使用浅色与深色 Host/系统主题，确认顶部文字、帮助文字、棋盘边框和格子在黑色背景下清晰可见。
6. 在真实 Host 中打开两个扫雷 Document，确认标题、棋盘、旗帜、难度和计时互不影响。

未来新增游戏时，在 `Features` 下建立与 `Minesweeper` 同级的独立目录，注册新的稳定 Document ID，并编写自己的
专项文档。只有出现至少两个真实且稳定的共同需求后，才评估抽取共享能力。
