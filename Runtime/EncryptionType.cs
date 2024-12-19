namespace Readymade.Persistence {
    /// <summary>
    /// Specifies the encryption in <see cref="PackSettings"/>.
    /// </summary>
    public enum EncryptionType {
        /// <summary>
        /// Use no encryption.
        /// </summary>
        None,
        /// <summary>
        /// Use AES encryption. Note: this is not meant to be secure, merely an obfuscation.
        /// </summary>
        AES
    }
}