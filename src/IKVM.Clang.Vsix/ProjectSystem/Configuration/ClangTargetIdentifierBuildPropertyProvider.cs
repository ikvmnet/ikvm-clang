using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Build;

namespace IKVM.Clang.Vsix.ProjectSystem.Configuration;

/// <summary>
/// Clears the TargetIdentifier global property for builds (not design-time builds) of projects with more than one
/// target identifier, so that a build runs the cross-targeting build, which builds every target identifier,
/// rather than only the one of the active configuration. .NET does the same for TargetFramework.
/// </summary>
[ExportBuildGlobalPropertiesProvider(designTimeBuildProperties: false)]
[AppliesTo(ClangProjectCapabilities.AppliesTo)]
internal sealed class ClangTargetIdentifierBuildPropertyProvider : StaticGlobalPropertiesProviderBase
{

    static readonly IImmutableDictionary<string, string> Cleared = ImmutableDictionary<string, string>.Empty.Add(ClangTargetIdentifierDimensionProvider.DimensionNameValue, "");

    readonly ConfiguredProject _configuredProject;

    /// <summary>
    /// Creates the provider for the given configured project.
    /// </summary>
    [ImportingConstructor]
    public ClangTargetIdentifierBuildPropertyProvider(IProjectService projectService, ConfiguredProject configuredProject) :
        base(projectService.Services)
    {
        _configuredProject = configuredProject;
    }

    /// <inheritdoc />
    public override Task<IImmutableDictionary<string, string>> GetGlobalPropertiesAsync(CancellationToken cancellationToken)
    {
        // a project that builds a single target identifier has no such dimension, and keeps its own value
        var dimensions = _configuredProject.ProjectConfiguration.Dimensions;
        return Task.FromResult(dimensions.ContainsKey(ClangTargetIdentifierDimensionProvider.DimensionNameValue) ? Cleared : ImmutableDictionary<string, string>.Empty);
    }

}
