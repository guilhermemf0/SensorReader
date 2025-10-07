using System.Text.Json;
using SensorReader.Models;

namespace SensorReader.Output;

public class JsonOutputFormatter : IOutputFormatter
{
    public void Write(HardwareReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        string jsonString = JsonSerializer.Serialize(report, options);
        Console.WriteLine(jsonString);
    }
}
