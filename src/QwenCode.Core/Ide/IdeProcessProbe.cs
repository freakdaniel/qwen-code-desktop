using System.Diagnostics;

namespace QwenCode.Core.Ide;

/// <summary>
/// Represents the Ide Process Probe
/// </summary>
public sealed class IdeProcessProbe : IIdeProcessProbe
{
    /// <summary>
    /// Executes exists
    /// </summary>
    /// <param name="processId">The process id</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public bool Exists(int processId)
    {
        try
        {
            return !Process.GetProcessById(processId).HasExited;
        }
        catch
        {
            return false;
        }
    }
}
