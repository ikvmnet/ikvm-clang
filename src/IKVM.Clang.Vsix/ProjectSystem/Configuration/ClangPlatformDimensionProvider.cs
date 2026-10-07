using System.ComponentModel.Composition;

using Microsoft.VisualStudio.ProjectSystem;

namespace IKVM.Clang.Vsix.ProjectSystem.Configuration;

/// <summary>
/// The Platform dimension, from the <c>Platforms</c> property. The platform does not select what is built, which
/// is the target identifier, so the default is the single AnyCPU platform.
/// </summary>
[Export(typeof(IProjectConfigurationDimensionsProvider))]
[AppliesTo(ClangProjectCapabilities.AppliesTo)]
[ConfigurationDimensionDescription(ConfigurationGeneral.Platform, isVariantDimension: false)]
[Order(Order)]
internal sealed class ClangPlatformDimensionProvider : ClangDimensionProvider
{

    public const int Order = 990;

    [ImportingConstructor]
    public ClangPlatformDimensionProvider() :
        base(ConfigurationGeneral.Platform, "Platforms", "AnyCPU")
    {

    }

}
