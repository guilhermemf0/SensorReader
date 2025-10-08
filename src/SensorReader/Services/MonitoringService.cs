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
        // 1. Coleta dados estáticos e de WMI.
        HardwareReport report = _wmiAdapter.GetHardwareReport();

        // 2. Tenta obter sensores da fonte primária (LibreHardware).
        _libreAdapter.GetHardwareReport(report);

        // 3. (PLANO B) Ativa o fallback para preencher temperaturas em falta.
        _wmiAdapter.FillMissingTemperatures(report);

        // 4. Finaliza e mostra o relatório mais completo possível.
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
