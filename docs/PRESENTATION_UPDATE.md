# 单角色与界面更新

正式入口仍为 `Assets/Scenes/Bootstrap.unity`，选择“独自出发”。本次将跑酷的视觉、碰撞和火力语义统一为一个角色。

## 跑酷

- 每条道路宽 16 米，角色中心左右移动边界各 7.25 米，路间距至少 22 米。
- 旧方阵不再生成，巨人类道具不再放大角色身体；单角色与根碰撞体保持对齐。
- 生命独立计算。门的 `+ − × ÷` 改变火力，火力最低为 1；普通射击伤害随火力改变，多重射击由道具提供。
- 两个门位于同一个横截面，左右各一个，每位玩家只允许触发一个选项。
- 进入首领场景时保留门带来的火力。
- 首领战出生点位于场内下方，俯视正交镜头使用矩形死区和平滑跟随；清除旧场景中央占位方块。
- 清除旧 prefab 内固定摆放的占位方块，只保留按 seed 生成的障碍。
- 镜头围绕当前道路中心稳定前进，角色横移不会带着整条路左右摇摆。

## 界面

- 主菜单、房间、路线图、跑酷 HUD、三选一和首领 HUD 使用同一套暖白、深灰、灰绿配色。
- 使用留白、细线和清楚的中文标签；移除彩色大面板、无意义的道具描述和重复的人口 UI。
- 路线图全屏纵向滚动，从下向上推进，可滚轮或拖动查看；线段避免交叉，首领位于最上层。
- 默认选路时间为 90 秒；全员投完立即进入关卡或奖励，地图随当前阶段立即关闭。
- 中文字体随包加载，不依赖操作系统字体或编辑器字体缓存。

## 回归入口

`RunningPlayModeSmoke.Run` 现在通过实际路线按钮的点击监听器提交选择，检查滚动内容确实移动、选路后地图关闭、单角色、宽路边界、火力与生命分离，以及同组门不重复生效；随后完成自然战斗与三选一。

`RunningArenaPlayModeSmoke.Begin` 检查火力、永久道具、临时道具剩余时长跨场景保留，普通弹体和主动道具能伤害首领，最后验证胜利事件。

`RunningPresentationCapture` 在带图形的 Editor 回归中将实际界面保存到 `Logs/Presentation`，用于检查菜单、路线图上下端、关卡、奖励与首领 HUD。

本次真实回归结果：`RUNNING_VALIDATION_OK`、`RUNNING_AUTOCOMBAT_CLEAR_OK`、`RUNNING_PLAYMODE_SMOKE_OK`、`RUNNING_ARENA_SMOKE_PASS`。日志保存在 `Logs/ui-redesign-running.log` 与 `Logs/ui-redesign-arena.log`。为保持当前编辑器会话，本次在独立工程副本运行验证。

## 实际画面

- [主菜单](preview/menu.png)
- [路线图起点](preview/route-map.png)
- [滚动到首领](preview/route-top.png)
- [单角色跑酷](preview/running.png)
- [道具选择](preview/reward.png)
- [首领场景](preview/arena.png)

已有 Play 会话中的旧实例不会自动重建。查看本次更新时，先退出当前 Play，再从 Bootstrap 开始新的行程。
