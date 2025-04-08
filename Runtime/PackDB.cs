using MessagePack;
using MessagePack.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Readymade.Persistence
{
    /// <summary>
    /// <para>
    /// This is a very simple flat-file key-value store database. All operations will be performed in memory and
    /// can be written to disk by calling <see cref="Commit"/> or cleared by calling <see cref="Revert"/>.
    /// Whenever the storage destination of the database changes that database will be loaded and replaces the current one.
    /// File management is automatic and can be configured via <see cref="Settings"/>. To support multiple database files,
    /// for example to implement a save-system, the database path can be overriden in most API calls.
    /// </para>
    /// <para>
    /// Database files can optionally be compressed and encrypted. 
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>Encryption: Note that the optional encryption is not particularly secure and can only discourage the casual
    /// user from editing the database.</para>
    /// <para>
    /// Performance: This is a rather inefficient database (as serialization with Newtonsoft isn't very efficient) and
    /// should not be used for performance critical code paths. In case <see cref="Backend.Json"/> is too slow, the backend
    /// can be changed to <see cref="Backend.MessagePack"/> with is considerably more efficient.
    /// </para>
    /// </remarks>
    public class PackDB : IKeyValueDatabase, IDisposable
    {
        // Encryption isn't meant to be secure, it is merely used as obfuscation. Any dedicated user is expected to be able to
        // break it.
        private static readonly byte[] AesKey = Encoding.ASCII.GetBytes("Wwzm3PLdJsSYmUS3JekLC3bpltU772Ez");
        private static readonly byte[] AesIv = Convert.FromBase64String("100fe0e65b6239d5dffe2675e48aab6e");

        private string _currentPath;
        private PackData _packData;
        private bool _hasUncommittedChanges;
        private SemaphoreSlim _lock = new(1, 1);

        /// <summary>
        /// Creates an instance with the given settings.
        /// </summary>
        /// <param name="settings"></param>
        public PackDB(PackSettings settings)
        {
            Settings = settings;
            _messagePackOptions = new MessagePackSerializerOptions(UnityResolver.Instance);
        }

        private MessagePackSerializerOptions _messagePackOptions;

        /// <inheritdoc />
        public PackSettings Settings { get; }

        /// <inheritdoc />
        public string SchemaVersion => _packData.Version;

        /// <inheritdoc />
        public string DataVersion => _packData.Build;

        public bool HasUncommittedChanges => _hasUncommittedChanges;
        public bool IsLocked => _lock.CurrentCount > 0;

        /// <inheritdoc />
        public void Set<T>(string key, T data, string path)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(path));
            SetInternal(key, data, Settings.Backend);
        }

        /// <inheritdoc />
        public void Set<T>(string key, T data)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(Settings));
            SetInternal(key, data, Settings.Backend);
        }

        private void EnsureUnlocked()
        {
            if (_lock.CurrentCount == 0)
            {
                throw new SynchronizationLockException("Lock is not available.");
            }
        }

        /// <inheritdoc />
        public bool TryGet<T>(string key, out T data, string path)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(path));
            return TryGetInternal(key, out data, Settings.Backend);
        }

        /// <inheritdoc />
        public bool TryGet<T>(string key, out T data)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(Settings));
            return TryGetInternal(key, out data, Settings.Backend);
        }

        /// <inheritdoc />
        public bool TryGet(Type type, string key, out object data, string path)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(path));
            return TryGetInternal(key, type, out data, Settings.Backend);
        }

        /// <inheritdoc />
        public bool TryGet(Type type, string key, out object data)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(Settings));
            return TryGetInternal(key, type, out data, Settings.Backend);
        }

        /// <inheritdoc />
        [Obsolete("This API is not supported by all backends. Aim to not rely on it.")]
        public bool TryPopulate<T>(string key, T target, string path)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(path));
            return TryPopulateInternal(key, target, Settings.Backend);
        }

        /// <inheritdoc />
        [Obsolete("This API is not supported by all backends. Aim to not rely on it.")]
        public bool TryPopulate<T>(string key, T target)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(Settings));
            return TryPopulateInternal(key, target, Settings.Backend);
        }

        /// <inheritdoc />
        public T Get<T>(string key, string path)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(path));
            return GetInternal<T>(key, Settings.Backend);
        }

        /// <inheritdoc />
        public object Get(Type t, string key, string path)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(path));
            return GetInternal(t, key, Settings.Backend);
        }

        /// <inheritdoc />
        public T Get<T>(string key)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(Settings));
            return GetInternal<T>(key, Settings.Backend);
        }

        /// <inheritdoc />
        public object Get(Type t, string key)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(Settings));
            return GetInternal(t, key, Settings.Backend);
        }

        /// <inheritdoc />
        [Obsolete("This API is not supported by all backends. Aim to not rely on it.")]
        public void Populate<T>(string key, T target, string path)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(path));
            if (!TryPopulateInternal(key, target, Settings.Backend))
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }
        }

        /// <inheritdoc />
        [Obsolete("This API is not supported by all backends. Aim to not rely on it.")]
        public void Populate<T>(string key, T target)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(Settings));
            JsonSerializer.CreateDefault().Populate(_packData.JsonEntries[key].CreateReader(), target);
            if (!TryPopulateInternal(key, target, Settings.Backend))
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }
        }

        /// <inheritdoc />
        public bool Delete(string key, string path)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(path));
            return DeleteInternal(key);
        }

        /// <inheritdoc />
        public bool Delete(string key)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(Settings));
            return DeleteInternal(key);
        }

        public void DeleteAll(string path)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(path));
            DeleteAllInternal();
        }

        public void DeleteAll()
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(Settings));
            DeleteAllInternal();
        }

        /// <inheritdoc />
        public void Clear(string path)
        {
            EnsureUnlocked();
            DeleteAll(path);
        }

        public void Clear()
        {
            EnsureUnlocked();
            DeleteAll();
        }

        /// <inheritdoc />
        public bool DeleteFile(string path)
        {
            string fullPath = GetFullPath(path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public bool Contains(string key, string path)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(path));
            return ContainsInternal(key, Settings.Backend);
        }

        /// <inheritdoc />
        public bool Contains(string key)
        {
            EnsureUnlocked();
            EnsureLoaded(GetFullPath(Settings));
            return ContainsInternal(key, Settings.Backend);
        }


        /// <inheritdoc />
        public void Commit()
        {
            Debug.Log($"[{nameof(PackDB)}] Committing database.");
            CommitInternalAsync(Settings.Encryption, Settings.Compression, Settings.Backend).Forget();
        }

        /// <inheritdoc />
        public async UniTask CommitAsync()
        {
            await CommitInternalAsync(Settings.Encryption, Settings.Compression, Settings.Backend);
        }

        /// <summary>
        /// Checks whether the given <paramref name="key"/> is present in the given <paramref name="backend"/>. 
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When the key doesn't exist</exception>
        private bool ContainsInternal(string key, Backend backend)
        {
            return backend switch
            {
                Backend.Json => _packData.JsonEntries.ContainsKey(key),
                Backend.MessagePack => _packData.MessagePackEntries.ContainsKey(key),
                Backend.LazyJson => _packData.LazyJsonEntries.ContainsKey(key),
                _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null)
            };
        }

        /// <summary>
        /// Gets the statically typed value of the given <paramref name="key"/> in the given <paramref name="backend"/>. 
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When the <paramref name="backend"/> option is not implemented.</exception>
        private T GetInternal<T>(string key, Backend backend)
        {
            return backend switch
            {
                Backend.Json => JsonSerializer.CreateDefault()
                    .Deserialize<T>(_packData.JsonEntries[key].CreateReader()),
                Backend.MessagePack => MessagePackSerializer.Deserialize<T>(_packData.MessagePackEntries[key],
                    _messagePackOptions),
                Backend.LazyJson => (T)_packData.ObjectEntries[key],
                _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null)
            };
        }

        /// <summary>
        /// Gets the runtime typed value of the given <paramref name="key"/> in the given <paramref name="backend"/>. 
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When the <paramref name="backend"/> option is not implemented.</exception>
        private object GetInternal(Type type, string key, Backend backend)
        {
            return backend switch
            {
                Backend.Json => JsonSerializer.CreateDefault()
                    .Deserialize(_packData.JsonEntries[key].CreateReader(), type),
                Backend.MessagePack => MessagePackSerializer.Deserialize(type, _packData.MessagePackEntries[key],
                    _messagePackOptions),
                Backend.LazyJson => Convert.ChangeType(_packData.ObjectEntries[key], type),
                _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null)
            };
        }

        /// <summary>
        /// Sets the statically typed <paramref name="data"/> value of the given <paramref name="key"/> in the given <paramref name="backend"/>. 
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When the <paramref name="backend"/> option is not implemented.</exception>
        private void SetInternal<T>([NotNull] string key, [NotNull] T data, Backend backend)
        {
            EnsureSerializer();
            Debug.Assert(!string.IsNullOrEmpty(key), "!string.IsNullOrEmpty ( key )");
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning($"[{nameof(PackDB)}] Key cannot be empty; ignored");
                return;
            }

            if (data == null)
            {
                Debug.LogWarning($"[{nameof(PackDB)}] Data cannot be null, key {key} will not be packed");
                return;
            }

            switch (backend)
            {
                case Backend.Json:
                    /*
                    JToken obj = JToken.FromObject(data);
                    if (obj is JObject jObj)
                    {
                        jObj.Add("__Type", data.GetType().FullName);
                    }
                    */

                    _packData.JsonEntries[key] = JToken.FromObject(data);
                    break;
                case Backend.MessagePack:
                    _packData.MessagePackEntries[key] = MessagePackSerializer.Serialize(data, _messagePackOptions);
                    break;
                case Backend.LazyJson:
                    _packData.ObjectEntries[key] = data;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _hasUncommittedChanges = true;
        }

        /// <summary>
        /// Sets the runtime typed <paramref name="data"/> value of the given <paramref name="key"/> in the given <paramref name="backend"/>. 
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When the <paramref name="backend"/> option is not implemented.</exception>
        private bool TryGetInternal(string key, Type type, out object data, Backend backend)
        {
            if (_lock.CurrentCount == 0)
            {
                Debug.LogError($"[{nameof(PackDB)}] Read lock is not available.");
                data = default;
                return false;
            }

            EnsureSerializer();
            switch (backend)
            {
                case Backend.Json:
                    if (_packData.JsonEntries.TryGetValue(key, out JToken token))
                    {
                        data = JsonSerializer.CreateDefault().Deserialize(token.CreateReader(), type);
                        return true;
                    }

                    break;
                case Backend.MessagePack:
                    if (_packData.MessagePackEntries.TryGetValue(key, out byte[] pack))
                    {
                        data = MessagePackSerializer.Deserialize(type, pack, _messagePackOptions);
                        return true;
                    }

                    break;
                case Backend.LazyJson:
                    if (_packData.ObjectEntries.TryGetValue(key, out object obj))
                    {
                        var isCorrectType = typeof(object) == type;
                        data = isCorrectType ? obj : default;
                        return isCorrectType;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


            data = default;
            return false;
        }

        /// <summary>
        /// Attempts to get the statically typed value of the given <paramref name="key"/> in the given <paramref name="backend"/>. 
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When the <paramref name="backend"/> option is not implemented.</exception>
        private bool TryGetInternal<T>(string key, out T data, Backend backend)
        {
            if (_lock.CurrentCount == 0)
            {
                Debug.LogError($"[{nameof(PackDB)}] Read lock is not available.");
                data = default;
                return false;
            }

            EnsureSerializer();
            switch (backend)
            {
                case Backend.Json:
                    if (_packData.JsonEntries.TryGetValue(key, out JToken token))
                    {
                        data = JsonSerializer.CreateDefault().Deserialize<T>(token.CreateReader());
                        return true;
                    }

                    break;
                case Backend.MessagePack:
                    if (_packData.MessagePackEntries.TryGetValue(key, out byte[] pack))
                    {
                        data = MessagePackSerializer.Deserialize<T>(pack, _messagePackOptions);
                        return true;
                    }

                    break;
                case Backend.LazyJson:
                    if (_packData.ObjectEntries.TryGetValue(key, out object obj))
                    {
                        data = (T)obj;
                        return true;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


            data = default;
            return false;
        }

        /// <summary>
        /// Attempts to get the statically typed value of the given <paramref name="key"/> in the given
        /// <paramref name="backend"/> and overwrite the <paramref name="target"/>'s state with it. 
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When the <paramref name="backend"/> option is not implemented.</exception>
        private bool TryPopulateInternal<T>(string key, T target, Backend backend)
        {
            if (_lock.CurrentCount == 0)
            {
                Debug.LogError($"[{nameof(PackDB)}] Read lock is not available.");
                return false;
            }

            EnsureSerializer();
            if (backend is Backend.MessagePack or Backend.LazyJson)
            {
                throw new InvalidOperationException(
                    $"Cannot use {nameof(Populate)} when using {nameof(Backend.MessagePack)} or {nameof(Backend.LazyJson)}.");
            }

            if (_packData.JsonEntries.TryGetValue(key, out JToken token))
            {
                JsonSerializer.CreateDefault().Populate(token.CreateReader(), target);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Deletes the given <paramref name="key"/> in the active backend. 
        /// </summary>
        private bool DeleteInternal(string key)
        {
            if (_lock.CurrentCount == 0)
            {
                Debug.LogError($"[{nameof(PackDB)}] Write lock is not available.");
                return false;
            }

            return _packData.JsonEntries.Remove(key) || _packData.MessagePackEntries.Remove(key);
        }

        /// <summary>
        /// Clears all data in the currently loaded database.
        /// </summary>
        private void DeleteAllInternal()
        {
            if (_lock.CurrentCount == 0)
            {
                Debug.LogError($"[{nameof(PackDB)}] Write lock is not available.");
            }

            _packData.JsonEntries.Clear();
            _packData.MessagePackEntries.Clear();
            _hasUncommittedChanges = true;
        }

        /// <summary>
        /// Commits the current in-memory changes to disk.
        /// </summary>
        /// <param name="encryption">The encryption to use.</param>
        /// <param name="compression">The compression to use.</param>
        /// <param name="backend">The backend to use.</param>
        /// <exception cref="ArgumentOutOfRangeException">When any option is not valid relative to other arguments.</exception>
        /// <exception cref="NotImplementedException">When any option is not implemented.</exception>
        private async UniTask CommitInternalAsync(EncryptionType encryption, CompressionType compression,
            Backend backend)
        {
            if (!await _lock.WaitAsync(TimeSpan.FromSeconds(1)))
            {
                Debug.LogError($"[{nameof(PackDB)}] Read lock timed out.");
                return;
            }

            Debug.Log($"[{nameof(PackDB)}] Read lock acquired.");

            try
            {
                var sw = Stopwatch.StartNew();
                if (Settings.Location == Location.Memory)
                {
                    Debug.LogWarning(
                        $"[{nameof(PackDB)}] Location is set to {Location.Memory}, commits will be ignored.");
                    return;
                }

                EnsureSerializer();
                if (_hasUncommittedChanges && _packData != null && !string.IsNullOrEmpty(_currentPath))
                {
                    UpdateMetaInformation();

                    await using (UniTask.ReturnToCurrentSynchronizationContext())
                    {
                        await UniTask.SwitchToThreadPool();

                        byte[] asBytes = backend switch
                        {
                            Backend.Json => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_packData)),
                            Backend.LazyJson => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_packData)),
                            Backend.MessagePack => MessagePackSerializer.Serialize(_packData, _messagePackOptions),
                            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null)
                        };

                        if (!File.Exists(_currentPath))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(_currentPath));
                        }

                        switch (compression)
                        {
                            case CompressionType.None:
                                break;
                            case CompressionType.Defalte:
                            {
                                throw new NotImplementedException();
                            }
                            default:
                                throw new ArgumentOutOfRangeException(nameof(compression), compression, null);
                        }

                        switch (encryption)
                        {
                            case EncryptionType.None:
                                break;
                            case EncryptionType.AES:
                            {
                                asBytes = await EncryptAsync(asBytes, AesKey, AesIv);
                                break;
                            }
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        await File.WriteAllBytesAsync(_currentPath, asBytes);
                        Debug.Log(
                            $"[{nameof(PackDB)}] Database committed to {_currentPath}. {asBytes.Length} bytes written in {sw.ElapsedMilliseconds} ms.");

                        _hasUncommittedChanges = false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                _lock.Release();
                Debug.Log($"[{nameof(PackDB)}] Read lock released.");
            }
        }

        /// <summary>
        /// Updates the meta-info in the current data object.
        /// </summary>
        private void UpdateMetaInformation()
        {
            _packData.Modified = DateTimeOffset.Now;
            _packData.Version = Application.version;
            _packData.Build = Application.buildGUID;
        }

        /// <inheritdoc />
        public void Revert() => LoadFromPathAsync(_currentPath).Forget();

        public async UniTask RevertAsync() => await LoadFromPathAsync(_currentPath);

        /// <summary>
        /// Ensures the serializer configured in <see cref="Settings"/> is instantiated and configured.
        /// </summary>
        private void EnsureSerializer()
        {
            /*
            _serializer ??= JsonSerializer.Create (
                new JsonSerializerSettings {
                    Formatting = Formatting.Indented,
                    TypeNameHandling = TypeNameHandling.None,
                    ContractResolver = new UnityTypeContractResolver (),
                }
            );
            */
        }

        /// <summary>
        /// Ensures the database at <paramref name="path"/> is loaded.
        /// </summary>
        private void EnsureLoaded(string path)
        {
            if (!IsLoaded(path))
            {
                throw new InvalidOperationException(
                    $"Database at path {path} is not loaded. Call {nameof(LoadFromPathAsync)} first.");
            }
        }

        /// <summary>
        /// Ensures the database at <paramref name="path"/> is loaded.
        /// </summary>
        /// <remarks>Call this before using any non-async API.</remarks>
        public async UniTask EnsureLoadedAsync(string path)
        {
            if (!IsLoaded(path))
            {
                await LoadFromPathAsync(path, Settings.Backend, Settings.Encryption, Settings.Compression);
            }
        }

        /// <inheritdoc />
        public bool IsLoaded(string path) => !string.IsNullOrEmpty(path) && _currentPath == path;

        /// <summary>
        /// Loads a database from path with a specific configuration.
        /// </summary>
        /// <param name="path">The fully qualified path to load from.</param>
        /// <param name="backend">The <see cref="Backend"/> to use.</param>
        /// <param name="encryption">The <see cref="EncryptionType"/> to use.</param>
        /// <param name="compression">The <see cref="EncryptionType"/> to use.</param>
        /// <exception cref="InvalidOperationException">When there are uncommitted changes in the currently loaded database.</exception>
        /// <exception cref="ArgumentOutOfRangeException">When any option is unavailable relative to others.</exception>
        /// <exception cref="NotImplementedException">When any option is not yet implemented.</exception>
        private async UniTask LoadFromPathAsync(
            string path,
            Backend backend = Backend.Json,
            EncryptionType encryption = EncryptionType.None,
            CompressionType compression = CompressionType.None
        )
        {
            try
            {
                EnsureSerializer();
                if (_hasUncommittedChanges)
                {
                    //Debug.LogWarning( $"[{nameof(PackDB)}] Database has uncommitted changes. Load rejected. Please commit or revert changes before loading.");
                    throw new InvalidOperationException(
                        $"Database '{_currentPath}' has uncommitted changes. Loading of '{path}' rejected. Please commit or revert changes before loading.");
                }

                if (Settings.Location == Location.Memory)
                {
                    Debug.LogWarning(
                        $"[{nameof(PackDB)}] Location is set to {Location.Memory}, loads will be virtual.");
                }

                if (!File.Exists(path))
                {
                    _packData = new PackData();
                    UpdateMetaInformation();
                    _currentPath = path;
                    _hasUncommittedChanges = false;
                    return;
                }

                if (!await _lock.WaitAsync(TimeSpan.FromSeconds(1)))
                {
                    Debug.LogError($"[{nameof(PackDB)}] Read lock timed out.");
                    return;
                }

                Debug.Log($"[{nameof(PackDB)}] Read lock acquired.");

                try
                {
                    await using (UniTask.ReturnToCurrentSynchronizationContext())
                    {
                        await UniTask.SwitchToThreadPool();
                        byte[] rawBytes = await File.ReadAllBytesAsync(path);
                        byte[] decryptedBytes = encryption switch
                        {
                            EncryptionType.None => rawBytes,
                            EncryptionType.AES => Decrypt(rawBytes, AesKey, AesIv),
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        byte[] plainBytes = compression switch
                        {
                            CompressionType.None => decryptedBytes,
                            CompressionType.Defalte => throw new NotImplementedException(),
                            _ => throw new ArgumentOutOfRangeException(nameof(compression), compression, null)
                        };

                        PackData packData = backend switch
                        {
                            Backend.Json =>
                                JsonConvert.DeserializeObject<PackData>(Encoding.UTF8.GetString(plainBytes)),
                            Backend.LazyJson =>
                                JsonConvert.DeserializeObject<PackData>(Encoding.UTF8.GetString(plainBytes)),
                            Backend.MessagePack => MessagePackSerializer.Deserialize<PackData>(plainBytes,
                                _messagePackOptions),
                            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null)
                        };

                        _packData = packData;
                        _hasUncommittedChanges = false;
                        _currentPath = path;
                        Debug.Log($"[{nameof(PackDB)}] Database loaded from {_currentPath}");
                    }
                }
                finally
                {
                    _lock.Release();
                    Debug.Log($"[{nameof(PackDB)}] Read lock released.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                Debug.LogWarning($"[{nameof(PackDB)}] Loading of {path} failed.");
            }
        }

        /// <summary>
        /// Encrypt the given data.
        /// </summary>
        /// <param name="data">The data to encrypt.</param>
        /// <param name="key">The symmetric key to use for encryption.</param>
        /// <param name="iv">The initial vector to use for encryption.</param>
        /// <returns>The encrypted bytes.</returns>
        private static async UniTask<byte[]> EncryptAsync(byte[] data, byte[] key, byte[] iv)
        {
            using Aes aes = Aes.Create();
            aes.IV = iv;
            aes.Key = key;
            ICryptoTransform enc = aes.CreateEncryptor(aes.Key, aes.IV);
            using MemoryStream memoryStream = new();
            await using CryptoStream cryptoStream = new(memoryStream, enc, CryptoStreamMode.Write);
            await using StreamWriter streamWriter = new(cryptoStream);
            await memoryStream.WriteAsync(data);
            //streamWriter.Write(text);
            byte[] encrypted = memoryStream.ToArray();
            return encrypted;
        }

        /// <summary>
        /// Decrypt the given data.
        /// </summary>
        /// <param name="cipher">The data to decrypt.</param>
        /// <param name="key">The symmetric key to use for encryption.</param>
        /// <param name="iv">The initial vector to use for encryption.</param>
        /// <returns>The encrypted bytes.</returns>
        private byte[] Decrypt(byte[] cipher, byte[] key, byte[] iv)
        {
            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using MemoryStream memoryStream = new(cipher);
            using CryptoStream cryptoStream = new(memoryStream, decryptor, CryptoStreamMode.Read);
            using StreamReader reader = new(cryptoStream);
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Get the fully qualified database filepath for the given <paramref name="settings"/>. 
        /// </summary>
        /// <param name="settings">The settings object for which to get the path.</param>
        /// <returns>The fully qualified path.</returns>
        public string GetFullPath(PackSettings settings) =>
            GetFullPath(settings.DefaultFilePath);

        /// <summary>
        /// Get the fully qualified database filepath for the given <paramref name="path"/>. 
        /// </summary>
        /// <param name="path">The path fragment. Can be just a filename a relative path or full path.</param>
        /// <returns>The fully qualified path.</returns>
        /// <remarks>The path will be constructed based on the <see cref="Settings"/> of this instance.</remarks>
        /// <exception cref="NotImplementedException">When the path argument was a rooted path.</exception>
        /// <exception cref="ArgumentException">When the path argument was invalid.</exception>
        public string GetFullPath(string path)
        {
            // TODO: add checking for malformed path argument
            string checkedPath;
            if (Path.IsPathFullyQualified(path))
            {
                checkedPath = path;
            }
            else if (Path.IsPathRooted(path))
            {
                throw new NotImplementedException();
            }
            else
            {
                // is relative path
                checkedPath = Path.Combine(Settings.DefaultFolderPath, path);
            }

            return checkedPath;
        }

        /// <summary>
        /// Copies the currently loaded database into another one, overwriting any existing keys in the destination
        /// with those from the source.
        /// </summary>
        /// <param name="destinationPath">The destination database.</param>
        public async UniTask CopyToAsync(string destinationPath)
        {
            EnsureUnlocked();

            /*
            PackData copy = new()
            {
                Version = string.Copy(_packData.Version),
                Build = string.Copy(_packData.Build),
                Modified = _packData.Modified,
                Entries = _packData.Entries.ToDictionary(item => item.Key, item => item.Value),
                EntriesPacked = _packData.EntriesPacked.ToDictionary(item => item.Key, item => item.Value)
            };
            */

            // unhook the current pack data and store a temporary reference.

            PackData copy = _packData;
            _packData = null;

            // load the destination database (this will also create a new pack data object)

            await EnsureLoadedAsync(GetFullPath(destinationPath));
            Debug.Assert(_packData != null, "_packData != null");

            // copy the old pack data to the new pack data object.

            if (copy != null)
            {
                foreach (var it in copy.JsonEntries)
                {
                    _packData.JsonEntries[it.Key] = it.Value;
                }

                foreach (var it in copy.MessagePackEntries)
                {
                    _packData.MessagePackEntries[it.Key] = it.Value;
                }
            }
        }

        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}