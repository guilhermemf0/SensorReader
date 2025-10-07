using System.CommandLine;
using SensorReader.Adapters;
using SensorReader.Services;
using SensorReader.Output;
using System.Security.Principal; // Adicionado para verificar privilégios

// Classe principal que configura e executa o programa
public class Program
{
    // Ponto de entrada assíncrono para lidar com operações como --interval
    public static async Task<int> Main(string[] args)
    {
        // --- VERIFICAÇÃO DE PRIVILÉGIOS DE ADMINISTRADOR ---
        bool isAdmin = false;
        using (var identity = WindowsIdentity.GetCurrent())
        {
            var principal = new WindowsPrincipal(identity);
            isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        if (!isAdmin)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[AVISO] O programa não está sendo executado como Administrador.");
            Console.WriteLine("Algumas informações detalhadas (como tipo de disco SSD/HDD) podem não estar disponíveis.");
            Console.WriteLine("Para um relatório completo, execute a partir de um terminal com privilégios de administrador.");
            Console.ResetColor();
            Console.WriteLine();
        }

        // --- Configuração dos Argumentos da Linha de Comando (CLI) ---
        var rootCommand = new RootCommand("SensorReader: Uma ferramenta robusta para monitoramento de hardware.");

        var onceOption = new Option<bool>("--once", "Executa a leitura uma única vez e encerra.");
        var intervalOption = new Option<int?>("--interval", "Executa a leitura continuamente no intervalo especificado (em segundos).");
        var formatOption = new Option<OutputFormat>("--format", () => OutputFormat.Json, "Formato da saída: PlainText ou Json.");

        rootCommand.AddOption(onceOption);
        rootCommand.AddOption(intervalOption);
        rootCommand.AddOption(formatOption);

        rootCommand.SetHandler(async (once, interval, format) =>
        {
            if (!once && interval == null)
            {
                await RunMonitoring(null, format);
                return;
            }
            if (once && interval != null)
            {
                Console.Error.WriteLine("Erro: --once e --interval são mutuamente exclusivos.");
                return;
            }
            await RunMonitoring(interval, format);
        }, onceOption, intervalOption, formatOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunMonitoring(int? interval, OutputFormat format)
    {
        IOutputFormatter formatter = format == OutputFormat.Json ? new JsonOutputFormatter() : new PlainTextOutputFormatter();
        var monitoringService = new MonitoringService(formatter);

        try
        {
            if (interval.HasValue)
            {
                await monitoringService.RunContinuous(interval.Value);
            }
            else
            {
                monitoringService.RunOnce();
            }
        }
        finally
        {
            monitoringService.DisposeAdapters();
        }
    }
}

public enum OutputFormat
{
    PlainText,
    Json
}
