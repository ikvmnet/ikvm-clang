namespace IKVM.Clang.Sdk.Tasks
{

    using System.Runtime.InteropServices;

    /// <summary>
    /// Executes llvm-ar with a set of arguments.
    /// </summary>
    public class LlvmArExe : ExeTask
    {

        /// <inheritdoc />
        protected override string ToolName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "llvm-ar.exe" : "llvm-ar";

    }

}
