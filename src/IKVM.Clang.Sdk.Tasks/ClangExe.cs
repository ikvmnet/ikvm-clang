namespace IKVM.Clang.Sdk.Tasks
{

    using System.Runtime.InteropServices;

    /// <summary>
    /// Executes clang with a set of arguments.
    /// </summary>
    public class ClangExe : ExeTask
    {

        /// <inheritdoc />
        protected override string ToolName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "clang.exe" : "clang";

    }

}