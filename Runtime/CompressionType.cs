namespace Readymade.Persistence {
    /// <summary>
    /// Specifies the compression type in <see cref="PackSettings"/>.
    /// </summary>
    public enum CompressionType {
        /// <summary>
        /// Use no compression.
        /// </summary>
        None,
        /// <summary>
        /// Use a DEFLATE derived algorithm (LZ, GZip, Zip, etc.).
        /// </summary>
        Defalte
    }
}