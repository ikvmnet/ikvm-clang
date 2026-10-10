namespace IKVM.Clang.Sdk.Tasks;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

/// <summary>
/// Finds the LLVM tools a Clang project uses: clang, clang++, llvm-ar and clangd. Each can be given by full path;
/// otherwise all come from <see cref="LlvmToolsPath"/> if set, or else from one LLVM installation found
/// automatically, so that the tools match each other. Tools that cannot be found are reported in
/// <see cref="Problems"/> rather than failing the task, leaving it to the targets that need a tool to fail, and
/// to design-time builds to pass the problems on to Visual Studio.
/// </summary>
public class ResolveLlvmToolset : Task
{

    /// <summary>
    /// A tool of the suite: the property that overrides its location, and its file name.
    /// </summary>
    sealed class Tool
    {

        public Tool(string id, string displayName, string overrideProperty, string? overridePath, string fileName)
        {
            Id = id;
            DisplayName = displayName;
            OverrideProperty = overrideProperty;
            OverridePath = string.IsNullOrWhiteSpace(overridePath) ? null : overridePath!.Trim();
            FileName = fileName;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string OverrideProperty { get; }

        public string? OverridePath { get; }

        public string FileName { get; }

    }

    static readonly ConcurrentDictionary<string, string> ResourceDirectories = new(StringComparer.OrdinalIgnoreCase);

    static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    static readonly StringComparer PathComparer = IsWindows || IsMacOS ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Directory to take every tool from, such as the <c>bin</c> directory of an LLVM installation. When set,
    /// tools are not looked for anywhere else.
    /// </summary>
    public string? LlvmToolsPath { get; set; }

    /// <summary>
    /// Full path of clang, overriding where it is looked for. Also the output: where clang was found.
    /// </summary>
    [Output]
    public string? ClangPath { get; set; }

    /// <summary>
    /// Full path of clang++, overriding where it is looked for. Also the output: where clang++ was found.
    /// </summary>
    [Output]
    public string? ClangCxxPath { get; set; }

    /// <summary>
    /// Full path of llvm-ar, overriding where it is looked for. Also the output: where llvm-ar was found.
    /// </summary>
    [Output]
    public string? LlvmArPath { get; set; }

    /// <summary>
    /// Full path of clangd, overriding where it is looked for. Also the output: where clangd was found.
    /// </summary>
    [Output]
    public string? ClangdPath { get; set; }

    /// <summary>
    /// Full path of the linker clang is to run, if set. Only checked for existence; clang otherwise finds the
    /// linker itself, next to it.
    /// </summary>
    [Output]
    public string? LinkerPath { get; set; }

    /// <summary>
    /// File name of clang.
    /// </summary>
    public string ClangFileName { get; set; } = IsWindows ? "clang.exe" : "clang";

    /// <summary>
    /// File name of clang++.
    /// </summary>
    public string ClangCxxFileName { get; set; } = IsWindows ? "clang++.exe" : "clang++";

    /// <summary>
    /// File name of llvm-ar.
    /// </summary>
    public string LlvmArFileName { get; set; } = IsWindows ? "llvm-ar.exe" : "llvm-ar";

    /// <summary>
    /// File name of clangd.
    /// </summary>
    public string ClangdFileName { get; set; } = IsWindows ? "clangd.exe" : "clangd";

    /// <summary>
    /// Directories searched after the LLVM installations, separated as in <c>PATH</c>. Defaults to <c>PATH</c>.
    /// </summary>
    public string? SearchPath { get; set; }

    /// <summary>
    /// Whether to look in the places LLVM is installed to on this platform, such as <c>C:\Program Files\LLVM\bin</c>,
    /// <c>/usr/lib/llvm-*/bin</c> and Homebrew's LLVM.
    /// </summary>
    public bool SearchPlatformLocations { get; set; } = true;

    /// <summary>
    /// Root of the Visual Studio installation, whose C++ Clang tools are among the places looked at.
    /// </summary>
    public string? VsInstallRoot { get; set; }

    /// <summary>
    /// Whether to ask clang for its resource directory.
    /// </summary>
    public bool QueryResourceDirectory { get; set; } = true;

    /// <summary>
    /// The directory of the LLVM installation clang came from.
    /// </summary>
    [Output]
    public string LlvmToolsDirectory { get; set; } = "";

    /// <summary>
    /// The resource directory reported by clang, which holds its builtin headers.
    /// </summary>
    [Output]
    public string ClangResourceDirectory { get; set; } = "";

    /// <summary>
    /// One item per tool that could not be resolved, named by the tool (<c>Clang</c>, <c>ClangCxx</c>,
    /// <c>LlvmAr</c>, <c>Clangd</c> or <c>Linker</c>), with <c>Code</c> and <c>Message</c> metadata. The message
    /// says how to correct it.
    /// </summary>
    [Output]
    public ITaskItem[] Problems { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// <see cref="Problems"/> as a JSON array of objects with <c>tool</c>, <c>code</c> and <c>message</c>, for
    /// tools such as the Visual Studio extension.
    /// </summary>
    [Output]
    public string ProblemsJson { get; set; } = "[]";

    readonly List<ITaskItem> _problems = new();

    /// <inheritdoc />
    public override bool Execute()
    {
        var clang = new Tool("Clang", "clang", nameof(ClangPath), ClangPath, ClangFileName);
        var clangCxx = new Tool("ClangCxx", "clang++", nameof(ClangCxxPath), ClangCxxPath, ClangCxxFileName);
        var llvmAr = new Tool("LlvmAr", "llvm-ar", nameof(LlvmArPath), LlvmArPath, LlvmArFileName);
        var clangd = new Tool("Clangd", "clangd", nameof(ClangdPath), ClangdPath, ClangdFileName);

        var llvmToolsPath = string.IsNullOrWhiteSpace(LlvmToolsPath) ? null : LlvmToolsPath!.Trim();
        if (llvmToolsPath is not null && Directory.Exists(llvmToolsPath) == false)
        {
            foreach (var tool in new[] { clang, clangCxx, llvmAr, clangd }.Where(i => i.OverridePath is null))
                AddProblem(tool, "ICLANG1003", $"{tool.DisplayName} was not found because LlvmToolsPath is set to '{llvmToolsPath}', which does not exist. Set it to the directory that holds the LLVM tools, such as the bin directory of an LLVM installation, or remove it to find them automatically.");

            ClangPath = clang.OverridePath is null ? "" : Resolve(clang, null, null);
            ClangCxxPath = clangCxx.OverridePath is null ? "" : Resolve(clangCxx, null, null);
            LlvmArPath = llvmAr.OverridePath is null ? "" : Resolve(llvmAr, null, null);
            ClangdPath = clangd.OverridePath is null ? "" : Resolve(clangd, null, null);
        }
        else
        {
            // every installation that has clang, those with the whole suite first, so that the tools match
            var installations = llvmToolsPath is not null ? new[] { Path.GetFullPath(llvmToolsPath) } : FindInstallations(clang.FileName, llvmAr.FileName);

            ClangPath = Resolve(clang, llvmToolsPath, installations);

            // the other tools come from clang's installation, wherever that is
            var clangDirectory = ClangPath.Length > 0 ? Path.GetDirectoryName(ClangPath) : null;
            var others = llvmToolsPath is not null ? installations : Prepend(clangDirectory, installations);

            ClangCxxPath = Resolve(clangCxx, llvmToolsPath, others);
            LlvmArPath = Resolve(llvmAr, llvmToolsPath, others);
            ClangdPath = Resolve(clangd, llvmToolsPath, others);
        }

        if (ClangPath.Length > 0)
        {
            LlvmToolsDirectory = Path.GetDirectoryName(ClangPath) ?? "";
            if (QueryResourceDirectory)
                ClangResourceDirectory = GetResourceDirectory(ClangPath) ?? "";
        }

        if (string.IsNullOrWhiteSpace(LinkerPath) == false)
        {
            if (File.Exists(LinkerPath))
            {
                LinkerPath = Path.GetFullPath(LinkerPath);
            }
            else
            {
                AddProblem("Linker", "ICLANG1002", $"LinkerPath is set to '{LinkerPath}', which does not exist. Correct it, or remove it to let clang find its linker.");
                LinkerPath = "";
            }
        }
        else
        {
            LinkerPath = "";
        }

        Problems = _problems.ToArray();
        ProblemsJson = ToJson(_problems);

        Log.LogMessage(MessageImportance.Low, "LLVM tools: clang={0}; clang++={1}; llvm-ar={2}; clangd={3}; resource directory={4}", ClangPath, ClangCxxPath, LlvmArPath, ClangdPath, ClangResourceDirectory);
        return true;
    }

    /// <summary>
    /// Resolves one tool: its override if set, which must exist; otherwise the first of the given directories that
    /// has it.
    /// </summary>
    string Resolve(Tool tool, string? llvmToolsPath, IReadOnlyList<string>? directories)
    {
        if (tool.OverridePath is not null)
        {
            if (File.Exists(tool.OverridePath))
                return Path.GetFullPath(tool.OverridePath);

            AddProblem(tool, "ICLANG1002", $"{tool.OverrideProperty} is set to '{tool.OverridePath}', which does not exist. Correct it, or remove it to find {tool.DisplayName} automatically.");
            return "";
        }

        directories ??= Array.Empty<string>();
        foreach (var directory in directories)
        {
            var path = TryCombine(directory, tool.FileName);
            if (path is not null && File.Exists(path))
                return Path.GetFullPath(path);
        }

        if (llvmToolsPath is not null)
            AddProblem(tool, "ICLANG1001", $"{tool.DisplayName} was not found in '{llvmToolsPath}', the directory LlvmToolsPath names. Install it there, set LlvmToolsPath to the directory that holds the LLVM tools, or set {tool.OverrideProperty} to the full path of {tool.FileName}.");
        else
            AddProblem(tool, "ICLANG1001", $"{tool.DisplayName} was not found. Install LLVM (https://github.com/llvm/llvm-project/releases) and add its bin directory to PATH, or set LlvmToolsPath to the directory that holds the LLVM tools, or {tool.OverrideProperty} to the full path of {tool.FileName}. Looked in: {(directories.Count > 0 ? string.Join("; ", directories) : "nowhere")}.");

        return "";
    }

    /// <summary>
    /// Gets the directories that have clang, in order of preference: those that also have llvm-ar, and so likely
    /// the whole suite, before those with clang alone (such as Apple's /usr/bin), each group in search order.
    /// </summary>
    IReadOnlyList<string> FindInstallations(string clangFileName, string llvmArFileName)
    {
        var directories = GetSearchDirectories().Distinct(PathComparer).ToList();
        var withClang = directories.Where(i => File.Exists(Path.Combine(i, clangFileName))).ToList();
        var complete = withClang.Where(i => File.Exists(Path.Combine(i, llvmArFileName)));
        return complete.Concat(withClang).Concat(directories).Distinct(PathComparer).ToList();
    }

    /// <summary>
    /// Gets the directories to look in: <see cref="SearchPath"/>, then the platform's usual LLVM installations.
    /// </summary>
    IEnumerable<string> GetSearchDirectories()
    {
        foreach (var directory in (SearchPath ?? Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            if (Normalize(directory) is string d)
                yield return d;

        if (SearchPlatformLocations == false)
            yield break;

        foreach (var directory in GetPlatformDirectories())
            if (Normalize(directory) is string d && Directory.Exists(d))
                yield return d;
    }

    IEnumerable<string?> GetPlatformDirectories()
    {
        if (IsWindows)
        {
            yield return TryCombine(Environment.GetEnvironmentVariable("ProgramW6432"), "LLVM", "bin");
            yield return TryCombine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LLVM", "bin");

            var architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "ARM64" : "x64";
            yield return TryCombine(VsInstallRoot, "VC", "Tools", "Llvm", architecture, "bin");
        }
        else if (IsMacOS)
        {
            // Homebrew's LLVM, which unlike Apple's clang includes llvm-ar, lld and clangd
            yield return "/opt/homebrew/opt/llvm/bin";
            yield return "/usr/local/opt/llvm/bin";
        }
        else
        {
            // Debian and Ubuntu's versioned packages, newest first
            foreach (var directory in GetVersionedDirectories("/usr/lib", "llvm-"))
                yield return directory;
        }
    }

    static IEnumerable<string> GetVersionedDirectories(string root, string prefix)
    {
        if (Directory.Exists(root) == false)
            return Enumerable.Empty<string>();

        try
        {
            return Directory.GetDirectories(root, prefix + "*")
                .Select(i => (Path: i, Version: int.TryParse(Path.GetFileName(i).Substring(prefix.Length), out var v) ? v : -1))
                .Where(i => i.Version >= 0)
                .OrderByDescending(i => i.Version)
                .Select(i => Path.Combine(i.Path, "bin"));
        }
        catch (IOException)
        {
            return Enumerable.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Enumerable.Empty<string>();
        }
    }

    static IReadOnlyList<string> Prepend(string? first, IReadOnlyList<string> rest)
    {
        return first is null ? rest : new[] { first }.Concat(rest).Distinct(PathComparer).ToList();
    }

    static string? Normalize(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        try
        {
            return Path.GetFullPath(directory!.Trim().Trim('"'));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
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

    void AddProblem(Tool tool, string code, string message)
    {
        AddProblem(tool.Id, code, message);
    }

    void AddProblem(string tool, string code, string message)
    {
        var item = new TaskItem(tool);
        item.SetMetadata("Code", code);
        item.SetMetadata("Message", Escape(message));
        _problems.Add(item);
    }

    static string ToJson(IEnumerable<ITaskItem> problems)
    {
        var json = new StringBuilder("[");
        foreach (var problem in problems)
        {
            if (json.Length > 1)
                json.Append(',');

            json.Append("{\"tool\":");
            ClangArguments.AppendJsonString(json, problem.ItemSpec);
            json.Append(",\"code\":");
            ClangArguments.AppendJsonString(json, problem.GetMetadata("Code"));
            json.Append(",\"message\":");
            ClangArguments.AppendJsonString(json, problem.GetMetadata("Message"));
            json.Append('}');
        }

        return json.Append(']').ToString();
    }

    /// <summary>
    /// Escapes the characters MSBuild treats specially, since <see cref="TaskItem"/> takes metadata in escaped form.
    /// </summary>
    static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is '%' or '*' or '?' or '@' or '$' or '(' or ')' or ';' or '\'')
                sb.Append('%').Append(((int)c).ToString("X2"));
            else
                sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Runs <c>clang -print-resource-dir</c>, once per compiler and version of it.
    /// </summary>
    string? GetResourceDirectory(string clang)
    {
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
