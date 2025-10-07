using SensorReader.Models;
using System.Text;
using System.Globalization;

namespace SensorReader.Output;

public class PlainTextOutputFormatter : IOutputFormatter
{
    public void Write(HardwareReport report)
    {
        var sb = new StringBuilder();

        // Formata os dados de cada componente
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

                string key = $"GPU_{sensor.Type.ToString().ToUpper()}_{SanitizeKey(sensor.Name)}";
                sb.AppendFormat("{0}:{1};", key, sensor.Value.Value.ToString("F1", CultureInfo.InvariantCulture));
            }
        }
    }

    private void FormatMemory(HardwareReport report, StringBuilder sb)
    {
        foreach (var mem in report.MemoryModules)
        {
            foreach (var sensor in mem.Sensors)
            {
                if (!sensor.Value.HasValue) continue;

                // Converte de GiB para MiB se for um sensor de dados (Data)
                float value = sensor.Type == SensorType.Data ? sensor.Value.Value * 1024 : sensor.Value.Value;

                string key = $"RAM_{sensor.Type.ToString().ToUpper()}_{SanitizeKey(sensor.Name)}";
                sb.AppendFormat("{0}:{1};", key, value.ToString("F1", CultureInfo.InvariantCulture));
            }
        }
    }

    private void FormatMotherboard(HardwareReport report, StringBuilder sb)
    {
        foreach (var mb in report.Motherboards)
        {
            foreach (var sensor in mb.Sensors)
            {
                 if (!sensor.Value.HasValue) continue;

                string key = $"MB_{sensor.Type.ToString().ToUpper()}_{SanitizeKey(sensor.Name)}";
                sb.AppendFormat("{0}:{1};", key, sensor.Value.Value.ToString("F1", CultureInfo.InvariantCulture));
            }
        }
    }

    private void FormatStorage(HardwareReport report, StringBuilder sb)
    {
        foreach (var drive in report.StorageDevices)
        {
            foreach (var sensor in drive.Sensors)
            {
                 if (!sensor.Value.HasValue) continue;

                string key = $"STORAGE_{SanitizeKey(drive.Name)}_{sensor.Type.ToString().ToUpper()}_{SanitizeKey(sensor.Name)}";
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
        return sb.ToString().Replace("__", "_");
    }
}
