using System.ComponentModel.Composition;

using Microsoft.VisualStudio.ProjectSystem;

namespace IKVM.Clang.Vsix.ProjectSystem.Configuration;

/// <summary>
/// The Configuration dimension, from the <c>Configurations</c> property.
/// </summary>
[Export(typeof(IProjectConfigurationDimensionsProvider))]
[AppliesTo(ClangProjectCapabilities.AppliesTo)]
[ConfigurationDimensionDescription(ConfigurationGeneral.Configuration, isVariantDimension: false)]
[Order(Order)]
internal sealed class ClangConfigurationDimensionProvider : ClangDimensionProvider
{

    public const int Order = 1000;

    [ImportingConstructor]
    public ClangConfigurationDimensionProvider() :
        base(ConfigurationGeneral.Configuration, "Configurations", "Debug;Release")
    {

    }

}
