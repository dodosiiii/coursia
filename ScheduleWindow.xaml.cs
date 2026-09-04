using System.Globalization;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

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
        if (library.ScheduleParserVersion < 4 && !string.IsNullOrWhiteSpace(library.SchedulePdfPath) && File.Exists(library.SchedulePdfPath))
        {
            library.Schedule.Clear();
            var importedEntries = ImportEntriesFromPdf(library.SchedulePdfPath);
            library.ScheduleParserVersion = 4;
            save();
        }
        DayPicker.SelectedIndex = Math.Max(0, (int)DateTime.Today.DayOfWeek - 1);
        if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday) DayPicker.SelectedIndex = 6;
        WeekPicker.SelectedIndex = CurrentWeekType() == "A" ? 0 : 1;
        RenderEntries();
        UpdatePdfStatus();
        UpdateWeekHint();
    }

    private void RenderEntries()
    {
        EntriesList.Items.Clear();
        if (library.Schedule.Count == 0)
        {
            EntriesList.Items.Add(new ListBoxItem { Content = "Aucun cours enregistré. Ajoute un PDF ou un créneau manuellement.", Padding = new Thickness(12), Foreground = Brushes.Gray, IsHitTestVisible = false });
            return;
        }
        RenderWeeklyGrid();
        foreach (var entry in FilteredSchedule().OrderBy(item => item.Day).ThenBy(item => item.StartMinutes))
        {
            var subject = FindSubject(entry.Subject);
            var weekLabel = entry.WeekType is "A" or "B" ? $" · Semaine {entry.WeekType}" : " · Toutes les semaines";
            var item = new ListBoxItem { Content = $"{DayName(entry.Day)}  ·  {FormatTime(entry.StartMinutes)} - {FormatTime(entry.EndMinutes)}  ·  {entry.Subject}{weekLabel}{(subject is null ? "" : $"  →  {subject.Name}")}", Padding = new Thickness(12, 9, 12, 9), Background = Brushes.White, Margin = new Thickness(0, 0, 0, 6), Tag = entry };
            item.MouseDoubleClick += (_, _) => RemoveEntry(entry);
            var menu = new ContextMenu();
            foreach (var week in new[] { (Label: "Toutes les semaines", Value: "Toutes"), (Label: "Semaine A", Value: "A"), (Label: "Semaine B", Value: "B") })
            {
                var weekItem = new MenuItem { Header = week.Label, IsChecked = entry.WeekType == week.Value };
                weekItem.Click += (_, _) => SetWeekType(entry, week.Value);
                menu.Items.Add(weekItem);
            }
            item.ContextMenu = menu;
            EntriesList.Items.Add(item);
        }
    }

    private void RenderWeeklyGrid()
    {
        WeeklyGrid.Children.Clear();
        WeeklyGrid.ColumnDefinitions.Clear();
        for (var day = 0; day < 7; day++) WeeklyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        var days = Enum.GetValues<DayOfWeek>().Where(day => day != DayOfWeek.Sunday).Append(DayOfWeek.Sunday).ToArray();
        for (var column = 0; column < days.Length; column++)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            panel.Children.Add(new TextBlock { Text = DayName(days[column]), FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)), Margin = new Thickness(0, 0, 0, 8) });
            foreach (var entry in FilteredSchedule().Where(entry => entry.Day == days[column]).OrderBy(entry => entry.StartMinutes))
            {
                var weekLabel = entry.WeekType is "A" or "B" ? $"Semaine {entry.WeekType}" : "Toutes";
                panel.Children.Add(new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(215, 221, 231)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 6), Child = new TextBlock { Text = $"{FormatTime(entry.StartMinutes)}\n{entry.Subject}\n{weekLabel}", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(24, 33, 47)) } });
            }
            if (panel.Children.Count == 1) panel.Children.Add(new TextBlock { Text = "Aucun cours", Foreground = Brushes.Gray, FontSize = 11 });
            Grid.SetColumn(panel, column);
            WeeklyGrid.Children.Add(panel);
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (DayPicker.SelectedItem is not ComboBoxItem dayItem || !TryParseClock(StartTime.Text, out var start) || !TryParseClock(EndTime.Text, out var end) || end <= start || string.IsNullOrWhiteSpace(SubjectInput.Text))
        {
            MessageBox.Show("Vérifie le jour, les heures et la matière.", "Emploi du temps", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var day = Enum.Parse<DayOfWeek>((string)dayItem.Tag);
        var subject = SubjectInput.Text.Trim();
        var weekType = (WeekPicker.SelectedItem as ComboBoxItem)?.Tag as string ?? "Toutes";
        if (library.Schedule.Any(item => item.Day == day && item.StartMinutes == (int)start.TotalMinutes && item.WeekType == weekType && item.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Ce cours existe déjà à cet horaire.", "Emploi du temps", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        library.Schedule.Add(new TimetableEntry { Day = day, StartMinutes = (int)start.TotalMinutes, EndMinutes = (int)end.TotalMinutes, Subject = subject, WeekType = weekType });
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

    private void SetWeekType(TimetableEntry entry, string weekType)
    {
        entry.WeekType = weekType;
        save();
        RenderEntries();
        UpdateWeekHint();
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
            library.Schedule.Clear();
            var importedEntries = ImportEntriesFromPdf(destination);
            library.ScheduleParserVersion = 4;
            save();
            UpdatePdfStatus();
            refresh();
            Process.Start(new ProcessStartInfo { FileName = destination, UseShellExecute = true });
            MessageBox.Show(importedEntries > 0 ? $"PDF ajouté. {importedEntries} cours ont été reconnus automatiquement." : "PDF ajouté, mais aucun texte exploitable n’a été trouvé. Ce PDF est peut-être scanné comme une image : ajoute les créneaux manuellement ou utilise un PDF avec texte sélectionnable.", "Emploi du temps", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void UpdateWeekHint()
    {
        WeekHintText.Text = library.Schedule.Any(entry => entry.WeekType is "A" or "B")
            ? "Les cours A/B sont filtrés par le sélecteur. Clic droit sur un cours pour changer sa semaine."
            : "Ce PDF ne sépare pas les semaines A/B : les cours sont communs. Clic droit sur un cours pour l’affecter à A ou B.";
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

    private int ImportEntriesFromPdf(string path)
    {
        var added = 0;
        try
        {
            using var pdf = PdfDocument.Open(path);
            var gridAdded = pdf.GetPages().Sum(ImportGridEntriesFromPage);
            if (gridAdded > 0) return gridAdded;

            var text = string.Join(Environment.NewLine, pdf.GetPages().Select(ExtractPageText));
            var patterns = new[]
            {
                new Regex(@"(?<day>lundi|lun\.?|mardi|mar\.?|mercredi|mer\.?|jeudi|jeu\.?|vendredi|ven\.?|samedi|sam\.?|dimanche|dim\.?)\s*[^\r\n\d]*(?<start>\d{1,2}(?:[:h.]\d{2}))\s*(?:-|–|à|a)\s*(?<end>\d{1,2}(?:[:h.]\d{2}))\s+(?<subject>[^\r\n]+)", RegexOptions.IgnoreCase),
                new Regex(@"(?<day>lundi|lun\.?|mardi|mar\.?|mercredi|mer\.?|jeudi|jeu\.?|vendredi|ven\.?|samedi|sam\.?|dimanche|dim\.?)\s+(?<start>\d{1,2}(?:[:h.]\d{2}))\s+(?<end>\d{1,2}(?:[:h.]\d{2}))\s+(?<subject>[^\r\n]+)", RegexOptions.IgnoreCase)
            };
            var matches = patterns.SelectMany(pattern => pattern.Matches(text).Cast<Match>()).OrderBy(match => match.Index);
            foreach (var match in matches)
            {
                var start = ParseTime(match.Groups["start"].Value);
                var end = ParseTime(match.Groups["end"].Value);
                var day = ParseDay(match.Groups["day"].Value);
                var subject = CleanSubject(match.Groups["subject"].Value);
                if (start is null || end is null || end <= start || string.IsNullOrWhiteSpace(subject) || library.Schedule.Any(item => item.Day == day && item.StartMinutes == start.Value && item.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase))) continue;
                library.Schedule.Add(new TimetableEntry { Day = day, StartMinutes = start.Value, EndMinutes = end.Value, Subject = subject });
                added++;
            }
        }
        catch
        {
            return 0;
        }
        return added;
    }

    private int ImportGridEntriesFromPage(UglyToad.PdfPig.Content.Page page)
    {
        var tokens = page.GetWords().Select(word => new PdfToken(word.Text, word.BoundingBox.Left, word.BoundingBox.Right, word.BoundingBox.Bottom)).ToList();
        var pageWeekType = DetectExplicitWeekType(tokens);
        var dayHeaders = tokens.Where(token => TryParseDay(token.Text, out _)).OrderBy(token => token.Left).ToList();
        var timeRows = tokens.Where(token => token.Left < 80 && ParseTime(token.Text) is not null).OrderByDescending(token => token.Bottom).ToList();
        if (dayHeaders.Count < 2 || timeRows.Count < 2) return 0;

        var added = 0;
        for (var rowIndex = 0; rowIndex < timeRows.Count - 1; rowIndex++)
        {
            var row = timeRows[rowIndex];
            var nextRow = timeRows[rowIndex + 1];
            var start = ParseTime(row.Text);
            var end = ParseTime(nextRow.Text);
            if (start is null || end is null || end <= start) continue;

            for (var dayIndex = 0; dayIndex < dayHeaders.Count; dayIndex++)
            {
                var dayHeader = dayHeaders[dayIndex];
                var center = (dayHeader.Left + dayHeader.Right) / 2;
                var previousCenter = dayIndex == 0 ? center - 125 : (dayHeaders[dayIndex - 1].Left + dayHeaders[dayIndex - 1].Right) / 2;
                var nextCenter = dayIndex == dayHeaders.Count - 1 ? center + 125 : (dayHeaders[dayIndex + 1].Left + dayHeaders[dayIndex + 1].Right) / 2;
                var leftBoundary = (previousCenter + center) / 2;
                var rightBoundary = (center + nextCenter) / 2;
                var cellTokens = tokens
                    .Where(token => token.Left >= leftBoundary && token.Right <= rightBoundary && token.Bottom < row.Bottom + 1 && token.Bottom > nextRow.Bottom + 2)
                    .ToList();
                var subjectGroup = cellTokens
                    .GroupBy(token => Math.Round(token.Bottom / 4d) * 4d)
                    .OrderByDescending(group => group.Key)
                    .Where(group => IsLikelySubject(string.Join(" ", group.OrderBy(token => token.Left).Select(token => token.Text))))
                    .FirstOrDefault();
                var subjectLine = subjectGroup is null ? null : string.Join(" ", subjectGroup.OrderBy(token => token.Left).Select(token => token.Text));
                if (string.IsNullOrWhiteSpace(subjectLine) || ParseTime(subjectLine) is not null || !subjectLine.Any(char.IsLetterOrDigit)) continue;

                var subject = CleanSubject(subjectLine);
                if (string.IsNullOrWhiteSpace(subject) || subject.Length < 2) continue;
                var day = ParseDay(dayHeader.Text);
                if (library.Schedule.Any(item => item.Day == day && item.StartMinutes == start.Value && item.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase))) continue;
                var weekType = DetectCellWeekType(cellTokens) ?? DetectExplicitWeekType(cellTokens) ?? pageWeekType ?? "Toutes";
                library.Schedule.Add(new TimetableEntry { Day = day, StartMinutes = start.Value, EndMinutes = end.Value, Subject = subject, WeekType = weekType });
                added++;
            }
        }
        return added;
    }

    private static string CleanSubject(string value) => Regex.Replace(value, @"\s+", " ").Trim(' ', '\t', '|', ';', '-');

    private static string? DetectExplicitWeekType(IEnumerable<PdfToken> tokens)
    {
        var values = tokens.Select(token => token.Text.Trim().Trim('.', ':', '(', ')', '[', ']').ToLowerInvariant()).ToList();
        var hasWeekWord = values.Any(value => value is "semaine" or "sem");
        if (!hasWeekWord) return null;
        var hasA = values.Contains("a");
        var hasB = values.Contains("b");
        return hasA == hasB ? null : hasA ? "A" : "B";
    }

    private static string? DetectCellWeekType(IEnumerable<PdfToken> tokens)
    {
        var markers = tokens
            .Select(token => token.Text.Trim().Trim('.', ':', '(', ')', '[', ']'))
            .Where(value => value is "A" or "B")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return markers.Count == 1 ? markers[0].ToUpperInvariant() : null;
    }

    private static bool IsLikelySubject(string value) =>
        !Regex.IsMatch(value, @"^[AB]\s+(?:TP|cours)\b", RegexOptions.IgnoreCase) &&
        !Regex.IsMatch(value, @"^(?:[A-Z]\s*)?\d{3}$", RegexOptions.IgnoreCase) &&
        !Regex.IsMatch(value, @"^\[.*\]$") &&
        !Regex.IsMatch(value, @"^\p{L}[\p{L}-]*\s+[A-ZÀ-ÖØ-Ý]\.$", RegexOptions.IgnoreCase);

    private static bool TryParseDay(string value, out DayOfWeek day)
    {
        var normalized = value.Trim().TrimEnd('.').ToLowerInvariant();
        day = normalized switch
        {
            "lundi" or "lun" => DayOfWeek.Monday,
            "mardi" or "mar" => DayOfWeek.Tuesday,
            "mercredi" or "mer" => DayOfWeek.Wednesday,
            "jeudi" or "jeu" => DayOfWeek.Thursday,
            "vendredi" or "ven" => DayOfWeek.Friday,
            "samedi" or "sam" => DayOfWeek.Saturday,
            "dimanche" or "dim" => DayOfWeek.Sunday,
            _ => default
        };
        return normalized is "lundi" or "lun" or "mardi" or "mar" or "mercredi" or "mer" or "jeudi" or "jeu" or "vendredi" or "ven" or "samedi" or "sam" or "dimanche" or "dim";
    }

    private sealed record PdfToken(string Text, double Left, double Right, double Bottom);

    private static string ExtractPageText(UglyToad.PdfPig.Content.Page page)
    {
        var words = page.GetWords()
            .GroupBy(word => Math.Round(word.BoundingBox.Bottom / 4d) * 4d)
            .OrderByDescending(group => group.Key)
            .Select(group => string.Join(" ", group.OrderBy(word => word.BoundingBox.Left).Select(word => word.Text)));
        return string.Join(Environment.NewLine, words);
    }

    private static int? ParseTime(string value)
    {
        var parts = value.ToLowerInvariant().Replace('h', ':').Replace('.', ':').Split(':');
        return parts.Length == 2 && int.TryParse(parts[0], out var hours) && int.TryParse(parts[1], out var minutes) && hours is >= 0 and <= 23 && minutes is >= 0 and <= 59 ? hours * 60 + minutes : null;
    }

    private static bool TryParseClock(string value, out TimeSpan time)
    {
        time = default;
        if (!TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var parsed) || parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1)) return false;
        time = parsed;
        return parsed.Seconds == 0;
    }

    private static DayOfWeek ParseDay(string value) => value.Trim().TrimEnd('.').ToLowerInvariant() switch { "lundi" or "lun" => DayOfWeek.Monday, "mardi" or "mar" => DayOfWeek.Tuesday, "mercredi" or "mer" => DayOfWeek.Wednesday, "jeudi" or "jeu" => DayOfWeek.Thursday, "vendredi" or "ven" => DayOfWeek.Friday, "samedi" or "sam" => DayOfWeek.Saturday, _ => DayOfWeek.Sunday };

    private StudySection? FindSubject(string value) => library.Sections.FirstOrDefault(section => Normalize(section.Name).Contains(Normalize(value)) || Normalize(value).Contains(Normalize(section.Name)) || AbbreviationMatches(value, section.Name));
    private static bool AbbreviationMatches(string abbreviation, string name) => Normalize(name).Split(' ', '-', '.').Any(word => word.StartsWith(Normalize(abbreviation), StringComparison.OrdinalIgnoreCase));
    private static string Normalize(string value) => new string(value.Normalize().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    private static string DayName(DayOfWeek day) => day switch { DayOfWeek.Monday => "Lundi", DayOfWeek.Tuesday => "Mardi", DayOfWeek.Wednesday => "Mercredi", DayOfWeek.Thursday => "Jeudi", DayOfWeek.Friday => "Vendredi", DayOfWeek.Saturday => "Samedi", _ => "Dimanche" };
    private static string FormatTime(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private IEnumerable<TimetableEntry> FilteredSchedule()
    {
        var selectedWeek = (WeekPicker.SelectedItem as ComboBoxItem)?.Tag as string ?? CurrentWeekType();
        return library.Schedule.Where(entry => selectedWeek == "Toutes" || entry.WeekType is "Toutes" or null || entry.WeekType == selectedWeek);
    }

    private void WeekPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized) RenderEntries();
    }
    private static string CurrentWeekType() => System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Today) % 2 == 0 ? "B" : "A";
}
