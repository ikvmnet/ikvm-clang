using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json.Linq;

namespace IKVM.Clang.Vsix.Clangd;

/// <summary>
/// The LLVM tools the SDK resolved for one context of a project, as reported by its <c>GetLlvmToolset</c> target.
/// </summary>
internal sealed class ClangToolset
{

    /// <summary>
    /// Creates the toolset.
    /// </summary>
    ClangToolset(ClangCompileContext context, ClangToolsetStatus status, string clangdPath, IReadOnlyList<ClangToolsetProblem> problems)
    {
        Context = context;
        Status = status;
        ClangdPath = clangdPath;
        Problems = problems;
    }

    /// <summary>
    /// The project and target identifier.
    /// </summary>
    public ClangCompileContext Context { get; }

    /// <summary>
    /// Whether the toolset was reported, or why not.
    /// </summary>
    public ClangToolsetStatus Status { get; }

    /// <summary>
    /// Whether the design-time build reported the toolset.
    /// </summary>
    public bool IsReported => Status == ClangToolsetStatus.Reported;

    /// <summary>
    /// Full path of clangd, or an empty string if the SDK did not find it.
    /// </summary>
    public string ClangdPath { get; }

    /// <summary>
    /// The tools the SDK could not resolve.
    /// </summary>
    public IReadOnlyList<ClangToolsetProblem> Problems { get; }

    /// <summary>
    /// Whether clangd can be run: the SDK found it and it is still there.
    /// </summary>
    public bool HasClangd => ClangdPath.Length > 0 && File.Exists(ClangdPath);

    /// <summary>
    /// Gets the toolset of a context whose design-time build did not report one, for the given reason.
    /// </summary>
    public static ClangToolset NotReported(ClangCompileContext context, ClangToolsetStatus status)
    {
        if (status == ClangToolsetStatus.Reported)
            throw new ArgumentOutOfRangeException(nameof(status));

        return new ClangToolset(context, status, "", Array.Empty<ClangToolsetProblem>());
    }

    /// <summary>
    /// Says why none of the given toolsets of a project was reported, and what to do about it.
    /// </summary>
    public static string ExplainNotReported(IReadOnlyCollection<ClangToolset> toolsets)
    {
        // only blame the SDK when it really lacks the toolset
        if (toolsets.All(i => i.Status == ClangToolsetStatus.NotSupported))
            return "this version of IKVM.Clang.Sdk does not report the LLVM tools it uses, so Visual Studio cannot start clangd for it. Update IKVM.Clang.Sdk to a newer version for code completion, navigation and diagnostics.";

        if (toolsets.Any(i => i.Status == ClangToolsetStatus.CrossTargeting))
            return "Visual Studio has loaded only the outer build of this project, which dispatches to each of its TargetIdentifiers and compiles nothing itself, so it reports no LLVM tools or compile commands. Reload the project so that each target identifier is loaded as its own configuration.";

        return "the design-time build reported no LLVM tools, most likely because it failed. Build the project, or look at a design-time build log, to see why.";
    }

    /// <summary>
    /// Reads the toolset from the metadata of the <c>LlvmToolset</c> item.
    /// </summary>
    public static ClangToolset FromMetadata(ClangCompileContext context, IReadOnlyDictionary<string, string> metadata)
    {
        metadata.TryGetValue("ClangdPath", out var clangdPath);
        metadata.TryGetValue("Problems", out var problemsJson);

        var problems = new List<ClangToolsetProblem>();
        if (string.IsNullOrWhiteSpace(problemsJson) == false)
        {
            try
            {
                foreach (var problem in JArray.Parse(problemsJson).OfType<JObject>())
                    problems.Add(new ClangToolsetProblem((string?)problem["tool"] ?? "", (string?)problem["code"] ?? "", (string?)problem["message"] ?? ""));
            }
            catch (Exception)
            {
                problems.Add(new ClangToolsetProblem("", "", "The LLVM tools reported by IKVM.Clang.Sdk could not be read."));
            }
        }

        return new ClangToolset(context, ClangToolsetStatus.Reported, clangdPath ?? "", problems);
    }

}
