namespace EdgeFolders.Models;

public sealed class LaunchItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "App";
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public bool RunAsAdmin { get; set; }

    public override string ToString() => Title;
}
