using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace IKVM.Clang.Vsix.Clangd
{

    /// <summary>
    /// Sits between Visual Studio's language client and clangd, passing messages through while:
    /// <list type="bullet">
    /// <item>giving clangd the compile command of every file, from <see cref="ClangCompileDatabase"/>, once it is
    /// initialized and whenever a design-time build changes them;</item>
    /// <item>acting as the project context provider Visual Studio asks for the editor's context list (one context per
    /// project and target identifier), which clangd knows nothing about;</item>
    /// <item>switching the command clangd uses for a file to that of the context the user picked.</item>
    /// </list>
    /// </summary>
    internal sealed class ClangdProxy
    {

        const string GetProjectContextsMethod = "textDocument/_vs_getProjectContexts";

        /// <summary>
        /// <c>VSProjectKind.CPlusPlus</c>.
        /// </summary>
        const int CPlusPlusProjectKind = 1;

        readonly LspStream _client;
        readonly LspStream _server;
        readonly ClangCompileDatabase _database;
        readonly Action<string> _log;
        readonly Action<string>? _trace;

        /// <summary>
        /// Context the user picked for each file, by full path.
        /// </summary>
        readonly ConcurrentDictionary<string, string> _selected = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Command last given to clangd for each file, by full path, so unchanged commands are not sent again.
        /// </summary>
        readonly ConcurrentDictionary<string, string> _sent = new(StringComparer.OrdinalIgnoreCase);

        JToken? _initializeId;
        volatile bool _initialized;

        /// <summary>
        /// Creates the proxy between the client end of Visual Studio's connection and clangd's standard input and
        /// output.
        /// </summary>
        public ClangdProxy(Stream client, Stream serverInput, Stream serverOutput, ClangCompileDatabase database, Action<string>? log = null, Action<string>? trace = null)
        {
            _trace = trace;
            _client = new LspStream(client ?? throw new ArgumentNullException(nameof(client)));
            _server = new LspStream(new DuplexStream(serverOutput, serverInput));
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _log = log ?? (_ => { });
        }

        /// <summary>
        /// Relays messages until either side closes.
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            _database.Changed += OnDatabaseChanged;
            try
            {
                var clientToServer = RelayClientToServerAsync(cancellationToken);
                var serverToClient = RelayServerToClientAsync(cancellationToken);
                await Task.WhenAny(clientToServer, serverToClient);
            }
            finally
            {
                _database.Changed -= OnDatabaseChanged;
            }
        }

        async Task RelayClientToServerAsync(CancellationToken cancellationToken)
        {
            while (await _client.ReadAsync(cancellationToken) is JObject message)
            {
                Trace("VS -> proxy", message);
                var method = (string?)message["method"];

                if (method == "initialize")
                    _initializeId = message["id"];

                // clangd takes compile commands only once initialized, and not from the initialization options
                if (method == "initialized")
                {
                    await _server.WriteAsync(message, cancellationToken);
                    _initialized = true;
                    await SendCommandsAsync(_database.GetDefaultEntries().Select(i => i.File), cancellationToken);
                    continue;
                }

                if (method == GetProjectContextsMethod)
                {
                    var response = new JObject()
                    {
                        ["jsonrpc"] = "2.0",
                        ["id"] = message["id"],
                        ["result"] = GetProjectContexts(message["params"]?["_vs_textDocument"]?["uri"]),
                    };

                    Trace("proxy -> VS", response);
                    await _client.WriteAsync(response, cancellationToken);
                    continue;
                }

                if (method is not null && method.StartsWith("textDocument/", StringComparison.Ordinal))
                    await ApplyContextAsync(method, message["params"]?["textDocument"], cancellationToken);

                await _server.WriteAsync(message, cancellationToken);
            }
        }

        async Task RelayServerToClientAsync(CancellationToken cancellationToken)
        {
            while (await _server.ReadAsync(cancellationToken) is JObject message)
            {
                Trace("clangd -> VS", message);

                // clangd does not provide project contexts; this proxy does
                if (_initializeId is not null && message["method"] is null && JToken.DeepEquals(message["id"], _initializeId) && message["result"]?["capabilities"] is JObject capabilities)
                    capabilities["_vs_projectContextProvider"] = true;

                await _client.WriteAsync(message, cancellationToken);
            }
        }

        /// <summary>
        /// Before a document request reaches clangd, makes sure clangd has the command of the document's selected
        /// context: the one named in the request, else the one picked earlier, else the default.
        /// </summary>
        async Task ApplyContextAsync(string method, JToken? textDocument, CancellationToken cancellationToken)
        {
            var path = ToPath((string?)textDocument?["uri"]);
            if (path is null)
                return;

            var contextId = (string?)textDocument?["_vs_projectContext"]?["_vs_id"];
            if (contextId is not null)
                _selected[path] = contextId;

            if (contextId is not null || method == "textDocument/didOpen")
                await SendCommandsAsync(new[] { path }, cancellationToken);
        }

        /// <summary>
        /// Answers Visual Studio's request for the contexts of a document.
        /// </summary>
        JObject GetProjectContexts(JToken? uri)
        {
            var path = ToPath((string?)uri);
            var contexts = path is null ? new List<ClangCompileContext>() : GetCandidates(path).Select(i => i.Context).ToList();

            var selected = path is not null && _selected.TryGetValue(path, out var id) ? contexts.FindIndex(i => i.Id == id) : -1;

            return new JObject()
            {
                ["_vs_projectContexts"] = new JArray(contexts.Select(i => new JObject()
                {
                    ["_vs_label"] = i.Label,
                    ["_vs_id"] = i.Id,
                    ["_vs_kind"] = CPlusPlusProjectKind,
                })),
                ["_vs_defaultIndex"] = Math.Max(selected, 0),
            };
        }

        /// <summary>
        /// Gets the commands a file can be compiled with, one per context, default first. A file that is not itself
        /// compiled, such as a header, borrows the commands of the closest source file in each project.
        /// </summary>
        IReadOnlyList<ClangCompileCommandEntry> GetCandidates(string path)
        {
            var entries = _database.GetEntries(path);
            if (entries.Count > 0)
                return entries;

            return _database.GetNearestEntries(path)
                .GroupBy(i => i.Context)
                .Select(i => i.First())
                .Select(i => HeaderCommand.Derive(i, path))
                .ToList();
        }

        /// <summary>
        /// Sends clangd the command of each file in its selected context, skipping those it already has.
        /// </summary>
        async Task SendCommandsAsync(IEnumerable<string> paths, CancellationToken cancellationToken)
        {
            if (_initialized == false)
                return;

            var changes = new JObject();

            foreach (var path in paths)
            {
                var candidates = GetCandidates(path);
                if (candidates.Count == 0)
                    continue;

                var entry = _selected.TryGetValue(path, out var id) ? candidates.FirstOrDefault(i => i.Context.Id == id) ?? candidates[0] : candidates[0];
                var signature = Signature(entry.WorkingDirectory, entry.Arguments);
                if (_sent.TryGetValue(path, out var previous) && previous == signature)
                    continue;

                _sent[path] = signature;
                changes[path] = ToCommand(entry.WorkingDirectory, entry.Arguments);
            }

            if (changes.Count == 0)
                return;

            var notification = new JObject()
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "workspace/didChangeConfiguration",
                ["params"] = new JObject() { ["settings"] = new JObject() { ["compilationDatabaseChanges"] = changes } },
            };

            Trace("proxy -> clangd", notification);
            await _server.WriteAsync(notification, cancellationToken);
        }

        void OnDatabaseChanged(object sender, ClangCompileDatabaseChangedEventArgs args)
        {
            // headers borrow commands from source files, so any change may affect the open ones too
            var paths = args.Files.Concat(_sent.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            _ = Task.Run(async () =>
            {
                try
                {
                    await SendCommandsAsync(paths, CancellationToken.None);
                }
                catch (Exception e)
                {
                    _log($"Failed to send compile commands to clangd: {e}");
                }
            });
        }

        void Trace(string direction, JObject message)
        {
            if (_trace is not null)
                _trace($"{direction}: {message.ToString(Newtonsoft.Json.Formatting.None)}");
        }

        static JObject ToCommand(string workingDirectory, IReadOnlyList<string> arguments)
        {
            return new JObject()
            {
                ["workingDirectory"] = workingDirectory,
                ["compilationCommand"] = new JArray(arguments),
            };
        }

        static string Signature(string workingDirectory, IReadOnlyList<string> arguments)
        {
            return workingDirectory + "\0" + string.Join("\0", arguments);
        }

        static string? ToPath(string? uri)
        {
            if (uri is null || Uri.TryCreate(uri, UriKind.Absolute, out var u) == false || u.IsFile == false)
                return null;

            return Path.GetFullPath(u.LocalPath);
        }

    }

}
