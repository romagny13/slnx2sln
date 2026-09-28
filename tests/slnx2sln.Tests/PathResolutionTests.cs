using NUnit.Framework;
using System;
using System.IO;
using System.Threading.Tasks;

namespace slnx2sln.Tests
{
    public class PathResolutionTests
    {
        [Test]
        public void GetExtension_IsLowerInvariant()
        {
            Assert.That(Program.GetExtension(@"C:\A.SLNX"), Is.EqualTo(".slnx"));
            Assert.That(Program.GetExtension(@"C:\A.Sln"), Is.EqualTo(".sln"));
        }

        [Test]
        public void TryResolveSolutionPair_SingleSlnx_DerivesSln()
        {
            string input = Path.GetFullPath("MyApp.slnx");

            bool ok = Program.TryResolveSolutionPair(input, null, out string sln, out string slnx, out string? error);

            Assert.That(ok, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(slnx, Is.EqualTo(input));
            Assert.That(sln, Is.EqualTo(Path.ChangeExtension(input, ".sln")));
        }

        [Test]
        public void TryResolveSolutionPair_SingleSln_DerivesSlnx()
        {
            string input = Path.GetFullPath("MyApp.sln");

            bool ok = Program.TryResolveSolutionPair(input, null, out string sln, out string slnx, out string? error);

            Assert.That(ok, Is.True);
            Assert.That(sln, Is.EqualTo(input));
            Assert.That(slnx, Is.EqualTo(Path.ChangeExtension(input, ".slnx")));
        }

        [Test]
        public void TryResolveSolutionPair_TwoPaths_OrderedCorrectly()
        {
            string sln = Path.GetFullPath("A.sln");
            string slnx = Path.GetFullPath("A.slnx");

            bool ok1 = Program.TryResolveSolutionPair(sln, slnx, out string s1, out string x1, out _);
            bool ok2 = Program.TryResolveSolutionPair(slnx, sln, out string s2, out string x2, out _);

            Assert.That(ok1, Is.True);
            Assert.That(ok2, Is.True);
            Assert.That(s1, Is.EqualTo(sln));
            Assert.That(x1, Is.EqualTo(slnx));
            Assert.That(s2, Is.EqualTo(sln));
            Assert.That(x2, Is.EqualTo(slnx));
        }

        [Test]
        public void TryResolveSolutionPair_InvalidExtension_Fails()
        {
            bool ok = Program.TryResolveSolutionPair("file.txt", null, out _, out _, out string? error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Unsupported"));
        }

        [Test]
        public void TryResolveSolutionPair_TwoSameExtensions_Fails()
        {
            bool ok = Program.TryResolveSolutionPair("a.sln", "b.sln", out _, out _, out string? error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("one must be .sln"));
        }
    }

    public class SyncDirectionTests
    {
        private string _dir = null!;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "slnx2sln_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }

        [Test]
        public void TryResolveSyncDirection_OnlySln_SourceIsSln()
        {
            string sln = Path.Combine(_dir, "A.sln");
            string slnx = Path.Combine(_dir, "A.slnx");
            File.WriteAllText(sln, "x");

            bool ok = Program.TryResolveSyncDirection(sln, slnx, out string source, out string target, out _);

            Assert.That(ok, Is.True);
            Assert.That(source, Is.EqualTo(sln));
            Assert.That(target, Is.EqualTo(slnx));
        }

        [Test]
        public void TryResolveSyncDirection_OnlySlnx_SourceIsSlnx()
        {
            string sln = Path.Combine(_dir, "A.sln");
            string slnx = Path.Combine(_dir, "A.slnx");
            File.WriteAllText(slnx, "x");

            bool ok = Program.TryResolveSyncDirection(sln, slnx, out string source, out string target, out _);

            Assert.That(ok, Is.True);
            Assert.That(source, Is.EqualTo(slnx));
            Assert.That(target, Is.EqualTo(sln));
        }

        [Test]
        public void TryResolveSyncDirection_Neither_Fails()
        {
            string sln = Path.Combine(_dir, "A.sln");
            string slnx = Path.Combine(_dir, "A.slnx");

            bool ok = Program.TryResolveSyncDirection(sln, slnx, out _, out _, out string? error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Neither"));
        }

        [Test]
        public void TryResolveSyncDirection_BothExist_NewerWins()
        {
            string sln = Path.Combine(_dir, "A.sln");
            string slnx = Path.Combine(_dir, "A.slnx");
            File.WriteAllText(sln, "old");
            File.WriteAllText(slnx, "new");

            // Make .slnx clearly newer
            File.SetLastWriteTimeUtc(sln, DateTime.UtcNow.AddHours(-2));
            File.SetLastWriteTimeUtc(slnx, DateTime.UtcNow);

            bool ok = Program.TryResolveSyncDirection(sln, slnx, out string source, out string target, out _);

            Assert.That(ok, Is.True);
            Assert.That(source, Is.EqualTo(slnx));
            Assert.That(target, Is.EqualTo(sln));
        }

        [Test]
        public void TryResolveSyncDirection_BothExist_SlnNewer_SourceIsSln()
        {
            string sln = Path.Combine(_dir, "A.sln");
            string slnx = Path.Combine(_dir, "A.slnx");
            File.WriteAllText(sln, "new");
            File.WriteAllText(slnx, "old");

            File.SetLastWriteTimeUtc(slnx, DateTime.UtcNow.AddHours(-2));
            File.SetLastWriteTimeUtc(sln, DateTime.UtcNow);

            bool ok = Program.TryResolveSyncDirection(sln, slnx, out string source, out string target, out _);

            Assert.That(ok, Is.True);
            Assert.That(source, Is.EqualTo(sln));
            Assert.That(target, Is.EqualTo(slnx));
        }
    }

    public class ConversionTests
    {
        private string _dir = null!;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "slnx2sln_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }

        private static string MinimalSlnx(string projectPath) =>
            $"<Solution>\n  <Project Path=\"{projectPath}\" />\n</Solution>\n";

        private static string MinimalSln(
            string projectPath,
            string guid = "11111111-1111-1111-1111-111111111111") =>
            $$"""
    Microsoft Visual Studio Solution File, Format Version 12.00
    # Visual Studio Version 17
    Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "{{projectPath}}", "{{{guid}}}"
    EndProject
    Global
        GlobalSection(SolutionConfigurationPlatforms) = preSolution
            Debug|Any CPU = Debug|Any CPU
            Release|Any CPU = Release|Any CPU
        EndGlobalSection
        GlobalSection(ProjectConfigurationPlatforms) = postSolution
            {{{guid}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            {{{guid}}}.Debug|Any CPU.Build.0 = Debug|Any CPU
            {{{guid}}}.Release|Any CPU.ActiveCfg = Release|Any CPU
            {{{guid}}}.Release|Any CPU.Build.0 = Release|Any CPU
        EndGlobalSection
    EndGlobal
    """;

        [Test]
        public async Task ConvertSlnxToSln_CreatesReadableSln()
        {
            string slnx = Path.Combine(_dir, "T.slnx");
            string sln = Path.Combine(_dir, "T.sln");
            await File.WriteAllTextAsync(slnx, MinimalSlnx("App/App.csproj"));

            int result = await Program.ConvertSlnxToSlnAsync(slnx, sln);

            Assert.That(result, Is.EqualTo(0));
            Assert.That(File.Exists(sln), Is.True);
            string content = await File.ReadAllTextAsync(sln);
            Assert.That(content, Does.Contain("Microsoft Visual Studio Solution File"));
            Assert.That(content, Does.Contain("App.csproj"));
        }

        [Test]
        public async Task ConvertSlnToSlnx_CreatesReadableSlnx()
        {
            string sln = Path.Combine(_dir, "T.sln");
            string slnx = Path.Combine(_dir, "T.slnx");
            await File.WriteAllTextAsync(sln, MinimalSln(@"App\App.csproj"));

            int result = await Program.ConvertSlnToSlnxAsync(sln, slnx);

            Assert.That(result, Is.EqualTo(0));
            Assert.That(File.Exists(slnx), Is.True);
            string content = await File.ReadAllTextAsync(slnx);
            Assert.That(content, Does.Contain("<Solution>"));
            Assert.That(content, Does.Contain("App.csproj"));
        }

        [Test]
        public async Task RoundTrip_PreservesProjectPath()
        {
            string slnx1 = Path.Combine(_dir, "R.slnx");
            string sln = Path.Combine(_dir, "R.sln");
            string slnx2 = Path.Combine(_dir, "R2.slnx");
            await File.WriteAllTextAsync(slnx1, MinimalSlnx("Src/MyApp.csproj"));

            Assert.That(await Program.ConvertSlnxToSlnAsync(slnx1, sln), Is.EqualTo(0));
            Assert.That(await Program.ConvertSlnToSlnxAsync(sln, slnx2), Is.EqualTo(0));

            string content = await File.ReadAllTextAsync(slnx2);
            Assert.That(content, Does.Contain("MyApp.csproj"));
        }

        [Test]
        public async Task Sync_OnlySlnx_CreatesSln()
        {
            string slnx = Path.Combine(_dir, "S.slnx");
            await File.WriteAllTextAsync(slnx, MinimalSlnx("P/P.csproj"));

            int result = await Program.SyncAsync(slnx);

            Assert.That(result, Is.EqualTo(0));
            Assert.That(File.Exists(Path.ChangeExtension(slnx, ".sln")), Is.True);
        }

        [Test]
        public async Task Sync_NewerSlnx_UpdatesSln()
        {
            string sln = Path.Combine(_dir, "S.sln");
            string slnx = Path.Combine(_dir, "S.slnx");

            await File.WriteAllTextAsync(sln, MinimalSln(@"Old\Old.csproj"));
            await File.WriteAllTextAsync(slnx, MinimalSlnx("New/New.csproj"));

            File.SetLastWriteTimeUtc(sln, DateTime.UtcNow.AddHours(-1));
            File.SetLastWriteTimeUtc(slnx, DateTime.UtcNow);

            int result = await Program.SyncAsync(slnx);

            Assert.That(result, Is.EqualTo(0));
            string content = await File.ReadAllTextAsync(sln);
            Assert.That(content, Does.Contain("New.csproj"));
            Assert.That(content, Does.Not.Contain("Old.csproj"));
        }

        //[Test]
        //public async Task Convert_MissingFile_DoesNotThrow_FromConvert_WhenCalledDirectly()
        //{
        //    // Convert* assume path exists when called from Main;
        //    // OpenAsync will throw → caught by Main. Here we expect exception or handle as you prefer.
        //    string missing = Path.Combine(_dir, "Missing.slnx");
        //    Assert.ThrowsAsync<Exception>(async () => await Program.ConvertSlnxToSlnAsync(missing));
        //}
    }
}