// READU.md - A lightweight Markdown reader
// Licensed under the MIT License.

using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using ReadU.Helpers;

namespace ReadU
{
    public partial class App : Application
    {
        private Window mainWindow;

        public static string AppFilename { get; set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            string[] cmdArgs = Environment.GetCommandLineArgs();
            string filePath = null;

            if (cmdArgs is { Length: >= 2 })
            {
                string potentialPath = cmdArgs[1];

                if (File.Exists(potentialPath))
                {
                    filePath = potentialPath;
                    AppFilename = filePath;
                }
                else
                {
                    Logger.LogWarning($"Invalid file path: {potentialPath}");
                }
            }

            mainWindow = new MainWindow(filePath);
            mainWindow.Activate();
        }
    }
}
