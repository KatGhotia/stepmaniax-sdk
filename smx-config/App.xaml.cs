using System;
using System.Windows;
using System.Runtime.InteropServices;
using System.Threading;
using System.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace smx_config
{
    public partial class App: Application
    {
        [DllImport("SMX.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SMX_Internal_OpenConsole();

        [DllImport("kernel32.dll")]
        public static extern bool SetStdHandle(int stdHandle, IntPtr handle);
        [DllImport("kernel32.dll")]
        public static extern bool AllocConsole();

        private readonly System.Windows.Forms.NotifyIcon trayIcon = new();
        private MainWindow? window;

        App()
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionEventHandler;
        }
        
        /*[STAThread]
        public static void Main1() {
            smx_config.App app = new();
            
            var services = new ServiceCollection();
            services.AddSingleton<ICurrentSMXDevice, CurrentSMXDevice>();
            
            app.Run();
        }*/

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // If an instance is already running, foreground it and exit.
            if (ForegroundExistingInstance())
            {
                Shutdown();
        return;
            }

            // Only create a console to show SMX.dll logs and other console logs if built for debug
#if DEBUG
            SetStdHandle(-10, IntPtr.Zero); // stdin
            SetStdHandle(-11, IntPtr.Zero); // stdout
            SetStdHandle(-12, IntPtr.Zero); // stderr
            AllocConsole();
#endif

            // This is used by the installer to close a running instance automatically when updating.
            ListenForShutdownRequest();

            // If we're being launched on startup, but the LaunchOnStartup setting is false,
            // then the user turned off auto-launching but we're still being launched for some
            // reason (eg. a renamed launch shortcut that we couldn't find to remove).  As
            // a safety so we don't launch when the user doesn't want us to, just exit in this
            // case.
            if (Helpers.LaunchedOnStartup() && !LaunchOnStartup.Enable)
            {
                Console.Error.WriteLine("LaunchOnStartup disabled in App config.");
                Shutdown();
        return;
            }

            LaunchOnStartup.Enable = true;
            if (!SMX.SMX.DLLExists())
            {
                MessageBox.Show($"SMXConfig startup error.\n\nSMX.dll couldn't be found:\n\n{Helpers.GetLastWin32ErrorString()}", "SMXConfig");
                Current.Shutdown();
        return;
            }

            if (!SMX.SMX.DLLAvailable())
            {
                MessageBox.Show($"SMXConfig initialization error.\n\nSMX.dll failed to load:\n\n{Helpers.GetLastWin32ErrorString()}", "SMXConfig");
                Current.Shutdown();
        return;
            }

            if (Helpers.GetDebug())
                SMX_Internal_OpenConsole();

            smxDevice = new CurrentSMXDevice();

            // Load animations.
            Helpers.LoadSavedPanelAnimations();

            //CreateTrayIcon();
            // Create a tray icon.  For some reason there's no WPF interface for this,
            // so we have to use Forms.
            var icon = new System.Drawing.Icon(GetResourceStream(
                    new Uri("pack://application:,,,/Resources/window%20icon%20grey.ico")
                ).Stream);

            trayIcon.Text = "StepManiaX";
            trayIcon.Visible = true;

            // Show or hide the application window on click.
            // pucgenie: On double-click only (but what about Touch input?)
            //trayIcon.Click += delegate (object sender, EventArgs e) { ToggleMainWindow();  };
            trayIcon.DoubleClick += delegate (object sender, EventArgs e) { ToggleMainWindow(); };

            smxDevice.ConfigurationChanged += RefreshTrayIcon;

            // Do the initial refresh.
            RefreshTrayIcon(smxDevice.GetState());

            // Create the main window.
            if (!Helpers.LaunchedOnStartup())
                ToggleMainWindow();
        }

        // Open or close the main window.
        //
        // We don't create our UI until the first time it's opened, so we use
        // less memory when we're launched on startup.  However, when we're minimized
        // back to the tray, we don't destroy the main window.  WPF is just too
        // leaky to recreate the main window each time it's called due to internal
        // circular references.  Instead, we just focus on minimizing CPU overhead.
        void ToggleMainWindow()
        {
            if (window == null)
            {
                window = new MainWindow(smxDevice!);
                window.Closed += MainWindowClosed;
                window.Show();
            }
            else if ((bool)IsMinimizedToTray()!)
            {
                window.Visibility = Visibility.Visible;
                window.Activate();
            }
            else
            {
                MinimizeToTray();
            }
        }

        public bool? IsMinimizedToTray()
        {
            return window == null ? null : (window.Visibility == Visibility.Collapsed);
        }

        public void MinimizeToTray()
        {
            if (window == null) {
                throw new Exception("window has to be initialized before");
            }
            // Just hide the window.  Don't actually set the window to minimized, since it
            // won't do anything and it causes problems when restoring the window.
            window.Visibility = Visibility.Collapsed;
        }

        public void BringToForeground()
        {
            // Restore or create the window.  Don't minimize if we're already restored.
            if (IsMinimizedToTray() ?? true)
                ToggleMainWindow();

            // Focus the window.
            window!.WindowState = WindowState.Normal;
            window.Activate();
        }

        private void MainWindowClosed(object sender, EventArgs e)
        {
            window = null;
        }

        private void UnhandledExceptionEventHandler(object sender, UnhandledExceptionEventArgs e)
        {
            string message = e.ExceptionObject.ToString();
            MessageBox.Show($"SMXConfig encountered an unexpected error:\n\n{message}", "SMXConfig");
            // TODO: pucgenie: save logs?
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            Console.Error.WriteLine("Application exiting");

            // Remove the tray icon. Always exists as soon as an object of this class is initialized.
            trayIcon.Visible = false;

            // Shut down cleanly, to make sure we don't run any threaded callbacks during shutdown.
            smxDevice!.Shutdown();
        }

        // If another instance other than this one is running, send it WM_USER to tell it to
        // foreground itself.  Return true if another instance was found.
        private bool ForegroundExistingInstance()
        {
            var createdNew = false;
            EventWaitHandle SMXConfigEvent = new(false, EventResetMode.AutoReset, "SMXConfigEvent", out createdNew);
            if (!createdNew)
            {
                // Signal the event to foreground the existing instance.
                SMXConfigEvent.Set();
        return true;
            }

            ThreadPool.RegisterWaitForSingleObject(SMXConfigEvent, ForegroundApplicationCallback, this, Timeout.Infinite, false);

            return false;
        }

        private static void ForegroundApplicationCallback(Object self, Boolean timedOut)
        {
            // This is called when another instance sends us a message over SMXConfigEvent.
            Application.Current.Dispatcher.Invoke(new Action(() => {
                App application = (App) Application.Current;
                application.BringToForeground();
            }));
        }

        /// <summary>
        /// TODO: pucgenie: Is this thread necessary anymore?
        /// </summary>
        private void ListenForShutdownRequest()
        {
            // We've already checked that we're the only instance when we get here, so this event shouldn't
            // exist.  If it already exists for some reason, we'll listen to it anyway.
            EventWaitHandle SMXConfigShutdown = new(false, EventResetMode.AutoReset, "SMXConfigShutdown");
            ThreadPool.RegisterWaitForSingleObject(SMXConfigShutdown, ShutdownApplicationCallback, this, Timeout.Infinite, false);
        }

        private static void ShutdownApplicationCallback(Object self, Boolean timedOut)
        {
            // This is called when another instance sends us a message over SMXConfigShutdown.
            Application.Current.Dispatcher.Invoke(new Action(() => {
                App application = (App) Application.Current;
                application.Shutdown();
            }));
        }

        // Refresh the tray icon when we're connected or disconnected.
        byte wasConnected = 0;
        private CurrentSMXDevice? smxDevice;

        private bool trayIconForceDone = false;
        void RefreshTrayIcon(LoadFromConfigDelegateArgs args)
        {
            var ConnectedCount = args.controller.Count(pad => pad.info.connected);
            // Skip the refresh if the connected state didn't change.
            if (wasConnected == ConnectedCount && trayIconForceDone)
        return;
            trayIconForceDone = true;
            wasConnected = (byte) ConnectedCount;

            trayIcon.Text = $"{ConnectedCount} SMX pads connected";

            // Set the tray icon.
            // TODO: pucgenie: Icons for every constellation (at least: 0, 1., 1.+2.)
            trayIcon.Icon = new System.Drawing.Icon(GetResourceStream(
                    new Uri($"pack://application:,,,/Resources/window%20icon{(ConnectedCount > 0 ? "%20grey" : "")}.ico")
                ).Stream);
        }
    }
}
