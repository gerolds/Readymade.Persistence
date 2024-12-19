using System;

namespace Readymade.Persistence.Pack
{
    /// <summary>
    /// Configurable variant for implementing migrations in-place via delegates.
    /// </summary>
    public class FunPackMigration : IPackMigration
    {
        private readonly Func<IKeyValueStore, IKeyValueStore> _onExecute;

        /// <summary>
        /// Create a migration action to be executed when a specific version trigger is met. </summary>
        ///
        public FunPackMigration(
            string trigger,
            Func<IKeyValueStore, IKeyValueStore> onExecute
        )
        {
            Trigger = trigger;
            _onExecute = onExecute;
        }

        /// <inheritdoc />
        public string Execute(IKeyValueStore data)
        {
            _onExecute?.Invoke(data);
            return data.SchemaVersion;
        }

        /// <inheritdoc />
        public string Trigger { get; }
    }
}