using System;
using System.Runtime.InteropServices;
using System.Threading;

using IKVM.Clang.Vsix.Content;
using IKVM.Clang.Vsix.Registration;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Utilities;

using Task = System.Threading.Tasks.Task;

namespace IKVM.Clang.Vsix.Packaging
{

    /// <summary>
    /// The IKVM.Clang Visual Studio package. Registers the extension's resources, TextMate grammars, binding path and
    /// the editor for Clang project source files; the project system itself is composed through MEF.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuid)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideBindingPath]
    [ProvideTextMateGrammars("Grammars")]
    [ProvideEditorFactory(typeof(ClangEditorFactory), 113, CommonPhysicalViewAttributes = (int)__VSPHYSICALVIEWATTRIBUTES.PVA_SupportsPreview, TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]
    [ProvideEditorLogicalView(typeof(ClangEditorFactory), VSConstants.LOGVIEWID.TextView_string)]
    [ProvideEditorLogicalView(typeof(ClangEditorFactory), VSConstants.LOGVIEWID.Code_string)]
    [ProvideEditorLogicalView(typeof(ClangEditorFactory), VSConstants.LOGVIEWID.Debugging_string)]
    public sealed class ClangPackage : AsyncPackage
    {

        public const string PackageGuid = "D31F7CDF-6323-47F9-B5A1-CFC5A256E5EF";

        /// <inheritdoc />
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            var componentModel = (IComponentModel?)await GetServiceAsync(typeof(SComponentModel)) ?? throw new InvalidOperationException("Could not obtain the component model.");
            var editorFactory = new ClangEditorFactory(
                componentModel.GetService<IVsEditorAdaptersFactoryService>(),
                componentModel.GetService<IContentTypeRegistryService>());

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RegisterEditorFactory(editorFactory);
        }

    }

}
