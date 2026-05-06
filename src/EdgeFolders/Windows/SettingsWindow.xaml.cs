using System.IO;
using System.Windows;
using EdgeFolders.Models;
using EdgeFolders.Services;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;
using WinForms = System.Windows.Forms;

namespace EdgeFolders.Windows;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;
    private readonly StartupService _startupService;
    private bool _isRefreshing;

    public SettingsWindow(ConfigService configService, StartupService startupService)
    {
        _configService = configService;
        _startupService = startupService;

        InitializeComponent();
        RefreshAll();
    }

    private FolderGroup? SelectedFolder => FoldersList.SelectedItem as FolderGroup;
    private LaunchItem? SelectedItem => ItemsList.SelectedItem as LaunchItem;

    private void RefreshAll(string? selectedFolderId = null)
    {
        _isRefreshing = true;

        FoldersList.ItemsSource = null;
        FoldersList.ItemsSource = _configService.Config.Folders;

        var folderToSelect = _configService.Config.Folders.FirstOrDefault(folder => folder.Id == selectedFolderId)
                             ?? _configService.Config.Folders.FirstOrDefault();
        FoldersList.SelectedItem = folderToSelect;

        EnableEdgeCheck.IsChecked = _configService.Config.EnableEdgeHover;
        StartWithWindowsCheck.IsChecked = _startupService.IsEnabled();
        PanelHeightBox.Text = _configService.Config.Overlay.PanelHeight.ToString();
        HotZoneBox.Text = _configService.Config.Overlay.EdgeHotZone.ToString();
        HideDelayBox.Text = _configService.Config.Overlay.HideDelayMs.ToString();

        _isRefreshing = false;
        RefreshItems();
    }

    private void RefreshItems()
    {
        if (_isRefreshing)
        {
            return;
        }

        var folder = SelectedFolder;
        ItemsTitle.Text = folder is null ? "Элементы" : $"Элементы: {folder.Name}";
        ItemsList.ItemsSource = null;
        ItemsList.ItemsSource = folder?.Items;
    }

    private void FoldersList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshItems();
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var name = InputDialog.Prompt(this, "Новая папка", "Название папки", "Новая папка");
        if (name is null)
        {
            return;
        }

        var folder = _configService.CreateFolder(name);
        _configService.Config.Folders.Add(folder);
        _configService.Save();
        RefreshAll(folder.Id);
    }

    private void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = SelectedFolder;
        if (folder is null)
        {
            return;
        }

        var name = InputDialog.Prompt(this, "Переименовать", "Новое название папки", folder.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        folder.Name = name.Trim();
        _configService.Save();
        RefreshAll(folder.Id);
    }

    private void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = SelectedFolder;
        if (folder is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Удалить папку \"{folder.Name}\" и все ее элементы?",
            "EdgeFolders",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _configService.Config.Folders.Remove(folder);
        _configService.Save();
        RefreshAll();
    }

    private void MoveFolderUp_Click(object sender, RoutedEventArgs e)
    {
        MoveFolder(-1);
    }

    private void MoveFolderDown_Click(object sender, RoutedEventArgs e)
    {
        MoveFolder(1);
    }

    private void MoveFolder(int direction)
    {
        var folder = SelectedFolder;
        if (folder is null)
        {
            return;
        }

        var list = _configService.Config.Folders;
        var oldIndex = list.IndexOf(folder);
        var newIndex = oldIndex + direction;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= list.Count)
        {
            return;
        }

        list.RemoveAt(oldIndex);
        list.Insert(newIndex, folder);
        _configService.Save();
        RefreshAll(folder.Id);
    }

    private void AddApp_Click(object sender, RoutedEventArgs e)
    {
        var folder = EnsureSelectedFolder();
        if (folder is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Приложения и ярлыки|*.exe;*.lnk;*.bat;*.cmd;*.ps1|Все файлы|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        AddPaths(folder, dialog.FileNames);
    }

    private void AddFolderItem_Click(object sender, RoutedEventArgs e)
    {
        var folder = EnsureSelectedFolder();
        if (folder is null)
        {
            return;
        }

        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Выбери папку, которую нужно открывать из EdgeFolders",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            AddPaths(folder, [dialog.SelectedPath]);
        }
    }

    private void AddUrl_Click(object sender, RoutedEventArgs e)
    {
        var folder = EnsureSelectedFolder();
        if (folder is null)
        {
            return;
        }

        var url = InputDialog.Prompt(this, "Добавить URL", "Вставь ссылку или ms-settings URI", "https://");
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        folder.Items.Add(new LaunchItem
        {
            Title = GuessTitleFromUri(url),
            Path = url.Trim()
        });
        _configService.Save();
        RefreshAll(folder.Id);
    }

    private void EditItem_Click(object sender, RoutedEventArgs e)
    {
        var folder = SelectedFolder;
        var item = SelectedItem;
        if (folder is null || item is null)
        {
            return;
        }

        var dialog = new ItemEditorWindow(item)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        item.Title = dialog.Item.Title;
        item.Path = dialog.Item.Path;
        item.Arguments = dialog.Item.Arguments;
        item.WorkingDirectory = dialog.Item.WorkingDirectory;
        item.RunAsAdmin = dialog.Item.RunAsAdmin;

        _configService.Save();
        RefreshAll(folder.Id);
        ItemsList.SelectedItem = item;
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        var folder = SelectedFolder;
        var item = SelectedItem;
        if (folder is null || item is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Удалить \"{item.Title}\"?",
            "EdgeFolders",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        folder.Items.Remove(item);
        _configService.Save();
        RefreshAll(folder.Id);
    }

    private void MoveItemUp_Click(object sender, RoutedEventArgs e)
    {
        MoveItem(-1);
    }

    private void MoveItemDown_Click(object sender, RoutedEventArgs e)
    {
        MoveItem(1);
    }

    private void MoveItem(int direction)
    {
        var folder = SelectedFolder;
        var item = SelectedItem;
        if (folder is null || item is null)
        {
            return;
        }

        var oldIndex = folder.Items.IndexOf(item);
        var newIndex = oldIndex + direction;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= folder.Items.Count)
        {
            return;
        }

        folder.Items.RemoveAt(oldIndex);
        folder.Items.Insert(newIndex, item);
        _configService.Save();
        RefreshAll(folder.Id);
        ItemsList.SelectedItem = item;
    }

    private void ItemsList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        var folder = EnsureSelectedFolder();
        if (folder is null || !e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
        AddPaths(folder, files);
        e.Handled = true;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _configService.Config.EnableEdgeHover = EnableEdgeCheck.IsChecked == true;
        _configService.Config.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        _configService.Config.Overlay.PanelHeight = ReadInt(PanelHeightBox.Text, _configService.Config.Overlay.PanelHeight, 210, 420);
        _configService.Config.Overlay.EdgeHotZone = ReadInt(HotZoneBox.Text, _configService.Config.Overlay.EdgeHotZone, 5, 18);
        _configService.Config.Overlay.HideDelayMs = ReadInt(HideDelayBox.Text, _configService.Config.Overlay.HideDelayMs, 260, 1600);

        try
        {
            _startupService.SetEnabled(_configService.Config.StartWithWindows);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Не получилось обновить автозапуск:\n{ex.Message}", "EdgeFolders", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _configService.Save();
        RefreshAll(SelectedFolder?.Id);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        _configService.OpenConfigFolder();
    }

    private FolderGroup? EnsureSelectedFolder()
    {
        if (SelectedFolder is not null)
        {
            return SelectedFolder;
        }

        var folder = _configService.CreateFolder("Быстрое");
        _configService.Config.Folders.Add(folder);
        _configService.Save();
        RefreshAll(folder.Id);
        return folder;
    }

    private void AddPaths(FolderGroup folder, IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            folder.Items.Add(_configService.CreateItemFromPath(path));
        }

        _configService.Save();
        RefreshAll(folder.Id);
    }

    private static int ReadInt(string text, int fallback, int min, int max)
    {
        return int.TryParse(text, out var value) ? Math.Clamp(value, min, max) : fallback;
    }

    private static string GuessTitleFromUri(string uriText)
    {
        if (Uri.TryCreate(uriText.Trim(), UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host.Replace("www.", "");
        }

        var fileName = Path.GetFileNameWithoutExtension(uriText);
        return string.IsNullOrWhiteSpace(fileName) ? "Ссылка" : fileName;
    }
}
