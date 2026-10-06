using SeewoMCUController.Cli;

namespace SeewoMCUController;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            return await CliRunner.RunAsync(args, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"发生错误: {ex.Message}");
            if (Environment.GetEnvironmentVariable("MCU_TRACE") == "1") Console.Error.WriteLine(ex);
            Console.ResetColor();
            return 1;
        }
    }
}
