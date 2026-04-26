using Clicky.Windows.Infrastructure;
using Clicky.Windows.Logging;

namespace Clicky.Windows;

public partial class App : System.Windows.Application
{
    private ClickyApplicationCoordinator? clickyApplicationCoordinator;

    protected override void OnStartup(System.Windows.StartupEventArgs startupEventArguments)
    {
        base.OnStartup(startupEventArguments);

        DispatcherUnhandledException += (_, dispatcherUnhandledExceptionEventArgs) =>
        {
            ClickyLogger.Error("Unhandled dispatcher exception.", dispatcherUnhandledExceptionEventArgs.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, unhandledExceptionEventArgs) =>
        {
            if (unhandledExceptionEventArgs.ExceptionObject is Exception exception)
            {
                ClickyLogger.Error("Unhandled app-domain exception.", exception);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, unobservedTaskExceptionEventArgs) =>
        {
            ClickyLogger.Error("Unobserved task exception.", unobservedTaskExceptionEventArgs.Exception);
        };
        ClickyLogger.Info("Clicky Windows starting.");

        DpiAwareness.EnablePerMonitorDpiAwareness();

        clickyApplicationCoordinator = ClickyApplicationCoordinator.CreateDefault();
        clickyApplicationCoordinator.Start();
    }

    protected override void OnExit(System.Windows.ExitEventArgs exitEventArguments)
    {
        clickyApplicationCoordinator?.Dispose();
        ClickyLogger.Info("Clicky Windows exiting.");
        base.OnExit(exitEventArguments);
    }
}
