# Neow Lock

在主菜单打开 RitsuLib 的 Mod 设置，可分别固定一个捏奥遗物选项、第一层地区，以及第二层和第三层各一个先古遗物选项。开始**单人标准跑局**时，Mod 检查原种子；若不满足所有已选条件，就继续寻找自然生成这些条件的种子。Mod 不向原版选项列表插入遗物，也不直接授予遗物。即使手动输入种子，不匹配时也会替换。所有项目都设为“不固定”时恢复原版行为。

## 第二、第三层先古锁定的前提

使用先古遗物锁定须使用**游戏内容全解锁且已发现所有原版地区的存档**。Mod 在开局前核对纪元、角色、卡牌与卡池、遗物和药水解锁状态，以及原版地区发现状态；不符合时拒绝开局。每日挑战、自定义模式、多人及带开局 Modifier 的跑局仍不进行筛选。其他 Mod 若改变地区、先古、遗物池或选项生成逻辑，须单独验证兼容性。

先古选项的随机数由种子决定，但部分选项池会读取抵达先古时的牌组或持有物。筛种时使用角色**正常初始牌组和初始遗物**，相当于假定下列条件在第二、第三层仍成立。五名原版常规角色的初始状态均符合这些动态条件；玩家途中改变状态后，目标遗物以及同一选项池里的其他遗物都可能改变。该功能因此保证的是**在列明前提下原版种子自然生成目标选项**，不保证任意玩法下必定出现或自动取得。

| 先古 | 遗物内部 ID | 抵达该层时必须满足的条件 |
| --- | --- | --- |
| Nonupeipe | `BeautifulBracelet` | 牌组至少有 4 张可接受 Swift 附魔的牌。 |
| Orobas | `TouchOfOrobas` | 仍持有一件初始稀有度遗物。 |
| Orobas | `ArchaicTooth` | 牌组中仍有本角色对应的特殊初始牌：Bash、Neutralize、Unleash、FallingStar 或 Dualcast。 |
| Pael | `PaelsClaw` | 至少有 3 张可接受 Goopy 附魔的防御牌。 |
| Pael | `PaelsTooth` | 至少有 5 张可移除的牌。 |
| Pael | `PaelsLegion` | 没有提供事件宠物的遗物，也没有 ByrdonisEgg 卡。 |
| Tanx | `TriBoomerang` | 至少有 3 张可接受 Instinct 附魔的攻击牌。 |
| Tezcatara | `NutritiousSoup` | 至少保留 1 张基础稀有度且带 Strike 标签的牌。 |

“可接受附魔”由原版检查牌的类型、已有附魔等条件，不只看牌名。修改任意相关条件都可能改变同一池的抽取顺序。Orobas 的 `SeaGlass`／`PrismaticGem` 和 Darv 的 `DustyTome` 另有由种子决定的随机分支，不依赖上述游玩状态。

第二层使用 Hive 的先古池，第三层使用 Glory 的先古池；Darv 是共享先古，一局至多分配给其中一层。因此不能在两层同时锁定 Darv 遗物。Darv 的 `Ectoplasm` 与 `Sozu` 只能在第二层生成；`PandorasBox` 要求没有清空牌组的开局 Modifier，而本 Mod 当前只筛选无 Modifier 的标准局。Darv 和 Orobas 还须通过对应纪元解锁。Mod 在搜索前拒绝不可能的层数或双 Darv 组合。

最多检查 **100,000** 个候选种子。已固定项目越多，寻找时间通常越长，可能出现较长的开局等待。达到上限仍无匹配时，Mod 报错并阻止开局，绝不放行不符合条件的种子。

## 构建与使用

目标游戏版本：`0.111.0`；运行时依赖：完整安装的 RitsuLib `0.6.2` 或更新兼容版本。

1. 安装 .NET 9 SDK，把 `local.props.example` 复制为 `local.props`，调整游戏和 RitsuLib 路径。
2. 运行 `dotnet build NeowLock.csproj -c Release`。
3. 把 `bin/Release/net9.0/NeowLock.dll` 与 `mod_manifest.json` 放到游戏 `mods/NeowLock/`；RitsuLib 仍须单独完整安装。
4. 启动游戏，在主菜单的 RitsuLib 设置里选择目标，再开始单人标准跑局。建议将游戏原生第一层地区下拉框保持“随机”。

## 实现依据

原版 `StartRunLobby.BeginRunLocally` 根据种子抽取各层地区。`RunManager.InitializeNewRun` 先用 UpFront RNG 填充遗物池；`RunManager.GenerateRooms` 再把共享先古分配给后续层，并按层顺序生成房间和先古。`EventModel.BeginEvent` 用跑局种子、单人玩家槽位和事件 ID 建立事件 RNG。本 Mod 为每个候选建立未接入 `RunManager` 的临时状态，按这一顺序调用原版生成方法，再用先古的原版选项方法判断目标遗物是否出现。命中后只把候选种子及其原版地区列表交给真实跑局。

以上接口和抽取顺序按当前游戏版本核对；游戏更新后须重新验证。创意工坊说明位于 `workshop/description.txt`，发布时须与本页条件保持一致。
