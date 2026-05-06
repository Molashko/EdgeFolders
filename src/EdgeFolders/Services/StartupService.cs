using Microsoft.Win32;
using System.Reflection;
using System.IO;

namespace EdgeFolders.Services;

public sealed class StartupService
{
    private const string AppName = "EdgeFolders";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                       ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(AppName, BuildStartupCommand());
            return;
        }

        key.DeleteValue(AppName, throwOnMissingValue: false);
    }

    private static string BuildStartupCommand()
    {
        var processPath = Environment.ProcessPath
                          ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                          ?? "";
        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "";
        var assemblyPath = string.IsNullOrWhiteSpace(assemblyName)
            ? ""
            : Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");

        if (processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase)
            && File.Exists(assemblyPath)
            && assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return $"\"{processPath}\" \"{assemblyPath}\"";
        }

        return $"\"{processPath}\"";
    }
}
