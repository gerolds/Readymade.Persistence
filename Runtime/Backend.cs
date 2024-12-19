namespace Readymade.Persistence
{
    /// <summary>
    /// Specifies the serialization and storage backend in <see cref="PackSettings"/>.
    /// </summary>
    public enum Backend
    {
        /// <summary>
        /// Use Newtonsoft.Json as database serialization backend and internal storage model. This option performs
        /// eager serialization and immediately parses all data into an intermediate, in-memory representation of the
        /// JSON file. This amortizes the cost of serialization over all calls, at the cost of being inefficient
        /// for runtime queries. Do not use this option when the database state is polled as part of performance
        /// critical code paths.
        /// </summary>
        Json,

        /// <summary>
        /// Use Newtonsoft.Json as database serialization backend and plain objects as internal storage. This is a lazy
        /// version of JSON serialization that batch-serializes the entire data model only on commit. This is more
        /// efficient when the database is queried frequently at runtime but may cause a more significant pause on
        /// commit. Use this option if garbage allocations should be minimized.
        /// </summary>
        LazyJson,

        /// <summary>
        /// Use MessagePack as database serialization backend and internal storage model. Considerably faster and
        /// more efficient than Json.
        /// </summary>
        MessagePack
    }
}