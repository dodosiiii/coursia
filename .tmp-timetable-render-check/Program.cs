using System.Globalization;
using Coursia;

static string FormatTime(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

var courses = new[]
{
    new TimetableEntry { Day = DayOfWeek.Monday, StartMinutes = 8 * 60, EndMinutes = 9 * 60, Subject = "Maths", Location = "C203", WeekType = "A" },
    new TimetableEntry { Day = DayOfWeek.Monday, StartMinutes = 10 * 60, EndMinutes = 11 * 60, Subject = "Français", Location = "B12", WeekType = "Toutes" },
    new TimetableEntry { Day = DayOfWeek.Tuesday, StartMinutes = 13 * 60, EndMinutes = 14 * 60, Subject = "Histoire", Location = "Bâtiment C — étage 2 — salle 3", WeekType = "B" }
};

foreach (var course in courses)
{
    var week = course.WeekType is "A" or "B" ? $" · {course.WeekType}" : string.Empty;
    var room = string.IsNullOrWhiteSpace(course.Location) ? string.Empty : $" · {course.Location}";
    var rendered = $"{FormatTime(course.StartMinutes)} - {FormatTime(course.EndMinutes)}  ·  {course.Subject}{room}{week}";
    Console.WriteLine(rendered);
    if (rendered.Contains(course.Location) == false) throw new Exception($"Rendering failed for {course.Subject}: no room shown");
}

Console.WriteLine("OK: each course renders with its own room.");
