#pragma warning disable CA1416 // Validate platform compatibility
using System.Management;
using SensorReader.Models;
using System.Linq;
using System.Collections.Generic;

namespace SensorReader.Adapters;

public class WmiAdapter : IHardwareAdapter
{
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

    // --- Métodos de Coleta de Dados ---

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
                    Name = obj["Name"]?.ToString()?.Trim() ?? "N/A",
                    Chemistry = (ushort)obj["Chemistry"],
                    DesignCapacity = obj["DesignCapacity"] != null ? (uint)obj["DesignCapacity"] : 0,
                    FullChargeCapacity = obj["FullChargeCapacity"] != null ? (uint)obj["FullChargeCapacity"] : 0,
                    Status = obj["Status"] != null ? ushort.Parse(obj["Status"].ToString()!) : (ushort)0,
                    EstimatedChargeRemaining = obj["EstimatedChargeRemaining"] != null ? (ushort)obj["EstimatedChargeRemaining"] : (ushort)0,
                    EstimatedRunTime = obj["EstimatedRunTime"] != null ? (uint)obj["EstimatedRunTime"] : 0,
                    TimeToFullCharge = obj["TimeToFullCharge"] != null ? (uint)obj["TimeToFullCharge"] : 0
                });
            }
        }
        catch (ManagementException) { /* Ignora se a consulta falhar (ex: em desktops) */ }
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
                    // LÓGICA DE FALLBACK: Tenta a propriedade mais nova primeiro, depois a mais antiga.
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

    private void FillStorageInfo(HardwareReport report)
    {
        var busTypeMap = new Dictionary<uint, string>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\microsoft\windows\storage");
            var query = new ObjectQuery("SELECT Number, BusType FROM MSFT_Disk");
            using var searcher = new ManagementObjectSearcher(scope, query);
            foreach (var disk in searcher.Get())
            {
                busTypeMap[(uint)disk["Number"]] = (ushort)disk["BusType"] switch { 11 => "SATA", 17 => "NVMe", 7 => "USB", _ => "Other" };
            }
        }
        catch (ManagementException) { /* Ignora */ }

        using var wmiDiskSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
        foreach (ManagementObject wmiDisk in wmiDiskSearcher.Get())
        {
            var index = (uint)wmiDisk["Index"];
            string mediaType = "Unspecified";
            if (busTypeMap.TryGetValue(index, out var busType))
            {
                mediaType = busType == "NVMe" ? "NVMe SSD" : (busType == "SATA" ? "SATA SSD" : busType);
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

    // --- Métodos Auxiliares ---
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

    private string ConvertCimMemoryType(object memoryType) => memoryType == null ? "Unknown" : Convert.ToUInt32(memoryType) switch
    {
        0x0 => "Unknown", 0x14 => "DDR", 0x15 => "DDR2", 0x18 => "DDR3", 0x1A => "DDR4", _ => "Other"
    };

    private string ConvertCimMemoryFormFactor(object formFactor) => formFactor == null ? "Unknown" : Convert.ToUInt32(formFactor) switch
    {
        8 => "DIMM", 9 => "SODIMM", _ => "Other"
    };
}
