using System.Text.Json.Serialization;

namespace SensorReader.Models;

public class Sensor
{
    public string Name { get; set; } = string.Empty;
    public float? Value { get; set; }
    public SensorType Type { get; set; }
    public string Unit { get; set; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SensorType
{
    Unknown, Temperature, Load, Fan, Clock, Power, Voltage, Data, Control, Rotation
}

public abstract class HardwareComponent
{
    public string Name { get; set; } = string.Empty;
    public List<Sensor> Sensors { get; set; } = new();
}

public class CpuReport : HardwareComponent { }
public class GpuReport : HardwareComponent { }
public class MemoryReport : HardwareComponent { }
public class MotherboardReport : HardwareComponent { }

public class StorageReport : HardwareComponent
{
    public string DriveType { get; set; } = string.Empty;
}

public class Fan
{
    public string Name { get; set; } = string.Empty;
    public float? Rpm { get; set; }
}

public class HardwareReport
{
    public string Timestamp { get; set; } = string.Empty;
    public string AdapterUsed { get; set; } = "None";
    public List<CpuReport> Cpus { get; set; } = new();
    public List<GpuReport> Gpus { get; set; } = new();
    public List<MemoryReport> MemoryModules { get; set; } = new();
    public List<MotherboardReport> Motherboards { get; set; } = new();
    public List<StorageReport> StorageDevices { get; set; } = new();
    public List<Fan> Fans { get; set; } = new();
}
