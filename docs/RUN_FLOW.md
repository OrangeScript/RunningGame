# Running：可玩版本运行流程与系统说明

> Unity：2022.3.62f2c1  
> 网络：Netcode for GameObjects 1.12.2  
> 正式入口：`Assets/Scenes/Bootstrap.unity`

本文描述当前版本已经接入的正式流程、操作方式、服务器权威边界和后续扩展接口。测试场景 `CombatTest`、`CrowdTest`、`StatusTest` 不属于正式流程。

## 1. 启动与操作

### Editor

1. 用 Unity 2022.3.62f2c1 打开项目。
2. 打开 `Assets/Scenes/Bootstrap.unity`。
3. 点击 Play。
4. 选择一种启动方式：
   - `独自出发`：启动本机 Host，直接进入完整 Running 流程。
   - `首领练习`：单人直接挑战三段符卡首领，预置双元素和消弹道具。
   - `创建房间`：Host 创建 2～4 人房间，选择跑酷旅程或首领练习后开始。
   - `加入房间`：Client 加入当前 Unity Transport 地址对应的房间。

### 操作

| 阶段 | 操作 |
|---|---|
| 爬塔地图 | 滚轮或拖动上下查看路线；点击可达节点投票；再次点击可改票 |
| Running | `A/D` 或 `←/→` 横向移动，角色自动前进与自动射击 |
| Running 主动道具 | `Space` 使用第一个主动道具，`2` 使用第二个 |
| Boss Arena | `WASD` 移动，`Shift` 精确移动，鼠标瞄准，左键或 `Space` 持续射击；擦弹蓄满后 `R` 消弹 |
| Arena 主动道具 | `Q/E` 使用第一/第二个主动道具 |
| 三选一 | 鼠标点击一张卡牌 |

### Build Settings

启用且按顺序排列：

1. `Bootstrap`
2. `RunLoop`
3. `Arena`

正式包不从测试场景启动。

普通道路最新流程、54 件道具、驻守敌人、沿途拾取与障碍机制见 [RUNNING_CONTINUOUS.md](RUNNING_CONTINUOUS.md)；首领符卡与联机预测见 [BOSS_DESIGN.md](BOSS_DESIGN.md)。

## 2. 一局游戏的正式闭环

```text
Bootstrap
  -> Host / Client 建立 NGO 会话
  -> RoomSession 统一加载 RunLoop
  -> 按 clientId 生成每位玩家的 Runner Pawn
  -> 服务器生成本局 seed 和爬塔 DAG
  -> 所有人在地图上公开投票
  -> 票数最高的可达节点胜出（全员投完可提前结算）
  -> 根据节点类型执行内容
       |-- Combat / Elite：生成 N 条独立随机道路
       |-- ?：发放临时奇异增益三选一
       |-- Reward：直接获得普通永久构筑三选一
       |-- Treasure：高品质宝藏三选一
       `-- Boss：保存构筑并切换 Arena
  -> 道路玩家持续前进、过门、前向射击、拾取击杀掉落，进度互不冻结
  -> 全部存活玩家到达终点
  -> 每位玩家独立三选一
  -> 返回地图，继续下一轮公开投票
  -> 最终 Boss 节点
  -> 同场俯视角移动、射击、躲避弹幕
  -> Victory / Defeat
```

地图节点不会直接加载若干互不相干的场景。地图是整局的路线状态；普通和精英节点在 `RunLoop` 中生成道路，最终 Boss 才通过 NGO 场景切换进入 `Arena`。

## 3. 随机爬塔地图

服务器用本局 seed 生成分层有向无环图（DAG）：

- 普通战斗 `Combat`
- 精英战斗 `Elite`
- 随机事件 `? / Event`
- 奖励关 `Reward`
- 宝藏关 `Treasure`
- 唯一最终 Boss

生成约束：

- 每局布局和节点内容 seed 可复现。
- 所有有效路线最终汇入唯一 Boss。
- 每张地图都包含全部节点类型。
- 玩家只能投当前节点实际可达的下一层节点。
- 客户端只提交节点 ID，不能自行决定路线结果。

多人投票规则：

1. UI 使用全屏纵向路线图，从下向上推进，支持滚轮和拖动；显示连线、可达节点和倒计时。
2. 每位玩家的选择、节点票数会同步给所有人。
3. 玩家可以在截止前改票。
4. 全部在线玩家投票后立即结算；否则倒计时结束时结算。
5. 默认保留 90 秒查看地图；全员投票可立即结算。最高票胜出，平票由 `matchSeed + 当前节点 + 投票轮次` 确定性打破。
6. 玩家断线后从本轮应投集合移除，避免房间永久等待。
7. 选路结算立即关闭地图，进入关卡或奖励；旧的投票回调不会再次打开界面。

核心实现：

- `Assets/Scripts/Game/RunMap/RunMapGenerator.cs`
- `Assets/Scripts/Game/RunMap/RunMapVoteResolver.cs`
- `Assets/Scripts/Game/RunMap/RunMapNetworkCoordinator.cs`
- `Assets/Scripts/Game/RunMap/RunMapRuntimeFlow.cs`
- `Assets/Scripts/Game/UI/RunMap/RunMapRuntimeUI.cs`

## 4. 多人并行随机道路

选择 Combat 或 Elite 后，服务器按当前有效玩家数生成 1～4 条并列道路。每位玩家拥有固定 `laneId`，并被传送到对应道路起点。

每条道路宽 16 米，角色中心可移动范围为左右各 7.25 米，相邻道路间距至少 22 米。地面、碰撞、终点与镜头使用一致的道路尺寸。每名玩家始终只有一个角色，模型与根 CharacterController 对齐，停用旧的人群方阵。

每条道路由自己的 lane seed 生成，具备：

- 不同的 section 内容 seed。
- 真正变化的 Run / Gate / Combat 编排。
- 每条路必定包含 Run、Gate、Combat。
- 相邻 section 不会是同一种类型。
- 门数值、障碍种类、敌人组合与位置独立随机。
- Elite 使用更高难度系数影响障碍、敌人预算和属性。

道路玩家状态是逐玩家维护的：

- `Running`：自动前进，可左右躲避。
- `Combat`：兼容旧接口的战斗标记，仍然前进；正式战区不再切换停车状态。
- `RewardChoice`：等待自己的三选一。
- `WaitingAtExit`：已完成，等待队友。
- `Eliminated`：本节点不再计入待完成集合。

Gate、Enemy、Encounter、终点触发器都带 lane 约束，防止一条路的玩家触发另一条路的内容。

核心实现：

- `Assets/Scripts/Game/Run/Generation/DeterministicSectionPlanner.cs`
- `Assets/Scripts/Game/Run/SectionManager.cs`
- `Assets/Scripts/RunnerController.cs`
- `Assets/Scripts/Game/Run/RunLaneExitTrigger.cs`

## 5. 门与随机障碍

门按配对选择生成，覆盖四种运算：

- `+ N`
- `− N`
- `× N`
- `÷ N`

门的运算与数值由 section seed 和难度决定，作用于火力而非角色数量。成对的门并排放置，每位玩家只能触发该组中的一个选项。火力最低为 1；单发伤害随火力提高，散射/多重射击由道具提供。生命独立计算，敌人和障碍扣生命，负向门不会直接杀死角色。进入 Arena 时保留已积累的火力。

每个道路 section 会生成确定性随机障碍：

- 木箱、加固掩体、爆桶：可被射击破坏，完整时挡住双方射线；爆桶可同路连锁。
- 横移挡板、旋臂：按服务器时间同步运动，改变射击与通行路线。
- 周期激光、伸缩地刺：预告危险窗口，留出稳定侧方逃生带。

服务器创建伤害和破坏判定，客户端按 seed 构建并接收可靠破坏状态。所有障碍对角色都使用触发器，不会把自动前进角色卡死；每段保留连续可绕行的空间。

核心实现：

- `Assets/Scripts/Game/Gate.cs`
- `Assets/Scripts/Game/Procedural/ProceduralObstacleField.cs`
- `Assets/Scripts/Game/Procedural/ProceduralSectionRuntime.cs`

## 6. Running 战斗与敌人 AI

敌人在玩家进入视野前已生成，固定驻守原地。玩家始终向前跑，默认弹丸只沿世界 +Z 发射，普通敌弹沿 -Z 迎面射来；只有实际弹道相交才命中。玩家通过横移对齐、绕障、拾取原地掉落形成构筑。轻追踪需要对应道具，有严格前方锥角和转速限制。

敌人生成使用预算制：

- 难度影响预算，实际每组限制为 2～4 名驻守敌人。
- 精英路包含特殊驻守者，生命与射击节奏有类型差异。
- 选点避开障碍整个运动包络，不够安全时宁可少生成。
- 位置、原型、数量和生命浮动由 encounter seed 决定。

当前 AI 原型：

- `Chaser`：普通驻守卫兵，严格向前直射。
- `Strafer`：驻守扇射手，三路迎面射击。
- `Charger`：驻守狙击手，较长预告后沿有限角度锁定方向开火。
- `Ranged`：驻守连射手，三发短连射。

原枚举值保留以兼容 prefab，Approach 只表示准备阶段。敌人只向本 lane 的迎面玩家开火，玩家跑过后停止攻击；漏杀不阻止关卡结束。击杀是真正的击杀事件，退场清理不算击杀，也不生成掉落。

核心实现：

- `Assets/Scripts/Game/Run/CombatEncounter.cs`
- `Assets/Scripts/Game/Combat/Enemy.cs`
- `Assets/Scripts/Game/Combat/AutoCombat.cs`

## 7. 道具、增幅和获取规则

### 三类道具

| 类型 | 生命周期 | 主要来源 |
|---|---|---|
| 被动 `Passive` | 整局保留，可按规则叠加 | 道具门、击杀原地拾取、节点结算、宝藏 |
| 主动 `Active` | 整局保留，拥有主动键和冷却 | 普通节点结算、宝藏 |
| 临时 `Temporary` | 限时，时间结束由服务器移除 | `?`、事件 |

临时道具只在玩家实际处于 Running、Combat 或 Arena 时推进剩余时间；地图投票、等待队友和三选一不会消耗时长。切换到 Boss Arena 时，构筑快照会保留临时道具的实际剩余时长，不会借切场景刷新持续时间。

获取规则由服务器统一校验：

- 道具门直接授予图标对应的被动；每个真正击杀在原地留下被动，靠近即获得。来源规则已按本轮新设计更新。
- Combat / Elite 完成后，每位存活玩家独立获得三选一。
- `?` 从临时道具池中选择；Reward 直接提供普通永久构筑三选一。
- 普通道具门最高 Rare；精英特殊敌人可掉落 Epic，宝藏继续提供高品质选择。
- 当前 Treasure 池至少包含三件 Epic，可稳定形成真正的三选一；棱镜齐射和虚空重炮在 Running/Arena 使用同一数值定义、不同攻击表现。
- 客户端只能选择服务器实际发给自己的候选 ID。
- 候选不足三张时安全降级，不会卡死结算。

### 同一道具跨玩法复用

`UpgradeData` 保留稳定 ID、分类、品质、图标和构筑身份；效果通过 `GameplayMode` 解释：

- `Runner`：影响火力、前向弹道、有限追踪、穿透、防御、恢复与拾取，不生成角色方阵。
- `BossArena`：玩家始终只有一个 2D 角色，人数/齐射类效果映射成多重射击、散射、攻速或伤害，而不是复制角色。
- `FirstPerson`：已预留同一个模式适配接口；当前版本未实现第一人称 Pawn。

Running 切换 Arena 前，`RoomSession` 保存每位玩家的道具定义；Arena Player 生成后恢复构筑，并把 ItemManager 切到 `BossArena`。

核心实现：

- `Assets/Scripts/Game/UpGrade/UpgradeData.cs`
- `Assets/Scripts/Game/Items/ItemModel.cs`
- `Assets/Scripts/Game/Items/ItemManager.cs`
- `Assets/Scripts/Game/Items/ContextualItemRuntime.cs`
- `Assets/Scripts/Game/Items/ItemRuntimes/TimedCombatBoostRuntime.cs`

## 8. Boss Arena

最终 Boss 节点执行以下顺序：

```text
冻结 Running 流程
  -> 保存每位玩家构筑
  -> NGO Single 模式加载 Arena
  -> 移除旧 Runner PlayerObject
  -> 按客户端席位生成 Arena PlayerObject
  -> 恢复构筑并切换 BossArena 模式
  -> 绑定本地俯视相机
  -> 生成 Boss、边界和 HUD
  -> 服务器权威移动、瞄准、射击、命中与血量
```

Boss 弹幕包含：

- 带两个移动缺口的环弹。
- 对每位存活玩家预告并锁定方向的弹流，与多臂螺旋交替。
- 最终符卡的缺口雨幕、弹流与环弹交错。
- 70% 和 35% 生命阈值切换符卡，清除旧弹幕；元素破甲另有消弹与弱点窗口。

玩家全灭判定 Defeat；Boss 生命归零判定 Victory。Arena 不再用“人口等于多个角色”的表现，每位玩家只控制一个角色。

核心实现：

- `Assets/Scripts/Arena/ArenaBootstrap.cs`
- `Assets/Scripts/Arena/ArenaPlayerNetwork.cs`
- `Assets/Scripts/Arena/ArenaBossController.cs`
- `Assets/Scripts/Arena/ArenaBossProjectile.cs`
- `Assets/Scripts/Arena/ArenaRuntimeProjectile.cs`

## 9. 权威状态边界

服务器负责：

- 本局 seed、地图和可达分支。
- 投票成员、票数、截止时间和平票结果。
- 道路计划、门、障碍碰撞和敌人生成。
- 每位玩家的 phase、lane、人口和库存。
- 三选一候选与获取合法性。
- Arena 移动、射击、弹幕伤害和胜负。

客户端负责：

- 提交移动、投票、选卡和主动技能请求。
- 展示地图、道路、投票、HUD 和特效。
- 根据同步状态做插值；不能直接改权威数值。

随机逻辑使用专用确定性 PRNG，不依赖 `UnityEngine.Random` 的全局状态；相同 seed 可用于复现问题。

## 10. 自动验收

主要 Editor 命令行验收入口：

- `RunningProjectValidator.ValidateCommandLine`
  - 检查 Build Settings、场景引用、NetworkObject、section prefab hash。
  - 检查道路核心类型、跨 seed 布局多样性、四种门运算。
  - 检查 Passive / Active / Temporary 池。
  - 对多组 seed 生成并校验完整爬塔图。
  - 检查 Arena Player 必需组件。
- `RunningPlayModeSmoke.Run`
  - 从 Bootstrap 真正启动 Host。
  - 进入 RunLoop、投票 Combat/Elite。
  - 转到 `RunningContinuousPlayModeSmoke.Run`，通过真实碰撞探针检查打空 / 命中 / 追踪 / 穿透 / 掩体。
  - 验证门图标奖励与原地拾取、防御、自动前进不中断。
  - 自然跑完整条道路，完成三选一并返回地图，再检查失败和新局重开。
- `RunningContinuousValidator.ValidateCommandLine`
  - 汇总新武器、敌人、障碍和掉落规则、54 件目录与模式适配。
- `RunningContinuousNetworkSmoke.BeginHost` / `BeginClient`
  - 双进程模拟延迟，覆盖实际投票、地图隐藏、前射、敌弹、拾取、构筑与破坏同步。
- `RunningMultiplayerLogicValidator.ValidateCommandLine`
  - 覆盖公开票、改票、平票、超时、非法票和断线移票/即时解析。
- `RunningArenaPlayModeSmoke.Begin`
  - 从 Bootstrap 启动 Host，经 RunLoop 真正切到 Arena。
  - 验证旧 Runner 被替换、唯一 Arena Player、Boss/HUD/俯视相机和 BossArena 道具恢复。
  - 验证普通射击、跨模式主动雷暴、Boss 扣血和 Victory 事件。

日志成功标记分别为：

```text
RUNNING_VALIDATION_OK
RUNNING_CONTINUOUS_VALIDATION_PASS
RUNNING_CONTINUOUS_PLAYMODE_PASS
RUNNING_CONTINUOUS_NETWORK_CLIENT_PASS
RUNNING_MULTIPLAYER_LOGIC_OK
RUNNING_ARENA_SMOKE_PASS
```

## 11. 当前明确边界

- 当前房间上限为 4 人。
- 本机/局域网联调用 Unity Transport；公网发布仍需接入可配置地址和 Relay/打洞方案。
- 第一人称模式只有道具上下文接口，尚未实现第一人称角色、武器和场景。
- 运行时 UI 以功能完整和可替换为目标，美术资源可在不改变网络领域逻辑的前提下替换。
