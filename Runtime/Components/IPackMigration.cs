using System;

namespace Readymade.Persistence {
    /// <summary>
    /// Enables a migration that can be applied to a versioned snapshot of a <see cref="IKeyValueStore"/>.
    /// </summary>
    [Obsolete ( "Experimental" )]
    public interface IPackMigration {
        /// <summary>
        /// Executes the migration on the referenced data.
        /// </summary>
        /// <param name="data">The data to migrate.</param>
        /// <returns>The new version after the migration has completed.</returns>
        public string Execute ( IKeyValueStore data );

        /// <summary>
        /// The version of the data that this migration is intended for.
        /// </summary>
        public string Trigger { get; }
    }
}