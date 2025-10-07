using SensorReader.Models;
using System.Text;
using System.Globalization;

namespace SensorReader.Output;

public class PlainTextOutputFormatter : IOutputFormatter
{
    public void Write(HardwareReport report)
    {
        var sb = new StringBuilder();

        // Formata os dados de cada componente com a nova estrutura
        FormatCpu(report, sb);
        FormatGpu(report, sb);
        FormatMemory(report, sb);
        FormatMotherboard(report, sb);
        FormatStorage(report, sb);

        // Imprime a string final no console
        Console.Write(sb.ToString());
    }

    private void FormatCpu(HardwareReport report, StringBuilder sb)
    {
        foreach (var cpu in report.Cpus)
        {
            foreach (var sensor in cpu.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                string key = $"CPU_{sensor.Type.ToString().ToUpper()}_{SanitizeKey(sensor.Name)}";
                sb.AppendFormat("{0}:{1};", key, sensor.Value.Value.ToString("F1", CultureInfo.InvariantCulture));
            }
        }
    }

    private void FormatGpu(HardwareReport report, StringBuilder sb)
    {
        foreach (var gpu in report.Gpus)
        {
            foreach (var sensor in gpu.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                string key = $"GPU_{SanitizeKey(gpu.Name)}_{sensor.Type.ToString().ToUpper()}_{SanitizeKey(sensor.Name)}";
                sb.AppendFormat("{0}:{1};", key, sensor.Value.Value.ToString("F1", CultureInfo.InvariantCulture));
            }
        }
    }

    private void FormatMemory(HardwareReport report, StringBuilder sb)
    {
        // Agora acessa report.Memory.GlobalSensors
        foreach (var sensor in report.Memory.GlobalSensors)
        {
            if (!sensor.Value.HasValue) continue;
            string key = $"MEMORY_{sensor.Type.ToString().ToUpper()}_{SanitizeKey(sensor.Name)}";
            sb.AppendFormat("{0}:{1};", key, sensor.Value.Value.ToString("F1", CultureInfo.InvariantCulture));
        }
    }

    private void FormatMotherboard(HardwareReport report, StringBuilder sb)
    {
        // Agora acessa report.Motherboard.Sensors
        foreach (var sensor in report.Motherboard.Sensors)
        {
            if (!sensor.Value.HasValue) continue;
            string key = $"MB_{sensor.Type.ToString().ToUpper()}_{SanitizeKey(sensor.Name)}";
            sb.AppendFormat("{0}:{1};", key, sensor.Value.Value.ToString("F1", CultureInfo.InvariantCulture));
        }
    }

    private void FormatStorage(HardwareReport report, StringBuilder sb)
    {
        foreach (var drive in report.StorageDevices)
        {
            foreach (var sensor in drive.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                // Agora usa drive.Model em vez de drive.Name
                string key = $"STORAGE_{SanitizeKey(drive.Model)}_{sensor.Type.ToString().ToUpper()}_{SanitizeKey(sensor.Name)}";
                sb.AppendFormat("{0}:{1};", key, sensor.Value.Value.ToString("F1", CultureInfo.InvariantCulture));
            }
        }
    }

    private string SanitizeKey(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var sb = new StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (c == ' ' || c == '_' || c == '-')
            {
                sb.Append('_');
            }
        }
        return sb.ToString().ToUpper();
    }
}
