using System.Windows;

namespace Coursia;

public partial class NotesWindow : Window
{
    public string Notes => NotesInput.Text;

    public NotesWindow(StudySection section)
    {
        InitializeComponent();
        TitleText.Text = $"Notes · {section.Name}";
        NotesInput.Text = section.Notes;
        Loaded += (_, _) => NotesInput.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
