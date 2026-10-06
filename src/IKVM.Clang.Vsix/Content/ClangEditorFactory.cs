using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace IKVM.Clang.Vsix.Content
{

    /// <summary>
    /// Text editor for the source files of Clang projects. It is the standard code editor, except that the buffer is
    /// created with the Clang content type for the file rather than the one Visual Studio maps its extension to. The
    /// factory is not associated with any file extension; <see cref="ClangProjectSpecificEditorProvider"/> selects it
    /// for files opened from a Clang project.
    /// </summary>
    [Guid(EditorFactoryGuidString)]
    internal sealed class ClangEditorFactory : IVsEditorFactory
    {

        public const string EditorFactoryGuidString = "5A0D8C3E-7B21-4C59-9E7A-3F6B1D2C8E41";

        public static readonly Guid EditorFactoryGuid = new(EditorFactoryGuidString);

        readonly IVsEditorAdaptersFactoryService _adapters;
        readonly IContentTypeRegistryService _contentTypes;
        IServiceProvider? _site;

        /// <summary>
        /// Creates the factory.
        /// </summary>
        public ClangEditorFactory(IVsEditorAdaptersFactoryService adapters, IContentTypeRegistryService contentTypes)
        {
            _adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
            _contentTypes = contentTypes ?? throw new ArgumentNullException(nameof(contentTypes));
        }

        /// <inheritdoc />
        public int SetSite(IServiceProvider psp)
        {
            _site = psp;
            return VSConstants.S_OK;
        }

        /// <inheritdoc />
        public int Close()
        {
            _site = null;
            return VSConstants.S_OK;
        }

        /// <inheritdoc />
        public int MapLogicalView(ref Guid rguidLogicalView, out string? pbstrPhysicalView)
        {
            // the code window is the only view, and it serves all text oriented logical views
            pbstrPhysicalView = null;

            if (rguidLogicalView == VSConstants.LOGVIEWID_Primary ||
                rguidLogicalView == VSConstants.LOGVIEWID_TextView ||
                rguidLogicalView == VSConstants.LOGVIEWID_Code ||
                rguidLogicalView == VSConstants.LOGVIEWID_Debugging)
                return VSConstants.S_OK;

            return VSConstants.E_NOTIMPL;
        }

        /// <inheritdoc />
        public int CreateEditorInstance(
            uint grfCreateDoc,
            string pszMkDocument,
            string pszPhysicalView,
            IVsHierarchy pvHier,
            uint itemid,
            IntPtr punkDocDataExisting,
            out IntPtr ppunkDocView,
            out IntPtr ppunkDocData,
            out string pbstrEditorCaption,
            out Guid pguidCmdUI,
            out int pgrfCDW)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ppunkDocView = IntPtr.Zero;
            ppunkDocData = IntPtr.Zero;
            pbstrEditorCaption = "";
            pguidCmdUI = VSConstants.GUID_TextEditorFactory;
            pgrfCDW = 0;

            if ((grfCreateDoc & (VSConstants.CEF_OPENFILE | VSConstants.CEF_SILENT)) == 0)
                return VSConstants.E_INVALIDARG;

            if (_site is null)
                return VSConstants.E_UNEXPECTED;

            IVsTextLines textLines;
            if (punkDocDataExisting != IntPtr.Zero)
            {
                // the document is already open, possibly in another editor: share its buffer if it is a text buffer
                if (Marshal.GetObjectForIUnknown(punkDocDataExisting) is not IVsTextLines existing)
                    return VSConstants.VS_E_INCOMPATIBLEDOCDATA;

                textLines = existing;
            }
            else
            {
                if (ContentTypeNames.TryGetForFile(pszMkDocument, out var contentTypeName) == false)
                    return VSConstants.VS_E_UNSUPPORTEDFORMAT;

                var contentType = _contentTypes.GetContentType(contentTypeName);
                if (contentType is null)
                    return VSConstants.VS_E_UNSUPPORTEDFORMAT;

                textLines = (IVsTextLines)_adapters.CreateVsTextBufferAdapter(_site, contentType);

                // without this the buffer picks a language service from the file extension when the shell loads the
                // file, which would replace the content type given above with the one for that extension
                if (textLines is IVsUserData userData)
                {
                    var detectLanguage = VSConstants.VsTextBufferUserDataGuid.VsBufferDetectLangSID_guid;
                    userData.SetData(ref detectLanguage, false);
                }
            }

            var codeWindow = _adapters.CreateVsCodeWindowAdapter(_site);
            ErrorHandler.ThrowOnFailure(codeWindow.SetBuffer(textLines));

            ppunkDocView = Marshal.GetIUnknownForObject(codeWindow);
            ppunkDocData = Marshal.GetIUnknownForObject(textLines);
            return VSConstants.S_OK;
        }

    }

}
