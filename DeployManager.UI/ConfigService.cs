using System.Text.Json;

namespace DeployManager.UI;

public class ConfigService
{
    private const string configFileName = "appsettings.json";
    public static readonly string ConfigFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeployManager", configFileName);

    public string RepoPath { get; set; } = string.Empty;
    public string BranchName { get; set; } = "main"; // Default branch name
    public string SelectedEnvironment { get; set; } = "test";

    public string[] Environments { get; set; } =
        {
            "dev-test",
            "traffic-test",
            "test",
            "training",
            "preprod",
            "prod",
    };

    public static ConfigService LoadConfig()
    {
        var configDirectory = Path.GetDirectoryName(ConfigFilePath)!;
        if (!Directory.Exists(configDirectory))
        {
            Directory.CreateDirectory(configDirectory);
        }

        if (!File.Exists(ConfigFilePath)) return new ConfigService();

        var json = File.ReadAllText(ConfigFilePath);
        var config = JsonSerializer.Deserialize<ConfigService>(json);
        return config ?? new ConfigService();
    }

    public void SaveConfig()
    {
        var configDirectory = Path.GetDirectoryName(ConfigFilePath)!;
        if (!Directory.Exists(configDirectory))
        {
            Directory.CreateDirectory(configDirectory);
        }

        var json = JsonSerializer.Serialize(this);
        File.WriteAllText(ConfigFilePath, json);
    }
}