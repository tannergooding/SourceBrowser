using System.IO;
using Microsoft.SourceBrowser.HtmlGenerator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace HtmlGenerator.Tests
{
    [TestClass]
    public class ConfigRegistryTests
    {
        private string tempRoot;

        [TestInitialize]
        public void Setup()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "ConfigRegistryTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        [TestMethod]
        public void NoConfigRegistered_ConfigsFileNeverCreated()
        {
            ConfigRegistry.EnsureConfigRegistered(tempRoot, null);
            ConfigRegistry.EnsureConfigRegistered(tempRoot, string.Empty);

            File.Exists(Path.Combine(tempRoot, ConfigRegistry.ConfigsFileName)).ShouldBeFalse();
            ConfigRegistry.GetRegisteredConfigs(tempRoot).ShouldBeEmpty();
        }

        [TestMethod]
        public void SingleConfig_IsRegistered()
        {
            ConfigRegistry.EnsureConfigRegistered(tempRoot, "windows");

            ConfigRegistry.GetRegisteredConfigs(tempRoot).ShouldBe(new[] { "windows" });
        }

        [TestMethod]
        public void RegisteringSameConfigTwice_DoesNotDuplicate()
        {
            ConfigRegistry.EnsureConfigRegistered(tempRoot, "windows");
            ConfigRegistry.EnsureConfigRegistered(tempRoot, "windows");

            ConfigRegistry.GetRegisteredConfigs(tempRoot).ShouldBe(new[] { "windows" });
        }

        [TestMethod]
        public void RegisteringSameConfigDifferentCase_DoesNotDuplicate()
        {
            ConfigRegistry.EnsureConfigRegistered(tempRoot, "windows");
            ConfigRegistry.EnsureConfigRegistered(tempRoot, "WINDOWS");

            ConfigRegistry.GetRegisteredConfigs(tempRoot).ShouldBe(new[] { "windows" });
        }

        [TestMethod]
        public void MultipleConfigs_AllRetained()
        {
            ConfigRegistry.EnsureConfigRegistered(tempRoot, "windows");
            ConfigRegistry.EnsureConfigRegistered(tempRoot, "linux");
            ConfigRegistry.EnsureConfigRegistered(tempRoot, "mac");

            var configs = ConfigRegistry.GetRegisteredConfigs(tempRoot);
            configs.Count.ShouldBe(3);
            configs.ShouldContain("windows");
            configs.ShouldContain("linux");
            configs.ShouldContain("mac");
        }

        [TestMethod]
        public void ConcurrentRegistrations_FromManyThreads_LoseNoEntries()
        {
            const int threadCount = 16;
            var threads = new System.Threading.Thread[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                var configName = "config" + i;
                threads[i] = new System.Threading.Thread(() => ConfigRegistry.EnsureConfigRegistered(tempRoot, configName));
            }

            foreach (var t in threads)
            {
                t.Start();
            }

            foreach (var t in threads)
            {
                t.Join();
            }

            var configs = ConfigRegistry.GetRegisteredConfigs(tempRoot);
            configs.Count.ShouldBe(threadCount);
            for (int i = 0; i < threadCount; i++)
            {
                configs.ShouldContain("config" + i);
            }
        }
    }
}
