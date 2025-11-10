using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using MT32EmuAvalonia;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        System.Console.WriteLine("[Program] Browser application starting");
        System.Console.WriteLine($"[Program] Args count: {args?.Length ?? 0}");
        var task = BuildAvaloniaApp()
            .WithInterFont()
            .LogToTrace()
            .StartBrowserAppAsync("out");
        System.Console.WriteLine("[Program] StartBrowserAppAsync called");
        return task;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        System.Console.WriteLine("[Program] BuildAvaloniaApp called");
        return AppBuilder.Configure<App>();
    }
}