# Neow Lock

在主菜单打开 RitsuLib 的 Mod 设置，可以分别固定一个捏奥遗物选项，以及第一层地区「密林」或「暗港」。开始**单人标准跑局**时，Mod 会先检查原本要使用的种子；若不满足所选条件，就继续生成候选种子。两项都设置时，同一个种子必须同时满足两项。捏奥选项仍由原版 `Neow.GenerateInitialOptions` 生成；第一层地区由原版 `ActModel.GetRandomList` 根据同一种子生成。Mod 不向捏奥选项列表插入内容，也不直接修改地区模型。

两项都选择“不固定”即可恢复原版随机行为。即使在角色选择界面手动输入了种子，只要启用的条件不符合，也会被替换。使用地区固定时，Mod 会用命中种子的原版地区抽取结果替换角色选择界面预先抽到或手动指定的地区；建议将原版第一层地区下拉框保持「随机」。暗港仍须在游戏进度中解锁并发现。每日挑战、自定义模式、多人和带有开局 Modifier 的跑局不会筛选。最多尝试 512 个种子；目标不可用或达到上限时，开局会失败并在日志里说明原因。

## 构建

目标游戏版本：`0.111.0`；运行时依赖：完整安装的 RitsuLib `0.6.2` 或更新兼容版本。

1. 安装 .NET 9 SDK，并把 `local.props.example` 复制为 `local.props`，按本机路径调整游戏和 RitsuLib 目录。
2. 运行 `dotnet build NeowLock.csproj -c Release`。
3. 把 `bin/Release/net9.0/NeowLock.dll` 和 `mod_manifest.json` 放到游戏目录 `mods/NeowLock/`。RitsuLib 本身仍需单独完整安装。
4. 启动游戏，在主菜单的 RitsuLib 设置入口选择捏奥选项及／或第一层地区，再开始单人标准跑局。

## 实现依据

原版 `StartRunLobby.BeginRunLocally` 会先用种子和 `act_selection` RNG 抽取各层地区，再把种子与地区列表交给 `NGame.StartNewSingleplayerRun`。后者按传入种子创建 `RunState`。原版 `EventModel.BeginEvent` 使用跑局 RNG 的种子、玩家槽位和事件模型 ID 建立事件 RNG；`Neow.GenerateInitialOptions` 用该 RNG 从原版选项池抽出开局选项。本 Mod 在创建正式跑局前，复用原版的地区抽取方法，并用同一套原版类型建立**未接入 RunManager 的临时候选状态**判定捏奥选项。命中后同时传递候选种子和它对应的地区列表。捏奥目标以遗物 `ModelId.Entry` 保存，避免依赖显示语言。

`NeowSeedSelector` 使用了当前游戏版本的原生方法、地区抽取 RNG 标签和事件 RNG 公式；游戏更新后应重新核对这些接口。其他 Mod 若改写地区或捏奥生成逻辑，也需要重新验证兼容性。
