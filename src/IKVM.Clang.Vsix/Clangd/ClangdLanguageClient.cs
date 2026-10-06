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

namespace IKVM.Clang.Vsix.Clangd
{

    /// <summary>
    /// Runs clangd for the source files of Clang projects. It applies to the Clang content types, which only files
    /// opened from Clang projects have (see <see cref="ClangEditorFactory"/>), so other C and C++ files are left to
    /// Visual Studio's own language service.
    /// </summary>
    [Export(typeof(ILanguageClient))]
    [ContentType(ContentTypeNames.CCode)]
    [ContentType(ContentTypeNames.CppCode)]
    [ContentType(ContentTypeNames.ObjCCode)]
    [ContentType(ContentTypeNames.ObjCppCode)]
    [ContentType(ContentTypeNames.CppHeader)]
    internal sealed class ClangdLanguageClient : ILanguageClient
    {

        readonly ClangCompileDatabase _database;
        Process? _process;
        CancellationTokenSource? _cancellation;

        /// <summary>
        /// Creates the client.
        /// </summary>
        [ImportingConstructor]
        public ClangdLanguageClient(ClangCompileDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
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
            if (StartAsync is not null)
                await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public Task<Connection?> ActivateAsync(CancellationToken token)
        {
            var clangd = ClangdLocator.Find();
            if (clangd is null)
                throw new FileNotFoundException(
                    $"clangd was not found. Install LLVM, add clangd to PATH, or set {ClangdLocator.PathVariable} to the path of clangd.exe. Looked at: " +
                    string.Join(", ", ClangdLocator.GetCandidates().Distinct(StringComparer.OrdinalIgnoreCase)));

            var process = Process.Start(new ProcessStartInfo(clangd)
            {
                Arguments = "--background-index --log=error",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }) ?? throw new InvalidOperationException($"Could not start {clangd}.");

            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Trace.WriteLine(e.Data, "clangd"); };
            process.BeginErrorReadLine();

            // Visual Studio talks to one end of a pipe pair, the proxy to the other
            var toProxy = new AnonymousPipeServerStream(PipeDirection.Out);
            var fromClient = new AnonymousPipeClientStream(PipeDirection.In, toProxy.ClientSafePipeHandle);
            var toClient = new AnonymousPipeServerStream(PipeDirection.Out);
            var fromProxy = new AnonymousPipeClientStream(PipeDirection.In, toClient.ClientSafePipeHandle);

            var cancellation = new CancellationTokenSource();
            var proxy = new ClangdProxy(new DuplexStream(fromClient, toClient), process.StandardInput.BaseStream, process.StandardOutput.BaseStream, _database, m => Trace.WriteLine(m, "clangd"));
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
                    Trace.WriteLine($"clangd proxy failed: {e}", "clangd");
                }
                finally
                {
                    toClient.Dispose();
                }
            });

            _process = process;
            _cancellation = cancellation;
            return Task.FromResult<Connection?>(new Connection(fromProxy, toProxy));
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

}
