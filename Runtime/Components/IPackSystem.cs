using Cysharp.Threading.Tasks;

namespace Readymade.Persistence
{
    /// <summary>
    /// Provides a contract for populating objects with packed state.
    /// </summary>
    public interface IPackSystem
    {
        /// <summary>
        /// Populates a <see cref="PackIdentity"/> and any nested <see cref="IPackableComponent"/> instances with state from the currently loaded database.
        /// </summary>
        /// <param name="packIdentity">The pack identity to populate with state.</param>
        UniTask RestoreAsync(PackIdentity packIdentity);

        /// <summary>
        /// Captures the state of a <see cref="PackIdentity"/> and any nested <see cref="IPackableComponent"/> instances and stores them in the currently loaded database.
        /// </summary>
        /// <param name="packIdentity">The pack identity to capture state from.</param>
        UniTask CaptureAsync(PackIdentity packIdentity);

        /// <summary>
        /// Deletes the state of a <see cref="PackIdentity"/> and any nested <see cref="IPackableComponent"/> instances from the currently loaded database.
        /// </summary>
        /// <param name="packIdentity">The pack identity to delete from the database.</param>
        UniTask DeleteAsync(PackIdentity packIdentity);
    }
}