//
//  App.xaml.cs
//  Hangly
//

using Microsoft.UI.Xaml;

namespace Hangly.App;

/// <summary>The application, which owns exactly one thing: the composition root.</summary>
/// <remarks>
/// There is no main window and no <c>Window</c> created here, which is why nothing
/// appears at launch. The overlay is built by <see cref="AppEnvironment"/> when the
/// settings say it is enabled, and the tray icon is the only thing that always exists.
/// </remarks>
public partial class App : Application
{
    private AppEnvironment? environment;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        environment = new AppEnvironment();
        environment.Bootstrap();
    }
}
