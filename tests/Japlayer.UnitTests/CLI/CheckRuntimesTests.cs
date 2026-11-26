using Japlayer.CLI.Apps;
using Japlayer.Data.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.CLI
{
    [TestClass]
    public class CheckRuntimesTests
    {
        [TestMethod]
        public void TryParseOptions_DefaultsToDryRun()
        {
            var success = CheckRuntimesApp.TryParseOptions([], out var options, out var errorMessage);

            Assert.IsTrue(success, errorMessage);
            Assert.IsFalse(options.Apply);
            Assert.AreEqual(2, options.ToleranceMinutes);
            Assert.AreEqual(5, options.SceneDiffSeconds);
            Assert.IsFalse(options.NoBackup);
        }

        [TestMethod]
        public void TryParseOptions_ParsesApplyReportAndThresholds()
        {
            var args = new[]
            {
                "--apply",
                "--only-invalid",
                "--no-backup",
                "--report", "audit.csv",
                "--tolerance-minutes", "3",
                "--scene-diff-seconds", "10",
                "--only", "ABP-174"
            };

            var success = CheckRuntimesApp.TryParseOptions(args, out var options, out var errorMessage);

            Assert.IsTrue(success, errorMessage);
            Assert.IsTrue(options.Apply);
            Assert.IsTrue(options.OnlyInvalid);
            Assert.IsTrue(options.NoBackup);
            Assert.AreEqual("audit.csv", options.ReportPath);
            Assert.AreEqual(3, options.ToleranceMinutes);
            Assert.AreEqual(10, options.SceneDiffSeconds);
            Assert.IsTrue(options.OnlyFilter.MatchesMedia("ABP-174"));
        }

        [TestMethod]
        public void BuildAuditReport_ClassifiesCorruptedMismatchedMissingAndOk()
        {
            var locations = new List<MediaLocation>
            {
                new() { MediaId = "OK-001", Scene = 1, Hostname = "h", Path = @"C:\v\ok.mp4" },
                new() { MediaId = "BAD-001", Scene = 1, Hostname = "h", Path = @"C:\v\bad.mp4" },
                new() { MediaId = "MIS-001", Scene = 1, Hostname = "h", Path = @"C:\v\mis_1.mp4" },
                new() { MediaId = "MIS-001", Scene = 2, Hostname = "h", Path = @"C:\v\mis_2.mp4" },
                new() { MediaId = "NORT-001", Scene = 1, Hostname = "h", Path = @"C:\v\nort.mp4" },
                new() { MediaId = "NOMETA-001", Scene = 1, Hostname = "h", Path = @"C:\v\nometa.mp4" },
                new() { MediaId = "COPY-001", Scene = 1, Hostname = "h", Path = @"C:\v\copy_4k.mp4" },
                new() { MediaId = "COPY-001", Scene = 1, Hostname = "h", Path = @"C:\v\copy_8k.mp4" }
            };

            var metadata = new List<MediaMetadata>
            {
                new() { MetadataUrl = "u1", MediaId = "OK-001", Title = "OK", RuntimeMinutes = 120 },
                new() { MetadataUrl = "u2", MediaId = "BAD-001", Title = "Bad", RuntimeMinutes = 138138 },
                new() { MetadataUrl = "u3", MediaId = "MIS-001", Title = "Mis", RuntimeMinutes = 90 },
                new() { MetadataUrl = "u4", MediaId = "NORT-001", Title = "NoRt", RuntimeMinutes = null },
                new() { MetadataUrl = "u5", MediaId = "COPY-001", Title = "Copy", RuntimeMinutes = 60 }
            };

            var probed = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase)
            {
                [@"C:\v\ok.mp4"] = 7210,       // 120.17m -> OK
                [@"C:\v\bad.mp4"] = 8280,      // 138m vs 138138 -> Corrupted
                [@"C:\v\mis_1.mp4"] = 3600,    // 60m + 40m = 100m vs 90m -> Mismatched
                [@"C:\v\mis_2.mp4"] = 2400,
                [@"C:\v\nort.mp4"] = 3000,     // 50m vs null -> MissingRuntime
                [@"C:\v\nometa.mp4"] = 2700,   // 45m -> MissingMetadataRow
                [@"C:\v\copy_4k.mp4"] = 3600,
                [@"C:\v\copy_8k.mp4"] = 3620   // 20s copy diff > 5s -> HasSceneCopyDiscrepancy
            };

            var options = new CheckRuntimesOptions();
            var report = CheckRuntimesApp.BuildAuditReport(locations, metadata, probed, options);

            Assert.AreEqual(6, report.Summary.TotalMedia);
            Assert.AreEqual(2, report.Summary.Ok); // OK-001 and COPY-001
            Assert.AreEqual(1, report.Summary.Corrupted);
            Assert.AreEqual(1, report.Summary.Mismatched);
            Assert.AreEqual(1, report.Summary.MissingRuntime);
            Assert.AreEqual(1, report.Summary.MissingMetadataRow);
            Assert.AreEqual(1, report.Summary.SceneCopyDiscrepancies);

            var multiScene = report.Items.Single(item => item.MediaId == "MIS-001");
            Assert.AreEqual(2, multiScene.SceneCount);
            Assert.AreEqual(100, multiScene.ActualRuntimeMinutes);
            Assert.AreEqual(10, multiScene.DeltaMinutes);

            var copyDiffItem = report.Items.Single(item => item.MediaId == "COPY-001");
            Assert.IsTrue(copyDiffItem.HasSceneCopyDiscrepancy);
            Assert.AreEqual(3620, copyDiffItem.ActualDurationSeconds); // 8k preferred
        }
    }
}
