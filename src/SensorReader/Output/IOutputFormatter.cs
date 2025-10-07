using SensorReader.Models;

namespace SensorReader.Output;

public interface IOutputFormatter
{
    void Write(HardwareReport report);
}
