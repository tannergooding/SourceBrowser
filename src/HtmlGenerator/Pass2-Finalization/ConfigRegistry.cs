using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    /// <summary>
    /// Maintains the shared, root-level Configs.txt that records every /config:&lt;name&gt; indexed into a
    /// given /out root. Separate /config:&lt;name&gt; invocations commonly run concurrently against the
    /// same /out (e.g. one process per platform build), so the read-modify-write-add-if-missing here is
    /// guarded by a named mutex and written via a temp file + atomic rename -- two concurrent runs can
    /// never lose each other's entry or observe a partially-written file.
    /// </summary>
    public static class ConfigRegistry
    {
        public const string ConfigsFileName = "Configs.txt";

        /// <summary>
        /// Adds <paramref name="configName"/> to outRoot/Configs.txt if it isn't already present. Does
        /// nothing when <paramref name="configName"/> is null/empty, so a default (no /config) run never
        /// creates Configs.txt and leaves the output tree exactly as before.
        /// </summary>
        public static void EnsureConfigRegistered(string outRoot, string configName)
        {
            if (string.IsNullOrEmpty(configName))
            {
                return;
            }

            Directory.CreateDirectory(outRoot);
            var configsFilePath = Path.Combine(outRoot, ConfigsFileName);

            using (var mutex = ConfigMergeCoordinator.CreateNamedMutex(configsFilePath))
            {
                mutex.WaitOne();
                try
                {
                    AddConfigIfMissing(configsFilePath, configName);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        /// <summary>
        /// Reads the currently-registered configs for a given /out root. Returns an empty list if no
        /// config has ever been registered (default/no-config runs never create the file).
        /// </summary>
        public static IReadOnlyList<string> GetRegisteredConfigs(string outRoot)
        {
            var configsFilePath = Path.Combine(outRoot, ConfigsFileName);
            if (!File.Exists(configsFilePath))
            {
                return Array.Empty<string>();
            }

            return File.ReadAllLines(configsFilePath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        }

        private static void AddConfigIfMissing(string configsFilePath, string configName)
        {
            var configs = File.Exists(configsFilePath)
                ? File.ReadAllLines(configsFilePath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList()
                : new List<string>();

            if (configs.Any(c => string.Equals(c, configName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            configs.Add(configName);

            var tempFilePath = configsFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllLines(tempFilePath, configs);
            File.Move(tempFilePath, configsFilePath, overwrite: true);
        }
    }
}
