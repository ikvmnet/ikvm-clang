using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace IKVM.Clang.Sdk.Tasks
{

    /// <summary>
    /// Finds an LLVM tool, such as clang or llvm-ar, so the build, and tools such as clangd that are given the
    /// build's compile commands, all use the same one.
    /// </summary>
    public class ResolveLlvmTool : Task
    {

        static readonly ConcurrentDictionary<string, string> ResourceDirectories = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// File name of the tool, such as <c>clang.exe</c>.
        /// </summary>
        [Required]
        public string ToolName { get; set; } = "";

        /// <summary>
        /// Directory set by the user to take the tool from. When set, nowhere else is looked at.
        /// </summary>
        public string? ToolPath { get; set; }

        /// <summary>
        /// Directory to look in before the others, such as the one clang was found in when looking for llvm-ar, so
        /// that both come from the same LLVM installation.
        /// </summary>
        public string? PreferredDirectory { get; set; }

        /// <summary>
        /// Root of the Visual Studio installation, whose C++ Clang tools are looked at last.
        /// </summary>
        public string? VsInstallRoot { get; set; }

        /// <summary>
        /// Whether to ask the tool, which must then be clang, for its resource directory.
        /// </summary>
        public bool QueryResourceDirectory { get; set; }

        /// <summary>
        /// Full path of the tool, or <see cref="ToolName"/> alone if it was not found, leaving it to the operating
        /// system to find when run.
        /// </summary>
        [Output]
        public string FullPath { get; set; } = "";

        /// <summary>
        /// The resource directory reported by clang, holding its builtin headers, if requested and available.
        /// </summary>
        [Output]
        public string ResourceDirectory { get; set; } = "";

        /// <inheritdoc />
        public override bool Execute()
        {
            FullPath = Find() ?? ToolName;

            if (FullPath == ToolName)
                Log.LogMessage(MessageImportance.Low, "{0} was not found; relying on the PATH when it is run.", ToolName);
            else
                Log.LogMessage(MessageImportance.Low, "Using {0}.", FullPath);

            if (QueryResourceDirectory && Path.IsPathRooted(FullPath))
                ResourceDirectory = GetResourceDirectory(FullPath) ?? "";

            return true;
        }

        string? Find()
        {
            if (string.IsNullOrWhiteSpace(ToolPath) == false)
                return Path.GetFullPath(Path.Combine(ToolPath, ToolName));

            return GetCandidateDirectories()
                .Where(i => string.IsNullOrWhiteSpace(i) == false)
                .Select(i => TryCombine(i!.Trim().Trim('"'), ToolName))
                .FirstOrDefault(i => i is not null && File.Exists(i));
        }

        IEnumerable<string?> GetCandidateDirectories()
        {
            yield return PreferredDirectory;

            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                yield return directory;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                yield return TryCombine(Environment.GetEnvironmentVariable("ProgramW6432"), "LLVM", "bin");
                yield return TryCombine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LLVM", "bin");

                var architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "ARM64" : "x64";
                yield return TryCombine(VsInstallRoot, "VC", "Tools", "Llvm", architecture, "bin");
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

        /// <summary>
        /// Runs <c>clang -print-resource-dir</c>, once per compiler and version of it.
        /// </summary>
        string? GetResourceDirectory(string clang)
        {
            if (File.Exists(clang) == false)
                return null;

            var key = clang + "|" + File.GetLastWriteTimeUtc(clang).Ticks;
            if (ResourceDirectories.TryGetValue(key, out var cached))
                return cached;

            try
            {
                using var process = Process.Start(new ProcessStartInfo(clang, "-print-resource-dir")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });

                if (process is null)
                    return null;

                var output = process.StandardOutput.ReadToEnd().Trim();
                if (process.WaitForExit(10000) == false || process.ExitCode != 0 || output.Length == 0)
                    return null;

                return ResourceDirectories[key] = Path.GetFullPath(output);
            }
            catch (Exception e)
            {
                Log.LogMessage(MessageImportance.Low, "Could not get the resource directory of {0}: {1}", clang, e.Message);
                return null;
            }
        }

    }

}
