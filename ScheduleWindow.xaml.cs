using System.Globalization;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Coursia;

public partial class ScheduleWindow : Window
{
    private readonly LibraryData library;
    private readonly Action save;
    private readonly Action refresh;
    private readonly Func<bool> chooseStorage;

    public ScheduleWindow(LibraryData library, Action save, Action refresh, Func<bool> chooseStorage)
    {
        InitializeComponent();
        this.library = library;
        this.save = save;
        this.refresh = refresh;
        this.chooseStorage = chooseStorage;
        DayPicker.SelectedIndex = Math.Max(0, (int)DateTime.Today.DayOfWeek - 1);
        if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday) DayPicker.SelectedIndex = 6;
        RenderEntries();
        UpdatePdfStatus();
    }

    private void RenderEntries()
    {
        EntriesList.Items.Clear();
        foreach (var entry in library.Schedule.OrderBy(item => item.Day).ThenBy(item => item.StartMinutes))
        {
            var subject = FindSubject(entry.Subject);
            var item = new ListBoxItem { Content = $"{DayName(entry.Day)}  ·  {FormatTime(entry.StartMinutes)} - {FormatTime(entry.EndMinutes)}  ·  {entry.Subject}{(subject is null ? "" : $"  →  {subject.Name}")}", Padding = new Thickness(12, 9, 12, 9), Background = Brushes.White, Margin = new Thickness(0, 0, 0, 6), Tag = entry };
            item.MouseDoubleClick += (_, _) => RemoveEntry(entry);
            EntriesList.Items.Add(item);
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (DayPicker.SelectedItem is not ComboBoxItem dayItem || !TimeSpan.TryParse(StartTime.Text.Trim(), CultureInfo.InvariantCulture, out var start) || !TimeSpan.TryParse(EndTime.Text.Trim(), CultureInfo.InvariantCulture, out var end) || end <= start || string.IsNullOrWhiteSpace(SubjectInput.Text))
        {
            MessageBox.Show("Vérifie le jour, les heures et la matière.", "Emploi du temps", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        library.Schedule.Add(new TimetableEntry { Day = Enum.Parse<DayOfWeek>((string)dayItem.Tag), StartMinutes = (int)start.TotalMinutes, EndMinutes = (int)end.TotalMinutes, Subject = SubjectInput.Text.Trim() });
        save();
        RenderEntries();
        refresh();
    }

    private void RemoveEntry(TimetableEntry entry)
    {
        if (MessageBox.Show("Supprimer ce cours de l'emploi du temps ?", "Emploi du temps", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        library.Schedule.Remove(entry);
        save();
        RenderEntries();
        refresh();
    }

    private void ImportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(library.StorageFolder) && !chooseStorage()) return;
        var dialog = new OpenFileDialog { Title = "Ajouter le PDF de ton emploi du temps", Filter = "PDF|*.pdf" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var folder = Path.Combine(library.StorageFolder!, "Emploi du temps");
            Directory.CreateDirectory(folder);
            var destination = GetUniquePath(folder, Path.GetFileName(dialog.FileName));
            File.Copy(dialog.FileName, destination);
            library.SchedulePdfPath = destination;
            save();
            UpdatePdfStatus();
            refresh();
            Process.Start(new ProcessStartInfo { FileName = destination, UseShellExecute = true });
        }
        catch (Exception error)
        {
            MessageBox.Show($"Le PDF n'a pas pu être ajouté : {error.Message}", "Import impossible", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdatePdfStatus()
    {
        var exists = !string.IsNullOrWhiteSpace(library.SchedulePdfPath) && File.Exists(library.SchedulePdfPath);
        PdfStatus.Text = exists ? $"PDF : {Path.GetFileName(library.SchedulePdfPath)}" : "Aucun PDF d'emploi du temps ajouté";
        OpenPdfButton.Visibility = exists ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenPdf_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(library.SchedulePdfPath) && File.Exists(library.SchedulePdfPath)) Process.Start(new ProcessStartInfo { FileName = library.SchedulePdfPath, UseShellExecute = true });
    }

    private static string GetUniquePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        var index = 1;
        while (File.Exists(path)) path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(fileName)} ({index++}){Path.GetExtension(fileName)}");
        return path;
    }

    private StudySection? FindSubject(string value) => library.Sections.FirstOrDefault(section => Normalize(section.Name).Contains(Normalize(value)) || Normalize(value).Contains(Normalize(section.Name)) || AbbreviationMatches(value, section.Name));
    private static bool AbbreviationMatches(string abbreviation, string name) => Normalize(name).Split(' ', '-', '.').Any(word => word.StartsWith(Normalize(abbreviation), StringComparison.OrdinalIgnoreCase));
    private static string Normalize(string value) => new string(value.Normalize().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    private static string DayName(DayOfWeek day) => day switch { DayOfWeek.Monday => "Lundi", DayOfWeek.Tuesday => "Mardi", DayOfWeek.Wednesday => "Mercredi", DayOfWeek.Thursday => "Jeudi", DayOfWeek.Friday => "Vendredi", DayOfWeek.Saturday => "Samedi", _ => "Dimanche" };
    private static string FormatTime(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
