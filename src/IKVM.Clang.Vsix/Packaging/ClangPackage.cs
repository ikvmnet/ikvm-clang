using IKVM.Clang.Vsix.Registration;

using Microsoft.VisualStudio.Shell;

using System.Runtime.InteropServices;

namespace IKVM.Clang.Vsix.Packaging
{

    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuid)]
    [InstalledProductRegistration("#110", "#112", "1.0")]
    [ProvideBindingPath]
    [ProvideTextMateGrammars("Grammars")]
    [ProvideImageManifest("IKVM.Clang.imagemanifest")]
    public sealed class ClangPackage : AsyncPackage
    {

        public const string PackageGuid = "D31F7CDF-6323-47F9-B5A1-CFC5A256E5EF";

    }

}
