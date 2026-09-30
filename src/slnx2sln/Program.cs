using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace slnx2sln
{
    public class Program
    {
        static Task<int> Main(string[] args)
        {
            return RunAsync(args, Directory.GetCurrentDirectory());
        }

        /// <summary>
        /// Point d'entrée testable. <paramref name="workingDirectory"/> est utilisé
        /// par "sync" sans argument pour chercher la solution.
        /// </summary>
        public static async Task<int> RunAsync(string[] args, string workingDirectory = null)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return 1;
            }

            if (workingDirectory == null)
                workingDirectory = Directory.GetCurrentDirectory();

            bool isSync = string.Equals(args[0], "sync", StringComparison.OrdinalIgnoreCase);
            string[] fileArgs = isSync ? args.Skip(1).ToArray() : args;

            try
            {
                if (isSync)
                {
                    // "slnx2sln sync" : détection automatique dans le dossier courant
                    if (fileArgs.Length == 0)
                    {
                        if (!TryFindSolutionPairInDirectory(
                                workingDirectory,
                                out string autoSlnPath,
                                out string autoSlnxPath,
                                out string findError))
                        {
                            Console.Error.WriteLine(findError);
                            return 1;
                        }

                        return await SyncAsync(autoSlnPath, autoSlnxPath);
                    }

                    // "slnx2sln sync <file> [file]" : comportement existant
                    string path1 = fileArgs[0];
                    string path2 = fileArgs.Length > 1 ? fileArgs[1] : null;
                    return await SyncAsync(path1, path2);
                }

                string inputPath = Path.GetFullPath(fileArgs[0]);
                string outputPath = fileArgs.Length > 1 ? Path.GetFullPath(fileArgs[1]) : null;

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
            string path2,
            out string slnPath,
            out string slnxPath,
            out string error)
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
            out string error)
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

        public static async Task<int> ConvertSlnxToSlnAsync(string slnxPath, string outputPath = null)
        {
            string slnPath = outputPath ?? Path.ChangeExtension(slnxPath, ".sln");

            SolutionModel solution = await SolutionSerializers.SlnXml.OpenAsync(slnxPath, CancellationToken.None);
            await SolutionSerializers.SlnFileV12.SaveAsync(slnPath, solution, CancellationToken.None);

            Console.WriteLine($"OK -> {slnPath}");
            return 0;
        }

        public static async Task<int> ConvertSlnToSlnxAsync(string slnPath, string outputPath = null)
        {
            string slnxPath = outputPath ?? Path.ChangeExtension(slnPath, ".slnx");

            SolutionModel solution = await SolutionSerializers.SlnFileV12.OpenAsync(slnPath, CancellationToken.None);
            await SolutionSerializers.SlnXml.SaveAsync(slnxPath, solution, CancellationToken.None);

            Console.WriteLine($"OK -> {slnxPath}");
            return 0;
        }

        // -------------------------------------------------------------------------
        // Sync
        // -------------------------------------------------------------------------

        public static async Task<int> SyncAsync(string path1, string path2 = null)
        {
            if (!TryResolveSolutionPair(path1, path2, out string slnPath, out string slnxPath, out string resolveError))
            {
                Console.Error.WriteLine(resolveError);
                return 1;
            }

            if (!TryResolveSyncDirection(slnPath, slnxPath, out string sourcePath, out string targetPath, out string syncError))
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

        /// <summary>
        /// Cherche dans <paramref name="directory"/> (non récursif) la paire .sln / .slnx.
        /// Échoue si aucun fichier n'est trouvé ou si plusieurs noms de solutions différents existent.
        /// Si un seul des deux fichiers existe, le chemin de l'autre est déduit.
        /// </summary>
        public static bool TryFindSolutionPairInDirectory(
            string directory,
            out string slnPath,
            out string slnxPath,
            out string error)
        {
            slnPath = string.Empty;
            slnxPath = string.Empty;
            error = null;

            // Filtrage explicite de l'extension : certains filtres "*.sln" peuvent
            // aussi remonter des ".slnx" selon la plateforme.
            string[] slnFiles = Directory.GetFiles(directory, "*.sln")
                .Where(f => GetExtension(f) == ".sln")
                .ToArray();
            string[] slnxFiles = Directory.GetFiles(directory, "*.slnx")
                .Where(f => GetExtension(f) == ".slnx")
                .ToArray();

            if (slnFiles.Length == 0 && slnxFiles.Length == 0)
            {
                error = "No .sln or .slnx file found in the current directory.";
                return false;
            }

            // Multiple distinct solution names → fail
            var names = slnFiles
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Concat(slnxFiles.Select(f => Path.GetFileNameWithoutExtension(f)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count > 1)
            {
                error = $"Multiple solutions found ({string.Join(", ", names)}). Please specify the file.";
                return false;
            }

            string name = names[0];
            slnPath = slnFiles.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? Path.Combine(directory, name + ".sln");
            slnxPath = slnxFiles.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? Path.Combine(directory, name + ".slnx");

            return true;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  slnx2sln <file.slnx> [file.sln]     # Convert .slnx → .sln");
            Console.WriteLine("  slnx2sln <file.sln>  [file.slnx]    # Convert .sln  → .slnx");
            Console.WriteLine("  slnx2sln sync                       # Auto-sync the solution found in the current directory");
            Console.WriteLine("  slnx2sln sync <file.sln|.slnx>      # Sync pair (newer wins)");
            Console.WriteLine("  slnx2sln sync <file.sln> <file.slnx>");
        }
    }
}