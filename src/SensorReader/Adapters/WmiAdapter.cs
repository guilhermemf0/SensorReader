#pragma warning disable CA1416 // Validate platform compatibility
using System.Management;
using SensorReader.Models;
using System.Linq;
using System.Collections.Generic;

namespace SensorReader.Adapters;

public class WmiAdapter : IHardwareAdapter
{
    // ... (métodos GetHardwareReport, FillOsInfo, FillNetworkInfo, FillBatteryInfo, FillCpuInfo, FillGpuInfo, FillMemoryInfo, FillMotherboardInfo permanecem os mesmos)
    public HardwareReport GetHardwareReport()
    {
        var report = new HardwareReport();
        report.DataSources.Add(GetType().Name);

        FillOsInfo(report);
        FillNetworkInfo(report);
        FillBatteryInfo(report);
        FillCpuInfo(report);
        FillGpuInfo(report);
        FillMemoryInfo(report);
        FillMotherboardInfo(report);
        FillStorageInfo(report);

        return report;
    }

    private void FillOsInfo(HardwareReport report)
    {
        using var searcher = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
        var os = searcher.Get().OfType<ManagementObject>().FirstOrDefault();
        if (os != null)
        {
            report.OsInfo.Caption = os["Caption"]?.ToString()?.Trim() ?? "N/A";
            report.OsInfo.Version = os["Version"]?.ToString() ?? "N/A";
            report.OsInfo.BuildNumber = os["BuildNumber"]?.ToString() ?? "N/A";
            report.OsInfo.OsArchitecture = os["OSArchitecture"]?.ToString() ?? "N/A";
            report.OsInfo.InstallDate = ManagementDateTimeConverter.ToDateTime(os["InstallDate"]?.ToString() ?? "");
            report.OsInfo.LastBootUpTime = ManagementDateTimeConverter.ToDateTime(os["LastBootUpTime"]?.ToString() ?? "");
        }
    }

    private void FillNetworkInfo(HardwareReport report)
    {
        var adapterTypes = new Dictionary<uint, string>();
        try
        {
            using var adapterSearcher = new ManagementObjectSearcher("SELECT Index, AdapterType FROM Win32_NetworkAdapter");
            foreach (var item in adapterSearcher.Get())
            {
                adapterTypes[(uint)item["Index"]] = item["AdapterType"]?.ToString() ?? "N/A";
            }
        }
        catch (ManagementException) { /* Ignora */ }

        var query = "SELECT Index, Description, MACAddress, IPAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=TRUE";
        using var configSearcher = new ManagementObjectSearcher(query);
        foreach (var obj in configSearcher.Get())
        {
            var index = (uint)obj["Index"];
            var adapter = new NetworkAdapterInfo
            {
                Name = obj["Description"]?.ToString()?.Trim() ?? "N/A",
                MacAddress = obj["MACAddress"]?.ToString() ?? "N/A",
                AdapterType = adapterTypes.TryGetValue(index, out var type) ? type : "N/A"
            };

            if (obj["IPAddress"] is string[] addresses) { adapter.IpAddresses.AddRange(addresses); }
            report.NetworkAdapters.Add(adapter);
        }
    }

    private void FillBatteryInfo(HardwareReport report)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("select * from Win32_Battery");
            foreach (ManagementObject obj in searcher.Get())
            {
                report.Batteries.Add(new BatteryInfo
                {
                    Name = GetPropertyValue<string>(obj, "Name") ?? "N/A",
                    Status = ConvertBatteryStatus(GetPropertyValue<ushort>(obj, "Status")),
                    Chemistry = ConvertBatteryChemistry(GetPropertyValue<ushort>(obj, "Chemistry")),
                    DesignCapacity = GetPropertyValue<uint>(obj, "DesignCapacity"),
                    FullChargeCapacity = GetPropertyValue<uint>(obj, "FullChargeCapacity"),
                    EstimatedChargeRemaining = GetPropertyValue<ushort>(obj, "EstimatedChargeRemaining"),
                    EstimatedRunTime = GetPropertyValue<uint>(obj, "EstimatedRunTime")
                });
            }
        }
        catch (ManagementException) { /* Ignora */ }
    }

    private void FillCpuInfo(HardwareReport report)
    {
        using var searcher = new ManagementObjectSearcher("select * from Win32_Processor");
        foreach (var obj in searcher.Get())
        {
            report.Cpus.Add(new CpuInfo
            {
                Name = obj["Name"]?.ToString()?.Trim() ?? "N/A",
                NumberOfCores = (uint)obj["NumberOfCores"],
                NumberOfLogicalProcessors = (uint)obj["NumberOfEnabledCore"],
                L2CacheSize = (uint)obj["L2CacheSize"],
                L3CacheSize = (uint)obj["L3CacheSize"]
            });
        }
    }

    private void FillGpuInfo(HardwareReport report)
    {
        using var searcher = new ManagementObjectSearcher("select * from Win32_VideoController");
        foreach (var obj in searcher.Get())
        {
            report.Gpus.Add(new GpuInfo
            {
                Name = obj["Name"]?.ToString()?.Trim() ?? "N/A",
                DriverVersion = obj["DriverVersion"]?.ToString()?.Trim() ?? "N/A",
                AdapterRAM = Convert.ToUInt64(obj["AdapterRAM"])
            });
        }
    }

    private void FillMemoryInfo(HardwareReport report)
    {
        using (var searcher = new ManagementObjectSearcher("select * from Win32_ComputerSystem"))
        {
            var compSystem = searcher.Get().OfType<ManagementObject>().FirstOrDefault();
            if (compSystem != null)
                report.Memory.TotalPhysicalMemory = (ulong)compSystem["TotalPhysicalMemory"];
        }

        using (var searcher = new ManagementObjectSearcher("select * from Win32_PhysicalMemory"))
        {
            foreach (var obj in searcher.Get())
            {
                report.Memory.Sticks.Add(new MemoryStick
                {
                    DeviceLocator = obj["DeviceLocator"]?.ToString()?.Trim() ?? "N/A",
                    Capacity = (ulong)obj["Capacity"],
                    Speed = (uint)obj["Speed"],
                    Manufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "N/A",
                    PartNumber = obj["PartNumber"]?.ToString()?.Trim() ?? "N/A",
                    FormFactor = ConvertCimMemoryFormFactor(obj["FormFactor"]),
                    MemoryType = ConvertCimMemoryType(obj["SMBIOSMemoryType"] ?? obj["MemoryType"])
                });
            }
        }
    }

    private void FillMotherboardInfo(HardwareReport report)
    {
        using (var searcher = new ManagementObjectSearcher("select * from Win32_BaseBoard"))
        {
            var board = searcher.Get().OfType<ManagementObject>().FirstOrDefault();
            if (board != null)
            {
                report.Motherboard.Product = board["Product"]?.ToString()?.Trim() ?? "N/A";
                report.Motherboard.Manufacturer = board["Manufacturer"]?.ToString()?.Trim() ?? "N/A";
                report.Motherboard.SerialNumber = board["SerialNumber"]?.ToString()?.Trim() ?? "N/A";
            }
        }
        using (var searcher = new ManagementObjectSearcher("select * from Win32_BIOS"))
        {
            var bios = searcher.Get().OfType<ManagementObject>().FirstOrDefault();
            if (bios != null)
                report.Motherboard.BiosVersion = bios["SMBIOSBIOSVersion"]?.ToString()?.Trim() ?? "N/A";
        }
    }


    // MÉTODO COMPLETAMENTE REESCRITO COM VERIFICAÇÃO CRUZADA
    private void FillStorageInfo(HardwareReport report)
    {
        var driveData = new Dictionary<string, (string busType, uint rotationRate)>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\microsoft\windows\storage");
            var query = new ObjectQuery("SELECT DeviceID, BusType, NominalMediaRotationRate FROM MSFT_PhysicalDisk");
            using var searcher = new ManagementObjectSearcher(scope, query);
            foreach (var disk in searcher.Get())
            {
                string deviceId = disk["DeviceID"]?.ToString() ?? "";
                var busType = (ushort)disk["BusType"];
                var rotationRate = disk["NominalMediaRotationRate"] != null ? (uint)disk["NominalMediaRotationRate"] : 0;

                driveData[deviceId] = (busType switch { 11 => "SATA", 17 => "NVMe", 7 => "USB", _ => "Other" }, rotationRate);
            }
        }
        catch (ManagementException) { /* Ignora se a consulta moderna falhar */ }

        using var wmiDiskSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
        foreach (ManagementObject wmiDisk in wmiDiskSearcher.Get())
        {
            string mediaType = "Unspecified";
            string wmiDeviceId = wmiDisk["DeviceID"]?.ToString()?.Replace("PHYSICALDRIVE", "") ?? "-1";

            if (driveData.TryGetValue(wmiDeviceId, out var data))
            {
                if (data.busType == "NVMe")
                    mediaType = "NVMe SSD";
                else if (data.busType == "SATA")
                    mediaType = data.rotationRate > 1 ? "HDD" : "SATA SSD"; // A verificação crucial!
                else
                    mediaType = data.busType;
            }

            var storageInfo = new StorageInfo
            {
                Model = wmiDisk["Model"]?.ToString()?.Trim() ?? "N/A",
                InterfaceType = wmiDisk["InterfaceType"]?.ToString()?.Trim() ?? "N/A",
                Size = (ulong)wmiDisk["Size"],
                MediaType = mediaType
            };

            storageInfo.LogicalDisks = GetLogicalDisksForDrive(wmiDisk.Path.Path);
            report.StorageDevices.Add(storageInfo);
        }
    }

    // NOVO MÉTODO DE FALLBACK PARA TEMPERATURA, MAIS INTELIGENTE
    public void FillMissingTemperatures(HardwareReport report)
    {
        bool cpuTempMissing = !report.Cpus.Any(c => c.Sensors.Any(s => s.Type == SensorType.Temperature));
        // A lógica pode ser expandida para GPU, etc., no futuro.

        if (!cpuTempMissing) return;

        try
        {
            var scope = new ManagementScope("root\\WMI");
            var query = new ObjectQuery("SELECT CurrentTemperature, InstanceName FROM MSAcpi_ThermalZoneTemperature");
            using var searcher = new ManagementObjectSearcher(scope, query);

            foreach (var obj in searcher.Get())
            {
                var tempKelvin = GetPropertyValue<uint>(obj, "CurrentTemperature");
                if (tempKelvin > 0)
                {
                    var tempCelsius = (tempKelvin / 10.0f) - 273.15f;
                    var instanceName = GetPropertyValue<string>(obj, "InstanceName") ?? "Thermal Zone";

                    var sensor = new Sensor
                    {
                        Name = instanceName,
                        Value = tempCelsius,
                        Type = SensorType.Temperature,
                        Unit = "°C",
                        DataSource = "WMI_Fallback"
                    };

                    // Lógica de atribuição: se o nome indicar CPU, atribui à CPU. Senão, à placa-mãe.
                    if (instanceName.ToUpper().Contains("CPU"))
                    {
                        report.Cpus.FirstOrDefault()?.Sensors.Add(sensor);
                    }
                    else
                    {
                        report.Motherboard.Sensors.Add(sensor);
                    }
                }
            }
        }
        catch (ManagementException) { /* Ignora se a consulta de fallback falhar */ }
    }

    // --- MÉTODOS AUXILIARES ---
    // ... (GetPropertyValue, GetLogicalDisksForDrive, e os conversores de tipo permanecem os mesmos)
    private T GetPropertyValue<T>(ManagementBaseObject obj, string propertyName)
    {
        try
        {
            var value = obj[propertyName];
            if (value == null) return default(T)!;
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch { return default(T)!; }
    }

    private List<LogicalDiskInfo> GetLogicalDisksForDrive(string diskDrivePath)
    {
        var logicalDisks = new List<LogicalDiskInfo>();
        var partitionQuery = new RelatedObjectQuery($"associators of {{{diskDrivePath}}} where AssocClass = Win32_DiskDriveToDiskPartition");
        using var partitionSearcher = new ManagementObjectSearcher(partitionQuery);

        foreach (ManagementObject partition in partitionSearcher.Get())
        {
            var logicalDiskQuery = new RelatedObjectQuery($"associators of {{{partition.Path.Path}}} where AssocClass = Win32_LogicalDiskToPartition");
            using var logicalDiskSearcher = new ManagementObjectSearcher(logicalDiskQuery);

            foreach (ManagementObject logicalDisk in logicalDiskSearcher.Get())
            {
                logicalDisks.Add(new LogicalDiskInfo
                {
                    DeviceID = logicalDisk["DeviceID"]?.ToString() ?? "N/A",
                    FileSystem = logicalDisk["FileSystem"]?.ToString() ?? "N/A",
                    Size = (ulong)logicalDisk["Size"],
                    FreeSpace = (ulong)logicalDisk["FreeSpace"]
                });
            }
        }
        return logicalDisks;
    }

    private string ConvertCimMemoryType(object memoryType) => memoryType == null ? "Unknown" : Convert.ToUInt32(memoryType) switch { 0x0 => "Unknown", 0x14 => "DDR", 0x15 => "DDR2", 0x18 => "DDR3", 0x1A => "DDR4", _ => "Other" };
    private string ConvertCimMemoryFormFactor(object formFactor) => formFactor == null ? "Unknown" : Convert.ToUInt32(formFactor) switch { 8 => "DIMM", 9 => "SODIMM", _ => "Other" };
    private string ConvertBatteryChemistry(ushort chemistryCode) => chemistryCode switch { 1 => "Other", 2 => "Unknown", 3 => "Lead Acid", 4 => "Nickel Cadmium", 5 => "Nickel Metal Hydride", 6 => "Lithium-ion", 7 => "Zinc air", 8 => "Lithium Polymer", _ => "N/A" };
    private string ConvertBatteryStatus(ushort statusCode) => statusCode switch { 1 => "Discharging", 2 => "On AC", 3 => "Fully Charged", 4 => "Low", 5 => "Critical", 6 => "Charging", 7 => "Charging and High", 8 => "Charging and Low", 9 => "Charging and Critical", 10 => "Undefined", 11 => "Partially Charged", _ => "Unknown" };
}
