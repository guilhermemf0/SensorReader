using SensorReader.Adapters;
using SensorReader.Models;
using SensorReader.Output;

namespace SensorReader.Services;

public class MonitoringService
{
    private readonly IHardwareAdapter _adapter;
    private readonly IOutputFormatter _formatter;

    public MonitoringService(IHardwareAdapter adapter, IOutputFormatter formatter)
    {
        _adapter = adapter;
        _formatter = formatter;
    }

    public void RunOnce()
    {
        HardwareReport? report = _adapter.GetHardwareReport();
        if (report != null)
        {
            report.AdapterUsed = _adapter.GetType().Name;
            _formatter.Write(report);
        }
        else
        {
            Console.Error.WriteLine("Erro: Não foi possível obter os dados do hardware.");
        }
    }

    public async Task RunContinuous(int intervalSeconds)
    {
        while (true)
        {
            RunOnce();
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
        }
    }
}
