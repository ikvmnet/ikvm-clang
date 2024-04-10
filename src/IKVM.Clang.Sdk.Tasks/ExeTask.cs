using System;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace IKVM.Clang.Sdk.Tasks
{

    /// <summary>
    /// Executes llvm-ar with a set of arguments.
    /// </summary>
    public abstract class ExeTask : ToolTask
    {

        /// <summary>
        /// Command line arguments to be passed to llvm-ar.
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
            {
                if (arg.ItemSpec.Length > 0)
                {
                    AppendEscaped(sb, arg.ItemSpec);

                    if (arg.GetMetadata("Value") is string value && value.Length > 0)
                    {
                        // separate the value
                        if (arg.GetMetadata("Separator") is string seperator && seperator.Length > 0)
                            sb.Append(seperator);

                        AppendEscaped(sb, value);

                        // additional Value/Separator metadata
                        for (int i = 2; i < 128; i++)
                        {
                            if (arg.GetMetadata($"Value{i}") is string nextValue && nextValue.Length > 0)
                            {
                                // seperate the second value
                                if (arg.GetMetadata($"Separator{i}") is string nextSeperator && nextSeperator.Length > 0)
                                    sb.Append(nextSeperator);

                                AppendEscaped(sb, nextValue);
                            }
                            else
                            {
                                // no more values, exit early
                                break;
                            }
                        }
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Escapes the specified string if it contains any characters the require escaping.
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        static StringBuilder AppendEscaped(StringBuilder sb, string value)
        {
            var ws = false;

            foreach (var c in value)
            {
                switch (c)
                {
                    case '\"':
                        sb.Append('\\').Append(c);
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    default:
                        ws |= char.IsWhiteSpace(c);
                        sb.Append(c);
                        break;
                }
            }

            if (ws)
            {
                sb.Insert(0, '\"');
                sb.Append('\"');
            }

            return sb;
        }

    }

}
