using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace GrafExtractorSkeleton // Remember to change the namespace for your own extractor
{
	/// <summary>
	/// Startup class for GRAF extractor
	/// </summary>
	class Program
	{
		/* To implement this class for GrafArch:
		 * 
		 * 1. Add variables for arguments
		 * 2. Add cases for additional options. Remember to check for duplicated options.
		 * 3. Change GrafArch's constructor to use those options, and modify instantiation code accordingly.
		 * 4. Implement the rest of GrafArch's code (see source for instructions).
		 * 5. Update the usage text (don't forget it, or users will get confused).
		 * 6. Make sure to fill out your assembly info, and remember to change "{{GRAF format name here}}" below.
		 */

		// Exit values:
		// 0 = normal
		// 1 = argument error
		// 2 = exception during processing

		/// <summary>
		/// The main entry point for the GRAF extractor
		/// This method is not meant to be called directly.
		/// </summary>
		/// <param name="args">An array of command line arguments</param>
		static void Main(string[] args)
		{
			// Write program name, version, and copyright to console
			Console.WriteLine("{0} {1}", getAssemblyTitle(), getAssemblyVersion());
			Console.WriteLine(getAssemblyCopyright());
			Console.WriteLine();

			// Some common variables
			string filePath = string.Empty;
			string outDir = string.Empty;
			List<string> paksToExtract = new List<string>();
			
			string platformReplacement = null;
			string textureReplacement = null;
			string regionReplacement = null;
			
			bool analyze = false;

			if (args.Length == 0) usage(); // Ran without arguments, print usage

			// Argument parser
			foreach (string arg in args)
			{
				// Each switch is in the format of "/option=value"

				if (arg.StartsWith("/")) // A switch
				{
					string[] argSplit = arg.Split(new char[] { '=' }, 2);
					switch (argSplit[0].ToLower()) // Check option. New: Now works with flag options (no "=value").
					{
						case "/d": // Common option: output path
							// Check if specified already. Such a check should be present for every option.
							if (!string.IsNullOrEmpty(outDir))
							{
								Console.WriteLine("Output directory path is specified more than once.");
								Console.WriteLine();
								usage();
							}
							else
								outDir = argSplit[1];
							break;
						case "/p":
							if (platformReplacement != null)
							{
								Console.WriteLine("[platform] text replacement is specified more than once.");
								Console.WriteLine();
								usage();
							}
							else
								platformReplacement = argSplit[1];
							break;
						case "/t":
							if (textureReplacement != null)
							{
								Console.WriteLine("[texture] text replacement is specified more than once.");
								Console.WriteLine();
								usage();
							}
							else
								textureReplacement = argSplit[1];
							break;
						case "/r":
							if (regionReplacement != null)
							{
								Console.WriteLine("[region] text replacement is specified more than once.");
								Console.WriteLine();
								usage();
							}
							else
								regionReplacement = argSplit[1];
							break;
						case "/k":
							string[] pathsSplit = argSplit[1].Split(',');
							paksToExtract.AddRange(pathsSplit);
							break;
						case "/a":
							if (analyze)
							{
								Console.WriteLine("Analyze is specified more than once.");
								Console.WriteLine();
								usage();
							}
							else if (argSplit.Length > 1)
							{
								Console.WriteLine("Analyze option does not take arguments.");
								Console.WriteLine();
								usage();
							}
							else
								analyze = true;
							break;
						default:
							Console.WriteLine("Unknown option {0}.", argSplit[0]);
							Console.WriteLine();
							usage();
							break;
					}
				}
				else
				{
					// Checks arguments that do not begin with '/'. Typically there's only the input file path.
					if (!string.IsNullOrEmpty(filePath))
					{
						Console.WriteLine("Archive path is specified more than once.");
						Console.WriteLine();
						usage();
					}
					else
						filePath = arg;
				}
			}

			// Input path must be specified
			if (string.IsNullOrEmpty(filePath))
			{
				Console.WriteLine("Archive path is not specified.");
				Console.WriteLine();
				usage();
			}

			// If no output directory is supplied, use input path plus "_extracted" appended
			if (string.IsNullOrEmpty(outDir)) outDir = filePath + "_extracted";

			// Begin extraction process
			GrafArch arch = null;
			// Load GRAF
			try
			{
				arch = new GrafArch(filePath);
				arch.ReadDirectory();
			}
			catch (Exception e)
			{
				Console.WriteLine("Error opening archive. Please make sure that you have read permission on the file, and that you are specifying the path for index.bin. ({0})", e.Message);
				// Debug workaround for SharpDevelop so I can see exceptions without getting rid of the catch block
				if (System.Diagnostics.Debugger.IsAttached)
				{
					Console.WriteLine(e.ToString());
					Console.ReadKey();
				}
				if (arch != null) arch.Dispose();
				Environment.Exit(2);
			}

			if (platformReplacement != null) arch.PlaceholderReplacements["platform"] = platformReplacement;
			if (textureReplacement != null) arch.PlaceholderReplacements["texture"] = textureReplacement;
			if (regionReplacement != null) arch.PlaceholderReplacements["region"] = regionReplacement;

			if (analyze)
			{
				Console.WriteLine("Performing analysis...");
				try
				{
					arch.AnalyzeP(outDir);
					Console.WriteLine("Analysis done.");
					return;
				}
				catch (Exception e)
				{
					Console.WriteLine("Error during analysis. ({0})", e.Message);
					Console.WriteLine(e);
					if (System.Diagnostics.Debugger.IsAttached)
					{
						Console.WriteLine(e.ToString());
						Console.ReadKey();
					}
					Environment.Exit(2);
				}
			}
			
			// Extract all files
			try
			{
				if (paksToExtract.Count == 0)
				{
					arch.ExtractAll(outDir);
				}
				else
				{
					foreach (string pak in paksToExtract)
					{
						Console.WriteLine("Extracting from archive {0}...", pak);
						if (!arch.ExtractAllFromArch(System.IO.Path.Combine(outDir, pak), pak))
						{
							Console.WriteLine("Archive \"{0}\" not found.", pak);
						}
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("Error while extracting files. ({0})", e.Message);
				if (System.Diagnostics.Debugger.IsAttached)
				{
					Console.WriteLine(e.ToString());
					Console.ReadKey();
				}
				Environment.Exit(2);
			}
			finally
			{
				arch.Dispose();
			}

			Console.WriteLine("Extraction complete.");
		}

		static void usage()
		{
			Console.WriteLine("{0} [/d=outputDir] [/k=pakName1,pakName2,...] [/p=platformReplacement] [/t=textureReplacement] [/r=regionReplacement] archPath", System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location));
			Console.WriteLine("\tarchPath\tPath to index.bin.");
			Console.WriteLine("\t/d=outputDir\tOutput directory path. By default it's the cache name plus \"_extracted\".");
			Console.WriteLine("\t/p=platformReplacement\tReplace instances of [platform] in file name with platformReplacement.");
			Console.WriteLine("\t/t=textureReplacement\tReplace instances of [texture] in file name with textureReplacement.");
			Console.WriteLine("\t/r=regionReplacement\tReplace instances of [region] in file name with regionReplacement.");
			Console.WriteLine("\t/k=pakName1,pakName2,...\tExtracts files from specified *.p files only. Separate pak names with a comma.");
			Environment.Exit(1);
		}

		static string getAssemblyTitle()
		{
			// http://stackoverflow.com/a/10203668
			var attribute = Assembly.GetExecutingAssembly()
				.GetCustomAttributes(typeof(AssemblyTitleAttribute), false)
				.Cast<AssemblyTitleAttribute>().FirstOrDefault();
			if (attribute != null)
			{
				return attribute.Title;
			}
			else
			{
				return "(Please add assembly title)";
			}
		}

		static string getAssemblyVersion()
		{
			return AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().Location).Version.ToString();
		}

		static string getAssemblyCopyright()
		{
			var attribute = Assembly.GetExecutingAssembly()
				.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)
				.Cast<AssemblyCopyrightAttribute>().FirstOrDefault();
			if (attribute != null)
			{
				return attribute.Copyright;
			}
			else
			{
				return string.Format("Copyright © {0}", DateTime.Today.Year);
			}
		}
	}
}
