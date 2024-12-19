namespace Readymade.Persistence.Components {
    public interface IWhenAnyPackingHandler {
        /// <summary>
        /// Called by <see cref="PackSystem"/> on all packable components of a <see cref="PackSystem"/> that implement it once the system has
        /// finished unpacking all registered components (i.e. <see cref="IPackableComponent.Unpack(object)"/> was called
        /// on all of them). This can be used to restore references between components that were restored in
        /// unspecified order as part of an unpacking procedure.
        /// </summary>
        public void OnAnyPacking ();
    }
}