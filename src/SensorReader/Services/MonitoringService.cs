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
        // Etapa 1: Obter dados estáticos do WMI
        HardwareReport report = _wmiAdapter.GetHardwareReport();

        // Etapa 2: Enriquecer o relatório com sensores dinâmicos do LibreHardware
        _libreAdapter.GetHardwareReport(report);

        // Etapa 3: Registrar o timestamp final e formatar a saída
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
