using System.IO;

namespace AduosSyncServices.ServicesManager.Helpers
{
    public static class LogFileReader
    {
        public static (List<string> lines, long startOffset, bool reachedStart)
            ReadLastLines(string path, int lineCount)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long fileLen = fs.Length;
            if (fileLen == 0) return (new List<string>(), 0, true);

            long pos = fileLen;
            int newlines = 0;
            var buffer = new byte[64 * 1024];

            while (pos > 0 && newlines <= lineCount)
            {
                int toRead = (int)Math.Min(buffer.Length, pos);
                pos -= toRead;
                fs.Position = pos;
                int read = fs.Read(buffer, 0, toRead);

                for (int i = read - 1; i >= 0; i--)
                {
                    if (buffer[i] == (byte)'\n')
                    {
                        newlines++;
                        if (newlines > lineCount)
                        {
                            long start = pos + i + 1;
                            return (ReadForwardLines(path, start, fileLen), start, false);
                        }
                    }
                }
            }

            return (ReadForwardLines(path, 0, fileLen), 0, true);
        }

        public static (List<string> lines, long newStart, bool reachedStart)
            ReadPreviousLines(string path, long currentStartOffset, int lineCount)
        {
            if (currentStartOffset <= 0)
                return (new List<string>(), 0, true);

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long pos = currentStartOffset;
            int newlines = 0;
            var buffer = new byte[64 * 1024];

            while (pos > 0 && newlines < lineCount)
            {
                int toRead = (int)Math.Min(buffer.Length, pos);
                pos -= toRead;
                fs.Position = pos;
                int read = fs.Read(buffer, 0, toRead);

                for (int i = read - 1; i >= 0; i--)
                {
                    if (buffer[i] == (byte)'\n')
                    {
                        newlines++;
                        if (newlines == lineCount)
                        {
                            long start = pos + i + 1;
                            var lines = ReadForwardLines(path, start, currentStartOffset);
                            return (lines, start, start == 0);
                        }
                    }
                }
            }

            var all = ReadForwardLines(path, 0, currentStartOffset);
            return (all, 0, true);
        }

        private static List<string> ReadForwardLines(string path, long startOffset, long endOffset)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Position = startOffset;
            long bytesToRead = endOffset - startOffset;

            using var limited = new LimitedStream(fs, bytesToRead);
            using var sr = new StreamReader(limited);

            var result = new List<string>();
            string? line;
            while ((line = sr.ReadLine()) != null)
                result.Add(line);
            return result;
        }

        private sealed class LimitedStream : Stream
        {
            private readonly Stream _base;
            private readonly long _limit;
            private long _read;

            public LimitedStream(Stream @base, long limit)
            { _base = @base; _limit = limit; }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _limit;
            public override long Position { get => _read; set => throw new NotSupportedException(); }

            public override void Flush() => _base.Flush();

            public override int Read(byte[] buffer, int offset, int count)
            {
                var remain = _limit - _read;
                if (remain <= 0) return 0;
                count = (int)Math.Min(count, remain);
                var n = _base.Read(buffer, offset, count);
                _read += n;
                return n;
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        public static List<string> ReadNewLines(string path, ref long lastReadOffset)
        {
            var result = new List<string>();

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (lastReadOffset > fs.Length)
                lastReadOffset = fs.Length; // file was truncated (log rotated)

            fs.Position = lastReadOffset;
            using var sr = new StreamReader(fs);

            string? line;
            while ((line = sr.ReadLine()) != null)
                result.Add(line);

            lastReadOffset = fs.Position;
            return result;
        }
    }
}