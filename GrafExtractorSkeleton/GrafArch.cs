using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using GMWare.IO;
using System.Xml;
using System.IO.Compression;

namespace GrafExtractorSkeleton
{
	/// <summary>
	/// Managed representation of a GRAF archive
	/// </summary>
	public sealed class GrafArch : IDisposable
	{
		/* To-do:
		 * 
		 * - Add overwrite confirmation to ExtractAll().
		 */

		// IO objects
		Stream grafStream;
		BinaryReader grafReader; // BinaryReader is usually little endian. To read big endian, try this: http://bytes.com/topic/c-sharp/answers/454822-binarywriter-reader-big-endian#post1740375
		XmlReader xmlReader;
		//BinaryWriter grafWriter; // For GRAF writing support

		// GRAF structural objects
		string dataPath;
		List<FileEntry> files = new List<FileEntry>();
		List<string> pakPaths = new List<string>();
		Dictionary<int, Stream> pakStreams = new Dictionary<int, Stream>();
		public Dictionary<string, string> PlaceholderReplacements {get; set;}
		bool disposed = false;

		// Properties
		/// <summary>
		/// Gets an enumerable collection of file entries.
		/// </summary>
		public IEnumerable<FileEntry> Files
		{
			get
			{
				if (disposed) throw new ObjectDisposedException(GetType().FullName);
				return files; // External code should not be modifying the files list directly, so return as IEnumerable
			}
		}

		/// <summary>
		/// Instantiate a new GRAF representation.
		/// </summary>
		/// <param name="archPath">Path to GRAF archive.</param>
		public GrafArch(string archPath)
		{
			if (string.IsNullOrEmpty(archPath)) throw new ArgumentNullException("archPath");
			PlaceholderReplacements = new Dictionary<string, string>();
			Console.WriteLine("Opening {0}", archPath);
			grafStream = File.OpenRead(archPath); // Change to "File.Open(archPath, FileMode.OpenOrCreate, FileAccess.ReadWrite)" if you need writing
			grafReader = new BinaryReader(grafStream, Encoding.ASCII); // Remember to change encoding if the GRAF happens to not be using ASCII for file names
			dataPath = Path.GetDirectoryName(archPath);
			string packagerPath = Path.Combine(dataPath, "pmindex.xml");
			Console.WriteLine("Opening packager index {0}", packagerPath);
			try
			{
				xmlReader = XmlReader.Create(packagerPath, new XmlReaderSettings() { IgnoreComments = true, IgnoreWhitespace = true });
			}
			catch
			{
				Console.WriteLine("Can't open packager index. File names and directory structure will not be available.");
			}
			//grafWriter = new BinaryWriter(grafStream, Encoding.ASCII);
		}

		/// <summary>
		/// Load existing files from GRAF.
		/// </summary>
		public void ReadDirectory()
		{
			if (disposed) throw new ObjectDisposedException(GetType().FullName);

			Console.WriteLine("Reading file table...");

			// Reset stream position; assuming seekable
			grafStream.Seek(0, SeekOrigin.Begin);
			files.Clear();

			// Read some header stuff
			int fileCount = grafReader.ReadInt32();

			// Read all file entries
			//Console.WriteLine("Index\tArchive\tFlags\tOffset\tCompressed Length\tLength"); // I usually like to output file entries as they're read. For debug only.
			for (int i = 0; i < fileCount; ++i)
			{
				FileEntry entry = new FileEntry();
				entry.ArchiveIndex = grafReader.ReadByte();
				entry.CompressionLevel = grafReader.ReadByte();
				entry.Offset = grafReader.ReadInt32();
				entry.CompressedLength = grafReader.ReadInt32();
				entry.Length = grafReader.ReadInt32();
				files.Add(entry);
				//Console.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", i, entry.ArchiveIndex, entry.CompressionLevel, entry.Offset, entry.CompressedLength, entry.Length); // Debug
			}
			Console.WriteLine("Done reading file table.");
			
			bool foundNames = true;
			
			if (xmlReader != null)
			{
				Console.WriteLine("Loading file names...");
				if (!xmlReader.ReadToFollowing("packager"))
				{
					foundNames = false;
					Console.WriteLine("Cannot get names from packager index. File names and directory structure will not be available.");
				}
				
				while (xmlReader.Read())
				{
					if (xmlReader.Name != "file") break;
					FileEntry entry = files[int.Parse(xmlReader.GetAttribute("index"))];
					entry.FileName = xmlReader.GetAttribute("name");
				}
			}
			else
			{
				foundNames = false;
			}
			
			if (!foundNames)
			{
				for (int i = 0; i < files.Count; ++i)
				{
					files[i].FileName = i + ".bin";
				}
			}
			Console.WriteLine("Done loading file names.");
			
			Console.WriteLine("Opening data archives...");
			int archNameCount = grafReader.ReadInt32();
			for (int i = 0; i < archNameCount; ++i)
			{
				string pakName = grafReader.ReadString();
				pakPaths.Add(pakName);
				if (pakStreams.ContainsKey(i)) continue; // Don't reopen files if reloading directory
				string pakPath = Path.Combine(dataPath, pakName);
				Console.WriteLine("Opening {0}", pakPath);
				pakStreams[i] = File.Open(pakPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
			}
			Console.WriteLine("Data archives opened.");
		}

		/// <summary>
		/// Extracts a file to disk.
		/// </summary>
		/// <param name="entry">Entry of file to extract</param>
		/// <param name="extractPath">Path to the extracted file</param>
		public void ExtractFile(FileEntry entry, string extractPath)
		{
			if (disposed) throw new ObjectDisposedException(GetType().FullName);
			if (entry == null) throw new ArgumentNullException("entry");
			if (string.IsNullOrEmpty(extractPath)) throw new ArgumentNullException("extractPath");

			Directory.CreateDirectory(Path.GetDirectoryName(extractPath)); // FileStream() will complain if the file's directory doesn't exist
			using (FileStream stream = File.Create(extractPath))
			{
				ExtractFile(entry, stream);
			}
		}

		/// <summary>
		/// Extracts a file to a stream.
		/// </summary>
		/// <param name="entry">Entry of file to extract</param>
		/// <param name="extractStream">Stream to write the file to</param>
		public void ExtractFile(FileEntry entry, Stream extractStream)
		{
			if (disposed) throw new ObjectDisposedException(GetType().FullName);
			if (entry == null) throw new ArgumentNullException("entry");
			if (extractStream == null) throw new ArgumentNullException("extractStream");

			if (!files.Contains(entry)) throw new ArgumentException("File entry is not in this archive.", "entry");
			if (entry.ArchiveIndex == 255) throw new InvalidOperationException("File does not have an associated data archive.");

			Stream targetArch = pakStreams[entry.ArchiveIndex];
			targetArch.Seek(entry.Offset, SeekOrigin.Begin);
			if (entry.CompressionLevel > 0) targetArch = new ZLibStream(targetArch, CompressionMode.Decompress, true); // I'm lazy. Assign deflate stream to target so I only have to write copy statement once.
			StreamUtils.StreamCopyWithLength(targetArch, extractStream, entry.Length); // Copy data to file stream
			if (entry.CompressionLevel > 0) targetArch.Close();
			extractStream.Flush();
		}

		/// <summary>
		/// Extracts all files to a directory.
		/// </summary>
		/// <param name="outputDir">Directory to extract file to</param>
		public void ExtractAll(string outputDir)
		{
			if (disposed) throw new ObjectDisposedException(GetType().FullName);
			if (string.IsNullOrEmpty(outputDir)) throw new ArgumentNullException("outputDir");

			foreach (FileEntry entry in files)
			{
				// Try to extract as many files as possible
				try
				{
					if (entry.ArchiveIndex == 255) continue; // File does not actually exist
					Console.WriteLine("Extracting {0}", entry.FileName);
					
					string replacedFileName = entry.FileName;
					foreach (KeyValuePair<string, string> pair in PlaceholderReplacements)
					{
						replacedFileName = replacedFileName.Replace("[" + pair.Key + "]", pair.Value);
					}

					string outPath = Path.Combine(outputDir, replacedFileName);
					ExtractFile(entry, outPath);
				}
				catch (Exception e)
				{
					Console.WriteLine("Error extracting {0} ({1})", entry.FileName, e.Message);
				}
			}
		}

		/// <summary>
		/// Extracts all files from specified *.p file to a directory.
		/// </summary>
		/// <param name="outputDir">Directory to extract file to</param>
		/// <param name="archName">*.p file to extract from</param>
		/// <returns></returns>
		public bool ExtractAllFromArch(string outputDir, string archName)
		{
			if (disposed) throw new ObjectDisposedException(GetType().FullName);
			
			if (string.IsNullOrEmpty(outputDir)) throw new ArgumentNullException("outputDir");
			if (string.IsNullOrEmpty(archName)) throw new ArgumentNullException("archName");
			
			archName = Path.GetFileName(archName);
			int archIndex = pakPaths.FindIndex((string s) => { return string.Equals(s, archName, StringComparison.OrdinalIgnoreCase); });
			if (archIndex == -1) return false;
			foreach (FileEntry entry in files)
			{
				// Try to extract as many files as possible
				try
				{
					if (entry.ArchiveIndex != archIndex) continue; // File does not actually exist
					Console.WriteLine("Extracting {0}", entry.FileName);
					
					string replacedFileName = entry.FileName;
					foreach (KeyValuePair<string, string> pair in PlaceholderReplacements)
					{
						replacedFileName = replacedFileName.Replace("[" + pair.Key + "]", pair.Value);
					}

					string outPath = Path.Combine(outputDir, replacedFileName);
					ExtractFile(entry, outPath);
				}
				catch (Exception e)
				{
					Console.WriteLine("Error extracting {0} ({1})", entry.FileName, e.Message);
				}
			}
			return true;
		}
		
		/// <summary>
		/// Get name of pack file from an entry's archive index.
		/// </summary>
		/// <param name="index">Archive index number from file entry</param>
		/// <returns>File name of .p file associated with the index</returns>
		public string GetPackNameFromIndex(int index)
		{
			if (disposed) throw new ObjectDisposedException(GetType().FullName);

			if (index == 255) return null; // Special case for index 0xFF
			if (index < 0 || index >= pakPaths.Count) throw new ArgumentOutOfRangeException("index");
			return pakPaths[index];
		}
		
		public void AnalyzeP(string logDir)
		{
			if (disposed) throw new ObjectDisposedException(GetType().FullName);
			
			Directory.CreateDirectory(logDir);
			StreamWriter summary = File.CreateText(Path.Combine(logDir, "summary.txt"));
			for (int i = 0; i < pakPaths.Count; ++i)
			{
				summary.WriteLine(pakPaths[i] + ":");
				BinaryReader br = new BinaryReader(pakStreams[i]);
				br.BaseStream.Seek(0, SeekOrigin.Begin);
				summary.WriteLine("pak index: {0}", i);
				summary.WriteLine("length (from pak): {0}", br.ReadInt32());
				summary.WriteLine("resident: {0}", br.ReadInt32());
				
				int compressedCount = 0;
				int uncompressedCount = 0;
				
				StreamWriter sw = File.CreateText(Path.Combine(logDir, pakPaths[i] + ".csv"));
				sw.WriteLine("index,name,complevel,offset,length,zlength,delta-length,postgap,z-header");
				List<FileEntry> pakFiles = files.FindAll((FileEntry fe) => { return fe.ArchiveIndex == i; });
				pakFiles.Sort((FileEntry x, FileEntry y) => { return x.Offset.CompareTo(y.Offset); });
				sw.WriteLine("-1,pregap,n/a,0,{0},n/a,n/a,n/a,n/a", pakFiles[0].Offset);
				for (int j = 0; j < pakFiles.Count; ++j)
				{
					FileEntry curr = pakFiles[j];
					long postgap;
					if (j != pakFiles.Count - 1)
					{
						FileEntry next = pakFiles[j+1];
						postgap = next.Offset - curr.Offset - curr.CompressedLength;
					}
					else
					{
						postgap = pakStreams[i].Length - curr.Offset - curr.CompressedLength;
					}
					string zheader;
					if (curr.CompressionLevel > 0)
					{
						++compressedCount;
						pakStreams[i].Seek(curr.Offset, SeekOrigin.Begin);
						byte a = (byte)pakStreams[i].ReadByte();
						byte b = (byte)pakStreams[i].ReadByte();
						ushort header = (ushort)((a << 8) | b);
						zheader = header.ToString("X2");
					}
					else
					{
						++uncompressedCount;
						zheader = "n/a";
					}
					sw.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
					             files.IndexOf(curr),
					             curr.FileName,
					             curr.CompressionLevel,
					             curr.Offset,
					             curr.Length,
					             curr.CompressedLength,
					             curr.CompressionLevel == 0 ? (curr.CompressedLength - curr.Length).ToString() : "n/a",
					             postgap,
					             zheader
					            );
				}
				sw.Flush();
				sw.Close();
				
				summary.WriteLine("count: {0}", pakFiles.Count);
				summary.WriteLine("compressed: {0}", compressedCount);
				summary.WriteLine("uncompressed: {0}", uncompressedCount);
				summary.WriteLine();
			}
			summary.Flush();
			summary.Close();
		}
		
		#region IDisposable Members

		public void Dispose()
		{
			disposed = true;
			foreach (KeyValuePair<int, Stream> pair in pakStreams)
			{
				pair.Value.Close();
			}
			xmlReader.Close();
			grafStream.Close();
		}

		#endregion
	}
}
