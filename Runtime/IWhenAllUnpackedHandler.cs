namespace Readymade.Persistence.Components {
    /// <summary>
    /// Allows implementation of <see cref="PackSystem"/> unpacking post-processor callbacks.
    /// </summary>
    /// <remarks>Implement this on any <see cref="IPackableComponent"/> that needs this callback, for example to hook up references to other objects.</remarks>
    public interface IWhenAllUnpackedHandler {
        /// <summary>
        /// Called by <see cref="PackSystem"/> on all packed components of a <see cref="PackSystem"/> that implement it once the system has
        /// finished unpacking all registered components (i.e. <see cref="IPackableComponent.Unpack(object)"/> was called
        /// on all of them). This can be used to restore references between components that were restored in
        /// unspecified order as part of an unpacking procedure.
        /// </summary>
        public void OnAllUnpacked ();
    }
}