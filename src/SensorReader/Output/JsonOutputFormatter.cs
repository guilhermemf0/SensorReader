using System.Text.Json;
using System.Text.Json.Serialization; // Adicionado este using
using SensorReader.Models;

namespace SensorReader.Output;

public class JsonOutputFormatter : IOutputFormatter
{
    public void Write(HardwareReport report)
    {
        // CORRIGIDO: Adiciona opções para lidar com números especiais (Infinito, NaN)
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            // Permite que o serializador escreva "Infinity" e "NaN" no JSON
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        string jsonString = JsonSerializer.Serialize(report, options);
        Console.WriteLine(jsonString);
    }
}
