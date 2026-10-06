using System;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace IKVM.Clang.Sdk.Tasks
{

    /// <summary>
    /// Executes executable with a set of arguments.
    /// </summary>
    public abstract class ExeTask : ToolTask
    {

        /// <summary>
        /// Command line arguments to be passed to executable. See <see cref="ClangArguments.Expand"/> for how items
        /// map to arguments.
        /// </summary>
        [Required]
        public ITaskItem[] Arguments { get; set; } = Array.Empty<ITaskItem>();

        /// <inheritdoc />
        protected override string GenerateFullPathToTool()
        {
            return ToolExe;
        }

        /// <inheritdoc />
        protected override string GenerateResponseFileCommands()
        {
            var sb = new StringBuilder();

            foreach (var argument in ClangArguments.Expand(Arguments))
                ClangArguments.AppendQuoted(sb, argument).AppendLine();

            return sb.ToString();
        }

    }

}
