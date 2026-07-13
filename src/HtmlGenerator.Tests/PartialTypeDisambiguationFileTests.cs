using System.Collections.Generic;
using System.IO;
using Microsoft.SourceBrowser.HtmlGenerator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace HtmlGenerator.Tests
{
    [TestClass]
    public class PartialTypeDisambiguationFileTests
    {
        private string testRoot;

        [TestInitialize]
        public void Setup()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "sb-partialtype-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }

        private string DisambiguationFilePath(string symbolId) =>
            Path.Combine(testRoot, Constants.PartialResolvingFileName, symbolId) + ".html";

        [TestMethod]
        public void Untagged_overload_and_null_config_tags_produce_byte_identical_output()
        {
            // Back-compat requirement: when there is no config involved (the overwhelming majority of
            // real usage today -- ordinary partial types with no config concept at all), the new
            // config-aware overload must render exactly what the original untagged overload does.
            var filePaths = new[] { "Foo.cs", "Foo.Partial.cs" };

            Markup.GeneratePartialTypeDisambiguationFile(testRoot, testRoot, "abc123", filePaths);
            var untagged = File.ReadAllText(DisambiguationFilePath("abc123"));
            File.Delete(DisambiguationFilePath("abc123"));

            Markup.GeneratePartialTypeDisambiguationFile(testRoot, testRoot, "abc123", filePaths, configTagsByFilePath: null);
            var explicitNull = File.ReadAllText(DisambiguationFilePath("abc123"));

            explicitNull.ShouldBe(untagged);
        }

        [TestMethod]
        public void Config_tags_are_rendered_alongside_each_location_link()
        {
            var filePaths = new[] { "Environment.Windows.cs", "Environment.Unix.cs" };
            var configTags = new Dictionary<string, IEnumerable<string>>
            {
                ["Environment.Windows.cs"] = new[] { "windows" },
                ["Environment.Unix.cs"] = new[] { "linux", "mac" },
            };

            Markup.GeneratePartialTypeDisambiguationFile(testRoot, testRoot, "envnewline", filePaths, configTags);

            var content = File.ReadAllText(DisambiguationFilePath("envnewline"));

            content.ShouldContain("Environment.Windows.cs");
            content.ShouldContain("Environment.Unix.cs");
            content.ShouldContain("[windows]");
            content.ShouldContain("[linux, mac]");
        }

        [TestMethod]
        public void A_file_with_no_config_tag_entry_renders_without_a_tag()
        {
            var filePaths = new[] { "Foo.cs" };
            var configTags = new Dictionary<string, IEnumerable<string>>();

            Markup.GeneratePartialTypeDisambiguationFile(testRoot, testRoot, "abc123", filePaths, configTags);

            var content = File.ReadAllText(DisambiguationFilePath("abc123"));
            content.ShouldNotContain("partialTypeConfigTag");
        }
    }
}
