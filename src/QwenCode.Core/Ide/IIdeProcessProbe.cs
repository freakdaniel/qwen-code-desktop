namespace QwenCode.Core.Ide;

/// <summary>
/// Defines the contract for Ide Process Probe
/// </summary>
public interface IIdeProcessProbe
{
    /// <summary>
    /// Executes exists
    /// </summary>
    /// <param name="processId">The process id</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool Exists(int processId);
}
