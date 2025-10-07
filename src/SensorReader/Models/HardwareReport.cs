using System.Text.Json.Serialization;

namespace SensorReader.Models;

// --- Modelos de Sensores (Dinâmicos) ---

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
    Unknown, Temperature, Load, Fan, Clock, Power, Voltage, Data, Control, Throughput, Frequency
}


// --- Modelos de Informações de Hardware (Estáticos e Dinâmicos) ---

public class CpuInfo
{
    public string Name { get; set; } = "N/A";
    public uint NumberOfCores { get; set; }
    public uint NumberOfLogicalProcessors { get; set; }
    public uint L2CacheSize { get; set; } // Em KB
    public uint L3CacheSize { get; set; } // Em KB
    public List<Sensor> Sensors { get; set; } = new();
}

public class GpuInfo
{
    public string Name { get; set; } = "N/A";
    public string DriverVersion { get; set; } = "N/A";
    public ulong AdapterRAM { get; set; } // Em bytes
    public double AdapterRAMinGB => Math.Round(AdapterRAM / 1024.0 / 1024.0 / 1024.0, 2); // Propriedade calculada
    public List<Sensor> Sensors { get; set; } = new();
}

public class MemoryInfo
{
    public ulong TotalPhysicalMemory { get; set; } // Em bytes
    public double TotalPhysicalMemoryInGB => Math.Round(TotalPhysicalMemory / 1024.0 / 1024.0 / 1024.0, 2); // Propriedade calculada
    public List<MemoryStick> Sticks { get; set; } = new();
    public List<Sensor> GlobalSensors { get; set; } = new();
}

public class MemoryStick
{
    public string DeviceLocator { get; set; } = "N/A";
    public ulong Capacity { get; set; } // Em bytes
    public uint Speed { get; set; } // Em MHz
    public string Manufacturer { get; set; } = "N/A";
    public string PartNumber { get; set; } = "N/A";
    public string FormFactor { get; set; } = "N/A"; // Ex: DIMM
    public string MemoryType { get; set; } = "N/A"; // Ex: DDR4
}

public class MotherboardInfo
{
    public string Product { get; set; } = "N/A";
    public string Manufacturer { get; set; } = "N/A";
    public string SerialNumber { get; set; } = "N/A";
    public string BiosVersion { get; set; } = "N/A";
    public List<Sensor> Sensors { get; set; } = new();
}

public class StorageInfo
{
    public string Model { get; set; } = "N/A";
    public string MediaType { get; set; } = "Unspecified"; // AQUI VAI ENTRAR "HDD", "SSD"
    public string InterfaceType { get; set; } = "N/A";
    public ulong Size { get; set; } // Em bytes
    public double SizeInGB => Math.Round(Size / 1024.0 / 1024.0 / 1024.0, 2);
    public List<Sensor> Sensors { get; set; } = new();
}


// --- O Relatório Principal ---

public class HardwareReport
{
    public string Timestamp { get; set; } = string.Empty;
    public List<string> DataSources { get; set; } = new();
    public List<CpuInfo> Cpus { get; set; } = new();
    public List<GpuInfo> Gpus { get; set; } = new();
    public MemoryInfo Memory { get; set; } = new(); // Agora é um objeto, não uma lista
    public MotherboardInfo Motherboard { get; set; } = new(); // Agora é um objeto
    public List<StorageInfo> StorageDevices { get; set; } = new();
}
