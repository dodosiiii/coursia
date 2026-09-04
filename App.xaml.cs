using System.Configuration;
using System.Data;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Coursia;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		if (e.Args.Contains("--coursia-notify", StringComparer.OrdinalIgnoreCase))
		{
			NotificationRunner.Run();
			Shutdown();
			return;
		}

		var window = new MainWindow();
		MainWindow = window;
		window.Show();
	}
}

internal static class NotificationRunner
{
	public static void Run()
	{
		if (Process.GetProcessesByName("Coursia").Any(process => process.Id != Environment.ProcessId && process.MainWindowHandle != IntPtr.Zero)) return;
		var dataFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Coursia", "library.json");
		if (!File.Exists(dataFile)) return;
		try
		{
			var library = JsonSerializer.Deserialize<LibraryData>(File.ReadAllText(dataFile));
			if (library is null) return;
			var now = DateTime.Now;
			var weekType = System.Globalization.ISOWeek.GetWeekOfYear(now.Date) % 2 == 0 ? "B" : "A";
			var next = library.Schedule.Where(entry => entry.WeekType is "Toutes" or null || entry.WeekType == weekType).Select(entry => (Entry: entry, When: NextOccurrence(entry, now))).Where(item => item.When is not null).OrderBy(item => item.When).FirstOrDefault();
			if (next.Entry is null || next.When is null || next.When.Value - now > TimeSpan.FromMinutes(15)) return;
			using var icon = new System.Windows.Forms.NotifyIcon { Visible = true, Icon = System.Drawing.SystemIcons.Application, Text = "Coursia" };
			icon.ShowBalloonTip(6000, "Coursia · cours bientôt", $"{next.Entry.Subject} commence à {next.When:HH\\:mm}.", System.Windows.Forms.ToolTipIcon.Info);
			Thread.Sleep(6500);
		}
		catch
		{
		}
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
}

