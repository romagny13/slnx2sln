using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace slnx2sln
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return 1;
            }

            string inputPath = Path.GetFullPath(args[0]);
            string outputPath = args.Length > 1 ? Path.GetFullPath(args[1]) : null;

            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"File not found: {inputPath}");
                return 1;
            }

            string extension = Path.GetExtension(inputPath).ToLowerInvariant();

            try
            {
                if (extension == ".slnx")
                {
                    return await ConvertSlnxToSlnAsync(inputPath, outputPath);
                }
                else if (extension == ".sln")
                {
                    return await ConvertSlnToSlnxAsync(inputPath, outputPath);
                }
                else
                {
                    Console.Error.WriteLine($"Unsupported file extension: {extension}");
                    PrintUsage();
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Conversion failed: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Converts a .slnx file to classic .sln format.
        /// </summary>
        public static async Task<int> ConvertSlnxToSlnAsync(string slnxPath, string? outputPath = null)
        {
            string slnPath = outputPath ?? Path.ChangeExtension(slnxPath, ".sln");

            SolutionModel solution = await SolutionSerializers.SlnXml.OpenAsync(slnxPath, CancellationToken.None);
            await SolutionSerializers.SlnFileV12.SaveAsync(slnPath, solution, CancellationToken.None);

            Console.WriteLine($"OK -> {slnPath}");
            return 0;
        }

        /// <summary>
        /// Converts a classic .sln file to .slnx format.
        /// </summary>
        public static async Task<int> ConvertSlnToSlnxAsync(string slnPath, string? outputPath = null)
        {
            string slnxPath = outputPath ?? Path.ChangeExtension(slnPath, ".slnx");

            SolutionModel solution = await SolutionSerializers.SlnFileV12.OpenAsync(slnPath, CancellationToken.None);
            await SolutionSerializers.SlnXml.SaveAsync(slnxPath, solution, CancellationToken.None);

            Console.WriteLine($"OK -> {slnxPath}");
            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  slnx2sln <file.slnx> [file.sln]   # Convert .slnx → .sln");
            Console.WriteLine("  slnx2sln <file.sln>  [file.slnx]  # Convert .sln  → .slnx");
        }
    }
}