using System.Windows;

namespace Coursia;

public partial class TutorialWindow : Window
{
    public TutorialWindow()
    {
        InitializeComponent();
    }

    private void Start_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
