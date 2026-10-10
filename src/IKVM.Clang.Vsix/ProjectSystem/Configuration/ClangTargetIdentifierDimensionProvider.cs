using System.ComponentModel.Composition;

using Microsoft.VisualStudio.ProjectSystem;

namespace IKVM.Clang.Vsix.ProjectSystem.Configuration;

/// <summary>
/// The TargetIdentifier dimension, from the <c>TargetIdentifiers</c> property. It is a variant dimension: every
/// target identifier of the active configuration and platform is loaded at once, each as its own configured
/// project with its own design-time build, as .NET does for <c>TargetFrameworks</c>. A project that sets only
/// <c>TargetIdentifier</c> has no such dimension.
/// </summary>
[Export(typeof(IProjectConfigurationDimensionsProvider))]
[AppliesTo(ClangProjectCapabilities.AppliesTo)]
[ConfigurationDimensionDescription(DimensionNameValue, isVariantDimension: true)]
[Order(Order)]
internal sealed class ClangTargetIdentifierDimensionProvider : ClangDimensionProvider
{

    public const string DimensionNameValue = "TargetIdentifier";

    public const int Order = 980;

    [ImportingConstructor]
    public ClangTargetIdentifierDimensionProvider() :
        base(DimensionNameValue, "TargetIdentifiers", null)
    {

    }

}
