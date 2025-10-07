using LibreHardwareMonitor.Hardware;
using SensorReader.Models;

namespace SensorReader.Adapters;

public class LibreHardwareAdapter : IHardwareAdapter, IDisposable
{
    private readonly Computer _computer;
    private bool _isComputerOpen = false;

    public LibreHardwareAdapter()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = false,
            IsControllerEnabled = false,
            IsPsuEnabled = false
        };
    }

    public HardwareReport? GetHardwareReport()
    {
        try
        {
            if (!_isComputerOpen)
            {
                _computer.Open();
                _isComputerOpen = true;
                Thread.Sleep(2000);
            }

            _computer.Accept(new UpdateVisitor());

            var report = new HardwareReport
            {
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            foreach (var hardware in _computer.Hardware)
            {
                ProcessHardware(hardware, report);
                foreach (var subHardware in hardware.SubHardware)
                {
                    ProcessHardware(subHardware, report);
                }
            }

            return report;
        }
        catch
        {
            return null;
        }
    }

    private void ProcessHardware(IHardware hardware, HardwareReport report)
    {
        switch (hardware.HardwareType)
        {
            case HardwareType.Cpu:
                report.Cpus.Add(CreateReport<CpuReport>(hardware));
                break;
            case HardwareType.GpuAmd:
            case HardwareType.GpuNvidia:
                report.Gpus.Add(CreateReport<GpuReport>(hardware));
                break;
            case HardwareType.Memory:
                report.MemoryModules.Add(CreateReport<MemoryReport>(hardware));
                break;
            case HardwareType.Motherboard:
            case HardwareType.SuperIO:
                report.Motherboards.Add(CreateReport<MotherboardReport>(hardware));
                break;
            case HardwareType.Storage:
                var storageReport = CreateReport<StorageReport>(hardware);
                storageReport.DriveType = GetDriveType(hardware);
                report.StorageDevices.Add(storageReport);
                break;
        }

        var fanSensors = hardware.Sensors.Where(s => s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Fan);
        foreach (var fanSensor in fanSensors)
        {
            report.Fans.Add(new Fan
            {
                Name = fanSensor.Name,
                Rpm = fanSensor.Value
            });
        }
    }

    private T CreateReport<T>(IHardware hardware) where T : HardwareComponent, new()
    {
        var componentReport = new T { Name = hardware.Name };
        foreach (var sensor in hardware.Sensors)
        {
            componentReport.Sensors.Add(new Models.Sensor
            {
                Name = sensor.Name,
                Value = sensor.Value,
                Type = ConvertSensorType(sensor.SensorType),
                Unit = GetSensorUnit(sensor.SensorType)
            });
        }
        return componentReport;
    }

    // LÓGICA ALTERNATIVA: Identifica o tipo de drive sem usar 'Rotation'
    private string GetDriveType(IHardware hardware)
    {
        if (hardware.Name.Contains("NVMe", StringComparison.OrdinalIgnoreCase)) return "NVMe SSD";
        // A detecção de HDD foi removida para evitar o bug. Podemos assumir SSD para os demais.
        return "SATA SSD/HDD";
    }

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
            LibreHardwareMonitor.Hardware.SensorType.Data => Models.SensorType.Data,
            LibreHardwareMonitor.Hardware.SensorType.Control => Models.SensorType.Control,
            // A linha 'Rotation' foi permanentemente removida daqui
            _ => Models.SensorType.Unknown,
        };
    }

    private string GetSensorUnit(LibreHardwareMonitor.Hardware.SensorType type)
    {
        return type switch
        {
            LibreHardwareMonitor.Hardware.SensorType.Voltage => "V",
            LibreHardwareMonitor.Hardware.SensorType.Clock => "MHz",
            LibreHardwareMonitor.Hardware.SensorType.Temperature => "°C",
            LibreHardwareMonitor.Hardware.SensorType.Load => "%",
            LibreHardwareMonitor.Hardware.SensorType.Fan => "RPM",
            LibreHardwareMonitor.Hardware.SensorType.Power => "W",
            LibreHardwareMonitor.Hardware.SensorType.Data => "GB",
            _ => "",
        };
    }

    public void Dispose()
    {
        if (_isComputerOpen)
        {
            _computer.Close();
        }
    }
}

internal class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer)
    {
        computer.Traverse(this);
    }

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware)
            subHardware.Accept(this);
    }

    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}
