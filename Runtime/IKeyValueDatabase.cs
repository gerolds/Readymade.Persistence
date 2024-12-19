using System;
using Cysharp.Threading.Tasks;

namespace Readymade.Persistence
{
    /// <summary>
    /// Represents a generic Key-Value database API.
    /// </summary>
    public interface IKeyValueDatabase : IKeyValueStore
    {
        /// <summary>
        /// The settings object used by this database instance.
        /// </summary>
        public PackSettings Settings { get; }

        /// <summary>
        /// Set the value of a key in the database located at a given path. Requires a <see cref="Commit"/> to be made persistent.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public void Set<T>(string key, T data, string path);

        /// <summary>
        /// Attempts to get the value of a key in the database located at a given path.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public bool TryGet<T>(string key, out T data, string path);

        /// <summary>
        /// Attempts to get the value of a key in the database located at a given path. Allows querying a type that is only known at runtime.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public bool TryGet(Type t, string key, out object data, string path);

        /// <summary>
        /// Attempts to get the value of a key in the database located at a given path and write the result to a target object.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public bool TryPopulate<T>(string key, T target, string path);

        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public T Get<T>(string key, string path);

        /// <summary>
        /// Ensures that the database located at a given path is loaded. This is an asynchronous operation. This must be called before any non-async operation is invoked.
        /// </summary>
        public UniTask EnsureLoadedAsync(string path);

        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public object Get(Type t, string key, string path);

        [Obsolete("This API is not supported by all backends. Aim to not rely on it.")]
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public void Populate<T>(string key, T target, string path);

        [Obsolete("This API is not supported by all backends. Aim to not rely on it.")]
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public void Populate<T>(string key, T target);

        /// <summary>
        /// Deletes the entry at a given key in the database located at a given path. Requires a <see cref="Commit"/> to be made persistent.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public bool Delete(string key, string path);

        /// <summary>
        /// Clears all entries in the database located at a given path. Requires a <see cref="Commit"/> to be made persistent.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public void DeleteAll(string path);

        /// <summary>
        /// Clears all entries in the database located at a given path. Requires a <see cref="Commit"/> to be made persistent.
        /// </summary>
        /// <param name="path"></param>
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public void Clear(string path);

        /// <summary>
        /// Checks whether a given key exists in the database located at a given path.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public bool Contains(string key, string path);

        /// <summary>
        /// Checks whether the database located at a given path is currently loaded.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool IsLoaded(string path);

        /// <summary>
        /// Deletes a database at a given path.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public bool DeleteFile(string path);

        /// <summary>
        /// Writes all uncommitted changes to disk. This will always write to the database file that was most recently loaded.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public void Commit();

        public UniTask CommitAsync();

        /// <summary>
        /// Clears all uncommitted changes and reloads the last state from disk.
        /// </summary>
        /// <exception cref="InvalidOperationException">When the given path is not currently loaded.</exception>
        /// <seealso cref="EnsureLoadedAsync"/>
        public void Revert();
        
        /// <summary>
        /// Clears all uncommitted changes and reloads the last state from disk.
        /// </summary>
        public UniTask RevertAsync();
    }

    public interface IKeyValueStore
    {
        public string SchemaVersion { get; }
        public string DataVersion { get; }

        /// <summary>
        /// Set the value of a key in the default database. Requires a <see cref="IKeyValueDatabase.Commit"/> to be made persistent.
        /// </summary>
        public void Set<T>(string key, T data);

        /// <summary>
        /// Attempts to get the value of a key in the default database.
        /// </summary>
        public bool TryGet<T>(string key, out T data);

        /// <summary>
        /// Attempts to get the value of a key in the default database. Allows querying a type that is only known at runtime.
        /// </summary>
        public bool TryGet(Type t, string key, out object data);

        /// <summary>
        /// Attempts to get the value of a key in the database located at a given path and write the result to a target object.
        /// </summary>
        /// <summary>
        /// Attempts to get the value of a key in the default database and write the result to a target object.
        /// </summary>
        public bool TryPopulate<T>(string key, T target);

        public T Get<T>(string key);

        public object Get(Type t, string key);

        /// <summary>
        /// Deletes the entry at a given key in the default database. Requires a <see cref="IKeyValueDatabase.Commit"/> to be made persistent.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Delete(string key);

        /// <summary>
        /// Clears all entries in the default database. Requires a <see cref="IKeyValueDatabase.Commit"/> to be made persistent.
        /// </summary>
        public void DeleteAll();

        /// <summary>
        /// Clears all entries in the default database. Requires a <see cref="IKeyValueDatabase.Commit"/> to be made persistent.
        /// </summary>
        public void Clear();

        /// <summary>
        /// Checks whether a given key exists in the default database.
        /// </summary>
        public bool Contains(string key);
    }
}