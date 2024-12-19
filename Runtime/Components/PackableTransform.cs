using UnityEngine;
using UnityEngine.Events;

namespace Readymade.Persistence.Components
{
    /// <summary>
    /// A <see cref="PackableComponent"/> that packs the position and rotation of a transform.
    /// </summary>
    [DisallowMultipleComponent]
    public class PackableTransform : PackableComponent<PackableTransform.TransformPackage>
    {
        [Tooltip(
            "Whether to pack the local or world transform. This setting applies only to packing; during unpack the transform " +
            "will be restored in whatever mode it was packed with.")]
        [SerializeField]
        private bool isLocal;

        [SerializeField] private UnityEvent onUnpacked;

        /// <inheritdoc />
        protected override void OnUnpack(TransformPackage package, AssetLookup lookup)
        {
            if (package.IsLocal)
            {
                transform.SetLocalPositionAndRotation(package.Position, package.Rotation);
            }
            else
            {
                transform.SetPositionAndRotation(package.Position, package.Rotation);
            }

            onUnpacked?.Invoke();
        }

        /// <inheritdoc />
        protected override TransformPackage OnPack()
        {
            return new TransformPackage
                {
                    IsLocal = isLocal,
                    Position = transform.localPosition,
                    Rotation = transform.localRotation,
                    ParentPackKey = transform.parent &&
                        transform.parent.TryGetComponent<PackIdentity>(out var parentPackIdentity)
                            ? parentPackIdentity.EntityID.ToString()
                            : default
                }
                ;
        }

        /// <summary>
        /// Packable state for <see cref="PackableTransform"/>. 
        /// </summary>
        public struct TransformPackage
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public string ParentPackKey;
            public bool IsLocal;
        }
    }
}