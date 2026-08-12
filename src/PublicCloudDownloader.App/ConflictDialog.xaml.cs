using System.Windows;
using PublicCloudDownloader.Core.Models;

namespace PublicCloudDownloader.App;

public partial class ConflictDialog : Window
{
    public ExistingFilePolicy Policy { get; private set; } = ExistingFilePolicy.Cancel;
    public ConflictDialog(int count) { InitializeComponent(); Description.Text = $"{count:N0} existing file{(count == 1 ? "" : "s")} would be affected."; }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
    private void Skip_Click(object sender, RoutedEventArgs e) { Policy = ExistingFilePolicy.Skip; DialogResult = true; }
    private void Overwrite_Click(object sender, RoutedEventArgs e) { Policy = ExistingFilePolicy.Overwrite; DialogResult = true; }
}
