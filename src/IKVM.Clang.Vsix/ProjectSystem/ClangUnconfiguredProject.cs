using System.ComponentModel.Composition;

using IKVM.Clang.Vsix.Packaging;

using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.VS;
using Microsoft.VisualStudio.Shell.Interop;

namespace IKVM.Clang.Vsix.ProjectSystem;

/// <summary>
/// Configuration-independent state of a Clang project. Also carries the registration of the Clang project type
/// with Visual Studio.
/// </summary>
[Export]
[AppliesTo(ClangProjectCapabilities.AppliesTo)]
[ProjectTypeRegistration(
    projectTypeGuid: ProjectType.Clang,
    displayName: "#1",
    displayProjectFileExtensions: "#2",
    defaultProjectExtension: ProjectExtension,
    language: Language,
    resourcePackageGuid: ClangPackage.PackageGuid,
    Capabilities = ClangProjectCapabilities.Default,
    DisableAsynchronousProjectTreeLoad = true,
    PossibleProjectExtensions = ProjectExtension,
    NewProjectRequireNewFolderVsTemplate = true,
    SupportsSolutionChangeWithoutReload = true)]
internal class ClangUnconfiguredProject
{

    internal const string ProjectExtension = "clangproj";

    internal const string Language = "Clang";

    /// <summary>
    /// Creates the instance for the given project.
    /// </summary>
    [ImportingConstructor]
    public ClangUnconfiguredProject(UnconfiguredProject unconfiguredProject)
    {
        UnconfiguredProject = unconfiguredProject;
        ProjectHierarchies = new OrderPrecedenceImportCollection<IVsHierarchy>(projectCapabilityCheckProvider: unconfiguredProject);
    }

    /// <summary>
    /// The CPS unconfigured project this instance belongs to.
    /// </summary>
    internal UnconfiguredProject UnconfiguredProject { get; }

    /// <summary>
    /// Provides data sources that follow whichever configuration is active.
    /// </summary>
    [Import]
    internal IActiveConfiguredProjectSubscriptionService SubscriptionService { get; private set; } = null!;

    /// <summary>
    /// Threading service of the project, used to switch to the UI thread and join project work.
    /// </summary>
    [Import]
    internal IProjectThreadingService ProjectThreadingService { get; private set; } = null!;

    /// <summary>
    /// The currently active configured project.
    /// </summary>
    [Import]
    internal ActiveConfiguredProject<ConfiguredProject> ActiveConfiguredProject { get; private set; } = null!;

    /// <summary>
    /// The Clang state of the currently active configured project.
    /// </summary>
    [Import]
    internal ActiveConfiguredProject<ClangConfiguredProject> ClangActiveConfiguredProject { get; private set; } = null!;

    /// <summary>
    /// The Visual Studio hierarchies exported for this project, in precedence order.
    /// </summary>
    [ImportMany(ExportContractNames.VsTypes.IVsProject, typeof(IVsProject))]
    internal OrderPrecedenceImportCollection<IVsHierarchy> ProjectHierarchies { get; }

    /// <summary>
    /// The Visual Studio hierarchy of this project, or <see langword="null"/> if none has been exported.
    /// </summary>
    internal IVsHierarchy? ProjectHierarchy => ProjectHierarchies.FirstOrDefault()?.Value;

}
