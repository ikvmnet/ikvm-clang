namespace IKVM.Clang.Vsix.Clangd;

/// <summary>
/// What a design-time build said about the LLVM tools of one context of a project.
/// </summary>
internal enum ClangToolsetStatus
{

    /// <summary>
    /// The SDK reported the toolset.
    /// </summary>
    Reported,

    /// <summary>
    /// The project's SDK does not define <c>GetLlvmToolset</c> or the <c>LlvmToolset</c> rule: an older version of
    /// IKVM.Clang.Sdk.
    /// </summary>
    NotSupported,

    /// <summary>
    /// The design-time build was the outer build of a project with several target identifiers, which compiles
    /// nothing and so has no toolset; the inner build of each target identifier reports one.
    /// </summary>
    CrossTargeting,

    /// <summary>
    /// The SDK defines the toolset, but the design-time build returned none, usually because it failed.
    /// </summary>
    NotReturned,

}
