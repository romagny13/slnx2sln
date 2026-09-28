using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace slnx2sln
{
    public class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return 1;
            }

            // Optional: "sync" as first argument
            bool isSync = string.Equals(args[0], "sync", StringComparison.OrdinalIgnoreCase);
            string[] fileArgs = isSync ? args[1..] : args;

            if (fileArgs.Length < 1)
            {
                PrintUsage();
                return 1;
            }

            try
            {
                if (isSync)
                {
                    string path1 = fileArgs[0];
                    string? path2 = fileArgs.Length > 1 ? fileArgs[1] : null;
                    return await SyncAsync(path1, path2);
                }

                string inputPath = Path.GetFullPath(fileArgs[0]);
                string? outputPath = fileArgs.Length > 1 ? Path.GetFullPath(fileArgs[1]) : null;

                if (!File.Exists(inputPath))
                {
                    Console.Error.WriteLine($"File not found: {inputPath}");
                    return 1;
                }

                string extension = GetExtension(inputPath);

                if (extension == ".slnx")
                    return await ConvertSlnxToSlnAsync(inputPath, outputPath);

                if (extension == ".sln")
                    return await ConvertSlnToSlnxAsync(inputPath, outputPath);

                Console.Error.WriteLine($"Unsupported file extension: {extension}");
                PrintUsage();
                return 1;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"File locked or inaccessible: {ex.Message}");
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Conversion failed: {ex.Message}");
                return 1;
            }
        }

        // -------------------------------------------------------------------------
        // Path helpers (pure / easy to unit test)
        // -------------------------------------------------------------------------

        public static string GetExtension(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant();
        }

        /// <summary>
        /// Resolves the pair (slnPath, slnxPath) from one or two user paths.
        /// </summary>
        public static bool TryResolveSolutionPair(
            string path1,
            string? path2,
            out string slnPath,
            out string slnxPath,
            out string? error)
        {
            slnPath = string.Empty;
            slnxPath = string.Empty;
            error = null;

            path1 = Path.GetFullPath(path1);
            string ext1 = GetExtension(path1);

            if (path2 != null)
            {
                path2 = Path.GetFullPath(path2);
                string ext2 = GetExtension(path2);

                if (ext1 == ".sln" && ext2 == ".slnx")
                {
                    slnPath = path1;
                    slnxPath = path2;
                    return true;
                }

                if (ext1 == ".slnx" && ext2 == ".sln")
                {
                    slnxPath = path1;
                    slnPath = path2;
                    return true;
                }

                error = "When two paths are provided, one must be .sln and the other .slnx.";
                return false;
            }

            if (ext1 == ".sln")
            {
                slnPath = path1;
                slnxPath = Path.ChangeExtension(path1, ".slnx");
                return true;
            }

            if (ext1 == ".slnx")
            {
                slnxPath = path1;
                slnPath = Path.ChangeExtension(path1, ".sln");
                return true;
            }

            error = $"Unsupported file extension: {ext1}";
            return false;
        }

        /// <summary>
        /// Decides which file is the source (newer or only existing) and which is the target.
        /// Returns false if neither file exists.
        /// </summary>
        public static bool TryResolveSyncDirection(
            string slnPath,
            string slnxPath,
            out string sourcePath,
            out string targetPath,
            out string? error)
        {
            sourcePath = string.Empty;
            targetPath = string.Empty;
            error = null;

            bool slnExists = File.Exists(slnPath);
            bool slnxExists = File.Exists(slnxPath);

            if (!slnExists && !slnxExists)
            {
                error = "Neither file exists.";
                return false;
            }

            if (slnExists && !slnxExists)
            {
                sourcePath = slnPath;
                targetPath = slnxPath;
                return true;
            }

            if (!slnExists && slnxExists)
            {
                sourcePath = slnxPath;
                targetPath = slnPath;
                return true;
            }

            // Both exist → newer wins
            DateTime slnTime = File.GetLastWriteTimeUtc(slnPath);
            DateTime slnxTime = File.GetLastWriteTimeUtc(slnxPath);

            if (slnxTime >= slnTime)
            {
                sourcePath = slnxPath;
                targetPath = slnPath;
            }
            else
            {
                sourcePath = slnPath;
                targetPath = slnxPath;
            }

            return true;
        }

        // -------------------------------------------------------------------------
        // Conversion
        // -------------------------------------------------------------------------

        public static async Task<int> ConvertSlnxToSlnAsync(string slnxPath, string? outputPath = null)
        {
            string slnPath = outputPath ?? Path.ChangeExtension(slnxPath, ".sln");

            SolutionModel solution = await SolutionSerializers.SlnXml.OpenAsync(slnxPath, CancellationToken.None);
            await SolutionSerializers.SlnFileV12.SaveAsync(slnPath, solution, CancellationToken.None);

            Console.WriteLine($"OK -> {slnPath}");
            return 0;
        }

        public static async Task<int> ConvertSlnToSlnxAsync(string slnPath, string? outputPath = null)
        {
            string slnxPath = outputPath ?? Path.ChangeExtension(slnPath, ".slnx");

            SolutionModel solution = await SolutionSerializers.SlnFileV12.OpenAsync(slnPath, CancellationToken.None);
            await SolutionSerializers.SlnXml.SaveAsync(slnxPath, solution, CancellationToken.None);

            Console.WriteLine($"OK -> {slnxPath}");
            return 0;
        }

        // -------------------------------------------------------------------------
        // Auto sync
        // -------------------------------------------------------------------------

        public static async Task<int> SyncAsync(string path1, string? path2 = null)
        {
            if (!TryResolveSolutionPair(path1, path2, out string slnPath, out string slnxPath, out string? resolveError))
            {
                Console.Error.WriteLine(resolveError);
                return 1;
            }

            if (!TryResolveSyncDirection(slnPath, slnxPath, out string sourcePath, out string targetPath, out string? syncError))
            {
                Console.Error.WriteLine(syncError);
                return 1;
            }

            string sourceExt = GetExtension(sourcePath);

            if (sourceExt == ".slnx")
                return await ConvertSlnxToSlnAsync(sourcePath, targetPath);

            if (sourceExt == ".sln")
                return await ConvertSlnToSlnxAsync(sourcePath, targetPath);

            Console.Error.WriteLine($"Unsupported file extension: {sourceExt}");
            return 1;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  slnx2sln <file.slnx> [file.sln]     # Convert .slnx → .sln");
            Console.WriteLine("  slnx2sln <file.sln>  [file.slnx]    # Convert .sln  → .slnx");
            Console.WriteLine("  slnx2sln sync <file.sln|.slnx>      # Sync pair (newer wins)");
            Console.WriteLine("  slnx2sln sync <file.sln> <file.slnx>");
        }
    }
}