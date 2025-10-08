#pragma warning disable CA1416
using LibreHardwareMonitor.Hardware;
using SensorReader.Models;
using System.Linq;

namespace SensorReader.Adapters;

public class LibreHardwareAdapter : IHardwareAdapter, IDisposable
{
    private readonly Computer _computer;

    public LibreHardwareAdapter()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true
        };
        _computer.Open();
        Thread.Sleep(2000);
    }

    public HardwareReport GetHardwareReport(HardwareReport? existingReport = null)
    {
        var report = existingReport ?? new HardwareReport();
        if (!report.DataSources.Contains(GetType().Name))
        {
            report.DataSources.Add(GetType().Name);
        }

        _computer.Accept(new UpdateVisitor());

        foreach (var hardware in _computer.Hardware)
        {
            ProcessHardware(hardware, report);
        }
        return report;
    }

    HardwareReport IHardwareAdapter.GetHardwareReport() => GetHardwareReport(null);

    private void ProcessHardware(IHardware hardware, HardwareReport report)
    {
        var sensors = GetSensors(hardware);

        switch (hardware.HardwareType)
        {
            case HardwareType.Cpu:
                // Assume o primeiro CPU da lista
                report.Cpus.FirstOrDefault()?.Sensors.AddRange(sensors);
                break;
            case HardwareType.GpuAmd:
            case HardwareType.GpuNvidia:
                 // Assume a primeira GPU da lista
                report.Gpus.FirstOrDefault()?.Sensors.AddRange(sensors);
                break;
            case HardwareType.Memory:
                report.Memory.GlobalSensors.AddRange(sensors);
                break;
            case HardwareType.Motherboard:
            case HardwareType.SuperIO:
                 // Atribui todos os sensores à única placa-mãe
                report.Motherboard.Sensors.AddRange(sensors);
                break;
            case HardwareType.Storage:
                // Lógica de correspondência aprimorada para armazenamento
                var storageDevice = report.StorageDevices
                    .FirstOrDefault(s => SanitizeName(hardware.Name).Contains(SanitizeName(s.Model)));
                storageDevice?.Sensors.AddRange(sensors);
                break;
        }

        // Processar sub-hardware (útil para alguns componentes)
        foreach (var subHardware in hardware.SubHardware)
        {
            ProcessHardware(subHardware, report);
        }
    }

    private IEnumerable<Models.Sensor> GetSensors(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
        {
            // APERFEIÇOADO: Se o valor for NaN ou Infinito, ele se torna null
            float? sensorValue = sensor.Value;
            if (sensorValue.HasValue && (float.IsNaN(sensorValue.Value) || float.IsInfinity(sensorValue.Value)))
            {
                sensorValue = null;
            }

            yield return new Models.Sensor
            {
                Name = sensor.Name,
                Value = sensorValue,
                Type = ConvertSensorType(sensor.SensorType),
                Unit = GetSensorUnit(sensor.SensorType),
                DataSource = "LibreHardwareMonitor" // ADICIONAR ESTA LINHA
            };
        }
    }

    private string SanitizeName(string name)
    {
        return name.Replace(" ", "").ToUpperInvariant();
    }

    // Métodos ConvertSensorType e GetSensorUnit permanecem os mesmos...

    private Models.SensorType ConvertSensorType(LibreHardwareMonitor.Hardware.SensorType type)
    {
        return type switch
        {
            LibreHardwareMonitor.Hardware.SensorType.Voltage => Models.SensorType.Voltage,
            LibreHardwareMonitor.Hardware.SensorType.Clock => Models.SensorType.Clock,
            LibreHardwareMonitor.Hardware.SensorType.Temperature => Models.SensorType.Temperature,
            LibreHardwareMonitor.Hardware.SensorType.Load => Models.SensorType.Load,
            LibreHardwareMonitor.Hardware.SensorType.Fan => Models.SensorType.Fan,
            LibreHardwareMonitor.Hardware.SensorType.Power => Models.SensorType.Power,
            LibreHardwareMonitor.Hardware.SensorType.Control => Models.SensorType.Control,
            LibreHardwareMonitor.Hardware.SensorType.Frequency => Models.SensorType.Frequency,
            LibreHardwareMonitor.Hardware.SensorType.Throughput => Models.SensorType.Throughput,

            LibreHardwareMonitor.Hardware.SensorType.Data => Models.SensorType.Data,
            LibreHardwareMonitor.Hardware.SensorType.SmallData => Models.SensorType.SmallData,

            _ => Models.SensorType.Unknown,
        };
    }

    private string GetSensorUnit(LibreHardwareMonitor.Hardware.SensorType type)
    {
        return type switch
        {
            LibreHardwareMonitor.Hardware.SensorType.Voltage => "V",
            LibreHardwareMonitor.Hardware.SensorType.Clock or LibreHardwareMonitor.Hardware.SensorType.Frequency => "MHz",
            LibreHardwareMonitor.Hardware.SensorType.Temperature => "°C",
            LibreHardwareMonitor.Hardware.SensorType.Load => "%",
            LibreHardwareMonitor.Hardware.SensorType.Fan => "RPM",
            LibreHardwareMonitor.Hardware.SensorType.Power => "W",
            LibreHardwareMonitor.Hardware.SensorType.Data => "GB",
            LibreHardwareMonitor.Hardware.SensorType.SmallData => "MB", // Unidade adicionada
            LibreHardwareMonitor.Hardware.SensorType.Throughput => "B/s",
            _ => "",
        };
    }

    public void Dispose() => _computer.Close();
}

internal class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);
    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware)
            subHardware.Accept(this);
    }
    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}
