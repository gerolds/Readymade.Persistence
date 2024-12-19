using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Readymade.Persistence.Components
{
    public static class PackSystemUtils
    {
        public static IEnumerable<IPackableComponent> EnsureValidPackableComponent(this IEnumerable<IPackableComponent> self)
        {
            return self.Select(it =>
            {
                if (it.GetPackId() == default)
                {
                    Debug.LogWarning(
                        $"[{nameof(PackSystem)}] Component {(it as Component)?.name} has no valid component key.",
                        it as Component);
                }

                return it;
            });
        }
    }
}