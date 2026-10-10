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
    ClangToolset(ClangCompileContext context, bool isReported, string clangdPath, IReadOnlyList<ClangToolsetProblem> problems)
    {
        Context = context;
        IsReported = isReported;
        ClangdPath = clangdPath;
        Problems = problems;
    }

    /// <summary>
    /// The project and target identifier.
    /// </summary>
    public ClangCompileContext Context { get; }

    /// <summary>
    /// Whether the project's SDK reports its toolset at all; older versions of IKVM.Clang.Sdk do not.
    /// </summary>
    public bool IsReported { get; }

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
    /// Gets the toolset of a project whose SDK does not report one.
    /// </summary>
    public static ClangToolset NotReported(ClangCompileContext context)
    {
        return new ClangToolset(context, false, "", Array.Empty<ClangToolsetProblem>());
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

        return new ClangToolset(context, true, clangdPath ?? "", problems);
    }

}
