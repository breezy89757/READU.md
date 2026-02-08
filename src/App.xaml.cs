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
            // Get command line arguments
            string[] cmdArgs = Environment.GetCommandLineArgs();
            string filePath = null;

            // Check if a file path was passed as an argument
            // cmdArgs[0] is the executable path, cmdArgs[1] is the first argument
            if (cmdArgs != null && cmdArgs.Length >= 2)
            {
                string potentialPath = cmdArgs[1];

                // Validate that the path exists and is a file
                if (File.Exists(potentialPath))
                {
                    filePath = potentialPath;
                    AppFilename = filePath;
                }
                else
                {
                    // Log or handle invalid path
                    Logger.LogWarning($"Invalid file path provided: {potentialPath}");
                }
            }

            // Create main window with optional file path
            mainWindow = new MainWindow(filePath);
            mainWindow.Activate();
        }
    }
}
