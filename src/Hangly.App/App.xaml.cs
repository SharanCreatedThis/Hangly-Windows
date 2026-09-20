//
//  App.xaml.cs
//  Hangly
//

using Hangly.App.Services;
using Microsoft.UI.Xaml;

namespace Hangly.App;

/// <summary>The application, which owns exactly one thing: the composition root.</summary>
/// <remarks>
/// There is no main window and no <c>Window</c> created here, which is why nothing appears
/// at launch. The overlay is built by <see cref="AppEnvironment"/> when the settings say
/// it is enabled, and the tray icon is the only thing that always exists.
///
/// <para>That also means a failure here is invisible — no window closes, because none was
/// ever opened. <see cref="Diagnostics"/> is installed before anything else runs, so the
/// app can say what happened even when there is nothing on screen to say it with.</para>
/// </remarks>
public partial class App : Application
{
    private AppEnvironment? environment;

    public App()
    {
        Diagnostics.Install();
        Diagnostics.Log("App constructed");

        // XAML swallows exceptions raised inside its own dispatch and shuts the process
        // down. This is the only place they can be seen.
        UnhandledException += (_, args) =>
        {
            Diagnostics.Failure("xaml", args.Exception);
            args.Handled = true;
        };

        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Diagnostics.Log("OnLaunched");

        try
        {
            environment = new AppEnvironment();
            environment.Bootstrap();
            Diagnostics.Log("Bootstrap returned");
            environment.ShowWelcomeIfNeeded();
        }
        catch (Exception exception)
        {
            Diagnostics.Failure("bootstrap", exception);
            throw;
        }
    }
}
