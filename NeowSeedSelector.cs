using System.Reflection;
using System.Runtime.ExceptionServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace NeowLock;

internal static class NeowSeedSelector
{
    internal readonly record struct Selection(string Seed, int Attempts, IReadOnlyList<ActModel> Acts);

    private const int MaxCandidates = 512;
    private static readonly PropertyInfo OwnerProperty = typeof(EventModel).GetProperty(
        nameof(EventModel.Owner), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingMemberException(nameof(EventModel), nameof(EventModel.Owner));
    private static readonly PropertyInfo RngProperty = typeof(EventModel).GetProperty(
        nameof(EventModel.Rng), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingMemberException(nameof(EventModel), nameof(EventModel.Rng));
    private static readonly MethodInfo GenerateOptions = typeof(Neow).GetMethod(
        "GenerateInitialOptions", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(Neow), "GenerateInitialOptions");

    internal static Selection FindMatchingSeed(
        CharacterModel character,
        IReadOnlyList<ActModel> acts,
        IReadOnlyList<ModifierModel> modifiers,
        GameMode gameMode,
        int ascensionLevel,
        string originalSeed,
        string requiredRelicId,
        string requiredFirstAct)
    {
        if (requiredFirstAct is not ("" or "overgrowth" or "underdocks"))
            throw new InvalidOperationException($"Unknown saved first-act selection '{requiredFirstAct}'.");

        var canonicalNeow = string.IsNullOrEmpty(requiredRelicId) ? null : ModelDb.Event<Neow>();
        var desiredRelic = canonicalNeow?.AllPossibleOptions
            .Select(option => option.Relic)
            .FirstOrDefault(relic => relic?.Id.Entry == requiredRelicId);
        if (!string.IsNullOrEmpty(requiredRelicId) && desiredRelic is null)
            throw new InvalidOperationException(
                $"Saved Neow option '{requiredRelicId}' is not present in this game version.");

        var unlockState = SaveManager.Instance.GenerateUnlockStateFromProgress();
        var attempted = new HashSet<string>(StringComparer.Ordinal);
        for (var attempt = 1; attempt <= MaxCandidates; attempt++)
        {
            var candidate = attempt == 1 ? originalSeed : SeedHelper.GetRandomSeed();
            if (!attempted.Add(candidate))
                continue;

            // The lobby rolls acts before NGame starts the run. Recreate that same native roll
            // for a replacement seed and pass its result along with the seed.
            var candidateActs = string.IsNullOrEmpty(requiredFirstAct)
                ? acts
                : RollActs(candidate, unlockState);
            if (!MatchesFirstAct(candidateActs[0], requiredFirstAct))
                continue;

            if (desiredRelic is null)
                return new Selection(candidate, attempt, candidateActs);

            // Reproduce the native pre-run setup without attaching the candidate to RunManager.
            var player = Player.CreateForNewRun(character, unlockState, 1UL);
            var clonedActs = candidateActs.Select(act => (ActModel)act.MutableClone()).ToArray();
            var candidateRun = RunState.CreateForNewRun(
                [player], clonedActs, modifiers, gameMode, ascensionLevel, candidate);
            if (!desiredRelic.IsAllowedAtNeow(player))
                throw new InvalidOperationException(
                    $"Neow option '{requiredRelicId}' is unavailable for this character or unlock state.");

            var neow = (Neow)canonicalNeow!.MutableClone();
            OwnerProperty.SetValue(neow, player);
            // EventModel.BeginEvent derives its RNG from the run seed, player slot and event ID.
            // The single-player slot is zero; no candidate advances the real run RNG.
            var eventSeed = unchecked(
                candidateRun.Rng.Seed + StringHelper.GetDeterministicHashCode(neow.Id.Entry));
            RngProperty.SetValue(neow, new Rng(eventSeed));

            IReadOnlyList<EventOption> generated;
            try
            {
                generated = (IReadOnlyList<EventOption>)(GenerateOptions.Invoke(neow, null)
                    ?? throw new InvalidOperationException("Neow returned no option list."));
            }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }

            if (generated.Any(option => option.Relic?.Id.Entry == requiredRelicId))
                return new Selection(candidate, attempt, candidateActs);
        }

        throw new InvalidOperationException(
            $"No natural seed matching Neow option '{requiredRelicId}' and first act '{requiredFirstAct}' was found in {MaxCandidates} attempts. The run was not started.");
    }

    private static IReadOnlyList<ActModel> RollActs(string seed, UnlockState unlockState)
    {
        var rng = new Rng(StringHelper.GetDeterministicHashCode(seed), "act_selection");
        return ActModel.GetRandomList(rng, unlockState, false).ToArray();
    }

    private static bool MatchesFirstAct(ActModel act, string requiredFirstAct) => requiredFirstAct switch
    {
        "" => true,
        "overgrowth" => act is Overgrowth,
        "underdocks" => act is Underdocks,
        _ => throw new InvalidOperationException($"Unknown first-act selection '{requiredFirstAct}'."),
    };
}
