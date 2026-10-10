using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IKVM.Clang.Vsix.Clangd;

/// <summary>
/// Reads and writes language server protocol messages: a <c>Content-Length</c> header, a blank line, and a JSON
/// body.
/// </summary>
internal sealed class LspStream
{

    readonly Stream _stream;
    readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Creates the reader and writer over the given stream.
    /// </summary>
    public LspStream(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    /// <summary>
    /// Reads the next message, or returns <see langword="null"/> at the end of the stream.
    /// </summary>
    public async Task<JObject?> ReadAsync(CancellationToken cancellationToken)
    {
        var length = -1;

        while (true)
        {
            var line = await ReadHeaderLineAsync(cancellationToken);
            if (line is null)
                return null;

            if (line.Length == 0)
                break;

            var colon = line.IndexOf(':');
            if (colon > 0 && string.Equals(line.Substring(0, colon).Trim(), "Content-Length", StringComparison.OrdinalIgnoreCase))
                length = int.Parse(line.Substring(colon + 1).Trim());
        }

        if (length < 0)
            throw new InvalidDataException("Language server message without a Content-Length header.");

        var body = new byte[length];
        for (int read = 0; read < length;)
        {
            var n = await _stream.ReadAsync(body, read, length - read, cancellationToken);
            if (n == 0)
                return null;

            read += n;
        }

        using var reader = new JsonTextReader(new StreamReader(new MemoryStream(body), Encoding.UTF8)) { DateParseHandling = DateParseHandling.None };
        return JObject.Load(reader);
    }

    /// <summary>
    /// Reads one header line, without its line ending, or returns <see langword="null"/> at the end of the stream.
    /// </summary>
    async Task<string?> ReadHeaderLineAsync(CancellationToken cancellationToken)
    {
        var line = new StringBuilder();
        var buffer = new byte[1];

        while (true)
        {
            if (await _stream.ReadAsync(buffer, 0, 1, cancellationToken) == 0)
                return null;

            var c = (char)buffer[0];
            if (c == '\n')
                return line.ToString().TrimEnd('\r');

            line.Append(c);
        }
    }

    /// <summary>
    /// Writes a message. Safe to call from several threads.
    /// </summary>
    public async Task WriteAsync(JObject message, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(message.ToString(Formatting.None));
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _stream.WriteAsync(header, 0, header.Length, cancellationToken);
            await _stream.WriteAsync(body, 0, body.Length, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

}
