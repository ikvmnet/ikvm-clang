using System.Runtime.InteropServices;

using IKVM.Clang.Vsix.Registration;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;

namespace IKVM.Clang.Vsix.Packaging
{

    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuid)]
    [InstalledProductRegistration("#110", "#112", "1.0")]
    [ProvideBindingPath]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideTextMateGrammars("Grammars")]
    [ProvideImageManifest("IKVM.Clang.imagemanifest")]
    public sealed class ClangPackage : AsyncPackage
    {

        public const string PackageGuid = "D31F7CDF-6323-47F9-B5A1-CFC5A256E5EF";

    }

}
