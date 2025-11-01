using SensorReader.Adapters;
using SensorReader.Models;
using SensorReader.Output;

namespace SensorReader.Services;

public class MonitoringService
{
    private readonly WmiAdapter _wmiAdapter;
    private readonly LibreHardwareAdapter _libreAdapter;
    private readonly IOutputFormatter _formatter;

    public MonitoringService(IOutputFormatter formatter)
    {
        _wmiAdapter = new WmiAdapter();
        _libreAdapter = new LibreHardwareAdapter();
        _formatter = formatter;
    }

    public void RunOnce()
    {
        HardwareReport report = _wmiAdapter.GetHardwareReport();
        _libreAdapter.GetHardwareReport(report);
        _wmiAdapter.FillMissingTemperatures(report);
        _wmiAdapter.FillMissingCpuLoad(report); // <-- SUGESTÃO
        report.Timestamp = DateTime.UtcNow.ToString("o");
        _formatter.Write(report);
    }

    public async Task RunContinuous(int intervalSeconds)
    {
        while (true)
        {
            RunOnce();
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
        }
    }

    // Limpeza dos adaptadores
    public void DisposeAdapters()
    {
        _libreAdapter.Dispose();
    }
}
