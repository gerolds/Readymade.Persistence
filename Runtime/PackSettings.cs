using System;
using System.IO;
using UnityEngine;

namespace Readymade.Persistence {
    public class PackSettings {
        /// <summary>
        /// <see cref="PackDB"/> default settings.
        /// </summary>
        public static PackSettings Default { get; } = new ();

        /// <summary>
        /// Creates a read-only settings instance.
        /// </summary>
        /// <param name="defaultFolderPath">Folder that any relative file-path is relative to.</param>
        /// <param name="defaultFilePath">A filename or path to a file that is used if not path argument is given in APIs that offer one.</param>
        /// <param name="backend">The serialisation and storage backend to use.</param>
        /// <param name="location">Where to keep the database.</param>
        /// <param name="encryption">Whether and how to encrypt the serialized data before writing to disk.</param>
        /// <param name="compression">Whether and how to compress the serialized data before writing to disk.</param>
        /// <remarks>Note that encryption is not particularly secure and can only discourage the casual user from editing the database.</remarks>
        public PackSettings (
            string defaultFolderPath = default,
            string defaultFilePath = default,
            Backend backend = Backend.Json,
            Location location = Location.File,
            EncryptionType encryption = EncryptionType.None,
            CompressionType compression = CompressionType.None
        ) {
            if ( string.IsNullOrEmpty ( defaultFolderPath ) ) {
                defaultFolderPath = Application.persistentDataPath;
            }

            if ( string.IsNullOrEmpty ( defaultFilePath ) ) {
                defaultFilePath = "pack.db";
            }

            string trimmedDefaultFolderPath = defaultFolderPath.TrimEnd ( '/', '\\' );
            if ( !Path.IsPathFullyQualified ( trimmedDefaultFolderPath ) ) {
                throw new ArgumentException ( $"Must be a fully qualified folder path ({trimmedDefaultFolderPath})",
                    nameof ( defaultFolderPath ) );
            }

            Location = location;
            DefaultFolderPath = trimmedDefaultFolderPath;
            DefaultFilePath = defaultFilePath;
            Backend = backend;
            Encryption = encryption;
            Compression = compression;
        }

        /// <summary>
        /// Where to keep the database.
        /// </summary>
        public Location Location { get; }

        /// <summary>
        /// The serialisation and storage backend to use.
        /// </summary>
        public Backend Backend { get; }

        /// <summary>
        /// Whether and how to encrypt the serialized data before writing to disk.
        /// </summary>
        public EncryptionType Encryption { get; }

        /// <summary>
        /// Whether and how to compress the serialized data before writing to disk.
        /// </summary>
        public CompressionType Compression { get; }

        /// <summary>
        /// Folder that any relative file-path is relative to. Default is <see cref="Application.persistentDataPath"/>.
        /// </summary>
        public string DefaultFolderPath { get; }

        /// <summary>
        /// A filename or path to a file that is used if not path argument is given in APIs that offer one. If the path given
        /// here is fully qualified then, <see cref="DefaultFolderPath"/> will be ignored. Otherwise the relative path will
        /// be relative to <see cref="DefaultFolderPath"/>. 
        /// </summary>
        public string DefaultFilePath { get; }
    }
}