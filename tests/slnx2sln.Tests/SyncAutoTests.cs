using System;
using System.IO;
using System.Threading.Tasks;

namespace slnx2sln.Tests
{
    [TestFixture]
    [NonParallelizable] // Console.SetError / SetOut sont globaux
    public class SyncAutoTests
    {
        private const string MinimalSlnx = "<Solution />";

        private string _dir = string.Empty;
        private TextWriter _originalOut = null;
        private TextWriter _originalError = null;
        private StringWriter _out = null;
        private StringWriter _err = null;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "slnx2sln-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);

            _originalOut = Console.Out;
            _originalError = Console.Error;
            _out = new StringWriter();
            _err = new StringWriter();
            Console.SetOut(_out);
            Console.SetError(_err);
        }

        [TearDown]
        public void TearDown()
        {
            Console.SetOut(_originalOut);
            Console.SetError(_originalError);

            // Dispose des StringWriter créés dans SetUp
            _out?.Dispose();
            _err?.Dispose();
            _out = null;
            _err = null;

            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        private string Create(string fileName, string content = "", DateTime? lastWriteUtc = null)
        {
            string path = Path.Combine(_dir, fileName);
            File.WriteAllText(path, content);
            if (lastWriteUtc.HasValue)
                File.SetLastWriteTimeUtc(path, lastWriteUtc.Value);
            return path;
        }

        // ---------------------------------------------------------------------
        // TryFindSolutionPairInDirectory
        // ---------------------------------------------------------------------

        [Test]
        public void TryFind_EmptyDirectory_Fails()
        {
            bool ok = Program.TryFindSolutionPairInDirectory(_dir, out _, out _, out string error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("No .sln or .slnx file found"));
        }

        [Test]
        public void TryFind_BothFilesSameName_ReturnsPair()
        {
            string sln = Create("App.sln");
            string slnx = Create("App.slnx");

            bool ok = Program.TryFindSolutionPairInDirectory(_dir, out string slnPath, out string slnxPath, out string error);

            Assert.That(ok, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(slnPath, Is.EqualTo(sln));
            Assert.That(slnxPath, Is.EqualTo(slnx));
        }

        [Test]
        public void TryFind_OnlySln_DerivesSlnxPath()
        {
            string sln = Create("App.sln");

            bool ok = Program.TryFindSolutionPairInDirectory(_dir, out string slnPath, out string slnxPath, out _);

            Assert.That(ok, Is.True);
            Assert.That(slnPath, Is.EqualTo(sln));
            Assert.That(slnxPath, Is.EqualTo(Path.Combine(_dir, "App.slnx")));
        }

        [Test]
        public void TryFind_OnlySlnx_DerivesSlnPath()
        {
            string slnx = Create("App.slnx");

            bool ok = Program.TryFindSolutionPairInDirectory(_dir, out string slnPath, out string slnxPath, out _);

            Assert.That(ok, Is.True);
            Assert.That(slnxPath, Is.EqualTo(slnx));
            Assert.That(slnPath, Is.EqualTo(Path.Combine(_dir, "App.sln")));
        }

        [Test]
        public void TryFind_TwoDifferentSolutionNames_Fails()
        {
            Create("A.sln");
            Create("B.slnx");

            bool ok = Program.TryFindSolutionPairInDirectory(_dir, out _, out _, out string error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Multiple solutions found"));
            Assert.That(error, Does.Contain("A").And.Contain("B"));
        }

        [Test]
        public void TryFind_TwoSlnWithDifferentNames_Fails()
        {
            Create("A.sln");
            Create("B.sln");

            bool ok = Program.TryFindSolutionPairInDirectory(_dir, out _, out _, out string error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Multiple solutions found"));
        }

        [Test]
        public void TryFind_NameComparisonIsCaseInsensitive()
        {
            Create("App.sln");
            Create("APP.slnx");

            bool ok = Program.TryFindSolutionPairInDirectory(_dir, out _, out _, out string error);

            Assert.That(ok, Is.True);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void TryFind_IgnoresOtherExtensionsAndSubdirectories()
        {
            Create("App.sln");
            Create("readme.md");
            Create("Other.csproj");

            string subDir = Path.Combine(_dir, "sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "Nested.sln"), "");

            bool ok = Program.TryFindSolutionPairInDirectory(_dir, out string slnPath, out _, out _);

            Assert.That(ok, Is.True);
            Assert.That(slnPath, Is.EqualTo(Path.Combine(_dir, "App.sln")));
        }

        [Test]
        public async Task Run_SyncWithoutArgs_NoSolution_ReturnsError()
        {
            int code = await Program.RunAsync(new[] { "sync" }, _dir);

            Assert.That(code, Is.EqualTo(1));
            Assert.That(_err.ToString(), Does.Contain("No .sln or .slnx file found"));
        }

        [Test]
        public async Task Run_SyncWithoutArgs_MultipleSolutions_ReturnsError_AndCreatesNothing()
        {
            Create("A.slnx", MinimalSlnx);
            Create("B.slnx", MinimalSlnx);

            int code = await Program.RunAsync(new[] { "sync" }, _dir);

            Assert.That(code, Is.EqualTo(1));
            Assert.That(_err.ToString(), Does.Contain("Multiple solutions found"));
            Assert.That(File.Exists(Path.Combine(_dir, "A.sln")), Is.False);
            Assert.That(File.Exists(Path.Combine(_dir, "B.sln")), Is.False);
        }

        [Test]
        public async Task Run_SyncWithoutArgs_OnlySlnx_GeneratesSln()
        {
            Create("App.slnx", MinimalSlnx);

            int code = await Program.RunAsync(new[] { "sync" }, _dir);

            Assert.That(code, Is.EqualTo(0), _err.ToString());
            Assert.That(File.Exists(Path.Combine(_dir, "App.sln")), Is.True);
        }

        [Test]
        public async Task Run_SyncWithoutArgs_OnlySln_GeneratesSlnx()
        {
            // On génère d'abord un .sln valide à partir d'un .slnx
            string slnx = Create("Seed.slnx", MinimalSlnx);
            Assert.That(await Program.ConvertSlnxToSlnAsync(slnx, Path.Combine(_dir, "App.sln")), Is.EqualTo(0));
            File.Delete(slnx);

            int code = await Program.RunAsync(new[] { "sync" }, _dir);

            Assert.That(code, Is.EqualTo(0), _err.ToString());
            Assert.That(File.Exists(Path.Combine(_dir, "App.slnx")), Is.True);
        }

        [Test]
        public async Task Run_SyncWithoutArgs_SlnxNewer_OverwritesSln()
        {
            DateTime now = DateTime.UtcNow;
            string sln = Create("App.sln", "OLD-CONTENT", now.AddMinutes(-10));
            Create("App.slnx", MinimalSlnx, now);

            int code = await Program.RunAsync(new[] { "sync" }, _dir);

            Assert.That(code, Is.EqualTo(0), _err.ToString());
            Assert.That(File.ReadAllText(sln), Does.Not.Contain("OLD-CONTENT"));
        }

        [Test]
        public async Task Run_SyncWithoutArgs_SlnNewer_OverwritesSlnx()
        {
            DateTime now = DateTime.UtcNow;
            string slnx = Create("Seed.slnx", MinimalSlnx);
            string sln = Path.Combine(_dir, "App.sln");
            await Program.ConvertSlnxToSlnAsync(slnx, sln);
            File.Delete(slnx);

            string targetSlnx = Create("App.slnx", "OLD-CONTENT", now.AddMinutes(-10));
            File.SetLastWriteTimeUtc(sln, now);

            int code = await Program.RunAsync(new[] { "sync" }, _dir);

            Assert.That(code, Is.EqualTo(0), _err.ToString());
            Assert.That(File.ReadAllText(targetSlnx), Does.Not.Contain("OLD-CONTENT"));
        }

        [Test]
        public async Task Run_SyncWithoutArgs_IsCaseInsensitiveOnCommandName()
        {
            Create("App.slnx", MinimalSlnx);

            int code = await Program.RunAsync(new[] { "SYNC" }, _dir);

            Assert.That(code, Is.EqualTo(0), _err.ToString());
            Assert.That(File.Exists(Path.Combine(_dir, "App.sln")), Is.True);
        }


        [Test]
        public async Task Run_SyncWithSingleFile_StillWorks()
        {
            string slnx = Create("App.slnx", MinimalSlnx);

            int code = await Program.RunAsync(new[] { "sync", slnx }, _dir);

            Assert.That(code, Is.EqualTo(0), _err.ToString());
            Assert.That(File.Exists(Path.Combine(_dir, "App.sln")), Is.True);
        }

        [Test]
        public async Task Run_SyncWithTwoFiles_StillWorks()
        {
            string slnx = Create("App.slnx", MinimalSlnx);
            string sln = Path.Combine(_dir, "App.sln");

            int code = await Program.RunAsync(new[] { "sync", sln, slnx }, _dir);

            Assert.That(code, Is.EqualTo(0), _err.ToString());
            Assert.That(File.Exists(sln), Is.True);
        }

        [Test]
        public async Task Run_SyncWithExplicitFile_IgnoresOtherSolutionsInDirectory()
        {
            string a = Create("A.slnx", MinimalSlnx);
            Create("B.slnx", MinimalSlnx);

            int code = await Program.RunAsync(new[] { "sync", a }, _dir);

            Assert.That(code, Is.EqualTo(0), _err.ToString());
            Assert.That(File.Exists(Path.Combine(_dir, "A.sln")), Is.True);
            Assert.That(File.Exists(Path.Combine(_dir, "B.sln")), Is.False);
        }

        [Test]
        public async Task Run_NoArgs_PrintsUsage()
        {
            int code = await Program.RunAsync(Array.Empty<string>(), _dir);

            Assert.That(code, Is.EqualTo(1));
            Assert.That(_out.ToString(), Does.Contain("slnx2sln sync"));
        }
    }
}