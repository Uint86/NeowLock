using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace NeowLock;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    public const string ModId = "NeowLock";
    private const string SettingsKey = "selection";

    private static bool _modelsReady;
    internal static bool Enabled { get; private set; } = true;
    internal static Logger Logger { get; private set; } = null!;

    public static void Initialize()
    {
        Logger = RitsuLibFramework.CreateLogger(ModId);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, Assembly.GetExecutingAssembly());

        using (RitsuLibFramework.BeginModDataRegistration(ModId))
        {
            RitsuLibFramework.GetDataStore(ModId).Register(
                key: SettingsKey,
                fileName: "selection.json",
                scope: SaveScope.Global,
                defaultFactory: () => new NeowLockSettings(),
                autoCreateIfMissing: true);
        }

        RitsuLibFramework.SubscribeLifecycle<ModelRegistryInitializedEvent>(_ => _modelsReady = true);
        RitsuLibFramework.RegisterModSettings(ModId, page => page
            .WithTitle(ModSettingsText.Literal("Neow Lock"))
            .WithModDisplayName(ModSettingsText.Literal("Neow Lock"))
            .WithVisibleOnHostSurfaces(ModSettingsHostSurface.MainMenu)
            .AddSection("seed_roll", section => section
                .WithTitle(ModSettingsText.Literal("开局种子 / Run seed"))
                .AddDynamicChoice(
                    "required_relic",
                    ModSettingsText.Literal("必出的捏奥选项 / Required Neow option"),
                    new ModSettingsValueBinding<NeowLockSettings, string>(
                        ModId, SettingsKey, SaveScope.Global,
                        settings => settings.RelicId,
                        (settings, relicId) => settings.RelicId = relicId),
                    GetAvailableChoices,
                    ModSettingsText.Literal(
                        "仅单人标准开局。筛选原版种子，不添加选项；如输入的种子不符合，也会换种。 / Standard single-player only. Rolls natural seeds and may replace a typed seed."),
                    ModSettingsChoicePresentation.Dropdown)
                .AddDynamicChoice(
                    "first_act",
                    ModSettingsText.Literal("第一层地区 / First act"),
                    new ModSettingsValueBinding<NeowLockSettings, string>(
                        ModId, SettingsKey, SaveScope.Global,
                        settings => settings.FirstAct,
                        (settings, firstAct) => settings.FirstAct = firstAct),
                    GetAvailableActChoices,
                    ModSettingsText.Literal(
                        "按原版地区抽取结果筛选种子；可与捏奥选项同时固定。 / Rolls native seeds and can be combined with the Neow option lock."),
                    ModSettingsChoicePresentation.Dropdown)
                .AddDynamicChoice(
                    "second_act_relic",
                    ModSettingsText.Literal("第二层先古遗物 / Act 2 Ancient relic"),
                    new ModSettingsValueBinding<NeowLockSettings, string>(
                        ModId, SettingsKey, SaveScope.Global,
                        settings => settings.SecondActRelicId,
                        (settings, relicId) => settings.SecondActRelicId = relicId),
                    () => GetAvailableAncientChoices(1),
                    ModSettingsText.Literal(
                        "要求全解锁存档；按抵达先古时仍满足选项条件来预测。 / Requires full unlocks and the reward conditions to remain true."),
                    ModSettingsChoicePresentation.Dropdown)
                .AddDynamicChoice(
                    "third_act_relic",
                    ModSettingsText.Literal("第三层先古遗物 / Act 3 Ancient relic"),
                    new ModSettingsValueBinding<NeowLockSettings, string>(
                        ModId, SettingsKey, SaveScope.Global,
                        settings => settings.ThirdActRelicId,
                        (settings, relicId) => settings.ThirdActRelicId = relicId),
                    () => GetAvailableAncientChoices(2),
                    ModSettingsText.Literal(
                        "仅筛选原版自然选项；途中改变牌组或遗物可能改变结果。 / Native choices only; changing your deck or relics can change them."),
                    ModSettingsChoicePresentation.Dropdown)));

        var patcher = RitsuLibFramework.CreatePatcher(ModId, "run-seed", "Neow seed selection");
        patcher.RegisterPatch<StartSingleplayerRunPatch>();
        RitsuLibFramework.ApplyRequiredPatcher(patcher, () => Enabled = false);
    }

    internal static string RequiredRelicId =>
        RitsuLibFramework.GetDataStore(ModId).Get<NeowLockSettings>(SettingsKey).RelicId;

    internal static string RequiredFirstAct =>
        RitsuLibFramework.GetDataStore(ModId).Get<NeowLockSettings>(SettingsKey).FirstAct;

    internal static string RequiredSecondActRelicId =>
        RitsuLibFramework.GetDataStore(ModId).Get<NeowLockSettings>(SettingsKey).SecondActRelicId;

    internal static string RequiredThirdActRelicId =>
        RitsuLibFramework.GetDataStore(ModId).Get<NeowLockSettings>(SettingsKey).ThirdActRelicId;

    private static IReadOnlyList<ModSettingsChoiceOption<string>> GetAvailableActChoices() =>
    [
        new(string.Empty, ModSettingsText.Literal("不固定 / Vanilla random")),
        new("overgrowth", ModSettingsText.Literal("密林 / Overgrowth")),
        new("underdocks", ModSettingsText.Literal("暗港 / Underdocks")),
    ];

    private static IReadOnlyList<ModSettingsChoiceOption<string>> GetAvailableChoices()
    {
        var choices = new List<ModSettingsChoiceOption<string>>
        {
            new(string.Empty, ModSettingsText.Literal("不固定 / Vanilla random")),
        };
        if (!_modelsReady)
            return choices;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in ModelDb.Event<Neow>().AllPossibleOptions)
        {
            if (option.Relic is not { } relic || !seen.Add(relic.Id.Entry))
                continue;
            choices.Add(new(relic.Id.Entry, ModSettingsText.LocString(relic.Title, relic.Id.Entry)));
        }
        return choices;
    }

    private static IReadOnlyList<ModSettingsChoiceOption<string>> GetAvailableAncientChoices(int actIndex)
    {
        var choices = new List<ModSettingsChoiceOption<string>>
        {
            new(string.Empty, ModSettingsText.Literal("不固定 / Vanilla random")),
        };
        if (!_modelsReady)
            return choices;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ancient in AncientRewardCatalog.ForAct(actIndex))
        foreach (var option in ancient.AllPossibleOptions)
        {
            if (option.Relic is not { } relic ||
                !AncientRewardCatalog.IsAvailableOnAct(actIndex, relic.Id.Entry) ||
                !seen.Add(relic.Id.Entry))
                continue;
            choices.Add(new(relic.Id.Entry, ModSettingsText.LocString(relic.Title, relic.Id.Entry)));
        }
        return choices;
    }
}
