using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;

namespace Readymade.Persistence
{
    public static class PackExtensions
    {
        /// <summary>
        /// Gets the Pack-ID (i.e. the ID actually used in the database) for the target <see cref="PackIdentity"/>.
        /// </summary>
        /// <param name="identity">The target component.</param>
        /// <returns>The Pack-ID.</returns>
        public static string GetPackKey(this PackIdentity identity) => identity.EntityID.ToString("N");
        
        /// <summary>
        /// Gets the Pack-ID (i.e. the ID actually used in the database) for a target <see cref="IPackableComponent"/>.
        /// </summary>
        /// <param name="component">The component to get the key of.</param>
        /// <returns>The key of the component.</returns>
        public static string GetPackKey(this IPackableComponent component) =>
            GetPackId(component).ToString("N");

        /// <summary>
        /// Gets the internal Pack-ID (i.e. the ID actually used in the database) that identifies the given component as a composite of its <see cref="PackIdentity"/>
        /// component's <see cref="PackIdentity.EntityID"/> and the static <see cref="IPackableComponent.ComponentKey"/> assigned the component.
        /// </summary>
        /// <param name="component">The component for which to get the ID.</param>
        /// <returns>The Pack-ID</returns>
        public static Guid GetPackId([NotNull] this IPackableComponent component)
        {
            Component goComponent = (Component)component;
            if (!goComponent)
            {
                Debug.LogWarning($"[{nameof(PackExtensions)}] Not a GameObject component");
                return default;
            }

            PackIdentity packIdentity = goComponent.GetComponentInParent<PackIdentity>();
            if (!packIdentity)
            {
                Debug.LogWarning(
                    $"[{nameof(PackExtensions)}] Component {goComponent.name} has no {nameof(PackIdentity)}, it will not be packable.",
                    goComponent);
                return default;
            }

            Guid parentID = packIdentity.EntityID;
            Debug.Assert(parentID != default,
                $"ASSERTION FAILED: Parent {nameof(PackIdentity)}.{nameof(PackIdentity.EntityID)} is not default.",
                packIdentity);
            Guid selfID = Guid.Parse(component.ComponentKey);
            Debug.Assert(selfID != default, $"ASSERTION FAILED: Component Key is not default.", goComponent);
            return BytewiseGuidXOR(parentID, selfID);

            static Guid BytewiseGuidXOR(Guid x, Guid y)
            {
                byte[] xs = x.ToByteArray();
                byte[] ys = y.ToByteArray();
                byte[] xor = new byte[xs.Length];
                for (int i = 0; i < xs.Length; i++)
                {
                    xor[i] = (byte)(xs[i] ^ ys[i]);
                }

                return new Guid(xor);
            }
        }

        /// <summary>
        /// Checks whether the component has a static <see cref="PackIdentity"/>.
        /// </summary>
        /// <param name="component">The target component</param>
        /// <returns>Whether a identity was found.</returns>
        public static bool HasStaticPackIdentity(this IPackableComponent component) =>
            ((Component)component).GetComponentsInParent<PackIdentity>().Any(it => it.HasAssetID);
    }
}