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

    private const int MaxCandidates = 100_000;
    private static readonly PropertyInfo OwnerProperty = typeof(EventModel).GetProperty(
        nameof(EventModel.Owner), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingMemberException(nameof(EventModel), nameof(EventModel.Owner));
    private static readonly PropertyInfo RngProperty = typeof(EventModel).GetProperty(
        nameof(EventModel.Rng), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingMemberException(nameof(EventModel), nameof(EventModel.Rng));
    private static readonly MethodInfo GenerateNeowOptions = typeof(Neow).GetMethod(
        "GenerateInitialOptions", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(Neow), "GenerateInitialOptions");
    private static readonly MethodInfo GenerateAncientOptions = typeof(AncientEventModel).GetMethod(
        "GenerateInitialOptionsWrapper", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(AncientEventModel), "GenerateInitialOptionsWrapper");

    internal static Selection FindMatchingSeed(
        CharacterModel character,
        IReadOnlyList<ModifierModel> modifiers,
        GameMode gameMode,
        int ascensionLevel,
        string originalSeed,
        string requiredRelicId,
        string requiredFirstAct,
        string requiredSecondActRelicId,
        string requiredThirdActRelicId)
    {
        if (requiredFirstAct is not ("" or "overgrowth" or "underdocks"))
            throw new InvalidOperationException($"Unknown saved first-act selection '{requiredFirstAct}'.");

        var secondTarget = AncientRewardCatalog.ResolveTarget(1, requiredSecondActRelicId);
        var thirdTarget = AncientRewardCatalog.ResolveTarget(2, requiredThirdActRelicId);
        if (secondTarget?.Ancient is Darv && thirdTarget?.Ancient is Darv)
            throw new InvalidOperationException(
                "Darv can be assigned to only one later act; two Darv relic locks cannot match one natural seed.");

        var canonicalNeow = string.IsNullOrEmpty(requiredRelicId) ? null : ModelDb.Event<Neow>();
        var desiredRelic = canonicalNeow?.AllPossibleOptions
            .Select(option => option.Relic)
            .FirstOrDefault(relic => relic?.Id.Entry == requiredRelicId);
        if (!string.IsNullOrEmpty(requiredRelicId) && desiredRelic is null)
            throw new InvalidOperationException(
                $"Saved Neow option '{requiredRelicId}' is not present in this game version.");

        var unlockState = SaveManager.Instance.GenerateUnlockStateFromProgress();
        if (secondTarget is not null || thirdTarget is not null)
            RequireFullyUnlockedProgress(unlockState);

        var attempted = new HashSet<string>(StringComparer.Ordinal);
        for (var draw = 0; attempted.Count < MaxCandidates; draw++)
        {
            var candidate = draw == 0 ? originalSeed : SeedHelper.GetRandomSeed();
            if (!attempted.Add(candidate))
                continue;
            var attempt = attempted.Count;

            // The lobby rolls acts before NGame starts the run. Always roll them again for a
            // replacement seed, even when the first-act lock is off.
            var candidateActs = RollActs(candidate, unlockState);
            if (!MatchesFirstAct(candidateActs[0], requiredFirstAct))
                continue;

            if (desiredRelic is null && secondTarget is null && thirdTarget is null)
                return new Selection(candidate, attempt, candidateActs);

            // All previews are detached from RunManager and use a normal new-run player.
            // This is the stated all-conditions-met assumption for future Ancient rewards.
            var player = Player.CreateForNewRun(character, unlockState, 1UL);
            var clonedActs = candidateActs.Select(act => (ActModel)act.MutableClone()).ToArray();
            var candidateRun = RunState.CreateForNewRun(
                [player], clonedActs, modifiers, gameMode, ascensionLevel, candidate);

            // Native setup rolls all rooms before entering Neow. Preserve that order
            // whenever later-act rewards are part of the same seed check.
            if (secondTarget is not null || thirdTarget is not null)
                NativeAncientPreview.GenerateRooms(candidateRun, player, ascensionLevel);

            if (desiredRelic is not null)
            {
                if (!desiredRelic.IsAllowedAtNeow(player))
                    throw new InvalidOperationException(
                        $"Neow option '{requiredRelicId}' is unavailable for this character or unlock state.");
                if (!MatchesNeow(candidateRun, player, canonicalNeow!, requiredRelicId))
                    continue;
            }

            if (secondTarget is not null || thirdTarget is not null)
            {
                if (secondTarget is { } second &&
                    !MatchesAncient(candidateRun, player, 1, second))
                    continue;
                if (thirdTarget is { } third &&
                    !MatchesAncient(candidateRun, player, 2, third))
                    continue;
            }

            return new Selection(candidate, attempt, candidateActs);
        }

        throw new InvalidOperationException(
            $"No natural seed matching all selected locks was found in {MaxCandidates} attempts. The run was not started.");
    }

    private static bool MatchesNeow(RunState run, Player player, Neow canonicalNeow, string relicId)
    {
        var neow = (Neow)canonicalNeow.MutableClone();
        PrepareEvent(neow, player, run);
        IReadOnlyList<EventOption> generated;
        try
        {
            generated = (IReadOnlyList<EventOption>)(GenerateNeowOptions.Invoke(neow, null)
                ?? throw new InvalidOperationException("Neow returned no option list."));
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }

        return generated.Any(option => option.Relic?.Id.Entry == relicId);
    }

    private static bool MatchesAncient(
        RunState run, Player player, int actIndex, AncientRewardCatalog.Target target)
    {
        var rolledAncient = run.Acts[actIndex].Ancient
            ?? throw new InvalidOperationException($"Act {actIndex + 1} has no rolled Ancient.");
        if (rolledAncient.Id.Entry != target.Ancient.Id.Entry)
            return false;

        run.CurrentActIndex = actIndex;
        var ancient = (AncientEventModel)rolledAncient.MutableClone();
        PrepareEvent(ancient, player, run);
        try
        {
            var options = (IReadOnlyList<EventOption>)(GenerateAncientOptions.Invoke(ancient, null)
                ?? throw new InvalidOperationException("Ancient returned no option list."));
            return options.Any(option => option.Relic?.Id.Entry == target.RelicId);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private static void PrepareEvent(EventModel model, Player player, RunState run)
    {
        OwnerProperty.SetValue(model, player);
        // EventModel.BeginEvent uses the run seed, player slot 0, and event ID.
        var eventSeed = unchecked(run.Rng.Seed + StringHelper.GetDeterministicHashCode(model.Id.Entry));
        RngProperty.SetValue(model, new Rng(eventSeed));
    }

    private static void RequireFullyUnlockedProgress(UnlockState unlockState)
    {
        var all = UnlockState.all;
        if (unlockState.EpochUnlockCount() != all.EpochUnlockCount() ||
            !SameIds(unlockState.Characters, all.Characters) ||
            !SameIds(unlockState.SharedAncients, all.SharedAncients) ||
            !SameIds(unlockState.Relics, all.Relics) ||
            !SameIds(unlockState.Cards, all.Cards) ||
            !SameIds(unlockState.CardPools, all.CardPools) ||
            !SameIds(unlockState.CharacterCardPools, all.CharacterCardPools) ||
            !SameIds(unlockState.Potions, all.Potions))
            throw new InvalidOperationException(
                "Ancient relic locks require a fully unlocked save. Complete all epochs and character/content unlocks first.");

        var discoveredActs = SaveManager.Instance.Progress.DiscoveredActs;
        ActModel[] nativeActs =
        [
            ModelDb.Act<Overgrowth>(), ModelDb.Act<Underdocks>(),
            ModelDb.Act<Hive>(), ModelDb.Act<Glory>(),
        ];
        if (nativeActs.Any(act => !act.IsDefault && !discoveredActs.Contains(act.Id)))
            throw new InvalidOperationException(
                "Ancient relic locks require a fully unlocked save with every native act discovered.");
    }

    private static bool SameIds<T>(IEnumerable<T> actual, IEnumerable<T> expected)
        where T : AbstractModel =>
        expected.Select(model => model.Id.Entry).ToHashSet(StringComparer.Ordinal)
            .SetEquals(actual.Select(model => model.Id.Entry));

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
