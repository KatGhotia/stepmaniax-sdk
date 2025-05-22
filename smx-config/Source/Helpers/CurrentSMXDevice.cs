using System;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Forms.VisualStyles;

namespace smx_config
{
    // The state and configuration of a pad.
    public struct LoadFromConfigDelegateArgsPerController
    {
        public SMX.SMXInfo info;
        public SMX.SMXConfig config;
        public SMX.SMXSensorTestModeData test_data;

        // The panels that are activated.  Note that to receive notifications from OnConfigChange
        // when inputs change state, set RefreshOnInputChange to true.  Otherwise, this field will
        // be filled in but notifications won't be sent due to only inputs changing.
        public bool[] inputs;
    }

    public struct LoadFromConfigDelegateArgs
    {
        // This indicates which fields changed since the last call.
        public bool ConnectionsChanged, ConfigurationChanged, InputChanged, TestDataChanged;

        // Data for each of two controllers:
        public LoadFromConfigDelegateArgsPerController[] controller;

        /// <summary>
        /// If we have more than one connected controller, we don't fully expect them to be the same version anymore.
        /// 
        /// If minVersion > maxVersion, detection was skipped. TODO: remove code depending on old behaviour.
        /// </summary>
        /// <returns>Return the {oldest, newest} firmware version that's connected.</returns>
        public Tuple<short, short> firmwareVersion()
        {
            short minVersion = short.MaxValue, maxVersion = short.MinValue;
            foreach (var data in controller)
            {
                if (data.info.connected) {
                    if (data.info.m_iFirmwareVersion > maxVersion) {
                        maxVersion = data.info.m_iFirmwareVersion;
                    }
                    if (data.info.m_iFirmwareVersion < minVersion) {
                        minVersion = data.info.m_iFirmwareVersion;
                    }
                }
            }
            Console.Error.WriteLine($"minVersion: {minVersion}, maxVersion: {maxVersion}");
            return new Tuple<short, short>(minVersion, maxVersion);
        }

        // The control that changed the configuration (passed to FireConfigurationChanged).
        public object source;
    };

    // This class tracks the device we're currently configuring, and runs a callback when
    // it changes.
    class CurrentSMXDevice
    {
        public static CurrentSMXDevice singleton;

        // This is fired when FireConfigurationChanged is called, and when the current device
        // changes.
        public delegate void ConfigurationChangedDelegate(LoadFromConfigDelegateArgs args);
        public event ConfigurationChangedDelegate ConfigurationChanged;

        private readonly bool[] WasConnected = new bool[2] { false, false };
        private readonly bool[][] LastInputs = new bool[2][];
        private SMX.SMXSensorTestModeData[] LastTestData = new SMX.SMXSensorTestModeData[2];
        private readonly Dispatcher MainDispatcher;

        public CurrentSMXDevice()
        {
            // Grab the main thread's dispatcher, so we can invoke into it.
            MainDispatcher = Dispatcher.CurrentDispatcher;

            // Set our update callback.  This will be called when something happens: connection or disconnection,
            // inputs changed, configuration updated, test data updated, etc.  It doesn't specify what's changed,
            // we simply check the whole state.
            SMX.SMX.Start(delegate(int PadNumber, SMX.SMX.SMXUpdateCallbackReason reason) {
                // Console.WriteLine("... " + reason);
                // This is called from a thread, with SMX's internal mutex locked.  We must not call into SMX
                // or do anything with the UI from here.  Just queue an update back into the UI thread.
                MainDispatcher.InvokeAsync(delegate() {
                    switch(reason)
                    {
                    case SMX.SMX.SMXUpdateCallbackReason.Updated:
                        CheckForChanges();
                        break;
                    case SMX.SMX.SMXUpdateCallbackReason.FactoryResetCommandComplete:
                        Console.WriteLine("SMX_FactoryResetCommandComplete");
                        FireConfigurationChanged(null);
                        break;
                    }
                });
            });
        }

        public void Shutdown()
        {
            SMX.SMX.Stop();
        }

        private void CheckForChanges()
        {
            LoadFromConfigDelegateArgs args = GetState();

            // Mark which parts have changed.
            //
            // For configuration, we only check for connection state changes.  Actual configuration
            // changes are fired by controls via FireConfigurationChanged.
            for (int pad = 0; pad < 2; ++pad)
            {
                LoadFromConfigDelegateArgsPerController controller = args.controller[pad];
                if (WasConnected[pad] != controller.info.connected)
                {
                    args.ConfigurationChanged = true;
                    args.ConnectionsChanged = true;
                    WasConnected[pad] = controller.info.connected;
                }

                if (LastInputs[pad] == null || !Enumerable.SequenceEqual(controller.inputs, LastInputs[pad]))
                {
                    args.InputChanged = true;
                    LastInputs[pad] = controller.inputs;
                }

                if (!controller.test_data.Equals(LastTestData[pad]))
                {
                    args.TestDataChanged = true;
                    LastTestData[pad] = controller.test_data;
                    // TODO: pucgenie: collect data for pressure statistics here?
                    Console.Out.WriteLine($"{pad} {controller.test_data.sensorLevel.Max()}");
                }
            }

            // Only fire the delegate if something has actually changed.
            if (args.ConfigurationChanged || args.InputChanged || args.TestDataChanged) {
                // TODO: pucgenie: Split it up into 3 distinct events?
                ConfigurationChanged?.Invoke(args);
            }
        }

        public void FireConfigurationChanged(object source)
        {
            LoadFromConfigDelegateArgs args = GetState();
            args.ConfigurationChanged = true;
            args.source = source;
            ConfigurationChanged?.Invoke(args);
        }

        public LoadFromConfigDelegateArgs GetState()
        {
            LoadFromConfigDelegateArgs args = new LoadFromConfigDelegateArgs
            {
                controller = new LoadFromConfigDelegateArgsPerController[2]
            };

            for (int pad = 0; pad < 2; ++pad)
            {
                LoadFromConfigDelegateArgsPerController controller;
                controller.test_data = new SMX.SMXSensorTestModeData();

                // Expand the inputs mask to an array.
                UInt16 Inputs = SMX.SMX.GetInputState(pad);
                controller.inputs = new bool[9];
                for (int i = 0; i < 9; ++i)
                    controller.inputs[i] = (Inputs & (1 << i)) != 0;
                SMX.SMX.GetInfo(pad, out controller.info);
                SMX.SMX.GetConfig(pad, out controller.config);
                SMX.SMX.GetTestData(pad, out controller.test_data);
                args.controller[pad] = controller;
            }

            return args;
        }

    }

    // Call a delegate on configuration change.  Configuration changes are notified by calling
    // FireConfigurationChanged.
    public class OnConfigChange
    {
        public delegate void LoadFromConfigDelegate(LoadFromConfigDelegateArgs args);
        private readonly Control Owner;
        private readonly LoadFromConfigDelegate Callback;
        private bool _RefreshOnInputChange = false;

        // If set to true, the callback will be invoked on input changes in addition to configuration
        // changes.  This can cause the callback to be run at any time, such as while the user is
        // interacting with the control.
        public bool RefreshOnInputChange {
            get { return _RefreshOnInputChange; }
            set {_RefreshOnInputChange = value; }
        }

        private bool _RefreshOnTestDataChange = false;

        // Like RefreshOnInputChange, but enables callbacks when test data changes.
        public bool RefreshOnTestDataChange {
            get { return _RefreshOnTestDataChange; }
            set { _RefreshOnTestDataChange = value; }
        }

        // Owner is the Control that we're calling.  This callback will be disabled when the
        // control is unloaded, and we won't call it if it's the same control that fired
        // the change via FireConfigurationChanged.
        //
        // In addition, the callback is called when the control is Loaded, to load the initial
        // state.
        public OnConfigChange(Control owner, LoadFromConfigDelegate callback)
        {
            Owner = owner;
            Callback = callback;

            Owner.Loaded += delegate(object sender, RoutedEventArgs e)
            {
                if(CurrentSMXDevice.singleton == null)
                    return;
                CurrentSMXDevice.singleton.ConfigurationChanged += ConfigurationChanged;

                // When a control is loaded, run the callback with the current state
                // as though a device was changed, so we'll refresh things like the
                // sensitivity sliders.
                LoadFromConfigDelegateArgs args = CurrentSMXDevice.singleton.GetState();
                args.ConnectionsChanged = true;
                Callback(args);
            };

            Owner.Unloaded += delegate(object sender, RoutedEventArgs e)
            {
                if(CurrentSMXDevice.singleton == null)
                    return;
                CurrentSMXDevice.singleton.ConfigurationChanged -= ConfigurationChanged;
            };
        }

        private void ConfigurationChanged(LoadFromConfigDelegateArgs args)
        {
            if (args.ConfigurationChanged ||
                (RefreshOnInputChange && args.InputChanged) ||
                (RefreshOnTestDataChange && args.TestDataChanged))
            {
                Callback(args);
            }
        }
    };

    public class OnInputChange
    {
        public delegate void LoadFromConfigDelegate(LoadFromConfigDelegateArgs args);
        private readonly Control Owner;
        private readonly LoadFromConfigDelegate Callback;

        // Owner is the Control that we're calling.  This callback will be disable when the
        // control is unloaded, and we won't call it if it's the same control that fired
        // the change via FireConfigurationChanged.
        //
        // In addition, the callback is called when the control is Loaded, to load the initial
        // state.
        public OnInputChange(Control owner, LoadFromConfigDelegate callback)
        {
            Owner = owner;
            Callback = callback;

            // This is available when the application is running, but will be null in the XAML designer.
            if (CurrentSMXDevice.singleton == null)
        return;

            Owner.Loaded += delegate(object sender, RoutedEventArgs e)
            {
                CurrentSMXDevice.singleton.ConfigurationChanged += ConfigurationChanged;
                if (CurrentSMXDevice.singleton != null)
                    Callback(CurrentSMXDevice.singleton.GetState());
            };

            Owner.Unloaded += delegate(object sender, RoutedEventArgs e)
            {
                CurrentSMXDevice.singleton.ConfigurationChanged -= ConfigurationChanged;
            };
        }

        private void ConfigurationChanged(LoadFromConfigDelegateArgs args)
        {
            Callback(args);
        }
    };
}
