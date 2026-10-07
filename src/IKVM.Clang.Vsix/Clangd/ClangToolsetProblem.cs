namespace IKVM.Clang.Vsix.Clangd;

/// <summary>
/// A tool the SDK could not resolve for a project, with the SDK's explanation of how to correct it.
/// </summary>
internal sealed class ClangToolsetProblem
{

    /// <summary>
    /// Creates the problem.
    /// </summary>
    public ClangToolsetProblem(string tool, string code, string message)
    {
        Tool = tool;
        Code = code;
        Message = message;
    }

    /// <summary>
    /// The tool: <c>Clang</c>, <c>ClangCxx</c>, <c>LlvmAr</c>, <c>Clangd</c> or <c>Linker</c>.
    /// </summary>
    public string Tool { get; }

    /// <summary>
    /// The SDK's diagnostic code, such as <c>ICLANG1001</c>.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// What is wrong and how to correct it.
    /// </summary>
    public string Message { get; }

}
