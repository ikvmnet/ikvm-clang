using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

using Task = System.Threading.Tasks.Task;

namespace IKVM.Clang.Vsix.Clangd;

/// <summary>
/// The info bar across the top of Visual Studio that says why clangd, and so code completion, navigation and
/// diagnostics, is not available for Clang projects, and how to correct it.
/// </summary>
[Export]
[PartCreationPolicy(CreationPolicy.Shared)]
internal sealed class ClangdInfoBar : IVsInfoBarUIEvents
{

    readonly IServiceProvider _serviceProvider;
    readonly JoinableTaskFactory _joinableTaskFactory;
    IVsInfoBarUIElement? _element;
    uint _cookie;
    string? _text;
    bool _dismissed;

    /// <summary>
    /// Creates the info bar.
    /// </summary>
    [ImportingConstructor]
    public ClangdInfoBar(SVsServiceProvider serviceProvider, JoinableTaskContext joinableTaskContext)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _joinableTaskFactory = joinableTaskContext?.Factory ?? throw new ArgumentNullException(nameof(joinableTaskContext));
    }

    /// <summary>
    /// Shows the message, replacing any shown before, with a link to open the project file it concerns. Once the
    /// user closes the info bar, the same message is not shown again.
    /// </summary>
    public Task ShowAsync(string text, string? projectPath)
    {
        return _joinableTaskFactory.RunAsync(async () =>
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();

            if (_element is not null && _text == text)
                return;

            if (_dismissed && _text == text)
                return;

            RemoveElement();
            _text = text;
            _dismissed = false;

            var shell = (IVsShell?)_serviceProvider.GetService(typeof(SVsShell));
            var factory = (IVsInfoBarUIFactory?)_serviceProvider.GetService(typeof(SVsInfoBarUIFactory));
            if (shell is null || factory is null)
                return;

            if (ErrorHandler.Failed(shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var hostObject)) || hostObject is not IVsInfoBarHost host)
                return;

            var actions = projectPath is null
                ? Array.Empty<IVsInfoBarActionItem>()
                : new IVsInfoBarActionItem[] { new InfoBarHyperlink("Open project file", projectPath) };

            var model = new InfoBarModel(new[] { new InfoBarTextSpan(text) }, actions, KnownMonikers.StatusWarning, isCloseButtonVisible: true);
            _element = factory.CreateInfoBar(model);
            _element.Advise(this, out _cookie);
            host.AddInfoBar(_element);
        }).Task;
    }

    /// <summary>
    /// Removes the info bar, such as once clangd is available.
    /// </summary>
    public Task CloseAsync()
    {
        return _joinableTaskFactory.RunAsync(async () =>
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            RemoveElement();
            _text = null;
        }).Task;
    }

    void RemoveElement()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (_element is null)
            return;

        var element = _element;
        _element = null;
        element.Unadvise(_cookie);
        element.Close();
    }

    /// <inheritdoc />
    public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (infoBarUIElement == _element)
        {
            _element.Unadvise(_cookie);
            _element = null;
            _dismissed = true;
        }
    }

    /// <inheritdoc />
    public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (actionItem.ActionContext is string path)
            VsShellUtilities.OpenDocument(_serviceProvider, path);
    }

}
