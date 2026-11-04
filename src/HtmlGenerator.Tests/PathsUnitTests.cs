using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class PathsUnitTests
    {
        private static readonly string TestRoot = Path.Combine(Path.GetTempPath(), "SourceBrowser", "PathsUnitTests");

        [TestMethod]
        public void TrimSlash_PreservesRoot()
        {
            string root = Path.GetPathRoot(TestRoot);

            Assert.AreEqual(root, Microsoft.SourceBrowser.Common.Paths.TrimSlash(root));
        }

        [TestMethod]
        public void TrimSlash_RemovesRepeatedTrailingSeparators()
        {
            string path = Path.Combine(TestRoot, "folder");
            string pathWithTrailingSeparators = path + new string(Path.DirectorySeparatorChar, 3);

            Assert.AreEqual(path, Microsoft.SourceBrowser.Common.Paths.TrimSlash(pathWithTrailingSeparators));
        }

        [TestMethod]
        public void CopyDirectory_PreservesRelativeFilePath()
        {
            string testDirectory = Path.Combine(Path.GetTempPath(), nameof(PathsUnitTests), Guid.NewGuid().ToString("N"));
            string sourceDirectory = Path.Combine(testDirectory, "source");
            string destinationDirectory = Path.Combine(testDirectory, "destination");
            string sourceFile = Path.Combine(sourceDirectory, "nested", "file.txt");
            string destinationFile = Path.Combine(destinationDirectory, "nested", "file.txt");
            const string contents = "copied";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(sourceFile));
                File.WriteAllText(sourceFile, contents);

                Microsoft.SourceBrowser.Common.FileUtilities.CopyDirectory(
                    sourceDirectory + Path.DirectorySeparatorChar,
                    destinationDirectory + Path.DirectorySeparatorChar);

                Assert.AreEqual(contents, File.ReadAllText(destinationFile));
            }
            finally
            {
                if (Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, recursive: true);
                }
            }
        }

        [TestMethod]
        public void MakeRelativeToFile1()
        {
            MakeRelativeToFile(
                Path.Combine(TestRoot, "abcd", "e", "f", "g.txt"),
                Path.Combine(TestRoot, "abc", "h", "i.txt"),
                Path.Combine("..", "..", "abcd", "e", "f", "g.txt"));
        }

        [TestMethod]
        public void MakeRelativeToFile2()
        {
            MakeRelativeToFile(
                Path.Combine(TestRoot, "1.txt"),
                Path.Combine(TestRoot, "2.txt"),
                @"1.txt");
        }

        [TestMethod]
        public void MakeRelativeToFile3()
        {
            MakeRelativeToFile(
                Path.Combine(TestRoot, "a", "1.txt"),
                Path.Combine(TestRoot, "2.txt"),
                Path.Combine("a", "1.txt"));
        }

        [TestMethod]
        public void MakeRelativeToFile4()
        {
            MakeRelativeToFile(
                Path.Combine(TestRoot, "..", "1.txt"),
                Path.Combine(TestRoot, "2.txt"),
                Path.Combine("..", "1.txt"));
        }

        [TestMethod]
        public void MakeRelativeToFile5()
        {
            MakeRelativeToFile(
                Path.Combine(TestRoot, "1.txt"),
                Path.Combine(TestRoot, "1.txt"),
                @"1.txt");
        }

        [TestMethod]
        public void MakeRelativeToFile6()
        {
            MakeRelativeToFile(
                Path.Combine(TestRoot, "project", "R", "1.html"),
                Path.Combine(TestRoot, "project", "document.txt"),
                Path.Combine("R", "1.html"));
        }

        [TestMethod]
        public void MakeRelativeToFile7()
        {
            MakeRelativeToFile(
                Path.Combine(TestRoot, "assembly", "A.html"),
                Path.Combine(TestRoot, "project", "a", "document.txt"),
                Path.Combine("..", "..", "assembly", "A.html"));
        }

        [TestMethod]
        public void MakeRelativeToFile8()
        {
            MakeRelativeToFile(
                TestRoot,
                Path.Combine(TestRoot, "project", "a", "document.txt"),
                Path.Combine("..", "..") + Path.DirectorySeparatorChar);
        }

        [TestMethod]
        public void MakeRelativeToFile9()
        {
            MakeRelativeToFile(
                TestRoot + Path.DirectorySeparatorChar,
                Path.Combine(TestRoot, "project", "a", "document.txt"),
                Path.Combine("..", "..") + Path.DirectorySeparatorChar);
        }

        [TestMethod]
        public void MakeRelativeToFile10()
        {
            MakeRelativeToFile(
                TestRoot + Path.DirectorySeparatorChar,
                TestRoot,
                Path.GetFileName(TestRoot) + Path.DirectorySeparatorChar);
        }

        [TestMethod]
        public void MakeRelativeToFile11()
        {
            MakeRelativeToFile(
                TestRoot,
                TestRoot,
                Path.GetFileName(TestRoot));
        }

        [TestMethod]
        public void MakeRelativeToFile12()
        {
            MakeRelativeToFile(
                TestRoot,
                TestRoot + Path.DirectorySeparatorChar,
                "");
        }

        [TestMethod]
        public void MakeRelativeToFile13()
        {
            MakeRelativeToFile(
                TestRoot + Path.DirectorySeparatorChar,
                TestRoot + Path.DirectorySeparatorChar,
                "");
        }

        [TestMethod]
        public void MakeRelativeToFolder1()
        {
            MakeRelativeToFolder(
                Path.Combine(TestRoot, "1.txt"),
                TestRoot + Path.DirectorySeparatorChar,
                @"1.txt");
        }

        [TestMethod]
        public void MakeRelativeToFolder2()
        {
            MakeRelativeToFolder(
                Path.Combine(TestRoot, "1.txt"),
                TestRoot,
                @"1.txt");
        }

        [TestMethod]
        public void MakeRelativeToFolder3()
        {
            MakeRelativeToFolder(
                Path.Combine(TestRoot, "a", "1.txt"),
                TestRoot,
                Path.Combine("a", "1.txt"));
        }

        [TestMethod]
        public void MakeRelativeToFolder4()
        {
            MakeRelativeToFolder(
                Path.Combine(TestRoot, "1.txt"),
                Path.Combine(TestRoot, "a"),
                Path.Combine("..", "1.txt"));
        }

        [TestMethod]
        public void MakeRelativeToFolder5()
        {
            MakeRelativeToFolder(
                Path.Combine(TestRoot, "1.txt"),
                Path.Combine(TestRoot, "a") + Path.DirectorySeparatorChar,
                Path.Combine("..", "1.txt"));
        }

        [TestMethod]
        public void IsOrContains_UsesPlatformPathCaseSemantics()
        {
            string path = Path.Combine(TestRoot, "case");
            string differentlyCasedDescendent = Path.Combine(TestRoot, "Case", "file.cs");

            Assert.AreEqual(OperatingSystem.IsWindows(), Paths.IsOrContains(path, differentlyCasedDescendent));
        }

        [TestMethod]
        public void MakeRelativeToFolder_CaseDifferenceUsesPlatformPathSemantics()
        {
            string path = Path.Combine(TestRoot, "case");
            string differentlyCasedFile = Path.Combine(TestRoot, "Case", "file.cs");
            string expected = OperatingSystem.IsWindows()
                ? "file.cs"
                : Path.Combine("..", "Case", "file.cs");

            Assert.AreEqual(expected, Paths.MakeRelativeToFolder(differentlyCasedFile, path));
        }

        private void MakeRelativeToFile(string a, string b, string expected)
        {
            var actual = Paths.MakeRelativeToFile(a, b);
            Assert.AreEqual(expected, actual);
        }

        private void MakeRelativeToFolder(string a, string b, string expected)
        {
            var actual = Paths.MakeRelativeToFolder(a, b);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void DisambiguateRelativePaths_NoCollision_PassesThroughUnchanged()
        {
            var relativePaths = new[] { Path.Combine("System", "Collections", "IEnumerable.cs"), Path.Combine("System", "Collections", "IEnumerator.cs") };
            var identityKeys = new[] { Path.Combine(TestRoot, "src", "a", "IEnumerable.cs"), Path.Combine(TestRoot, "src", "a", "IEnumerator.cs") };

            var actual = Paths.DisambiguateRelativePaths(relativePaths, identityKeys);

            CollectionAssert.AreEqual(relativePaths, actual);
        }

        [TestMethod]
        public void DisambiguateRelativePaths_SameFileSurfacedTwice_KeepsOriginalPathForBoth()
        {
            // Same physical file linked into the project more than once (e.g. a shared/linked
            // source file) -- this is the existing, legitimate dedup and must not be disambiguated.
            var relativePaths = new[] { Path.Combine("System", "Collections", "IEnumerable.cs"), Path.Combine("System", "Collections", "IEnumerable.cs") };
            var identityKeys = new[] { Path.Combine(TestRoot, "src", "shared", "IEnumerable.cs"), Path.Combine(TestRoot, "src", "shared", "IEnumerable.cs") };

            var actual = Paths.DisambiguateRelativePaths(relativePaths, identityKeys);

            CollectionAssert.AreEqual(relativePaths, actual);
        }

        [TestMethod]
        public void DisambiguateRelativePaths_DifferentFilesCollide_AssignsDeterministicSuffixes()
        {
            // Two genuinely different physical files that happen to resolve to the same
            // folders+filename (the #194 repro: two unrelated "IEnumerable.cs" files).
            var relativePaths = new[] { Path.Combine("System", "Collections", "IEnumerable.cs"), Path.Combine("System", "Collections", "IEnumerable.cs") };
            var identityKeys = new[] { Path.Combine(TestRoot, "src", "b", "IEnumerable.cs"), Path.Combine(TestRoot, "src", "a", "IEnumerable.cs") };

            var actual = Paths.DisambiguateRelativePaths(relativePaths, identityKeys);

            // Ordered by identity key ("a" before "b"), so the "a" file (index 1) keeps the
            // original path and the "b" file (index 0) gets the suffix -- regardless of input order.
            Assert.AreEqual(Path.Combine("System", "Collections", "IEnumerable_2.cs"), actual[0]);
            Assert.AreEqual(Path.Combine("System", "Collections", "IEnumerable.cs"), actual[1]);
        }

        [TestMethod]
        public void DisambiguateRelativePaths_ThreeDifferentFilesCollide_AssignsIncrementingSuffixes()
        {
            var relativePaths = new[]
            {
                Path.Combine("System", "Collections", "IEnumerable.cs"),
                Path.Combine("System", "Collections", "IEnumerable.cs"),
                Path.Combine("System", "Collections", "IEnumerable.cs"),
            };
            var identityKeys = new[]
            {
                Path.Combine(TestRoot, "src", "a", "IEnumerable.cs"),
                Path.Combine(TestRoot, "src", "b", "IEnumerable.cs"),
                Path.Combine(TestRoot, "src", "c", "IEnumerable.cs"),
            };

            var actual = Paths.DisambiguateRelativePaths(relativePaths, identityKeys);

            Assert.AreEqual(Path.Combine("System", "Collections", "IEnumerable.cs"), actual[0]);
            Assert.AreEqual(Path.Combine("System", "Collections", "IEnumerable_2.cs"), actual[1]);
            Assert.AreEqual(Path.Combine("System", "Collections", "IEnumerable_3.cs"), actual[2]);
        }

        [TestMethod]
        public void DisambiguateRelativePaths_CollisionAtProjectRoot_SuffixesFileNameOnly()
        {
            var relativePaths = new[] { "IEnumerable.cs", "IEnumerable.cs" };
            var identityKeys = new[] { Path.Combine(TestRoot, "src", "b", "IEnumerable.cs"), Path.Combine(TestRoot, "src", "a", "IEnumerable.cs") };

            var actual = Paths.DisambiguateRelativePaths(relativePaths, identityKeys);

            Assert.AreEqual("IEnumerable_2.cs", actual[0]);
            Assert.AreEqual("IEnumerable.cs", actual[1]);
        }

        [TestMethod]
        public void ShortenRelativePathIfNecessary_ShortPath_PassesThroughUnchanged()
        {
            var relativePath = Path.Combine("System", "Collections", "Generic", "List.cs");

            var actual = Paths.ShortenRelativePathIfNecessary(relativePath);

            Assert.AreEqual(relativePath, actual);
        }

        [TestMethod]
        public void ShortenRelativePathIfNecessary_LongSourceGeneratedPath_ShortensAndKeepsGroupingAndFileName()
        {
            // A representative source-generated document path: Generated\<generator assembly>\<fully
            // qualified generator type>\<hint name>. The fully-qualified type folder is what blows the
            // path budget.
            var relativePath = Path.Combine(
                "Generated",
                "System.Text.Json.SourceGeneration",
                "System.Text.Json.SourceGeneration.JsonSourceGenerator",
                "System.Text.Json.Serialization.JsonSerializerContext.SomeVeryLongContextTypeName.g.cs");
            Assert.IsTrue(relativePath.Length > 140, "test input should exceed the cap");

            var actual = Paths.ShortenRelativePathIfNecessary(relativePath);

            Assert.IsTrue(actual.Length <= 140, "shortened path must fit within the cap");
            Assert.IsTrue(actual.StartsWith("Generated" + Path.DirectorySeparatorChar, StringComparison.Ordinal), "top-level folder is preserved for grouping");
            Assert.IsTrue(actual.EndsWith(@"SomeVeryLongContextTypeName.g.cs", StringComparison.Ordinal), "leaf file name is preserved");
            // Deterministic: the same input always maps to the same shortened path so links stay stable.
            Assert.AreEqual(Paths.ShortenRelativePathIfNecessary(relativePath), actual);
        }

        [TestMethod]
        public void ShortenRelativePathIfNecessary_DistinctLongFolders_DoNotCollide()
        {
            var basePath = Path.Combine("Generated", "Some.Generator", "Some.Generator.TheGenerator");
            var padding = new string('x', 140);
            var a = Path.Combine(basePath, "A", padding + ".g.cs");
            var b = Path.Combine(basePath, "B", padding + ".g.cs");
            var shortenedA = Paths.ShortenRelativePathIfNecessary(a);
            var shortenedB = Paths.ShortenRelativePathIfNecessary(b);

            Assert.AreNotEqual(shortenedA, shortenedB);
        }

        [TestMethod]
        public void ShortenRelativePathIfNecessary_LongFileNameAtRoot_ShortensFileName()
        {
            var relativePath = new string('a', 200) + ".g.cs";

            var actual = Paths.ShortenRelativePathIfNecessary(relativePath);

            Assert.IsTrue(actual.Length <= 140, "shortened path must fit within the cap");
            Assert.IsTrue(actual.EndsWith(".cs", StringComparison.Ordinal), "extension is preserved");
        }

        [TestMethod]
        public void GetFullPathInFolderCone1()
        {
            var folder = Path.Combine(TestRoot, "a", "b");
            TestGetFullPathInFolderCone(
                folder,
                Path.Combine(TestRoot, "1.txt"),
                Path.Combine(folder, "1.txt"));
            TestGetFullPathInFolderCone(
                folder,
                Path.Combine(TestRoot, "a", "1.txt"),
                Path.Combine(folder, "1.txt"));
        }

        [TestMethod]
        public void GetFullPathInFolderCone_CaseDifferenceUsesPlatformPathSemantics()
        {
            var folder = Path.Combine(TestRoot, "case");
            var filePath = Path.Combine(TestRoot, "Case", "file.cs");
            var expected = OperatingSystem.IsWindows()
                ? Path.GetFullPath(filePath)
                : Path.Combine(Path.GetFullPath(folder), "Case", "file.cs");

            TestGetFullPathInFolderCone(folder, filePath, expected);
        }

        private void TestGetFullPathInFolderCone(string folder, string filePath, string expected)
        {
            var actual = Paths.GetFullPathInFolderCone(folder, filePath);
            Assert.AreEqual(expected, actual);
        }
    }
}
