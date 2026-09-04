using System.Text.Json;
using Coursia;

var library = new LibraryData
{
    Schedule = new List<TimetableEntry>
    {
        new() { Day = DayOfWeek.Monday, StartMinutes = 8 * 60, EndMinutes = 9 * 60, Subject = "Maths", Location = "C203", WeekType = "A" },
        new() { Day = DayOfWeek.Monday, StartMinutes = 9 * 60, EndMinutes = 10 * 60, Subject = "Français", Location = "B12", WeekType = "Toutes" },
        new() { Day = DayOfWeek.Tuesday, StartMinutes = 10 * 60, EndMinutes = 11 * 60, Subject = "Histoire", Location = "Batiment C - Etage 2 - Salle 3", WeekType = "B" },
    }
};

var json = JsonSerializer.Serialize(library);
var roundtrip = JsonSerializer.Deserialize<LibraryData>(json);
if (roundtrip is null || roundtrip.Schedule.Count != 3)
    throw new Exception("Roundtrip failed: schedule count mismatch");

var maths = roundtrip.Schedule.First(x => x.Subject == "Maths");
var francais = roundtrip.Schedule.First(x => x.Subject == "Français");
var histoire = roundtrip.Schedule.First(x => x.Subject == "Histoire");

if (maths.Location != "C203") throw new Exception($"Wrong room for Maths: {maths.Location}");
if (francais.Location != "B12") throw new Exception($"Wrong room for Français: {francais.Location}");
if (histoire.Location != "Batiment C - Etage 2 - Salle 3") throw new Exception($"Wrong room for Histoire: {histoire.Location}");

Console.WriteLine("OK: room values are preserved across serialization.");
Console.WriteLine($"Maths -> {maths.Location}");
Console.WriteLine($"Français -> {francais.Location}");
Console.WriteLine($"Histoire -> {histoire.Location}");
