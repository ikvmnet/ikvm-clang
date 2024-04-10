using System.Runtime.InteropServices;

namespace IKVM.Clang.Sdk.Tasks
{

    /// <summary>
    /// Executes clang with a set of arguments.
    /// </summary>
    public class ClangExe : ExeTask
    {

        /// <inheritdoc />
        protected override string ToolName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "clang.exe" : "clang";

    }

}