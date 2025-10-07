using SensorReader.Models;

namespace SensorReader.Adapters;

public interface IHardwareAdapter
{
    HardwareReport? GetHardwareReport();
}
