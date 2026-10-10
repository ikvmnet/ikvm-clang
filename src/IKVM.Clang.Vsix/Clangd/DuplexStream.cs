using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IKVM.Clang.Vsix.Clangd;

/// <summary>
/// Presents a stream to read from and a stream to write to, such as the two ends of a process's standard I/O, as
/// one stream.
/// </summary>
internal sealed class DuplexStream : Stream
{

    readonly Stream _read;
    readonly Stream _write;

    /// <summary>
    /// Creates the stream.
    /// </summary>
    public DuplexStream(Stream read, Stream write)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = write ?? throw new ArgumentNullException(nameof(write));
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => _write.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _write.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => _read.Read(buffer, offset, count);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _read.ReadAsync(buffer, offset, count, cancellationToken);
    public override void Write(byte[] buffer, int offset, int count) => _write.Write(buffer, offset, count);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _write.WriteAsync(buffer, offset, count, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _read.Dispose();
            _write.Dispose();
        }

        base.Dispose(disposing);
    }

}
