using System.Runtime.InteropServices;

using IKVM.Clang.Vsix.Registration;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;

namespace IKVM.Clang.Vsix
{

    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [InstalledProductRegistration("#110", "#112", "1.0")]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideTextMateGrammars("Grammars")]
    public sealed class ClangPackage : AsyncPackage
    {

        public const string PackageGuidString = "D31F7CDF-6323-47F9-B5A1-CFC5A256E5EF";
        public const string ProjectTypeGuid = "C16F8DA6-AF2B-48DF-A3C0-8B93BDB1BB12";

    }

}
