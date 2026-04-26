using Clicky.Windows.Infrastructure;

namespace Clicky.Windows;

public partial class App : System.Windows.Application
{
    private ClickyApplicationCoordinator? clickyApplicationCoordinator;

    protected override void OnStartup(System.Windows.StartupEventArgs startupEventArguments)
    {
        base.OnStartup(startupEventArguments);

        DpiAwareness.EnablePerMonitorDpiAwareness();

        clickyApplicationCoordinator = ClickyApplicationCoordinator.CreateDefault();
        clickyApplicationCoordinator.Start();
    }

    protected override void OnExit(System.Windows.ExitEventArgs exitEventArguments)
    {
        clickyApplicationCoordinator?.Dispose();
        base.OnExit(exitEventArguments);
    }
}
