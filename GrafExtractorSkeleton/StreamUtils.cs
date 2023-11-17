using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GMWare.IO
{
    // I can't be bothered to pull out the original source, so here's a decompiled version
    public class StreamUtils
    {
        public static bool StreamCopyWithLength(Stream src, Stream dest, int length)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dest == null) throw new ArgumentNullException(nameof(dest));

            byte[] buffer = new byte[4096];
            int byteRemaining = length;
            int bytesRead;

            if (byteRemaining / buffer.Length != 0)
            {
                while ((bytesRead = src.Read(buffer, 0, buffer.Length)) > 0)
                {
                    dest.Write(buffer, 0, bytesRead);
                    byteRemaining -= bytesRead;
                    if (byteRemaining / buffer.Length == 0) break;
                }
            }

            if (src.CanSeek && src.Position == src.Length && byteRemaining != 0)
            {
                throw new EndOfStreamException();
            }

            while ((bytesRead = src.Read(buffer, 0, byteRemaining)) > 0)
            {
                dest.Write(buffer, 0, bytesRead);
                byteRemaining -= bytesRead;
            }

            return byteRemaining == 0;
        }
    }
}
