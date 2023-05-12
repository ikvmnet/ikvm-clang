using System.ComponentModel.Composition;

using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.VS;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace IKVM.Clang.Vsix
{

    [Export]
    [AppliesTo(UniqueCapability)]
    [ProjectTypeRegistration(ClangPackage.ProjectTypeGuid, "#1", "#2", ProjectExtension, Language, resourcePackageGuid: ClangPackage.PackageGuidString, PossibleProjectExtensions = ProjectExtension,ProjectTemplatesDir = "..\\..\\Templates\\Projects\\ClangProject")]
    [ProvideProjectItem(ClangPackage.ProjectTypeGuid, "My Items", "..\\..\\Templates\\ProjectItems\\ClangProject", 500)]
    internal class ClangUnconfiguredProject
    {

        internal const string ProjectExtension = "clangproj";

        internal const string UniqueCapability = "Clang";

        internal const string Language = "Clang";

        [ImportingConstructor]
        public ClangUnconfiguredProject(UnconfiguredProject unconfiguredProject)
        {
            ProjectHierarchies = new OrderPrecedenceImportCollection<IVsHierarchy>(projectCapabilityCheckProvider: unconfiguredProject);
        }

        [Import]
        internal UnconfiguredProject UnconfiguredProject { get; private set; }

        [Import]
        internal IActiveConfiguredProjectSubscriptionService SubscriptionService { get; private set; }

        [Import]
        internal IProjectThreadingService ProjectThreadingService { get; private set; }

        [Import]
        internal ActiveConfiguredProject<ConfiguredProject> ActiveConfiguredProject { get; private set; }

        [Import]
        internal ActiveConfiguredProject<ClangConfiguredProject> ClangActiveConfiguredProject { get; private set; }

        [ImportMany(ExportContractNames.VsTypes.IVsProject, typeof(IVsProject))]
        internal OrderPrecedenceImportCollection<IVsHierarchy> ProjectHierarchies { get; private set; }

        internal IVsHierarchy ProjectHierarchy => ProjectHierarchies.Single().Value;

    }

}
