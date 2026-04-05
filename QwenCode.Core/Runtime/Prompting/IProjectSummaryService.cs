using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public interface IProjectSummaryService
{
    ProjectSummarySnapshot? Read(QwenRuntimeProfile runtimeProfile);
}
