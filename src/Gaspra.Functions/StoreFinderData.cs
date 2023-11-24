using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyCsvParser;
using TinyCsvParser.Mapping;

namespace Gaspra.Functions;

public class OpeningHoursCsvData
{
    public string OpeningHours { get; set; }
    public int ReferenceId { get; set; }
}

public class OpeningHours
{
    public string Monday { get; set; } = "";
    public string Tuesday { get; set; } = "";
    public string Wednesday { get; set; } = "";
    public string Thursday { get; set; } = "";
    public string Friday { get; set; } = "";
    public string Saturday { get; set; } = "";
    public string Sunday { get; set; } = "";
    public int ReferenceId { get; set; }
}

public static class OpeningHoursExtension
{
    public static OpeningHours ToOpeningHours(this OpeningHoursCsvData openingCsv)
    {
        if (!(openingCsv is { OpeningHours: not null }) ||
            string.IsNullOrWhiteSpace(openingCsv.OpeningHours) ||
            openingCsv.OpeningHours.TrimStart().TrimEnd().Equals("-"))
        {
            return new OpeningHours()
            {
                ReferenceId = openingCsv.ReferenceId
            };
        }

        var splitOpeningHours = openingCsv.OpeningHours.Split(',');

        var openingHours = new OpeningHours() { ReferenceId = openingCsv.ReferenceId };
        
        foreach (var splitOpeningHour in splitOpeningHours)
        {
            if (splitOpeningHour.StartsWith("Monday"))
            {
                openingHours.Monday = splitOpeningHour.Split("Monday:").Last().Trim().OpeningRange().DaysOpeningHours();
            } 
            else if (splitOpeningHour.StartsWith("Tuesday"))
            {
                //Tuesday: 12:00 – 2:00 PM, 7:45 – 9:30 PM,
                openingHours.Tuesday = splitOpeningHour.Split("Tuesday:").Last().Trim().OpeningRange().DaysOpeningHours();
            }
            else if (splitOpeningHour.StartsWith("Wednesday"))
            {
                openingHours.Wednesday = splitOpeningHour.Split("Wednesday:").Last().Trim().OpeningRange().DaysOpeningHours();
            }
            else if (splitOpeningHour.StartsWith("Thursday"))
            {
                openingHours.Thursday = splitOpeningHour.Split("Thursday:").Last().Trim().OpeningRange().DaysOpeningHours();
            }
            else if (splitOpeningHour.StartsWith("Friday"))
            {
                openingHours.Friday = splitOpeningHour.Split("Friday:").Last().Trim().OpeningRange().DaysOpeningHours();
            }
            else if (splitOpeningHour.StartsWith("Saturday"))
            {
                openingHours.Saturday = splitOpeningHour.Split("Saturday:").Last().Trim().OpeningRange().DaysOpeningHours();
            }
            else if (splitOpeningHour.StartsWith("Sunday"))
            {
                openingHours.Sunday = splitOpeningHour.Split("Sunday:").Last().Trim().OpeningRange().DaysOpeningHours();
            }
        }

        return openingHours;
    }

    private static string OpeningRange(this string openingHours)
    {
        if (openingHours.Contains('/'))
        {
            var firstOpen = openingHours.Split('/').First().Split(" – ").First();
            
            var lastClose = openingHours.Split('/').Last().Split(" – ").Last();
            
            return $"{firstOpen} – {lastClose}";
        }
        else
        {
            return openingHours;
        }
    }
    
    private static string DaysOpeningHours(this string openingHours)
    {
        if (openingHours.ToLowerInvariant().StartsWith("closed"))
        {
            return "CLOSED";
        }

        if (openingHours.ToLowerInvariant().StartsWith("open 24"))
        {
            return "";
        }

        try
        {
            var openString = openingHours.Split("–").First().Trim().Replace(" ", "-");

            if (openString.Split(":").First().Length <= 1)
            {
                openString = $"0{openString}";
            }

            var openFormat = openString.Contains("-") ? "hh:mm-tt" : "HH:mm";
            
            var open = TimeOnly.ParseExact(
                openString,
                openFormat, CultureInfo.InvariantCulture);

            var closeString = openingHours.Split("–").Last().Trim().Replace(" ", "-");

            if (closeString.Split(":").First().Length <= 1)
            {
                closeString = $"0{closeString}";
            }

            var closeFormat = closeString.Contains("-") ? "hh:mm-tt" : "HH:mm";
            
            var close = TimeOnly.ParseExact(
                closeString,
                closeFormat, CultureInfo.InvariantCulture);

            var openingHoursFormatted = $"{open.ToString("HH:mm")}|{close.ToString("HH:mm")}";

            return openingHoursFormatted;
        }
        catch (Exception ex)
        {
            return null;
        }
    }
}

public class OpeningHoursCsvDataMapping : CsvMapping<OpeningHoursCsvData>
{
    public OpeningHoursCsvDataMapping() : base()
    {
        MapProperty(0, x => x.OpeningHours);
        MapProperty(1, x => x.ReferenceId);
    }
}

public class OpeningHoursDataConverter
{
    public async Task ConvertCsv()
    {
        var csvFilePath = $"{Directory.GetCurrentDirectory()}/OpeningHours.csv";

        var csvParserOptions = new CsvParserOptions(true, ',');

        var csvMapper = new OpeningHoursCsvDataMapping();

        var csvParser = new CsvParser<OpeningHoursCsvData>(csvParserOptions, csvMapper);

        var openingHoursCsv = csvParser
            .ReadFromFile(csvFilePath, Encoding.Default)
            .ToList();

        var openingHoursList = new List<OpeningHours>();
        
        foreach (var csv in openingHoursCsv)
        {
            var openingHours = csv.Result.ToOpeningHours();

            openingHoursList.Add(openingHours);
        }

        var openingHoursCsvFormattedString =
            $"Opening_Mon,Opening_Tues,Opening_Wed,Opening_Thurs,Opening_Fri,Opening_Sat,Opening_Sun,Reference_Id_Formatted{Environment.NewLine}";

        foreach (var openingHour in openingHoursList.OrderBy(o => o.ReferenceId))
        {
            openingHoursCsvFormattedString +=
                $"{openingHour.Monday},{openingHour.Tuesday},{openingHour.Wednesday},{openingHour.Thursday},{openingHour.Friday},{openingHour.Saturday},{openingHour.Sunday},{openingHour.ReferenceId}{Environment.NewLine}";
        }

        await File.WriteAllTextAsync($"{Directory.GetCurrentDirectory()}/formatted.csv",
            openingHoursCsvFormattedString, Encoding.Default);
    }
}
