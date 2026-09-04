using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace Coursia;

public partial class SettingsWindow : Window
{
    public string Accent { get; private set; }
    public string AppIconValue { get; private set; }
    public bool IsCompact { get; private set; }
    public bool ShowFileExtensions { get; private set; }
    public bool ReplayTutorial { get; private set; }
    public bool ResetRequested { get; private set; }
    public bool BackupRequested { get; private set; }
    public bool RestoreRequested { get; private set; }
    public string UserName { get; private set; }

    public SettingsWindow(string accent, string icon, bool isCompact, bool showFileExtensions, string userName)
    {
        InitializeComponent();
        Accent = accent;
        AppIconValue = icon;
        IsCompact = isCompact;
        ShowFileExtensions = showFileExtensions;
        UserName = userName;
        UserNameInput.Text = userName;
        CompactMode.IsChecked = isCompact;
        ShowExtensions.IsChecked = showFileExtensions;
        AccentPicker.SelectedValue = accent;
    }

    private void Icon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string icon) AppIconValue = icon;
    }

    private void Tutorial_Click(object sender, RoutedEventArgs e)
    {
        ReplayTutorial = true;
        DialogResult = true;
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Supprimer toutes les matières, préférences et copies de fichiers ? Cette action est irréversible.", "Réinitialiser Coursia", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        ResetRequested = true;
        DialogResult = true;
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        BackupRequested = true;
        DialogResult = true;
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        RestoreRequested = true;
        DialogResult = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Accent = AccentPicker.SelectedValue as string ?? "#2563EB";
        IsCompact = CompactMode.IsChecked == true;
        ShowFileExtensions = ShowExtensions.IsChecked == true;
        UserName = UserNameInput.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
