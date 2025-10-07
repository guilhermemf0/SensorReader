using System.CommandLine;
using SensorReader.Adapters;
using SensorReader.Services;
using SensorReader.Output;

// Classe principal que configura e executa o programa
public class Program
{
    // Ponto de entrada assíncrono para lidar com operações como --interval
    public static async Task<int> Main(string[] args)
    {
        // --- Configuração dos Argumentos da Linha de Comando (CLI) ---
        var rootCommand = new RootCommand("SensorReader: Uma ferramenta robusta para monitoramento de hardware.");

        var onceOption = new Option<bool>("--once", "Executa a leitura uma única vez e encerra.");
        var intervalOption = new Option<int?>("--interval", "Executa a leitura continuamente no intervalo especificado (em segundos).");
        var formatOption = new Option<OutputFormat>("--format", () => OutputFormat.PlainText, "Formato da saída: PlainText ou Json.");
        // Mais opções podem ser adicionadas aqui (verbose, adapter, etc.)

        rootCommand.AddOption(onceOption);
        rootCommand.AddOption(intervalOption);
        rootCommand.AddOption(formatOption);

        // Define o que acontece quando o comando é executado
        rootCommand.SetHandler(async (once, interval, format) =>
        {
            // Validação dos argumentos
            if (!once && interval == null)
            {
                // Se nenhum modo for escolhido, executa --once por padrão.
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

    // Método principal que orquestra o monitoramento
    private static async Task RunMonitoring(int? interval, OutputFormat format)
    {
        // A lógica de seleção de adapter viria aqui. Por enquanto, usamos o LibreHardwareAdapter.
        IHardwareAdapter adapter = new LibreHardwareAdapter();
        IOutputFormatter formatter = format == OutputFormat.Json ? new JsonOutputFormatter() : new PlainTextOutputFormatter();

        var monitoringService = new MonitoringService(adapter, formatter);

        if (interval.HasValue)
        {
            // Modo de intervalo: executa continuamente
            await monitoringService.RunContinuous(interval.Value);
        }
        else
        {
            // Modo "once": executa uma vez
            monitoringService.RunOnce();
        }
    }
}

// Enum para os formatos de saída
public enum OutputFormat
{
    PlainText,
    Json
}
