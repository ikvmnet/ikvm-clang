using System;
using System.IO;

namespace IKVM.Clang.Vsix.Clangd
{

    /// <summary>
    /// Diagnostic trace of the clangd integration: the messages between Visual Studio and clangd, and the compile
    /// commands collected from projects. Written only when <see cref="Variable"/> names a file.
    /// </summary>
    internal static class ClangdTrace
    {

        /// <summary>
        /// Environment variable naming the file to append the trace to.
        /// </summary>
        public const string Variable = "IKVM_CLANG_CLANGD_TRACE";

        static readonly object Sync = new();
        static readonly string? Path = Environment.GetEnvironmentVariable(Variable);

        /// <summary>
        /// Whether tracing is on.
        /// </summary>
        public static bool IsEnabled => string.IsNullOrWhiteSpace(Path) == false;

        /// <summary>
        /// Appends a line to the trace, if tracing is on.
        /// </summary>
        public static void Write(string line)
        {
            if (IsEnabled == false)
                return;

            try
            {
                lock (Sync)
                    File.AppendAllText(Path!, $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // tracing must never break the integration
            }
        }

    }

}
