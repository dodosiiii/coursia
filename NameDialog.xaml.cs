using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace Coursia;

public partial class NameDialog : Window
{
    public string Value => NameInput.Text.Trim();
    public string SelectedIcon { get; private set; } = "◈";
    public string SelectedColor { get; private set; } = "#2563EB";

    public NameDialog(string title, string hint, string defaultValue, string icon = "◈", string color = "#2563EB", bool profileOnly = false)
    {
        InitializeComponent();
        SelectedIcon = icon;
        SelectedColor = color;
        DialogTitle.Text = title;
        DialogHint.Text = hint;
        NameInput.Text = defaultValue;
        if (profileOnly)
        {
            IconLabel.Visibility = Visibility.Collapsed;
            IconOptions.Visibility = Visibility.Collapsed;
            CustomizationOptions.Visibility = Visibility.Collapsed;
            Height = 250;
        }
        Loaded += (_, _) => { NameInput.Focus(); NameInput.SelectAll(); };
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            MessageBox.Show("Écris un nom avant de continuer.", "Nom manquant", MessageBoxButton.OK, MessageBoxImage.Information);
            NameInput.Focus();
            return;
        }
        DialogResult = true;
    }

    private void Icon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string icon) SelectedIcon = icon;
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string color) SelectedColor = color;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
