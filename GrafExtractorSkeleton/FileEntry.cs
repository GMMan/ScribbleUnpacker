using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GrafExtractorSkeleton
{
    /// <summary>
    /// An entry representing the file in the GRAF.
    /// </summary>
    public sealed class FileEntry
    {
        // Use properties instead of bare fields. It's a good practice.

        public string FileName { get; set; }
        public byte ArchiveIndex {get; set;}
        public byte CompressionLevel {get; set;}
        public int Offset { get; set; }
        public int Length { get; set; }
        public int CompressedLength {get; set;}
    }
}
