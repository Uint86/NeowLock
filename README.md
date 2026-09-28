# Neow Lock

在主菜单打开 RitsuLib 的 Mod 设置，选择一个捏奥遗物选项。此后开始**单人标准跑局**时，Mod 会先检查原本要使用的种子；若该种子的原版捏奥选项不包含所选遗物，就继续生成候选种子。只有原版 `Neow.GenerateInitialOptions` 自己生成目标选项的种子才会交给游戏开局。它不会修改捏奥选项列表，也不会直接发放遗物。

选择“不固定”即可恢复原版随机行为。即使在角色选择界面手动输入了种子，只要目标选项已启用且输入种子不符合，也会被替换。每日挑战、自定义模式、多人和带有开局 Modifier 的跑局不会筛选。最多尝试 512 个种子；目标在当前角色或解锁状态下不可用、或达到上限时，开局会失败并在日志里说明原因，而不会进入不符合要求的跑局。

## 构建

目标游戏版本：`0.111.0`；运行时依赖：完整安装的 RitsuLib `0.6.2` 或更新兼容版本。

1. 安装 .NET 9 SDK，并把 `local.props.example` 复制为 `local.props`，按本机路径调整游戏和 RitsuLib 目录。
2. 运行 `dotnet build NeowLock.csproj -c Release`。
3. 把 `bin/Release/net9.0/NeowLock.dll` 和 `mod_manifest.json` 放到游戏目录 `mods/NeowLock/`。RitsuLib 本身仍需单独完整安装。
4. 启动游戏，在主菜单的 RitsuLib 设置入口选择目标捏奥选项，再开始单人标准跑局。

## 实现依据

原版 `NGame.StartNewSingleplayerRun` 先按传入种子创建 `RunState`，之后才启动跑局。原版 `EventModel.BeginEvent` 使用跑局 RNG 的种子、玩家槽位和事件模型 ID 建立事件 RNG；`Neow.GenerateInitialOptions` 用该 RNG 从原版选项池抽出开局选项。本 Mod 在创建正式跑局前，用同一套原版类型建立**未接入 RunManager 的临时候选状态**，调用原版生成方法作判定。目标以遗物 `ModelId.Entry` 保存，避免依赖显示语言。

`NeowSeedSelector` 使用了当前游戏版本的原生方法和事件 RNG 公式；游戏更新后应重新核对这些接口。其他 Mod 若改写捏奥生成逻辑，也需要重新验证兼容性。
