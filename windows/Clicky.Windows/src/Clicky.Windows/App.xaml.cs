using System.Windows;
using Clicky.Windows.Infrastructure;

namespace Clicky.Windows;

public partial class App : Application
{
    private ClickyApplicationCoordinator? clickyApplicationCoordinator;

    protected override void OnStartup(StartupEventArgs startupEventArguments)
    {
        base.OnStartup(startupEventArguments);

        DpiAwareness.EnablePerMonitorDpiAwareness();

        clickyApplicationCoordinator = ClickyApplicationCoordinator.CreateDefault();
        clickyApplicationCoordinator.Start();
    }

    protected override void OnExit(ExitEventArgs exitEventArguments)
    {
        clickyApplicationCoordinator?.Dispose();
        base.OnExit(exitEventArguments);
    }
}
