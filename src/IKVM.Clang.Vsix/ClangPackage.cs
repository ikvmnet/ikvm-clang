using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.Shell;

namespace IKVM.Clang.Vsix
{

    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    public sealed class ClangPackage : AsyncPackage
    {

        public const string PackageGuidString = "D31F7CDF-6323-47F9-B5A1-CFC5A256E5EF";
        public const string ProjectTypeGuid = "6DE1C62B-E8D7-451A-8734-87EAEB46E35B";

    }

}
