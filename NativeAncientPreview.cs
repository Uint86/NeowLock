using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;

namespace NeowLock;

internal static class NativeAncientPreview
{
    internal static void GenerateRooms(RunState run, Player player, int ascensionLevel)
    {
        var rng = run.Rng.UpFront;
        var unlockState = run.UnlockState;

        // Match RunManager.InitializeNewRun before it calls GenerateRooms. Both relic
        // grab bags consume UpFront RNG, so skipping them changes later Ancients.
        run.SharedRelicGrabBag.Populate(
            ModelDb.RelicPool<SharedRelicPool>().GetUnlockedRelics(unlockState), rng);
        player.PopulateRelicGrabBagIfNecessary(rng);
        new AscensionManager(ascensionLevel).ApplyEffectsTo(player);

        // Match RunManager.GenerateRooms through every Ancient selection: distribute
        // shuffled shared Ancients without replacement, then generate acts in order.
        // Ascension 10+ consumes one further boss roll after this point; that draw
        // cannot change either Ancient or its separately seeded event options.
        var shared = unlockState.SharedAncients.ToList().UnstableShuffle(rng);
        foreach (var act in run.Acts.Skip(1))
        {
            var count = rng.NextInt(shared.Count + 1);
            var subset = shared.Take(count).ToList();
            shared = shared.Except(subset).ToList();
            act.SetSharedAncientSubset(subset);
        }

        foreach (var act in run.Acts)
            act.GenerateRooms(rng, unlockState, false);
    }
}
