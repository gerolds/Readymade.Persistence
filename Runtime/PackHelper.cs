using System;
using System.Collections.Generic;
using UnityEngine;

namespace Readymade.Persistence {
    /// <summary>
    /// Various helper methods for packing and unpacking.
    /// </summary>
    public static class PackHelper {
        /// <summary>
        /// Restores a collection of references to components on a <see cref="PackIdentity"/> object from their LifeIDs.
        /// </summary>
        /// <param name="lifeIDs">A set of IDs designating active <see cref="PackIdentity"/> instances.</param>
        /// <param name="collection">The collection to which all components of type <typeparamref name="TReference"/> on the discovered <see cref="PackIdentity"/> objects will be added.</param>
        /// <typeparam name="TReference">The collection item type. Can be an interface implemented by a <see cref="Component"/>.</typeparam>
        /// <remarks>The <typeparamref name="TReference"/>-component should be unique on the object associated with the ID. When multiple components of the same type exist on the target, the first one will be returned, selection is however non-deterministic.</remarks>
        public static void RestoreReferencesFromIDs<TReference> (
            ICollection<Guid> lifeIDs,
            ICollection<TReference> collection
        ) {
            collection.Clear ();
            foreach ( Guid id in lifeIDs ) {
                if ( PackIdentity.TryGetByEntityID( id, out PackIdentity packIdentity ) ) {
                    if ( packIdentity.TryGetComponent<TReference> ( out TReference item ) ) {
                        collection.Add ( item );
                    }
                }
            }

            lifeIDs.Clear ();
        }

        /// <summary>
        /// Restores a reference to a component on a <see cref="PackIdentity"/> object from its LifeID.
        /// </summary>
        /// <param name="lifeID">The ID designating an active <see cref="PackIdentity"/> instance.</param>
        /// <param name="target">The field to which any component of type <typeparamref name="TReference"/> on the discovered <see cref="PackIdentity"/> object will be added.</param>
        /// <typeparam name="TReference">The item type. Can be an interface implemented by a <see cref="Component"/>.</typeparam>
        /// <remarks>The <typeparamref name="TReference"/>-component should be unique on the object associated with the ID. When multiple components of the same type exist on the target, the first one will be returned, selection is however non-deterministic.</remarks>
        public static void RestoreReferenceFromID<TReference> ( Guid lifeID, ref TReference target ) {
            if ( PackIdentity.TryGetByEntityID ( lifeID, out PackIdentity packIdentity ) ) {
                if ( packIdentity.TryGetComponent<TReference> ( out TReference component ) ) {
                    target = component;
                }
            }
        }
    }
}