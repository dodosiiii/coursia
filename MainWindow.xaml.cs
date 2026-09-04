using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Coursia;

public partial class MainWindow : Window
{
    private readonly string dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Coursia");
    private readonly string dataFile;
    private LibraryData library = new();
    private string? selectedSectionId;
    private bool compactMode;
    private bool showFileExtensions;
    private readonly DispatcherTimer batteryTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte Reserved;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    public MainWindow()
    {
        InitializeComponent();
        dataFile = Path.Combine(dataFolder, "library.json");
        LoadLibrary();
        compactMode = library.CompactMode;
        showFileExtensions = library.ShowFileExtensions;
        ApplySettings();
        ApplyPowerMode();
        UpdateBatteryStatus();
        batteryTimer.Tick += (_, _) => { UpdateBatteryStatus(); UpdateScheduleSummary(); };
        batteryTimer.Start();
        RenderSections();
        RenderDocuments();
        UpdateScheduleSummary();
    }

    private void LoadLibrary()
    {
        try
        {
            if (File.Exists(dataFile)) library = JsonSerializer.Deserialize<LibraryData>(File.ReadAllText(dataFile)) ?? new LibraryData();
        }
        catch (Exception error)
        {
            MessageBox.Show($"Impossible de lire la bibliothèque : {error.Message}", "Coursia", MessageBoxButton.OK, MessageBoxImage.Warning);
            library = new LibraryData();
        }
    }

    private void SaveLibrary()
    {
        try
        {
            Directory.CreateDirectory(dataFolder);
            File.WriteAllText(dataFile, JsonSerializer.Serialize(library, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            MessageBox.Show($"La bibliothèque n'a pas pu être sauvegardée : {error.Message}", "Coursia", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (library.TutorialSeen) return;
        ShowTutorial();
        library.TutorialSeen = true;
        SaveLibrary();
    }

    private void ShowTutorial()
    {
        var tutorial = new TutorialWindow { Owner = this };
        tutorial.ShowDialog();
    }

    private void ApplySettings()
    {
        if (TryParseColor(library.AccentColor, out var color) && Resources["Blue"] is SolidColorBrush blueBrush)
        {
            blueBrush.Color = color;
            AddSubjectButton.Background = new SolidColorBrush(color);
        }
    }

    private void UpdateBatteryStatus()
    {
        if (!GetSystemPowerStatus(out var status)) return;
        var percent = status.BatteryLifePercent <= 100 ? status.BatteryLifePercent : 0;
        var onBattery = status.ACLineStatus == 0;
        BatteryIcon.Text = onBattery ? (percent <= 20 ? "▱" : "▰") : "⚡";
        BatteryIcon.Foreground = onBattery && percent <= 20 ? new SolidColorBrush(Color.FromRgb(244, 114, 94)) : new SolidColorBrush(Color.FromRgb(66, 211, 146));
        BatteryStatusText.Text = onBattery ? $"Batterie {percent}%" : "Secteur connecté";
        PowerModeText.Text = library.PowerSavingMode ? "Mode économie activé" : "Mode économie désactivé";
    }

    private void PowerButton_Click(object sender, RoutedEventArgs e)
    {
        library.PowerSavingMode = !library.PowerSavingMode;
        ApplyPowerMode();
        SaveLibrary();
        UpdateBatteryStatus();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => batteryTimer.Stop();

    private void ApplyPowerMode()
    {
        try
        {
            Process.GetCurrentProcess().PriorityClass = library.PowerSavingMode ? ProcessPriorityClass.BelowNormal : ProcessPriorityClass.Normal;
        }
        catch
        {
        }
        PowerModeText.Text = library.PowerSavingMode ? "Mode économie activé" : "Mode économie désactivé";
    }

    private static bool TryParseColor(string value, out Color color)
    {
        try
        {
            color = (Color)ColorConverter.ConvertFromString(value);
            return true;
        }
        catch
        {
            color = Colors.DodgerBlue;
            return false;
        }
    }

    private void RenderSections()
    {
        SectionList.Children.Clear();
        foreach (var section in library.Sections.Where(section => section.ParentId is null))
        {
            AddSectionButton(section, false);
            foreach (var child in library.Sections.Where(child => child.ParentId == section.Id)) AddSectionButton(child, true);
        }
        if (library.Sections.Count == 0) SectionList.Children.Add(new TextBlock { Text = "Aucune matière\nCommence avec le bouton ci-dessous.", Foreground = new SolidColorBrush(Color.FromRgb(117, 129, 150)), FontSize = 12, LineHeight = 20, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4, 8, 12) });
    }

    private void AddSectionButton(StudySection section, bool isSubsection)
    {
        var button = new Button { Content = new TextBlock { Text = (isSubsection ? "   " : "") + section.Icon + "  " + section.Name, TextTrimming = TextTrimming.CharacterEllipsis }, Tag = section.Id, HorizontalContentAlignment = HorizontalAlignment.Left, Background = section.Id == selectedSectionId ? new SolidColorBrush(Color.FromRgb(42, 56, 80)) : Brushes.Transparent, Foreground = new SolidColorBrush(isSubsection ? Color.FromRgb(159, 174, 196) : Color.FromRgb(224, 231, 241)), Padding = new Thickness(10, 8, 10, 8), Margin = isSubsection ? new Thickness(14, 0, 0, 2) : new Thickness(0, 0, 0, 2), FontSize = isSubsection ? 12 : 13, ToolTip = "Ouvrir " + section.Name };
        button.MouseEnter += (_, _) => { if (section.Id != selectedSectionId) button.Background = new SolidColorBrush(Color.FromRgb(32, 46, 67)); };
        button.MouseLeave += (_, _) => { if (section.Id != selectedSectionId) button.Background = Brushes.Transparent; };
        button.ContextMenu = CreateSectionMenu(section);
        button.Click += Section_Click;
        SectionList.Children.Add(button);
    }

    private ContextMenu CreateSectionMenu(StudySection section)
    {
        var menu = new ContextMenu();
        var customize = new MenuItem { Header = "Personnaliser" };
        customize.Click += (_, _) => CustomizeSection(section);
        menu.Items.Add(customize);
        var notes = new MenuItem { Header = "Ouvrir les notes" };
        notes.Click += (_, _) => EditNotes(section);
        menu.Items.Add(notes);
        var delete = new MenuItem { Header = "Supprimer la matière" };
        delete.Click += (_, _) => DeleteSection(section);
        menu.Items.Add(delete);
        return menu;
    }

    private ContextMenu CreateDocumentMenu(StudyDocument document)
    {
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "Ouvrir" };
        open.Click += (_, _) => OpenDocument(document);
        var locate = new MenuItem { Header = "Afficher dans le dossier" };
        locate.Click += (_, _) => ShowInFolder(document);
        var favorite = new MenuItem { Header = document.IsFavorite ? "Retirer des favoris" : "Ajouter aux favoris" };
        favorite.Click += (_, _) => ToggleFavorite(document);
        var delete = new MenuItem { Header = "Supprimer de Coursia" };
        delete.Click += (_, _) => DeleteDocument(document);
        menu.Items.Add(open);
        menu.Items.Add(locate);
        menu.Items.Add(favorite);
        menu.Items.Add(new Separator());
        menu.Items.Add(delete);
        return menu;
    }

    private void RenderDocuments()
    {
        CourseCards.Children.Clear();
        RecentFiles.Children.Clear();
        if (selectedSectionId is null)
        {
            RenderOverview();
            return;
        }

        RenderSectionContents();
    }

    private void RenderOverview()
    {
        BackToOverviewButton.Visibility = Visibility.Collapsed;
        var query = SearchBox?.Text?.Trim() ?? "";
        var subjects = library.Sections.Where(section => section.ParentId is null).Where(section => string.IsNullOrWhiteSpace(query) || section.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        CourseTitle.Text = "Mes matières";
        CourseCount.Text = $"{subjects.Count} matière{(subjects.Count > 1 ? "s" : "")}";
        if (subjects.Count == 0)
        {
            CourseCards.Children.Add(CreateEmptyStateButton());
        }
        foreach (var subject in subjects) CourseCards.Children.Add(CreateSectionCard(subject));
        RenderRecentFiles(library.Documents);
    }

    private void RenderSectionContents()
    {
        var section = library.Sections.FirstOrDefault(item => item.Id == selectedSectionId);
        if (section is null) { selectedSectionId = null; RenderOverview(); return; }
        BackToOverviewButton.Visibility = Visibility.Visible;
        var query = SearchBox?.Text?.Trim() ?? "";
        var sectionIds = GetDescendantIds(section.Id).ToHashSet();
        var children = library.Sections.Where(item => item.ParentId == section.Id).Where(item => string.IsNullOrWhiteSpace(query) || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        var documents = library.Documents.Where(document => sectionIds.Contains(document.SectionId)).Where(document => string.IsNullOrWhiteSpace(query) || document.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).OrderByDescending(document => document.AddedAt).ToList();
        CourseTitle.Text = section.Name;
        CourseCount.Text = $"{children.Count + documents.Count} élément{(children.Count + documents.Count > 1 ? "s" : "")}";
        if (children.Count == 0 && documents.Count == 0) CourseCards.Children.Add(CreateEmptyStateButton());
        foreach (var child in children) CourseCards.Children.Add(CreateSectionCard(child));
        foreach (var document in documents) CourseCards.Children.Add(CreateDocumentCard(document));
        RenderRecentFiles(documents);
    }

    private IEnumerable<string> GetDescendantIds(string sectionId)
    {
        yield return sectionId;
        foreach (var child in library.Sections.Where(item => item.ParentId == sectionId))
        {
            foreach (var descendantId in GetDescendantIds(child.Id)) yield return descendantId;
        }
    }

    private Button CreateSectionCard(StudySection section)
    {
        var documentCount = library.Documents.Count(document => GetDescendantIds(section.Id).Contains(document.SectionId));
        var childCount = library.Sections.Count(item => item.ParentId == section.Id);
        var card = new Button { Width = compactMode ? 198 : 218, Height = compactMode ? 132 : 150, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(230, 233, 239)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 15, 15), Padding = new Thickness(compactMode ? 13 : 17), HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top, Tag = section.Id, ToolTip = "Ouvrir cette matière" };
        var content = new StackPanel();
        var sectionColor = TryParseColor(section.Color, out var parsedColor) ? parsedColor : Colors.DodgerBlue;
        content.Children.Add(new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(9), Background = new SolidColorBrush(Color.FromArgb(28, sectionColor.R, sectionColor.G, sectionColor.B)), Child = new TextBlock { Text = section.Icon, FontSize = 20, Foreground = new SolidColorBrush(sectionColor), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
        content.Children.Add(new TextBlock { Text = section.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 3), Foreground = new SolidColorBrush(Color.FromRgb(24, 33, 47)) });
        content.Children.Add(new TextBlock { Text = $"{childCount} sous-section{(childCount > 1 ? "s" : "")} · {documentCount} fichier{(documentCount > 1 ? "s" : "")}", Foreground = new SolidColorBrush(Color.FromRgb(117, 128, 147)), FontSize = 12 });
        card.Content = content;
        card.ContextMenu = CreateSectionMenu(section);
        card.Click += Section_Click;
        return card;
    }

    private void CustomizeSection(StudySection section)
    {
        var dialog = new NameDialog("Personnaliser la matière", "Modifie le nom, l'icône ou la couleur.", section.Name, section.Icon, section.Color) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        section.Name = dialog.Value;
        section.Icon = dialog.SelectedIcon;
        section.Color = dialog.SelectedColor;
        SaveLibrary();
        RenderSections();
        RenderDocuments();
    }

    private void EditNotes(StudySection section)
    {
        var dialog = new NotesWindow(section) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        section.Notes = dialog.Notes;
        SaveLibrary();
    }

    private void ToggleFavorite(StudyDocument document)
    {
        document.IsFavorite = !document.IsFavorite;
        SaveLibrary();
        RenderDocuments();
    }

    private Button CreateDocumentCard(StudyDocument document)
    {
        var card = new Button { Width = compactMode ? 198 : 218, Height = compactMode ? 140 : 160, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(230, 233, 239)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 15, 15), Padding = new Thickness(compactMode ? 13 : 17), HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top, Tag = document, ToolTip = "Ouvrir le fichier" };
        var content = new StackPanel();
        content.Children.Add(new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(9), Background = new SolidColorBrush(Color.FromRgb(234, 247, 242)), Child = new TextBlock { Text = document.Extension, Foreground = new SolidColorBrush(Color.FromRgb(25, 124, 84)), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
        content.Children.Add(new TextBlock { Text = (document.IsFavorite ? "★  " : "") + (showFileExtensions ? $"{document.Name}.{document.Extension.ToLowerInvariant()}" : document.Name), FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 3), Foreground = new SolidColorBrush(Color.FromRgb(24, 33, 47)) });
        content.Children.Add(new TextBlock { Text = File.Exists(document.StoredPath) ? "Cliquer pour ouvrir" : "Fichier introuvable", Foreground = new SolidColorBrush(Color.FromRgb(117, 128, 147)), FontSize = 12 });
        card.Content = content;
        card.ContextMenu = CreateDocumentMenu(document);
        card.Click += (_, _) => OpenDocument(document);
        return card;
    }

    private void RenderRecentFiles(IEnumerable<StudyDocument> documents)
    {
        foreach (var document in documents.OrderByDescending(document => document.AddedAt).Take(12))
        {
            var recent = new Button { Content = "▧  " + document.Name, Tag = document, HorizontalContentAlignment = HorizontalAlignment.Left, Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.FromRgb(24, 33, 47)), Padding = new Thickness(0), Margin = new Thickness(0, 0, 0, 12), ToolTip = "Ouvrir le fichier" };
            recent.ContextMenu = CreateDocumentMenu(document);
            recent.Click += RecentFile_Click;
            RecentFiles.Children.Add(recent);
        }
    }

    private void UpdateScheduleSummary()
    {
        var now = DateTime.Now;
        var next = library.Schedule.Select(entry => (Entry: entry, When: NextOccurrence(entry, now))).Where(item => item.When is not null).OrderBy(item => item.When).FirstOrDefault();
        if (next.Entry is null)
        {
            NextClassText.Text = "▦  Aucun cours programmé\nAjoute ton emploi du temps depuis la barre latérale.";
            return;
        }
        var subject = FindSubject(next.Entry.Subject);
        var label = subject?.Name ?? next.Entry.Subject;
        var remaining = next.When!.Value - now;
        NextClassText.Text = remaining.TotalMinutes <= 60
            ? $"⏰  Prochain cours : {label} à {next.When:HH\\:mm}\nPense à relire le cours avant de partir."
            : $"▦  Prochain cours : {label}\n{DayLabel(next.When.Value)}, {next.When:HH\\:mm}";
    }

    private static DateTime? NextOccurrence(TimetableEntry entry, DateTime now)
    {
        for (var offset = 0; offset <= 7; offset++)
        {
            var date = now.Date.AddDays(offset);
            if (date.DayOfWeek != entry.Day) continue;
            var occurrence = date.AddMinutes(entry.StartMinutes);
            if (occurrence > now) return occurrence;
        }
        return null;
    }

    private static string DayLabel(DateTime date) => date.Date == DateTime.Today ? "Aujourd'hui" : date.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));

    private StudySection? FindSubject(string value) => library.Sections.FirstOrDefault(section => Normalize(section.Name).Contains(Normalize(value), StringComparison.OrdinalIgnoreCase) || Normalize(value).Contains(Normalize(section.Name), StringComparison.OrdinalIgnoreCase) || Normalize(section.Name).Split(' ', '-', '.').Any(word => word.StartsWith(Normalize(value), StringComparison.OrdinalIgnoreCase)));
    private static string Normalize(string value) => new string(value.Normalize().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private Button CreateEmptyStateButton()
    {
        var button = new Button
        {
            Width = 420,
            Height = 118,
            Content = selectedSectionId is null ? "＋\nCréer une matière" : "＋\nAjouter un document",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            Background = new SolidColorBrush(Color.FromRgb(239, 245, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(191, 211, 249)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 10, 15, 15),
            ToolTip = "Cliquer pour commencer"
        };
        button.Click += selectedSectionId is null ? AddSection_Click : AddFile_Click;
        return button;
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        if (selectedSectionId is null) { MessageBox.Show("Sélectionne d'abord une section dans la barre latérale.", "Ajouter un document", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (string.IsNullOrWhiteSpace(library.StorageFolder) && !ChooseStorageFolder()) return;
        var dialog = new OpenFileDialog { Title = "Ajouter des documents", Multiselect = true, Filter = "Documents|*.pdf;*.doc;*.docx;*.ppt;*.pptx;*.txt;*.xlsx;*.jpg;*.png|Tous les fichiers|*.*" };
        if (dialog.ShowDialog() != true) return;
        ImportFiles(dialog.FileNames);
    }

    private void CreateDocument_Click(object sender, RoutedEventArgs e)
    {
        if (selectedSectionId is null)
        {
            MessageBox.Show("Sélectionne d'abord une matière ou un sous-dossier.", "Créer un fichier", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(library.StorageFolder) && !ChooseStorageFolder()) return;
        var dialog = new CreateDocumentWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var folder = GetSectionFolder(selectedSectionId);
            Directory.CreateDirectory(folder);
            var extension = "." + dialog.FileType;
            var fileName = SanitizeFileName(dialog.FileName);
            if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) fileName = fileName[..^extension.Length];
            var path = GetUniquePath(folder, fileName + extension);
            CreateRealFile(path, dialog.FileType, dialog.FileName);
            library.Documents.Add(new StudyDocument { Name = Path.GetFileNameWithoutExtension(path), Extension = dialog.FileType.ToUpperInvariant(), StoredPath = path, SectionId = selectedSectionId, AddedAt = DateTimeOffset.UtcNow });
            SaveLibrary();
            RenderDocuments();
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception error)
        {
            MessageBox.Show($"Le fichier n'a pas pu être créé : {error.Message}", "Création impossible", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool ChooseStorageFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Choisir où enregistrer tes cours", Multiselect = false };
        if (dialog.ShowDialog() != true) return false;
        library.StorageFolder = dialog.FolderName;
        SaveLibrary();
        return true;
    }

    private string GetSectionFolder(string sectionId)
    {
        var names = new Stack<string>();
        var current = library.Sections.First(section => section.Id == sectionId);
        names.Push(SanitizeFileName(current.Name));
        while (current.ParentId is not null)
        {
            current = library.Sections.First(section => section.Id == current.ParentId);
            names.Push(SanitizeFileName(current.Name));
        }
        var folder = library.StorageFolder!;
        foreach (var name in names) folder = Path.Combine(folder, name);
        return folder;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = new string(Path.GetInvalidFileNameChars());
        var result = new string(value.Where(character => !invalid.Contains(character)).ToArray()).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(result) ? "Sans nom" : result;
    }

    private static void CreateRealFile(string path, string type, string title)
    {
        if (type == "txt")
        {
            File.WriteAllText(path, $"{title}{Environment.NewLine}{Environment.NewLine}Créé avec Coursia.", Encoding.UTF8);
            return;
        }
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        if (type == "docx") CreateWordDocument(archive, title);
        else CreatePowerPoint(archive, title);
    }

    private static void AddZipEntry(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void CreateWordDocument(ZipArchive archive, string title)
    {
        AddZipEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        AddZipEntry(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
        var safeTitle = System.Security.SecurityElement.Escape(title);
        AddZipEntry(archive, "word/document.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>{safeTitle}</w:t></w:r></w:p><w:p><w:r><w:t>Créé avec Coursia.</w:t></w:r></w:p><w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1440\" w:right=\"1440\" w:bottom=\"1440\" w:left=\"1440\"/></w:sectPr></w:body></w:document>");
    }

    private static void CreatePowerPoint(ZipArchive archive, string title)
    {
        AddZipEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/><Override PartName=\"/ppt/slides/slide1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/></Types>");
        AddZipEntry(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"ppt/presentation.xml\"/></Relationships>");
        AddZipEntry(archive, "ppt/presentation.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:presentation xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><p:sldMasterIdLst/><p:slideIdLst><p:sldId id=\"256\" r:id=\"rId1\"/></p:slideIdLst><p:sldSz cx=\"12192000\" cy=\"6858000\"/></p:presentation>");
        AddZipEntry(archive, "ppt/_rels/presentation.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" Target=\"slides/slide1.xml\"/></Relationships>");
        var safeTitle = System.Security.SecurityElement.Escape(title);
            AddZipEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/><Override PartName=\"/ppt/slideMasters/slideMaster1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml\"/><Override PartName=\"/ppt/slideLayouts/slideLayout1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml\"/><Override PartName=\"/ppt/theme/theme1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\"/><Override PartName=\"/ppt/slides/slide1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/></Types>");
            AddZipEntry(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"ppt/presentation.xml\"/></Relationships>");
            AddZipEntry(archive, "ppt/presentation.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:presentation xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rId1\"/></p:sldMasterIdLst><p:slideIdLst><p:sldId id=\"256\" r:id=\"rId2\"/></p:slideIdLst><p:sldSz cx=\"12192000\" cy=\"6858000\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/></p:presentation>");
            AddZipEntry(archive, "ppt/_rels/presentation.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster\" Target=\"slideMasters/slideMaster1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" Target=\"slides/slide1.xml\"/></Relationships>");
            AddZipEntry(archive, "ppt/slideMasters/slideMaster1.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:sldMaster xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:cSld><p:spTree><p:nvGrpSpPr/><p:grpSpPr/></p:spTree></p:cSld><p:sldLayoutIdLst><p:sldLayoutId id=\"1\" r:id=\"rId1\"/></p:sldLayoutIdLst><p:txStyles/><p:clrMap accent1=\"accent1\" accent2=\"accent2\" bg1=\"lt1\" tx1=\"dk1\"/></p:sldMaster>");
            AddZipEntry(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout\" Target=\"../slideLayouts/slideLayout1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme\" Target=\"../theme/theme1.xml\"/></Relationships>");
            AddZipEntry(archive, "ppt/slideLayouts/slideLayout1.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:sldLayout xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" type=\"blank\"><p:cSld><p:spTree><p:nvGrpSpPr/><p:grpSpPr/></p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>");
            AddZipEntry(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster\" Target=\"../slideMasters/slideMaster1.xml\"/></Relationships>");
            AddZipEntry(archive, "ppt/theme/theme1.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><a:theme xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" name=\"Coursia\"><a:themeElements><a:clrScheme name=\"Coursia\"><a:dk1><a:sysClr val=\"windowText\" lastClr=\"000000\"/></a:dk1><a:lt1><a:sysClr val=\"window\" lastClr=\"FFFFFF\"/></a:lt1><a:accent1><a:srgbClr val=\"2563EB\"/></a:accent1><a:accent2><a:srgbClr val=\"16805B\"/></a:accent2></a:clrScheme><a:fontScheme name=\"Coursia\"><a:majorFont><a:latin typeface=\"Aptos Display\"/></a:majorFont><a:minorFont><a:latin typeface=\"Aptos\"/></a:minorFont></a:fontScheme><a:fmtScheme name=\"Coursia\"><a:fillStyleLst/><a:lnStyleLst/><a:effectStyleLst/><a:bgFillStyleLst/></a:fmtScheme></a:themeElements></a:theme>");
            AddZipEntry(archive, "ppt/slides/slide1.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:sld xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:cSld><p:spTree><p:nvGrpSpPr/><p:grpSpPr/><p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Titre\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr><p:spPr><a:xfrm><a:off x=\"914400\" y=\"914400\"/><a:ext cx=\"10668000\" cy=\"914400\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr><p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr lang=\"fr-FR\" sz=\"2800\"/><a:t>{safeTitle}</a:t></a:r></p></p:txBody></p:sp></p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sld>");
            AddZipEntry(archive, "ppt/slides/_rels/slide1.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout\" Target=\"../slideLayouts/slideLayout1.xml\"/></Relationships>");
    }

    private void ImportFiles(IEnumerable<string> sourcePaths)
    {
        if (selectedSectionId is null) return;
        var sectionId = selectedSectionId;
        var documentsFolder = GetSectionFolder(sectionId);
        Directory.CreateDirectory(documentsFolder);
        var importedCount = 0;
        foreach (var sourcePath in sourcePaths)
        {
            try
            {
                var destinationPath = GetUniquePath(documentsFolder, Path.GetFileName(sourcePath));
                File.Copy(sourcePath, destinationPath);
                library.Documents.Add(new StudyDocument { Name = Path.GetFileNameWithoutExtension(sourcePath), Extension = Path.GetExtension(sourcePath).TrimStart('.').ToUpperInvariant(), StoredPath = destinationPath, SectionId = sectionId, AddedAt = DateTimeOffset.UtcNow });
                importedCount++;
            }
            catch (Exception error)
            {
                MessageBox.Show($"Impossible d'ajouter {Path.GetFileName(sourcePath)} : {error.Message}", "Import interrompu", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        if (importedCount > 0)
        {
            SaveLibrary();
            RenderDocuments();
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = selectedSectionId is not null && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (selectedSectionId is null)
        {
            MessageBox.Show("Sélectionne une matière avant de déposer un document.", "Importer un document", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths) ImportFiles(paths);
        e.Handled = true;
    }

    private static string GetUniquePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        var index = 1;
        while (File.Exists(path)) path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(fileName)} ({index++}){Path.GetExtension(fileName)}");
        return path;
    }

    private void AddSection_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NameDialog("Ajouter une matière", "Exemple : Mathématiques, Histoire ou Anglais.", "");
        dialog.Owner = this;
        if (dialog.ShowDialog() != true) return;
        var name = dialog.Value;
        var section = new StudySection { Name = name, Icon = dialog.SelectedIcon, Color = dialog.SelectedColor };
        library.Sections.Add(section);
        selectedSectionId = section.Id;
        SaveLibrary();
        RenderSections();
        RenderDocuments();
    }

    private void AddSubsection_Click(object sender, RoutedEventArgs e)
    {
        if (selectedSectionId is null) { MessageBox.Show("Sélectionne d'abord la section parente.", "Nouvelle sous-section", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var dialog = new NameDialog("Ajouter une sous-section", "Exemple : Chapitre 1, Exercices ou Révisions.", "");
        dialog.Owner = this;
        if (dialog.ShowDialog() != true) return;
        var name = dialog.Value;
        library.Sections.Add(new StudySection { Name = name, ParentId = selectedSectionId, Icon = dialog.SelectedIcon, Color = dialog.SelectedColor });
        SaveLibrary();
        RenderSections();
    }

    private void OpenDocument(StudyDocument document)
    {
        if (!File.Exists(document.StoredPath)) { MessageBox.Show("Ce fichier n'existe plus à son emplacement local.", "Fichier introuvable", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        Process.Start(new ProcessStartInfo { FileName = document.StoredPath, UseShellExecute = true });
    }

    private void ShowInFolder(StudyDocument document)
    {
        if (!File.Exists(document.StoredPath))
        {
            MessageBox.Show("Ce fichier n'existe plus à son emplacement local.", "Fichier introuvable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{document.StoredPath}\"", UseShellExecute = true });
    }

    private void DeleteDocument(StudyDocument document)
    {
        if (MessageBox.Show($"Supprimer « {document.Name} » de Coursia et supprimer sa copie locale ?", "Supprimer le fichier", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            if (File.Exists(document.StoredPath)) File.Delete(document.StoredPath);
            library.Documents.Remove(document);
            SaveLibrary();
            RenderDocuments();
        }
        catch (Exception error)
        {
            MessageBox.Show($"Le fichier n'a pas pu être supprimé : {error.Message}", "Coursia", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteSection(StudySection section)
    {
        var sectionIds = GetDescendantIds(section.Id).ToHashSet();
        var documentCount = library.Documents.Count(document => sectionIds.Contains(document.SectionId));
        if (MessageBox.Show($"Supprimer « {section.Name} », ses sous-sections et {documentCount} fichier(s) de la bibliothèque ?", "Supprimer la matière", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        foreach (var document in library.Documents.Where(document => sectionIds.Contains(document.SectionId)).ToList())
        {
            try { if (File.Exists(document.StoredPath)) File.Delete(document.StoredPath); } catch { }
            library.Documents.Remove(document);
        }
        library.Sections.RemoveAll(item => sectionIds.Contains(item.Id));
        if (selectedSectionId is not null && sectionIds.Contains(selectedSectionId)) selectedSectionId = null;
        SaveLibrary();
        RenderSections();
        RenderDocuments();
    }

    private void RecentFile_Click(object sender, RoutedEventArgs e) => OpenDocument((StudyDocument)((Button)sender).Tag);
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RenderDocuments();
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow(library.AccentColor, library.AppIcon, compactMode, showFileExtensions) { Owner = this };
        if (settings.ShowDialog() != true) return;
        if (settings.ResetRequested)
        {
            ResetApplication();
            return;
        }
        if (settings.BackupRequested)
        {
            ExportBackup();
            return;
        }
        if (settings.ReplayTutorial)
        {
            ShowTutorial();
            return;
        }
        library.AccentColor = settings.Accent;
        library.AppIcon = settings.AppIconValue;
        library.CompactMode = settings.IsCompact;
        library.ShowFileExtensions = settings.ShowFileExtensions;
        compactMode = settings.IsCompact;
        showFileExtensions = settings.ShowFileExtensions;
        ApplySettings();
        SaveLibrary();
        RenderDocuments();
    }

    private void Schedule_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(library.SchedulePdfPath) && File.Exists(library.SchedulePdfPath))
        {
            Process.Start(new ProcessStartInfo { FileName = library.SchedulePdfPath, UseShellExecute = true });
        }
        var schedule = new ScheduleWindow(library, SaveLibrary, UpdateScheduleSummary, ChooseStorageFolder) { Owner = this };
        schedule.ShowDialog();
        UpdateScheduleSummary();
    }

    private void ResetApplication()
    {
        try
        {
            if (Directory.Exists(dataFolder)) Directory.Delete(dataFolder, true);
            library = new LibraryData();
            selectedSectionId = null;
            compactMode = false;
            SearchBox.Clear();
            ApplySettings();
            RenderSections();
            RenderDocuments();
            MessageBox.Show("Coursia a été réinitialisé.", "Réinitialisation terminée", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception error)
        {
            MessageBox.Show($"La réinitialisation a échoué : {error.Message}", "Coursia", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportBackup()
    {
        try
        {
            SaveLibrary();
            var dialog = new SaveFileDialog { Title = "Enregistrer la sauvegarde Coursia", Filter = "Archive ZIP|*.zip", FileName = $"Coursia-sauvegarde-{DateTime.Now:yyyy-MM-dd}.zip", AddExtension = true };
            if (dialog.ShowDialog() != true) return;
            var sourceFolder = Path.GetFullPath(dataFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(dialog.FileName).StartsWith(sourceFolder, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Choisis un emplacement différent du dossier interne de Coursia.", "Emplacement invalide", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (File.Exists(dialog.FileName)) File.Delete(dialog.FileName);
            ZipFile.CreateFromDirectory(dataFolder, dialog.FileName, CompressionLevel.Optimal, false);
            MessageBox.Show("La sauvegarde a été créée avec succès.", "Sauvegarde Coursia", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception error)
        {
            MessageBox.Show($"La sauvegarde n'a pas pu être créée : {error.Message}", "Coursia", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (e.Key == System.Windows.Input.Key.N)
        {
            AddSection_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.O)
        {
            AddFile_Click(sender, e);
            e.Handled = true;
        }
    }
    private void Overview_Click(object sender, RoutedEventArgs e) { selectedSectionId = null; PageTitle.Text = "Vue d'ensemble"; RenderSections(); RenderDocuments(); }
    private void Section_Click(object sender, RoutedEventArgs e)
    {
        var sectionId = (string)((Button)sender).Tag;
        var section = library.Sections.FirstOrDefault(item => item.Id == sectionId);
        if (section is null) return;
        selectedSectionId = section.Id;
        PageTitle.Text = section.Name;
        RenderSections();
        RenderDocuments();
    }
}

public sealed class LibraryData
{
    public List<StudySection> Sections { get; set; } = new();
    public List<StudyDocument> Documents { get; set; } = new();
    public string AccentColor { get; set; } = "#2563EB";
    public string AppIcon { get; set; } = "◈";
    public bool CompactMode { get; set; }
    public bool TutorialSeen { get; set; }
    public string StorageFolder { get; set; } = "";
    public string SchedulePdfPath { get; set; } = "";
    public bool PowerSavingMode { get; set; }
    public bool ShowFileExtensions { get; set; }
    public List<TimetableEntry> Schedule { get; set; } = new();
}

public sealed class TimetableEntry
{
    public DayOfWeek Day { get; set; }
    public int StartMinutes { get; set; }
    public int EndMinutes { get; set; }
    public string Subject { get; set; } = "";
}

public sealed class StudySection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string? ParentId { get; set; }
    public string Icon { get; set; } = "◈";
    public string Color { get; set; } = "#2563EB";
    public string Notes { get; set; } = "";
}

public sealed class StudyDocument
{
    public string Name { get; set; } = "";
    public string Extension { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public string SectionId { get; set; } = "";
    public DateTimeOffset AddedAt { get; set; }
    public bool IsFavorite { get; set; }
}