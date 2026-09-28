using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace NeowLock;

internal sealed class StartSingleplayerRunPatch : IPatchMethod
{
    public static string PatchId => "neow_lock_before_singleplayer_run";
    public static string Description => "Choose a naturally matching seed before creating a standard single-player run";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(NGame),
            nameof(NGame.StartNewSingleplayerRun),
            [
                typeof(CharacterModel), typeof(bool), typeof(IReadOnlyList<ActModel>),
                typeof(IReadOnlyList<ModifierModel>), typeof(string), typeof(GameMode),
                typeof(int), typeof(DateTimeOffset?),
            ]),
    ];

    public static bool Prefix(
        CharacterModel character,
        ref IReadOnlyList<ActModel> acts,
        IReadOnlyList<ModifierModel> modifiers,
        GameMode gameMode,
        int ascensionLevel,
        ref string seed,
        ref Task<RunState> __result)
    {
        if (!Entry.Enabled || gameMode != GameMode.Standard || modifiers.Count != 0)
            return true;

        var requiredRelicId = Entry.RequiredRelicId;
        var requiredFirstAct = Entry.RequiredFirstAct;
        if (string.IsNullOrWhiteSpace(requiredRelicId) && string.IsNullOrWhiteSpace(requiredFirstAct))
            return true;

        try
        {
            var selected = NeowSeedSelector.FindMatchingSeed(
                character, acts, modifiers, gameMode, ascensionLevel, seed,
                requiredRelicId, requiredFirstAct);
            Entry.Logger.Info($"Run seed roll: relic={requiredRelicId}, firstAct={requiredFirstAct}, attempts={selected.Attempts}, seed={selected.Seed}");
            seed = selected.Seed;
            acts = selected.Acts;
            return true;
        }
        catch (Exception error)
        {
            // Propagate through the game's existing async start-run error path; never start a nonmatching run.
            Entry.Logger.Error($"Run seed roll failed: {error}");
            __result = Task.FromException<RunState>(error);
            return false;
        }
    }
}
