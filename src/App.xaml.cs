// READU.md — Licensed under the MIT License.

using System.IO;
using Microsoft.UI.Xaml;
using ReadU.Helpers;

namespace ReadU;

public partial class App : Application
{
    private Window _mainWindow;

    public static string AppFilename { get; set; }

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string filePath = null;
        var cmdArgs = System.Environment.GetCommandLineArgs();

        if (cmdArgs is { Length: >= 2 } && File.Exists(cmdArgs[1]))
        {
            filePath = cmdArgs[1];
            AppFilename = filePath;
        }

        _mainWindow = new MainWindow(filePath);
        _mainWindow.Activate();
    }
}
