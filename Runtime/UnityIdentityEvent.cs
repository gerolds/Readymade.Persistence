using System;
using UnityEngine.Events;

namespace Readymade.Persistence {
    /// <summary>
    /// A <see cref="UnityEvent{T0}"/> for <see cref="IEntity"/> arguments.
    /// </summary>
    [Serializable]
    public class UnityIdentityEvent : UnityEvent<IEntity> {
    }
}