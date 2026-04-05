using QwenCode.App.Models;

namespace QwenCode.App.Ide;

/// <summary>
/// Defines the contract for Ide Context Service
/// </summary>
public interface IIdeContextService
{
    /// <summary>
    /// Normalizes value
    /// </summary>
    /// <param name="snapshot">The snapshot</param>
    /// <returns>The resulting ide context snapshot</returns>
    IdeContextSnapshot Normalize(IdeContextSnapshot snapshot);

    /// <summary>
    /// Sets value
    /// </summary>
    /// <param name="snapshot">The snapshot</param>
    void Set(IdeContextSnapshot snapshot);

    /// <summary>
    /// Gets value
    /// </summary>
    /// <returns>The resulting ide context snapshot?</returns>
    IdeContextSnapshot? Get();

    /// <summary>
    /// Executes clear
    /// </summary>
    void Clear();
}
