namespace EdgeFolders.Models;

public sealed class EdgeFoldersConfig
{
    public int Version { get; set; } = 1;
    public bool EnableEdgeHover { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public OverlaySettings Overlay { get; set; } = new();
    public List<FolderGroup> Folders { get; set; } = [];
}
