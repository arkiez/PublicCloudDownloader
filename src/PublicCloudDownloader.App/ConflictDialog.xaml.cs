using System.Windows;
using PublicCloudDownloader.Core.Models;

namespace PublicCloudDownloader.App;

public partial class ConflictDialog : Window
{
    public ExistingFilePolicy Policy { get; private set; } = ExistingFilePolicy.Cancel;
    public ConflictDialog(IReadOnlyList<FileCollision> conflicts)
    {
        InitializeComponent();
        Description.Text = $"{conflicts.Count:N0} existing file{(conflicts.Count == 1 ? "" : "s")} would be affected:";
        foreach (var conflict in conflicts) ConflictList.Items.Add(conflict.Item.RelativeOutputPath);
        Loaded += (_, _) => CancelButton.Focus();
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
    private void Skip_Click(object sender, RoutedEventArgs e) { Policy = ExistingFilePolicy.Skip; DialogResult = true; }
    private void Overwrite_Click(object sender, RoutedEventArgs e) { Policy = ExistingFilePolicy.Overwrite; DialogResult = true; }
}
