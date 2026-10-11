using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using IKVM.Clang.Vsix.Content;

using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace IKVM.Clang.Vsix.Clangd;

/// <summary>
/// Runs clangd for the source files of Clang projects. It applies to the Clang content types, which only files
/// opened from Clang projects have (see <see cref="ClangEditorFactory"/>), so other C and C++ files are left to
/// Visual Studio's own language service.
/// </summary>
/// <remarks>
/// clangd is the one IKVM.Clang.Sdk resolved for the projects (its ClangdPath), reported by their design-time
/// builds. Activation waits until a project has reported a usable clangd; if none can, an info bar says why and how
/// to correct it, and activation completes as soon as a design-time build reports one. Waiting inside activation,
/// rather than starting the client late, means Visual Studio still hands clangd the documents already open.
/// </remarks>
[Export(typeof(ILanguageClient))]
[ContentType(ContentTypeNames.CCode)]
[ContentType(ContentTypeNames.CppCode)]
[ContentType(ContentTypeNames.ObjCCode)]
[ContentType(ContentTypeNames.ObjCppCode)]
[ContentType(ContentTypeNames.CppHeader)]
internal sealed class ClangdLanguageClient : ILanguageClient
{

    readonly ClangCompileDatabase _database;
    readonly ClangdInfoBar _infoBar;
    readonly SemaphoreSlim _sync = new(1, 1);
    Process? _process;
    CancellationTokenSource? _cancellation;
    bool _loaded;
    TaskCompletionSource<string> _clangdReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Creates the client. The Error List reporter is imported only so that it exists, and reports toolset
    /// problems, whenever Clang projects are open.
    /// </summary>
    [ImportingConstructor]
    public ClangdLanguageClient(ClangCompileDatabase database, ClangdInfoBar infoBar, ClangToolsetErrorList errorList)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _infoBar = infoBar ?? throw new ArgumentNullException(nameof(infoBar));
        _ = errorList ?? throw new ArgumentNullException(nameof(errorList));
    }

    /// <inheritdoc />
    public string Name => "clangd";

    /// <inheritdoc />
    public IEnumerable<string>? ConfigurationSections => null;

    /// <inheritdoc />
    public object? InitializationOptions => null;

    /// <inheritdoc />
    public IEnumerable<string>? FilesToWatch => null;

    /// <inheritdoc />
    public bool ShowNotificationOnInitializeFailed => true;

    /// <inheritdoc />
    public event AsyncEventHandler<EventArgs>? StartAsync;

    /// <inheritdoc />
    /// <remarks>
    /// Never raised: clangd runs until Visual Studio shuts it down, and if it exits on its own the connection
    /// closes, which Visual Studio reports.
    /// </remarks>
    public event AsyncEventHandler<EventArgs>? StopAsync { add { } remove { } }

    /// <inheritdoc />
    public async Task OnLoadedAsync()
    {
        _loaded = true;
        _database.ToolsetsChanged += (_, _) => _ = Task.Run(EvaluateAsync);
        await EvaluateAsync();

        if (StartAsync is not null)
            await StartAsync.InvokeAsync(this, EventArgs.Empty);
    }

    /// <summary>
    /// Starts clangd if a project has reported one, or else explains why it cannot be started.
    /// </summary>
    async Task EvaluateAsync()
    {
        await _sync.WaitAsync();
        try
        {
            if (_loaded == false || _clangdReady.Task.IsCompleted)
                return;

            // nothing reported yet: the design-time builds are still running
            var toolsets = _database.GetToolsets();
            if (toolsets.Count == 0)
                return;

            var usable = toolsets.FirstOrDefault(i => i.HasClangd);
            if (usable is not null)
            {
                ClangdTrace.Write($"clangd is {usable.ClangdPath}, as resolved for {usable.Context.Label}");
                await _infoBar.CloseAsync();
                _clangdReady.TrySetResult(usable.ClangdPath);
                return;
            }

            await _infoBar.ShowAsync(Explain(toolsets), toolsets[0].Context.ProjectPath);
        }
        catch (Exception e)
        {
            ClangdTrace.Write($"could not start clangd: {e}");
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <summary>
    /// Says why no project can provide clangd, and how to correct it.
    /// </summary>
    static string Explain(IReadOnlyList<ClangToolset> toolsets)
    {
        const string Off = "Code completion, navigation and diagnostics for Clang projects are off because clangd is not available. ";

        var reported = toolsets.FirstOrDefault(i => i.IsReported);
        if (reported is null)
        {
            var project = toolsets.Where(i => StringComparer.OrdinalIgnoreCase.Equals(i.Context.ProjectPath, toolsets[0].Context.ProjectPath)).ToList();
            var name = Path.GetFileNameWithoutExtension(toolsets[0].Context.ProjectPath);
            return Off + $"{name}: {ClangToolset.ExplainNotReported(project)}";
        }

        var problem = reported.Problems.FirstOrDefault(i => i.Tool == "Clangd");
        if (problem is not null)
            return Off + problem.Message;

        // the SDK found it, but it is gone since
        return Off + $"clangd was expected at '{reported.ClangdPath}', which no longer exists. Reload the project to find the LLVM tools again, or set ClangdPath to the full path of clangd.";
    }

    /// <inheritdoc />
    public async Task<Connection?> ActivateAsync(CancellationToken token)
    {
        while (true)
        {
            // completed from the thread pool, with asynchronous continuations, so waiting on it cannot deadlock
#pragma warning disable VSTHRD003
            var connection = await TryActivateAsync(await _clangdReady.Task.WithCancellation(token), token);
#pragma warning restore VSTHRD003
            if (connection is not null)
                return connection;
        }
    }

    /// <summary>
    /// Starts the given clangd and connects it, or returns <see langword="null"/>, ready to wait for another, if
    /// it cannot be started.
    /// </summary>
    async Task<Connection?> TryActivateAsync(string clangd, CancellationToken token)
    {
        Process process;
        try
        {
            process = Process.Start(new ProcessStartInfo(clangd)
            {
                Arguments = "--background-index --log=error",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }) ?? throw new InvalidOperationException("The process did not start.");
        }
        catch (Exception e)
        {
            // let a later design-time build try again, possibly with another clangd
            ClangdTrace.Write($"could not start {clangd}: {e}");
            await _sync.WaitAsync(token);
            _clangdReady = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _sync.Release();

            await _infoBar.ShowAsync($"Code completion, navigation and diagnostics for Clang projects are off because clangd could not be started from '{clangd}': {e.Message} Set ClangdPath or LlvmToolsPath in the project to a working LLVM installation.", null);
            return null;
        }

        ClangdTrace.Write($"started {clangd} (process {process.Id})");
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) ClangdTrace.Write("clangd stderr: " + e.Data); };
        process.BeginErrorReadLine();

        // Visual Studio talks to one end of a pipe pair, the proxy to the other
        var toProxy = new AnonymousPipeServerStream(PipeDirection.Out);
        var fromClient = new AnonymousPipeClientStream(PipeDirection.In, toProxy.ClientSafePipeHandle);
        var toClient = new AnonymousPipeServerStream(PipeDirection.Out);
        var fromProxy = new AnonymousPipeClientStream(PipeDirection.In, toClient.ClientSafePipeHandle);

        var cancellation = new CancellationTokenSource();
        var proxy = new ClangdProxy(new DuplexStream(fromClient, toClient), process.StandardInput.BaseStream, process.StandardOutput.BaseStream, _database, ClangdTrace.Write, ClangdTrace.IsEnabled ? ClangdTrace.Write : null);
        _ = Task.Run(async () =>
        {
            try
            {
                await proxy.RunAsync(cancellation.Token);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException)
            {
                // either side went away
            }
            catch (Exception e)
            {
                ClangdTrace.Write($"proxy failed: {e}");
            }
            finally
            {
                toClient.Dispose();
            }
        });

        _process = process;
        _cancellation = cancellation;
        return new Connection(fromProxy, toProxy);
    }

    /// <inheritdoc />
    public Task OnServerInitializedAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<InitializationFailureContext?> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
    {
        Shutdown();
        return Task.FromResult<InitializationFailureContext?>(new InitializationFailureContext()
        {
            FailureMessage = $"clangd failed to start: {initializationState.StatusMessage}",
        });
    }

    void Shutdown()
    {
        _cancellation?.Cancel();

        try
        {
            if (_process is { HasExited: false })
                _process.Kill();
        }
        catch (InvalidOperationException)
        {
            // already gone
        }

        _process = null;
        _cancellation = null;
    }

}
