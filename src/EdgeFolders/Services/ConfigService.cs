using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using EdgeFolders.Models;

namespace EdgeFolders.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _appDirectory;

    public ConfigService()
    {
        _appDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EdgeFolders");
        ConfigPath = Path.Combine(_appDirectory, "config.json");
    }

    public event EventHandler? ConfigChanged;

    public string ConfigPath { get; }
    public EdgeFoldersConfig Config { get; private set; } = new();

    public void Load()
    {
        Directory.CreateDirectory(_appDirectory);

        if (!File.Exists(ConfigPath))
        {
            Config = CreateDefaultConfig();
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            Config = JsonSerializer.Deserialize<EdgeFoldersConfig>(json, JsonOptions) ?? CreateDefaultConfig();
            Normalize(Config);
        }
        catch
        {
            var backupPath = Path.Combine(_appDirectory, $"config.broken.{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(ConfigPath, backupPath, overwrite: true);
            Config = CreateDefaultConfig();
            Save();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(_appDirectory);
        Normalize(Config);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, JsonOptions));
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OpenConfigFolder()
    {
        Directory.CreateDirectory(_appDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _appDirectory,
            UseShellExecute = true
        });
    }

    public LaunchItem CreateItemFromPath(string path)
    {
        return new LaunchItem
        {
            Title = GuessTitle(path),
            Path = path,
            WorkingDirectory = GuessWorkingDirectory(path)
        };
    }

    public FolderGroup CreateFolder(string name)
    {
        var accents = new[]
        {
            "#FFCA5F",
            "#63E6BE",
            "#FF8A65",
            "#B6E35A",
            "#7DD3FC",
            "#F0A6FF"
        };

        var index = Config.Folders.Count % accents.Length;
        return new FolderGroup
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Новая папка" : name.Trim(),
            Accent = accents[index]
        };
    }

    private static EdgeFoldersConfig CreateDefaultConfig()
    {
        return new EdgeFoldersConfig
        {
            Folders =
            [
                new FolderGroup
                {
                    Name = "Быстрое",
                    Accent = "#FFCA5F",
                    Items =
                    [
                        new LaunchItem { Title = "Проводник", Path = "explorer.exe" },
                        new LaunchItem { Title = "Блокнот", Path = "notepad.exe" },
                        new LaunchItem { Title = "Настройки", Path = "ms-settings:" }
                    ]
                },
                new FolderGroup
                {
                    Name = "Работа",
                    Accent = "#63E6BE",
                    Items = []
                },
                new FolderGroup
                {
                    Name = "Игры",
                    Accent = "#FF8A65",
                    Items = []
                }
            ]
        };
    }

    private static void Normalize(EdgeFoldersConfig config)
    {
        config.Overlay ??= new OverlaySettings();
        config.Folders ??= [];

        config.Overlay.PanelHeight = Math.Clamp(config.Overlay.PanelHeight, 210, 420);
        config.Overlay.EdgeHotZone = Math.Clamp(config.Overlay.EdgeHotZone, 5, 18);
        config.Overlay.HideDelayMs = Math.Clamp(config.Overlay.HideDelayMs, 260, 1600);
        config.Overlay.AnimationMs = Math.Clamp(config.Overlay.AnimationMs, 80, 420);
        config.Overlay.PanelMaxWidth = Math.Clamp(config.Overlay.PanelMaxWidth, 720, 1800);

        foreach (var folder in config.Folders)
        {
            if (string.IsNullOrWhiteSpace(folder.Id))
            {
                folder.Id = Guid.NewGuid().ToString("N");
            }

            folder.Name = string.IsNullOrWhiteSpace(folder.Name) ? "Папка" : folder.Name.Trim();
            folder.Accent = string.IsNullOrWhiteSpace(folder.Accent) ? "#FFCA5F" : folder.Accent;
            folder.Width = folder.Width <= 0 ? 276 : folder.Width;
            folder.Height = folder.Height <= 0 ? 232 : folder.Height;
            folder.Items ??= [];

            foreach (var item in folder.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    item.Id = Guid.NewGuid().ToString("N");
                }

                item.Title = string.IsNullOrWhiteSpace(item.Title) ? GuessTitle(item.Path) : item.Title.Trim();
                item.Path = item.Path?.Trim() ?? "";
                item.Arguments = item.Arguments?.Trim() ?? "";
                item.WorkingDirectory = item.WorkingDirectory?.Trim() ?? "";
            }
        }
    }

    private static string GuessTitle(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Приложение";
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return string.IsNullOrWhiteSpace(uri.Host) ? uri.Scheme : uri.Host.Replace("www.", "");
        }

        if (Directory.Exists(path))
        {
            return new DirectoryInfo(path).Name;
        }

        if (File.Exists(path))
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(path);
            if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
            {
                return versionInfo.FileDescription.Trim();
            }
        }

        var fileName = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(fileName) ? path : fileName;
    }

    private static string GuessWorkingDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return "";
        }

        if (Directory.Exists(path))
        {
            return path;
        }

        return File.Exists(path) ? Path.GetDirectoryName(path) ?? "" : "";
    }
}
