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
        FillCpuInfo(report);
        FillGpuInfo(report);
        FillMemoryInfo(report);
        FillMotherboardInfo(report);
        FillStorageInfo(report);

        return report;
    }

    // --- Métodos de Coleta de Dados (sem alterações aqui) ---

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
        using var searcher = new ManagementObjectSearcher("select * from Win32_NetworkAdapterConfiguration where IPEnabled=TRUE");
        foreach (var obj in searcher.Get())
        {
            var adapter = new NetworkAdapterInfo
            {
                Name = obj["Description"]?.ToString()?.Trim() ?? "N/A",
                MacAddress = obj["MacAddress"]?.ToString() ?? "N/A",
            };
            if (obj["IPAddress"] is string[] addresses) { adapter.IpAddresses.AddRange(addresses); }
            report.NetworkAdapters.Add(adapter);
        }
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
                    MemoryType = ConvertCimMemoryType(obj["MemoryType"])
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
        foreach (ManagementObject wmiDisk in wmiDiskSearcher.Get()) // Cast aqui para garantir o tipo
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

        // CORRIGIDO: Adicionado o cast para ManagementObject
        foreach (ManagementObject partition in partitionSearcher.Get())
        {
            var logicalDiskQuery = new RelatedObjectQuery($"associators of {{{partition.Path.Path}}} where AssocClass = Win32_LogicalDiskToPartition");
            using var logicalDiskSearcher = new ManagementObjectSearcher(logicalDiskQuery);

            // CORRIGIDO: Adicionado o cast para ManagementObject
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

    private string ConvertCimMemoryType(object memoryType) => memoryType == null ? "Unknown" : Convert.ToUInt32(memoryType) switch { 0 => "Unknown", 20 => "DDR", 21 => "DDR2", 24 => "DDR3", 26 => "DDR4", _ => "Other" };
    private string ConvertCimMemoryFormFactor(object formFactor) => formFactor == null ? "Unknown" : Convert.ToUInt32(formFactor) switch { 8 => "DIMM", 9 => "SODIMM", _ => "Other" };
}
