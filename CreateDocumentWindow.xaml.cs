using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace Coursia;

public partial class CreateDocumentWindow : Window
{
    public string FileName => FileNameInput.Text.Trim();
    public string FileType => PowerPointOption.IsChecked == true ? "pptx" : TextOption.IsChecked == true ? "txt" : "docx";

    public CreateDocumentWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => { FileNameInput.Focus(); FileNameInput.SelectAll(); };
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FileName))
        {
            MessageBox.Show("Donne un nom au fichier.", "Nom manquant", MessageBoxButton.OK, MessageBoxImage.Information);
            FileNameInput.Focus();
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
