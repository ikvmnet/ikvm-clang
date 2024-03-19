using System;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace IKVM.Clang.Sdk.Tasks
{

    /// <summary>
    /// Executes clang with a set of arguments.
    /// </summary>
    public class ClangExe : ToolTask
    {

        /// <inheritdoc />
        protected override string ToolName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "clang.exe" : "clang";

        /// <summary>
        /// Command line arguments to be passed to clang.
        /// </summary>
        [Required]
        public ITaskItem[] Arguments { get; set; } = Array.Empty<ITaskItem>();

        /// <inheritdoc />
        protected override string GenerateFullPathToTool()
        {
            return ToolExe;
        }

        protected override string GenerateResponseFileCommands()
        {
            var sb = new StringBuilder();

            foreach (var arg in Arguments)
                sb.AppendLine(arg.ItemSpec);

            return sb.ToString();
        }

    }

}