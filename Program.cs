using Avalonia;
using System;
using System.Threading.Tasks;

namespace GravadorMulti;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Console.Error.WriteLine("=== UNHANDLED EXCEPTION ===");
            Console.Error.WriteLine(e.ExceptionObject);
            Console.Error.Flush();
        };
        
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Console.Error.WriteLine("=== UNOBSERVED TASK EXCEPTION ===");
            Console.Error.WriteLine(e.Exception);
            Console.Error.Flush();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("=== FATAL EXCEPTION ===");
            Console.Error.WriteLine(ex);
            Console.Error.Flush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
