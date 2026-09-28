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
                    ModSettingsChoicePresentation.Dropdown)));

        var patcher = RitsuLibFramework.CreatePatcher(ModId, "run-seed", "Neow seed selection");
        patcher.RegisterPatch<StartSingleplayerRunPatch>();
        RitsuLibFramework.ApplyRequiredPatcher(patcher, () => Enabled = false);
    }

    internal static string RequiredRelicId =>
        RitsuLibFramework.GetDataStore(ModId).Get<NeowLockSettings>(SettingsKey).RelicId;

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
}
