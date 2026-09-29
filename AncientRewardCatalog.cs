using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;

namespace NeowLock;

internal static class AncientRewardCatalog
{
    internal readonly record struct Target(AncientEventModel Ancient, string RelicId);

    internal static IEnumerable<AncientEventModel> ForAct(int actIndex) => actIndex switch
    {
        1 => ModelDb.Act<Hive>().AllAncients.Append(ModelDb.AncientEvent<Darv>()),
        2 => ModelDb.Act<Glory>().AllAncients.Append(ModelDb.AncientEvent<Darv>()),
        _ => throw new ArgumentOutOfRangeException(nameof(actIndex)),
    };

    internal static bool IsAvailableOnAct(int actIndex, string relicId) =>
        actIndex != 2 ||
        (relicId != ModelDb.Relic<Ectoplasm>().Id.Entry &&
         relicId != ModelDb.Relic<Sozu>().Id.Entry);

    internal static Target? ResolveTarget(int actIndex, string relicId)
    {
        if (string.IsNullOrEmpty(relicId))
            return null;

        var matches = ForAct(actIndex)
            .Where(ancient => ancient.AllPossibleOptions.Any(option =>
                option.Relic?.Id.Entry == relicId))
            .ToArray();
        if (matches.Length != 1 || !IsAvailableOnAct(actIndex, relicId))
            throw new InvalidOperationException(
                $"Ancient relic '{relicId}' cannot naturally appear in act {actIndex + 1} in this game version.");

        return new Target(matches[0], relicId);
    }
}
