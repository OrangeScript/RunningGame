# 《Running》游戏策划案

**文档版本：** v0.1  
**项目名称：** Running  
**游戏类型：** 3D 跑酷 + 自动战斗 + Roguelike 构筑 + 元素反应 + 多人合作  
**开发引擎：** Unity  
**目标模式：** 单人 / 未来 2–4 人合作  
**当前阶段：** 核心玩法原型阶段

---

# 1. 游戏概述

## 1.1 一句话介绍

《Running》是一款以“**不断前进的人群**”为核心载体，将数字跑酷、自动战斗、元素反应与 Roguelike 道具组合融合在一起的 3D 动作游戏。

玩家在关卡中不断向前奔跑，通过数字门改变自身人口规模，在战斗区域停止前进并自动攻击敌人。

每场战斗结束后，玩家可以从随机出现的强化中选择一个，使自己的人群形态、攻击方式、子弹性质、元素体系甚至整个战斗规则不断发生变化。

游戏希望形成一种：

> **跑酷负责积累资源，战斗负责检验构筑，Roguelike 道具负责制造离谱组合。**

的核心体验。

---

# 2. 核心体验

Running 的重点不是单纯追求“数字越来越大”。

玩家真正需要获得的体验是：

### 2.1 人群不断成长

最开始可能只有一个角色。

经过：

- +10
- ×2
- ×3
- 战斗奖励
- 特殊道具

之后逐渐形成几十甚至上百人的队伍。

人口既是玩家最直观的成长反馈，也是游戏中非常重要的资源。

---

### 2.2 构筑逐渐失控

游戏中的强化尽量避免：

- 攻击力 +10%
- 射速 +5%
- 暴击率 +3%

这种纯数值成长。

更希望出现类似：

**巨人化**

> 100 人的人口依然存在，但画面上只剩一个巨大角色。  
> 一轮只发射一颗巨大炮弹。  
> 攻击速度降低，但伤害和子弹尺寸大幅增加。

**弹射**

> 子弹击中敌人后会寻找附近的新敌人继续攻击。

**爆炸**

> 每次命中都会产生范围伤害。

**穿透**

> 子弹可以继续穿向后方敌人。

当这些能力组合以后：

> 巨型炮弹 + 穿透 + 爆炸 + 火元素

和：

> 大量小子弹 + 弹射 + 雷元素 + 感电

应该形成完全不同的战斗体验。

---

# 3. 游戏核心循环

当前游戏核心循环定义为：

**Running**

↓  

**经过跑酷区域 / 数值门 / 陷阱**

↓  

**进入 Combat Encounter**

↓  

**停止自动前进**

↓  

**生成敌人**

↓  

**自动战斗**

↓  

**敌人全部死亡**

↓  

**三选一强化**

↓  

**获得新道具 / 新能力**

↓  

**继续 Running**

↓  

**Elite / Boss**

↓  

**完成本局**

---

# 4. Run 状态系统

当前游戏一局 Run 被划分为以下状态：

| 状态 | 含义 |
|---|---|
| Running | 玩家自动向前奔跑 |
| Combat | 玩家停止向前，进行战斗 |
| UpgradeSelection | 战斗结束，选择强化 |
| GameOver | 本局失败 |
| Victory | 本局胜利 |

当前代码已经完成：

**Running → Combat → UpgradeSelection → Running**

这一核心状态循环。

GameOver 和 Victory 已经进入状态设计，但完整触发逻辑仍属于后续内容。

---

# 5. 关卡结构

游戏不采用一整张完全写死的大地图，而是使用 **Section** 作为基础关卡单位。

当前 Section 类型包括：

- Run
- Gate
- Combat
- Elite
- Boss
- Finish

---

## 5.1 Run Section

普通奔跑区域。

主要作用：

- 提供节奏缓冲
- 横向移动
- 躲避障碍
- 收集资源
- 为下一个选择做准备

后续可以加入：

- 金币
- 陷阱
- 可破坏物
- 临时 Buff
- 分岔道路
- 隐藏区域

---

## 5.2 Gate Section

数字门区域。

典型形式：

> +10 / ×2  
>   
> ÷2 / +30  
>   
> ×3 / 获得特殊效果

Gate 首先作用于人口系统。

后续可以扩展出特殊门：

**元素门**

> 所有普通子弹暂时变成火元素。

**风险门**

> 人口 -50%，但获得稀有强化。

**赌博门**

> 随机 ×0.5 ～ ×4。

**变异门**

> 改变人群形态，而不是直接改变数字。

---

# 6. Combat Encounter

Combat Encounter 是跑酷与战斗之间的转换节点。

玩家进入 Combat 区域后：

1. Encounter 被触发。
2. 前进停止。
3. 战斗区域封锁。
4. 按 Spawn Point 生成敌人。
5. 玩家开始自动攻击。
6. Enemy 死亡后从存活列表移除。
7. 全部敌人死亡。
8. Combat Clear。
9. 打开三选一界面。
10. 玩家选择强化。
11. 战斗区域解除。
12. 恢复 Running。

因此战斗不是独立场景。

它是整个跑酷路线中的一个 **战斗房间**。

---

# 7. 玩家移动

玩家基础移动采用：

**自动前进 + 玩家控制左右**

Running 状态：

> 自动向前移动。

Combat 状态：

> 停止向前移动。

目前横向移动仍然保留，因此未来可以决定战斗时采用哪种方式：

### 方案 A：战斗阶段允许左右移动

玩家可以通过移动：

- 躲避攻击
- 调整子弹角度
- 靠近特殊区域

操作性更强。

### 方案 B：战斗阶段完全固定

玩家主要关注：

- 构筑
- 元素组合
- 人群状态

游戏更加偏向 Roguelike 构筑。


### 方案 C：可以变成俯视角射击游戏？？

玩家主要关注：

- 构筑
- 元素组合
- 人群状态
- 射击技术

游戏更加偏向 Roguelike 构筑。

**当前推荐采用方案 A。**
**希望变成C**

这样不会让 Combat 阶段完全退化成“站着看”。

---

# 8. 人口系统

人口 Population 是 Running 最重要的基础资源之一。

Population 不等同于屏幕上的人物数量。

当前系统已经将：

**逻辑人口**

和

**视觉人口**

进行了分离。

---

## 8.1 逻辑人口

例如：

> Population = 137

意味着玩家实际拥有 137 人。

这个数字可以参与：

- 数字门
- 攻击计算
- 伤害承受
- 道具效果
- 特殊机制

---

## 8.2 视觉人口

为了性能和视觉表现，不要求 Population = 137 时真的生成 137 个完整角色。

当前视觉单位存在最大数量限制。

例如：

> Population = 300  
> Visual Unit = 80

逻辑依然按照 300 计算。

---

## 8.3 特殊形态

Population 与 Visual 分离后，可以制造大量特殊构筑。

例如 Giant：

> Population = 80  
>   
> Visual Unit = 1

逻辑上仍然拥有 80 人，但画面中变成一个巨人。

这也是未来很多特殊道具的重要设计基础。

---

# 9. 自动战斗系统

玩家进入攻击范围后自动寻找附近敌人。

当前攻击流程为：

**PlayerStats**

↓

生成基础 **AttackPlan**

↓

所有 Item 修改 AttackPlan

↓

确定这一轮攻击：

- Projectile Count
- Attack Interval
- Attack Range
- Shot Spacing

↓

Projectile Archetype

↓

生成基础 **ProjectileSpec**

↓

所有 Item 修改 ProjectileSpec

↓

生成最终 Projectile

---

这种结构意味着道具并不是直接操纵具体 Projectile。

而是：

> 一层修改“这一轮怎么攻击”。

另一层修改：

> “每颗子弹最终是什么样”。

这是未来产生复杂组合的重要基础。

---

# 10. Projectile 系统

Projectile 是当前战斗构筑的核心载体。

基础属性包括：

- Damage
- Speed
- Size
- Element

目前已经存在的特殊行为包括：

### Bounce

子弹命中敌人后，可以寻找附近其他敌人继续弹射。

---

### Explosion

命中目标时产生范围爆炸，对附近敌人造成伤害。

---

### Pierce

命中敌人后继续寻找攻击方向前方的敌人。

---

因此理论上可以产生：

> 穿透 + 弹射

> 爆炸 + 弹射

> 巨型 + 爆炸

> 穿透 + 爆炸 + 元素

等组合。

后续 Projectile 行为可以继续增加：

- Homing
- Split
- Orbit
- Chain
- Returning
- Shotgun
- Laser
- Black Hole
- Boomerang

但所有新行为都应该优先考虑：

> **能否与已有行为产生组合？**

而不是单独做一个好看的效果。

---

# 11. Roguelike 道具系统

道具是 Running 中最主要的 Build 来源。

每完成一个 Combat Encounter：

> 随机出现三个 Upgrade。

玩家从中选择一个。

Upgrade 创建对应 ItemRuntime，并加入当前 ItemManager。

之后 ItemRuntime 可以持续影响玩家。

---

# 12. 道具类型

目前游戏可以将道具分成四类。

## 12.1 Attack Modifier

修改整轮攻击。

例如：

- 攻击次数
- 子弹数量
- Attack Interval
- Attack Range

---

## 12.2 Projectile Modifier

直接改变 Projectile。

例如：

- Bounce
- Explosion
- Pierce
- Heavy Bullet

---

## 12.3 Element Core

改变攻击元素或与元素系统产生联动。

目前仓库中已经出现：

- Ice Core
- Rain Core
- Earth Core

后续可以形成完整七元素 Core。

---

## 12.4 Active Item

需要玩家主动触发的攻击型道具。

例如当前已有：

**Thunder Strike**

玩家主动释放后：

> 对一定范围内的敌人施加雷元素并造成伤害。

Active Item 拥有自己的 Cooldown。

未来可以形成类似：

> Q / E / Space 主动技能

的体系。

---

# 13. Giant

Giant 是目前第一个真正体现 Running 道具设计理念的道具。

获得 Giant 后：

### 人群

视觉人群变成：

> 1 个巨大角色。

但实际 Population 不消失。

### 攻击

Projectile Count：

> 强制变为 1。

攻击频率降低。

伤害大幅提高。

Projectile Size 大幅提高。

Projectile Speed 降低。

最终战斗体验从：

> 一群角色连续射击。

变成：

> 一个巨大角色缓慢发射巨型炮弹。

这种“改变玩法规则”的道具应该成为以后道具设计的主要方向。

---

# 14. 元素系统

Running 当前拥有七种元素：

- Fire
- Water
- Ice
- Lightning
- Wind
- Earth
- Nature

以及 None。

敌人可以拥有 Element Aura。

元素存在：

- Aura Amount
- Aura Duration

新的元素攻击命中敌人时，会检测已有 Aura，并尝试触发 Reaction。

---

# 15. 当前元素反应

目前系统已经定义：

### Frozen

Water + Ice

效果：

> 冻结目标一段时间。

---

### Melt

Fire + Ice

根据触发顺序获得不同伤害倍率。

---

### Vaporize

Fire + Water

根据触发顺序获得不同伤害倍率。

---

### Overload

Fire + Lightning

效果：

> 在目标附近产生范围伤害。

---

### Electro-Charged

Water + Lightning

效果：

> 持续造成雷元素伤害，并向附近敌人传递伤害。

---

### Superconduct

Ice + Lightning

已经进入 Reaction 定义。

完整 Gameplay Effect 仍需要进一步设计。

---

目前：

**Wind / Earth / Nature**

还没有形成完整的反应网络。

这三种元素后续应当根据 Running 自身玩法设计，而不需要完全复刻其他游戏。

---

# 16. 元素设计原则

元素系统不应该只是：

> 火：红色 +10 伤害  
> 冰：蓝色 +10 伤害

元素的重点应该是改变攻击行为。

例如：

### 冰

倾向：

- 控制
- 减速
- 冻结
- 粉碎

### 火

倾向：

- 爆炸
- 持续伤害
- 范围伤害

### 水

倾向：

- Aura 扩散
- 连接
- 为反应提供底层条件

### 雷

倾向：

- 连锁
- 高频
- 群体攻击

### 风

倾向：

- 聚怪
- 扩散元素
- 改变 Projectile 轨迹

### 岩

倾向：

- 巨型 Projectile
- 冲击
- 护盾
- 地形

### 草

倾向：

- 生成物
- 延迟反应
- 增殖
- 自动攻击单位

---

# 17. Build 组合设计

Running 的长期乐趣来自“组合”，而不是单件道具。

例如：

## Build A：雷暴流

Rain Core

+

Thunder Core

+

Bounce

+

Thunder Strike

↓

大量敌人被附着 Water。

↓

Lightning 触发 Electro-Charged。

↓

电流不断向其他敌人传播。

↓

形成大范围雷暴。

---

## Build B：巨炮流

Giant

+

Heavy Bullet

+

Pierce

+

Explosion

↓

巨大角色。

↓

一次只射一颗炮弹。

↓

炮弹穿透敌群。

↓

每命中一个敌人都触发爆炸。

---

## Build C：冰爆流

Ice Core

+

Rain Core

+

Pierce

+

Explosion

↓

大范围 Frozen。

↓

追加高伤害攻击。

↓

触发后续“碎冰”机制。

↓

产生连锁爆炸。

---

理想状态下：

> 玩家不是在找“最强的单件装备”。

而是在想：

> “这个东西能不能和我现在的 Build 搞出一个奇怪组合？”

---

# 18. 敌人设计

当前 Enemy 已经具备：

- Health
- Damage
- Death
- Aim Position
- Element Aura
- Status
- Reaction
- Died Event

现阶段敌人主要承担测试战斗系统的作用。

未来建议逐步分成：

### 普通敌人

数量多，能力简单。

### 特殊敌人

拥有明显机制。

例如：

- 护盾怪
- 冲锋怪
- 治疗怪
- 远程怪
- 元素怪

### Elite

一个 Combat Section 中的强化敌人。

承担：

> Build 检验。

### Boss

一整个阶段的最终检查。

Boss 不应该只是：

> 血量 ×20。

而应该拥有阶段机制和移动压力。

---

# 19. Section 关卡系统

当前 SectionManager 已经能够根据 Section Prefab 顺序拼接有限长度 Run。

例如：

**Run**

↓

**Gate**

↓

**Run**

↓

**Combat**

↓

**Run**

↓

**Gate**

↓

**Combat**

↓

**Elite**

↓

**Boss**

↓

**Finish**

当前首先采用有限关卡是合理的。

因为开发阶段更容易：

- 控制节奏
- 测试 Build
- 调整难度
- Debug

---

# 20. 未来随机 Run

当基础循环稳定以后，再把 SectionManager 扩展成随机生成。

例如：

一个 Stage：

> Run → Gate → Combat

随机若干次。

随后：

> Elite

然后进入下一阶段。

最终：

> Boss → Finish

可以根据权重随机生成不同 Section：

| Section | 出现概率 |
|---|---:|
| Run | 高 |
| Gate | 高 |
| Combat | 高 |
| Treasure | 中 |
| Event | 低 |
| Elite | 固定节点 |
| Boss | 阶段结尾 |

---

# 21. 游戏节奏

建议一局控制在：

**20–35 分钟**

一局可以划分成：

### Stage 1

Build 成型。

玩家开始拥有：

2–4 个核心效果。

### Stage 2

Build 开始产生组合。

出现 Elite。

### Stage 3

敌人明显增强。

玩家需要利用元素和道具协同。

### Final Stage

构筑基本完全体。

进入最终 Boss。

---

# 22. 多人合作

Running 最终目标支持：

**2–4 人合作。**

当前项目已经引入 Unity Netcode for GameObjects 与 ParrelSync。

但目前属于：

> **网络基础设施准备阶段。**

并不代表核心 Gameplay 已经网络化。

后续所有新系统设计都应尽量遵循：

> 不把单机逻辑写死到某一个本地 Player 身上。

---

# 23. 多人模式基本设想

多人游戏不建议只是：

> 单人游戏里出现四个一模一样的人。

多人需要产生合作 Build。

例如四名玩家分别倾向：

### 玩家 A

Water / 控制 / 辅助附着

### 玩家 B

Lightning / Chain

### 玩家 C

Fire / Explosion

### 玩家 D

Giant / Heavy Projectile

因此：

> A 给敌人挂水。

> B 触发感电。

> C 触发蒸发。

> D 用巨炮完成收割。

元素体系天然适合作为合作玩法的基础。

---

# 24. 多人人口系统

需要后续确定一个关键问题：

### 方案 A：每人拥有独立 Crowd

例如：

Player 1：40 人  
Player 2：80 人  
Player 3：20 人

优点：

构筑独立。

推荐。

---

### 方案 B：所有玩家共享 Population

例如：

Team Population = 150

优点：

合作感强。

缺点：

个人成长反馈较弱。

---

目前策划默认：

> **每个玩家拥有独立 Population 与 Build。**

团队共同完成 Encounter。

---

# 25. UI 系统

当前已经或正在形成的 UI 包括：

### Population UI

实时显示当前逻辑人口。

### Combat Text

显示：

- Damage
- Element Reaction

### Element Aura UI

敌人头顶显示当前元素附着。

### Upgrade Selection

Combat Clear 后进行三选一。

未来需要增加：

- Player HP / Population
- Active Item Cooldown
- 当前 Build
- Boss HP
- Stage Progress
- Multiplayer Teammate Status

---

# 26. 美术方向

目前项目仍以机制开发为主，美术风格暂不锁死。

但 Running 的视觉应该满足三个核心要求：

### 第一：信息非常清楚

玩家必须一眼识别：

- 哪个是火
- 哪个是冰
- 哪个是雷
- 是否 Frozen
- 是否发生 Reaction

### 第二：数量感强

几十个人奔跑、射击时要有视觉爽感。

### 第三：Build 变化必须肉眼可见

获得 Giant 后必须巨大。

获得 Explosion 后必须爆炸明显。

获得 Bounce 后必须明显看到子弹弹射。

获得 Lightning 后必须看到电流连锁。

玩家应该：

> 不看 UI 也能大概知道自己的 Build 变成了什么。

---

# 27. 音效方向

声音应重点强化：

- Gate 选择
- 人口增加
- Projectile 发射
- Projectile 命中
- Explosion
- Element Reaction
- Upgrade 获得
- Combat Clear
- Boss 出场

随着 Build 变强，声音也应该逐渐变得夸张。

最终形成：

> 从几颗普通子弹的声音，

变成：

> 满屏连锁爆炸、电流和元素反应。

---

# 28. 单局成长

当前优先开发：

> **Run 内成长。**

即：

进入游戏时基本没有 Build。

通过：

Combat → Upgrade

逐渐形成完整构筑。

暂时不急于加入：

- 永久属性
- 天赋树
- 装备强化
- 永久攻击力

等局外成长。

因为当前阶段应该首先验证：

> **“一局游戏本身是否足够好玩”。**

---

# 29. 游戏失败

暂定失败核心条件：

> Population 归零。

后续还可以加入：

- Boss 战失败
- 全体玩家死亡
- 特殊任务失败

GameOver 后：

展示本局：

- 通过距离
- 击杀敌人数
- 最大 Population
- 获得道具
- 触发元素反应次数
- 最终 Build

随后：

> Restart / Main Menu。

---

# 30. 游戏胜利

玩家完成最终 Boss Encounter 后：

进入：

**Finish Section**

↓

RunState.Victory

↓

显示本局 Build 与统计数据。

未来 Roguelike 模式还可以提供：

> Continue / Endless Mode

让完成 Build 的玩家继续测试强度。

---

# 31. 当前版本已经形成的系统

截至本策划案对应仓库版本，目前游戏已经形成：

- Runner 自动前进
- 横向移动
- Gate 基础人口运算
- Crowd Population
- Crowd Visualizer
- Population 与视觉人数分离
- Auto Combat
- AttackPlan
- ProjectileSpec
- Projectile Archetype
- Bounce
- Explosion
- Pierce
- Giant
- Active Item
- Thunder Strike
- 七元素基础定义
- Element Aura
- Frozen
- Melt
- Vaporize
- Overload
- Electro-Charged
- Superconduct 基础定义
- Element UI
- Combat Text
- Upgrade / ItemRuntime 架构
- RunState
- Combat Encounter
- 战后 Upgrade Selection 流程
- Level Section
- Section Manager
- Run / Gate / Combat Section Prefab
- Netcode for GameObjects 基础依赖
- ParrelSync 联机测试环境

因此项目已经开始从：

> “功能测试 Demo”

进入：

> **“完整 Core Loop 原型”**

阶段。

---

# 32. 当前尚未完成的核心内容

接下来的重点不是疯狂增加新系统，而是把一整局真正跑通。

优先级建议：

**P0**

完整 Run：

> Running  
> → Gate  
> → Combat  
> → Upgrade  
> → Running

反复循环。

然后：

> Elite → Boss → Victory / GameOver

---

**P1**

完善现有 Build：

- Giant
- Bounce
- Explosion
- Pierce
- Element
- Active Item

让它们真正能够互相组合。

---

**P2**

增加：

- 敌人种类
- Elite
- Boss
- 随机 Section
- 更多 Upgrade

---

**P3**

开始真正多人化：

- Network Player
- Network Crowd State
- Encounter Authority
- Enemy Authority
- Item Sync
- Projectile Sync / 表现同步
- Run State Sync

---

# 33. MVP 定义

Running 的第一个真正可玩的 MVP 不要求：

100 个道具。

也不要求：

完整七元素。

更不要求：

十个 Boss。

MVP 只需要做到：

### 一局可以完整开始和结束

Start

↓

Run

↓

Gate

↓

Combat

↓

Upgrade

↓

重复 3–5 次

↓

Boss

↓

Victory / GameOver

并拥有：

- 5–10 个有明显差异的 Upgrade
- 3 种左右普通敌人
- 1 个 Elite
- 1 个 Boss
- 3–4 种元素
- 4–6 种 Reaction
- 2–3 套明显不同的 Build

如果这个版本已经能让玩家产生：

> “我再开一把试试另一个组合。”

Running 的核心方向就是成立的。

---

# 34. Running 的设计原则

以后增加任何玩法前，都应该问下面几个问题：

**① 它会让玩家做选择吗？**

如果只是攻击力 +5%，价值有限。

**② 它能和其他东西组合吗？**

如果完全独立，优先级降低。

**③ 它能改变视觉效果吗？**

好的 Build 应该能被玩家看见。

**④ 它会改变玩法吗？**

优先做 Giant 这种改变规则的道具。

**⑤ 它适合未来多人吗？**

避免现在写出未来完全无法同步的系统。

---

# 35. 最终目标

Running 最终希望形成一种：

> **非常容易理解，但组合深度很高的合作 Roguelike。**

玩家第一次玩时只需要理解：

> 往前跑。

> 选数字门。

> 打敌人。

> 三选一。

但随着游戏深入，会逐渐发现：

> Population 可以改变攻击。

> Projectile 可以组合。

> 元素可以反应。

> 道具可以改变整个攻击规则。

> 队友之间的元素和 Build 还能进一步组合。

最终形成从：

**简单数字跑酷**

逐渐演化成：

**满屏奇怪 Build、元素连锁和多人配合的 Roguelike 战斗游戏。**