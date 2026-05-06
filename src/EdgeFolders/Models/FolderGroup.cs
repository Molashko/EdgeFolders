using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EdgeFolders.Models;

public sealed class FolderGroup : INotifyPropertyChanged
{
    private double _width = 276;
    private double _height = 232;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Folder";
    public string Accent { get; set; } = "#FFCA5F";

    public double Width
    {
        get => _width;
        set
        {
            var next = Math.Clamp(value, 220, 560);
            if (Math.Abs(_width - next) > 0.1)
            {
                _width = next;
                OnPropertyChanged();
            }
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            var next = Math.Clamp(value, 190, 420);
            if (Math.Abs(_height - next) > 0.1)
            {
                _height = next;
                OnPropertyChanged();
            }
        }
    }

    public List<LaunchItem> Items { get; set; } = [];

    public override string ToString() => Name;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
