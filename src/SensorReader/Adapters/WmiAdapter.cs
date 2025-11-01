using System.Management;
using SensorReader.Models;
using System.Linq;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text.Json;
using System;

namespace SensorReader.Adapters;

public class WmiAdapter : IHardwareAdapter
{
    private class PsDiskInfo
    {
        public string DeviceId { get; set; } = "";
        public string Model { get; set; } = "";
        public ushort BusType { get; set; }
        public ushort MediaType { get; set; }
        public uint SpindleSpeed { get; set; }
    }

    private struct MsftDiskInfo
    {
        public uint RotationRate { get; set; }
        public ushort MediaType { get; set; }
    }

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
        catch (ManagementException) { }

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
                var estimatedRunTime = GetPropertyValue<uint>(obj, "EstimatedRunTime");
                if (estimatedRunTime == 71582788) estimatedRunTime = 0;

                report.Batteries.Add(new BatteryInfo
                {
                    Name = GetPropertyValue<string>(obj, "Name") ?? "N/A",
                    Status = ConvertBatteryStatus(GetPropertyValue<ushort>(obj, "Status")),
                    Chemistry = ConvertBatteryChemistry(GetPropertyValue<ushort>(obj, "Chemistry")),
                    DesignCapacity = GetPropertyValue<uint>(obj, "DesignCapacity"),
                    FullChargeCapacity = GetPropertyValue<uint>(obj, "FullChargeCapacity"),
                    EstimatedChargeRemaining = GetPropertyValue<ushort>(obj, "EstimatedChargeRemaining"),
                    EstimatedRunTime = estimatedRunTime
                });
            }
        }
        catch (ManagementException) { }
    }

    private void FillCpuInfo(HardwareReport report)
    {
        try
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
        catch (ManagementException)
        {
            Console.Error.WriteLine("Falha ao consultar Win32_Processor.");
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


    private void FillStorageInfo(HardwareReport report)
    {
        var psDiskDetails = GetDiskDetailsWithPowerShell();

        var msftDiskDetails = new Dictionary<string, MsftDiskInfo>();
        if (psDiskDetails == null)
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\microsoft\windows\storage");
                var query = new ObjectQuery("SELECT DeviceID, NominalMediaRotationRate, MediaType FROM MSFT_PhysicalDisk");
                using var searcher = new ManagementObjectSearcher(scope, query);
                foreach (var disk in searcher.Get())
                {
                    msftDiskDetails[GetPropertyValue<string>(disk, "DeviceID") ?? ""] = new MsftDiskInfo
                    {
                        RotationRate = GetPropertyValue<uint>(disk, "NominalMediaRotationRate"),
                        MediaType = GetPropertyValue<ushort>(disk, "MediaType")
                    };
                }
            }
            catch (ManagementException) { }
        }

        using var wmiDiskSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
        foreach (ManagementObject wmiDisk in wmiDiskSearcher.Get())
        {
            string mediaType = "Unspecified";
            string diskIndex = GetPropertyValue<uint>(wmiDisk, "Index").ToString();
            string interfaceType = GetPropertyValue<string>(wmiDisk, "InterfaceType")?.Trim() ?? "N/A";
            string model = GetPropertyValue<string>(wmiDisk, "Model")?.Trim().ToUpperInvariant() ?? "N/A";
            bool identified = false;

            if (psDiskDetails != null && psDiskDetails.TryGetValue(diskIndex, out var psData))
            {
                if (psData.BusType == 17)
                {
                    mediaType = "NVMe SSD";
                    identified = true;
                }
                else if (psData.BusType == 11 && psData.MediaType == 4)
                {
                    mediaType = "SATA SSD";
                    identified = true;
                }
                else if (psData.MediaType == 4)
                {
                    mediaType = "SSD";
                    identified = true;
                }
                else if (psData.MediaType == 3 || psData.SpindleSpeed > 1)
                {
                    mediaType = "HDD";
                    identified = true;
                }
            }

            if (!identified && msftDiskDetails.TryGetValue(diskIndex, out var msftData))
            {
                if (msftData.MediaType == 4)
                {
                    if (interfaceType.Equals("SATA", StringComparison.OrdinalIgnoreCase))
                        mediaType = "SATA SSD";
                    else if (model.Contains("NVME") || model.Contains("SNV"))
                        mediaType = "NVMe SSD";
                    else
                        mediaType = "SSD";
                    identified = true;
                }
                else if (msftData.MediaType == 3 || msftData.RotationRate > 1)
                {
                    mediaType = "HDD";
                    identified = true;
                }
            }

            if (!identified)
            {
                if (model.Contains("SNV") || model.Contains("IM2P") || model.Contains("NVME"))
                {
                    mediaType = "NVMe SSD";
                    identified = true;
                }
                else if (model.StartsWith("TOSHIBA MQ") || model.StartsWith("ST") || model.StartsWith("WD") || model.StartsWith("HGST"))
                {
                    if (!model.Contains("SSD") && !model.Contains(" SN"))
                    {
                        mediaType = "HDD";
                        identified = true;
                    }
                }
            }

            var storageInfo = new StorageInfo
            {
                Model = wmiDisk["Model"]?.ToString()?.Trim() ?? "N/A",
                InterfaceType = interfaceType,
                Size = GetPropertyValue<ulong>(wmiDisk, "Size"),
                MediaType = mediaType
            };

            storageInfo.LogicalDisks = GetLogicalDisksForDrive(wmiDisk.Path.Path);
            report.StorageDevices.Add(storageInfo);
        }
    }


    private Dictionary<string, PsDiskInfo>? GetDiskDetailsWithPowerShell()
    {
        try
        {
            using var ps = PowerShell.Create();

            ps.AddScript("Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -Force");
            ps.AddScript("$Error.Clear()");

            ps.AddScript("Get-PhysicalDisk | Select-Object DeviceId, Model, BusType, MediaType, SpindleSpeed | ConvertTo-Json -Depth 1");

            var results = ps.Invoke();
            if (ps.HadErrors || results.Count == 0) return null;

            var json = results[0].BaseObject as string;
            if (string.IsNullOrEmpty(json)) return null;

            if (!json.TrimStart().StartsWith("[")) json = $"[{json}]";

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var psDisks = JsonSerializer.Deserialize<List<PsDiskInfo>>(json, options);

            return psDisks?.ToDictionary(d => d.DeviceId, d => d);
        }
        catch
        {
            return null;
        }
    }

    public void FillMissingTemperatures(HardwareReport report)
    {
        bool cpuTempMissing = !report.Cpus.Any(c => c.Sensors.Any(s => s.Type == SensorType.Temperature && s.DataSource != "WMI_Fallback"));
        bool gpuTempMissing = !report.Gpus.Any(g => g.Sensors.Any(s => s.Type == SensorType.Temperature && s.DataSource != "WMI_Fallback"));
        bool mbTempMissing = !report.Motherboard.Sensors.Any(s => s.Type == SensorType.Temperature && s.DataSource != "WMI_Fallback");

        if (!cpuTempMissing && !mbTempMissing && !gpuTempMissing) return;

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
                        Name = instanceName.Replace("ACPI\\ThermalZone\\", ""),
                        Value = tempCelsius,
                        Type = SensorType.Temperature,
                        Unit = "°C",
                        DataSource = "WMI_Fallback"
                    };

                    string upperInstance = instanceName.ToUpperInvariant();

                    if (cpuTempMissing && (upperInstance.Contains("CPU") || upperInstance.Contains("PROCESSOR") || upperInstance.Contains("TC1P")))
                    {
                        report.Cpus.FirstOrDefault()?.Sensors.Add(sensor);
                    }

                    else if (gpuTempMissing && (upperInstance.Contains("GPU") || upperInstance.Contains("VIDEO") || upperInstance.Contains("GFX") || upperInstance.Contains("TGP")))
                    {
                        report.Gpus.FirstOrDefault()?.Sensors.Add(sensor);
                    }

                    else if (mbTempMissing)
                    {
                        if (!report.Motherboard.Sensors.Any(s => s.Name == sensor.Name && s.DataSource == "WMI_Fallback"))
                        {
                            report.Motherboard.Sensors.Add(sensor);
                        }
                    }
                }
            }
        }
        catch (ManagementException) { }
    }

    public void FillMissingCpuLoad(HardwareReport report)
    {
        var cpuTotalLoadExists = report.Cpus.FirstOrDefault()?
            .Sensors.Any(s => s.Type == SensorType.Load && s.Name.ToUpper().Contains("TOTAL")) ?? false;

        if (cpuTotalLoadExists)
            return;

        try
        {
            var query = "SELECT Name, PercentProcessorTime FROM Win32_PerfFormattedData_Counters_ProcessorInformation";
            using var searcher = new ManagementObjectSearcher("root\\CIMV2", query);

            foreach (var obj in searcher.Get())
            {
                string name = obj["Name"]?.ToString() ?? "N/A";
                float load = Convert.ToSingle(obj["PercentProcessorTime"]);

                if (name == "_Total")
                {
                    var sensor = new Sensor
                    {
                        Name = "CPU Total",
                        Value = load,
                        Type = SensorType.Load,
                        Unit = "%",
                        DataSource = "WMI_Fallback"
                    };

                    if (report.Cpus.Any())
                    {
                        report.Cpus.First().Sensors.Add(sensor);
                    }
                }
            }
        }
        catch (ManagementException) { }
    }
    private T GetPropertyValue<T>(ManagementBaseObject obj, string propertyName)
    {
        try { var value = obj[propertyName]; return value == null ? default(T)! : (T)Convert.ChangeType(value, typeof(T)); } catch { return default(T)!; }
    }
    private List<LogicalDiskInfo> GetLogicalDisksForDrive(string diskDrivePath)
    {
        var logicalDisks = new List<LogicalDiskInfo>();
         try {
            var partitionQuery = new RelatedObjectQuery($"associators of {{{diskDrivePath}}} where AssocClass = Win32_DiskDriveToDiskPartition");
            using var partitionSearcher = new ManagementObjectSearcher(partitionQuery);
            foreach (ManagementObject partition in partitionSearcher.Get())
            {
                var logicalDiskQuery = new RelatedObjectQuery($"associators of {{{partition.Path.Path}}} where AssocClass = Win32_LogicalDiskToPartition");
                using var logicalDiskSearcher = new ManagementObjectSearcher(logicalDiskQuery);
                foreach (ManagementObject logicalDisk in logicalDiskSearcher.Get())
                {
                    logicalDisks.Add(new LogicalDiskInfo { DeviceID = logicalDisk["DeviceID"]?.ToString() ?? "N/A", FileSystem = logicalDisk["FileSystem"]?.ToString() ?? "N/A", Size = GetPropertyValue<ulong>(logicalDisk, "Size"), FreeSpace = GetPropertyValue<ulong>(logicalDisk, "FreeSpace") });
                }
            }
        } catch (ManagementException) { }
        return logicalDisks;
    }
    private string ConvertCimMemoryType(object memoryType) => memoryType == null ? "Unknown" : Convert.ToUInt32(memoryType) switch { 0x0 => "Unknown", 0x14 => "DDR", 0x15 => "DDR2", 0x18 => "DDR3", 0x1A => "DDR4", _ => "Other" };
    private string ConvertCimMemoryFormFactor(object formFactor) => formFactor == null ? "Unknown" : Convert.ToUInt32(formFactor) switch { 8 => "DIMM", 9 => "SODIMM", _ => "Other" };
    private string ConvertBatteryChemistry(ushort chemistryCode) => chemistryCode switch { 1 => "Other", 2 => "Unknown", 3 => "Lead Acid", 4 => "Nickel Cadmium", 5 => "Nickel Metal Hydride", 6 => "Lithium-ion", 7 => "Zinc air", 8 => "Lithium Polymer", _ => "N/A" };
    private string ConvertBatteryStatus(ushort statusCode) => statusCode switch { 1 => "Discharging", 2 => "On AC", 3 => "Fully Charged", 4 => "Low", 5 => "Critical", 6 => "Charging", 7 => "Charging and High", 8 => "Charging and Low", 9 => "Charging and Critical", 10 => "Undefined", 11 => "Partially Charged", _ => "Unknown" };
}
