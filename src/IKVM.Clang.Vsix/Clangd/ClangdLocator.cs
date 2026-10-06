using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IKVM.Clang.Vsix.Clangd
{

    /// <summary>
    /// Finds the clangd executable.
    /// </summary>
    internal static class ClangdLocator
    {

        /// <summary>
        /// Environment variable that names the clangd executable to use.
        /// </summary>
        public const string PathVariable = "IKVM_CLANG_CLANGD_PATH";

        const string FileName = "clangd.exe";

        /// <summary>
        /// Returns the full path of clangd, or <see langword="null"/> if it cannot be found. Looks, in order, at
        /// <see cref="PathVariable"/>, the directories of the compilers the projects build with (so that clangd
        /// matches them), the directories on <c>PATH</c>, the default LLVM installation, and the LLVM tools that ship
        /// with Visual Studio's C++ workload.
        /// </summary>
        public static string? Find(IEnumerable<string> compilerDirectories)
        {
            return GetCandidates(compilerDirectories).FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// Places looked at by <see cref="Find"/>, for reporting when clangd is missing.
        /// </summary>
        public static IEnumerable<string> GetCandidates(IEnumerable<string> compilerDirectories)
        {
            var configured = Environment.GetEnvironmentVariable(PathVariable);
            if (string.IsNullOrWhiteSpace(configured) == false)
                yield return Environment.ExpandEnvironmentVariables(configured);

            foreach (var directory in compilerDirectories)
            {
                var path = TryCombine(directory, FileName);
                if (path is not null)
                    yield return path;
            }

            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                var path = TryCombine(directory.Trim().Trim('"'), FileName);
                if (path is not null)
                    yield return path;
            }

            foreach (var programFiles in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetEnvironmentVariable("ProgramW6432") })
            {
                var path = TryCombine(programFiles, "LLVM", "bin", FileName);
                if (path is not null)
                    yield return path;
            }

            // devenv.exe lives in Common7\IDE under the Visual Studio installation
            var devenv = Process.GetCurrentProcess().MainModule?.FileName;
            var installation = devenv is null ? null : Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(devenv)));
            foreach (var architecture in new[] { "x64", "ARM64" })
            {
                var path = TryCombine(installation, "VC", "Tools", "Llvm", architecture, "bin", FileName);
                if (path is not null)
                    yield return path;
            }
        }

        static string? TryCombine(params string?[] parts)
        {
            if (parts.Any(string.IsNullOrEmpty))
                return null;

            try
            {
                return Path.Combine(parts!);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

    }

}
