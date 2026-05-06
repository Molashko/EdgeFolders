using System.Diagnostics;
using System.IO;
using System.Windows;
using EdgeFolders.Models;
using MessageBox = System.Windows.MessageBox;

namespace EdgeFolders.Services;

public sealed class LaunchService
{
    public void Launch(LaunchItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Path))
        {
            MessageBox.Show("У этого элемента не указан путь.", "EdgeFolders", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = item.Path,
                Arguments = item.Arguments ?? "",
                UseShellExecute = true,
                WorkingDirectory = ResolveWorkingDirectory(item)
            };

            if (item.RunAsAdmin)
            {
                startInfo.Verb = "runas";
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не получилось запустить:\n{item.Path}\n\n{ex.Message}",
                "EdgeFolders",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string ResolveWorkingDirectory(LaunchItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.WorkingDirectory) && Directory.Exists(item.WorkingDirectory))
        {
            return item.WorkingDirectory;
        }

        if (File.Exists(item.Path))
        {
            return Path.GetDirectoryName(item.Path) ?? "";
        }

        return "";
    }
}
