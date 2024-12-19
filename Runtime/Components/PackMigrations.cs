using System.Collections.Generic;
using Readymade.Persistence.Pack;
using UnityEngine;

namespace Readymade.Persistence {
    /// <summary>
    /// A collection of <see cref="IPackMigration"/>s that can be applied to a <see cref="IKeyValueStore"/>.
    /// </summary>
    public abstract class PackMigrations : ScriptableObject {
        private List<IPackMigration> _migrations = new ();

        public IReadOnlyList<IPackMigration> Migrations => _migrations;

        private void Awake () {
            Init ();
        }

        private void OnEnable () {
            Init ();
        }

        private void Init () {
            _migrations.Clear ();
            _migrations.Add ( new FunPackMigration ( "0.1.0", data => data ) );
        }
    }
}