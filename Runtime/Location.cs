using System;

namespace Readymade.Persistence {
    /// <summary>
    /// Specifies the storage location in <see cref="PackSettings"/>.
    /// </summary>
    public enum Location {
        /// <summary>
        /// Not yet implemented. Keeps the database entirely in memory. Improves performance when the database is used as a
        /// live runtime storage for application state.
        /// </summary>
        [Obsolete ( "Not yet implemented" )]
        Memory,

        /// <summary>
        /// Allows committing the database to disk.
        /// </summary>
        File
    }
}