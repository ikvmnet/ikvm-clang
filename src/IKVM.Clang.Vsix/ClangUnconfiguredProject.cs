using System.ComponentModel.Composition;

using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.VS;
using Microsoft.VisualStudio.Shell.Interop;

namespace IKVM.Clang.Vsix
{

    [Export]
    [AppliesTo(UniqueCapability)]
    [ProjectTypeRegistration(
        projectTypeGuid: ClangPackage.ProjectTypeGuid,
        displayName: "#1",
        displayProjectFileExtensions: "#2",
        defaultProjectExtension: ProjectExtension,
        language: Language,
        resourcePackageGuid: ClangPackage.PackageGuidString,
        Capabilities = UniqueCapability,
        PossibleProjectExtensions = ProjectExtension)]
    internal class ClangUnconfiguredProject
    {

        internal const string ProjectExtension = "clangproj";

        internal const string UniqueCapability = "Clang";

        internal const string Language = "Clang";

        [ImportingConstructor]
        public ClangUnconfiguredProject(UnconfiguredProject unconfiguredProject)
        {
            UnconfiguredProject = unconfiguredProject;
            ProjectHierarchies = new OrderPrecedenceImportCollection<IVsHierarchy>(projectCapabilityCheckProvider: unconfiguredProject);
        }

        internal UnconfiguredProject UnconfiguredProject { get; }

        [Import]
        internal IActiveConfiguredProjectSubscriptionService SubscriptionService { get; private set; }

        [Import]
        internal IProjectThreadingService ProjectThreadingService { get; private set; }

        [Import]
        internal ActiveConfiguredProject<ConfiguredProject> ActiveConfiguredProject { get; private set; }

        [Import]
        internal ActiveConfiguredProject<ClangConfiguredProject> ClangActiveConfiguredProject { get; private set; }

        [ImportMany(ExportContractNames.VsTypes.IVsProject, typeof(IVsProject))]
        internal OrderPrecedenceImportCollection<IVsHierarchy> ProjectHierarchies { get; }

        internal IVsHierarchy ProjectHierarchy => ProjectHierarchies.FirstOrDefault().Value;

    }

}
