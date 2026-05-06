using System.IO;
using System.Windows;
using EdgeFolders.Models;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;
using WinForms = System.Windows.Forms;

namespace EdgeFolders.Windows;

public partial class ItemEditorWindow : Window
{
    public ItemEditorWindow(LaunchItem item)
    {
        InitializeComponent();

        Item = new LaunchItem
        {
            Id = item.Id,
            Title = item.Title,
            Path = item.Path,
            Arguments = item.Arguments,
            WorkingDirectory = item.WorkingDirectory,
            RunAsAdmin = item.RunAsAdmin
        };

        TitleBox.Text = Item.Title;
        PathBox.Text = Item.Path;
        ArgumentsBox.Text = Item.Arguments;
        WorkingDirectoryBox.Text = Item.WorkingDirectory;
        RunAsAdminCheck.IsChecked = Item.RunAsAdmin;
    }

    public LaunchItem Item { get; }

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Приложения и ярлыки|*.exe;*.lnk;*.bat;*.cmd;*.ps1|Все файлы|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            PathBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                TitleBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }

            if (string.IsNullOrWhiteSpace(WorkingDirectoryBox.Text))
            {
                WorkingDirectoryBox.Text = Path.GetDirectoryName(dialog.FileName) ?? "";
            }
        }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Выбери рабочую папку или папку для запуска",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            WorkingDirectoryBox.Text = dialog.SelectedPath;
            if (string.IsNullOrWhiteSpace(PathBox.Text))
            {
                PathBox.Text = dialog.SelectedPath;
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PathBox.Text))
        {
            MessageBox.Show(this, "Укажи путь или URL.", "EdgeFolders", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Item.Title = string.IsNullOrWhiteSpace(TitleBox.Text)
            ? Path.GetFileNameWithoutExtension(PathBox.Text)
            : TitleBox.Text.Trim();
        Item.Path = PathBox.Text.Trim();
        Item.Arguments = ArgumentsBox.Text.Trim();
        Item.WorkingDirectory = WorkingDirectoryBox.Text.Trim();
        Item.RunAsAdmin = RunAsAdminCheck.IsChecked == true;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
