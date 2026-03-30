using System;
using System.IO;
using System.Text.Json;
using REFrameworkNET;

namespace RE3DotNet_CC
{
    /// <summary>
    /// Plugin configuration that persists to JSON file
    /// </summary>
    public class PluginConfig
    {
        private static readonly string ConfigFilePath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
            "RE9-CrowdControl.json"
        );

        public bool ShowNameplates { get; set; } = false;
        public bool EnableLogging { get; set; } = false;
        public int MaxSpawnedEnemies { get; set; } = 20;
        public bool ShowSettingsUI { get; set; } = false;
        public bool AllWeapons { get; set; } = false;
        public bool AlwaysAllowSpawns { get; set; } = false;
        public bool AllowNoirCostume { get; set; } = true;

        /// <summary>
        /// Load configuration from JSON file
        /// </summary>
        public static PluginConfig Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<PluginConfig>(json);
                    if (config != null)
                    {
                        Logger.SetEnabled(config.EnableLogging);
                        Logger.LogInfo($"RE9DotNet-CC: Loaded config from {ConfigFilePath}");
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"RE9DotNet-CC: Error loading config - {ex.Message}");
            }

            // Return default config if file doesn't exist or failed to load
            Logger.SetEnabled(false);
            Logger.LogInfo("RE9DotNet-CC: Using default config");
            return new PluginConfig();
        }

        /// <summary>
        /// Save configuration to JSON file
        /// </summary>
        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigFilePath, json);
                Logger.SetEnabled(EnableLogging);
                Logger.LogInfo($"RE9DotNet-CC: Saved config to {ConfigFilePath}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"RE9DotNet-CC: Error saving config - {ex.Message}");
            }
        }
    }
}



