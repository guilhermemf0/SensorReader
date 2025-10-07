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

        FillCpuInfo(report);
        FillGpuInfo(report);
        FillMemoryInfo(report);
        FillMotherboardInfo(report);
        FillStorageInfo(report);

        return report;
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
            {
                report.Motherboard.BiosVersion = bios["SMBIOSBIOSVersion"]?.ToString()?.Trim() ?? "N/A";
            }
        }
    }

    private void FillStorageInfo(HardwareReport report)
    {
        // MÉTODO DEFINITIVO: Usa a API de armazenamento moderna do Windows para obter o tipo de barramento.
        var busTypeMap = new Dictionary<uint, string>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\microsoft\windows\storage");
            var query = new ObjectQuery("SELECT Number, BusType FROM MSFT_Disk");
            using var searcher = new ManagementObjectSearcher(scope, query);
            foreach (var disk in searcher.Get())
            {
                var diskNumber = (uint)disk["Number"];
                var busType = (ushort)disk["BusType"];
                busTypeMap[diskNumber] = busType switch
                {
                    11 => "SATA",
                    17 => "NVMe",
                    7  => "USB",
                    _  => "Other"
                };
            }
        }
        catch (ManagementException) { /* Ignora se a consulta moderna falhar */ }

        using var wmiDiskSearcher = new ManagementObjectSearcher("SELECT Index, Model, InterfaceType, Size FROM Win32_DiskDrive");
        foreach (var wmiDisk in wmiDiskSearcher.Get())
        {
            var index = (uint)wmiDisk["Index"];
            string mediaType = "Unspecified";

            if (busTypeMap.TryGetValue(index, out var busType))
            {
                if (busType == "NVMe")
                    mediaType = "NVMe SSD";
                else if (busType == "SATA")
                    mediaType = "SATA SSD"; // Assumimos que SATA em hardware moderno é SSD, mas poderia ser HDD.
                else
                    mediaType = busType;
            }

            report.StorageDevices.Add(new StorageInfo
            {
                Model = wmiDisk["Model"]?.ToString()?.Trim() ?? "N/A",
                InterfaceType = wmiDisk["InterfaceType"]?.ToString()?.Trim() ?? "N/A",
                Size = (ulong)wmiDisk["Size"],
                MediaType = mediaType
            });
        }
    }

    // --- Métodos Auxiliares ---
    private string ConvertCimMemoryType(object memoryType) => memoryType == null ? "Unknown" : Convert.ToUInt32(memoryType) switch { 0 => "Unknown", 20 => "DDR", 21 => "DDR2", 24 => "DDR3", 26 => "DDR4", _ => "Other" };
    private string ConvertCimMemoryFormFactor(object formFactor) => formFactor == null ? "Unknown" : Convert.ToUInt32(formFactor) switch { 8 => "DIMM", 9 => "SODIMM", _ => "Other" };
}
