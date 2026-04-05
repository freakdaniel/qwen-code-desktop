using System.Diagnostics;

namespace QwenCode.App.Ide;

public sealed class IdeProcessProbe : IIdeProcessProbe
{
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
