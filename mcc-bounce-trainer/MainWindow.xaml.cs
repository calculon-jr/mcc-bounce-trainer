using Memory;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace MccBounceTrainer {
    public partial class MainWindow : Window {
        private const string tickrate_adr = "halo2.dll+19A2F40,0x02";
        private const string wireframe_adr = "halo2.dll+12B2574";

        private readonly byte[] tick30 = new byte[] { 0x1E, 0x00, 0x89, 0x88, 0x08, 0x3D };
        private readonly byte[] tick60 = new byte[] { 0x3C, 0x00, 0x89, 0x88, 0x88, 0x3C };

        private Mem mem;

        private GamepadKeyCode[] TickrateHotkeys = new GamepadKeyCode[2];
        private GamepadKeyCode[] WireframeHotkeys = new GamepadKeyCode[2];

        public MainWindow() {
            InitializeComponent();

            foreach (GamepadKeyCode button in Enum.GetValues(typeof(GamepadKeyCode))) {
                TickrateHotkey1ComboBox.Items.Add(button);
                TickrateHotkey2ComboBox.Items.Add(button);
                WireframeHotkey1ComboBox.Items.Add(button);
                WireframeHotkey2ComboBox.Items.Add(button);
            }

            TickrateHotkey1ComboBox.SelectedIndex = 0;
            TickrateHotkey2ComboBox.SelectedIndex = 0;
            WireframeHotkey1ComboBox.SelectedIndex = 0;
            WireframeHotkey2ComboBox.SelectedIndex = 0;

            TickrateHotkey1ComboBox.SelectionChanged += SetHotkeys;
            TickrateHotkey2ComboBox.SelectionChanged += SetHotkeys;
            WireframeHotkey1ComboBox.SelectionChanged += SetHotkeys;
            WireframeHotkey2ComboBox.SelectionChanged += SetHotkeys;

            WireframeEnabledCheckBox.Checked += WireframeEnabledCheckBox_Checked;
            WireframeEnabledCheckBox.Unchecked += WireframeEnabledCheckBox_Unchecked;
            TickrateEnabledCheckBox.Checked += TickrateEnabledCheckBox_Checked;
            TickrateEnabledCheckBox.Unchecked += TickrateEnabledCheckBox_Unchecked;

            var worker = new BackgroundWorker();
            worker.DoWork += Worker_DoWork;
            Loaded += (o, e) => worker.RunWorkerAsync();
        }

        private byte[] GetTickrate() {
            return mem.ReadBytes(tickrate_adr, 6);
        }

        private int GetWireframe() {
            return mem.ReadInt(wireframe_adr);
        }

        private void TickrateEnabledCheckBox_Checked(object sender, RoutedEventArgs e) {
            SetTickrate(true);
        }

        private void TickrateEnabledCheckBox_Unchecked(object sender, RoutedEventArgs e) {
            SetTickrate(false);
        }

        private void WireframeEnabledCheckBox_Checked(object sender, RoutedEventArgs e) {
            SetWireframe(true);
        }

        private void WireframeEnabledCheckBox_Unchecked(object sender, RoutedEventArgs e) {
            SetWireframe(false);
        }

        private void SetHotkeys(object sender, SelectionChangedEventArgs e) {
            TickrateHotkeys[0] = (GamepadKeyCode)TickrateHotkey1ComboBox.SelectedItem;
            TickrateHotkeys[1] = (GamepadKeyCode)TickrateHotkey2ComboBox.SelectedItem;

            WireframeHotkeys[0] = (GamepadKeyCode)WireframeHotkey1ComboBox.SelectedItem;
            WireframeHotkeys[1] = (GamepadKeyCode)WireframeHotkey2ComboBox.SelectedItem;
        }

        private void SetTickrate(bool enable) {
            if (enable) {
                mem.WriteBytes(tickrate_adr, tick30);
            }
            else {
                mem.WriteBytes(tickrate_adr, tick60);
            }
        }

        private void ToggleTickrate() {
            this.Dispatcher.Invoke(() => {
                var tickrate = GetTickrate();
                var tickrateIsOn = Enumerable.SequenceEqual(tickrate, tick30);

                if (tickrateIsOn) {
                    if (TickrateEnabledCheckBox.IsChecked == true) {
                        TickrateEnabledCheckBox.IsChecked = false;
                    }
                    else {
                        // fix desync from restarting game/trainer
                        SetTickrate(false);
                    }
                }
                else {
                    if (TickrateEnabledCheckBox.IsChecked == true) {
                        TickrateEnabledCheckBox.IsChecked = false;
                    }
                    else {
                        TickrateEnabledCheckBox.IsChecked = true;
                    }
                }
            });
        }

        private void SetWireframe(bool enable) {
            if (enable) {
                mem.WriteMemory(wireframe_adr, "int", "1");
            }
            else {
                mem.WriteMemory(wireframe_adr, "int", "0");
            }
        }

        private void ToggleWireframe() {
            this.Dispatcher.Invoke(() => {
                var wireframe = GetWireframe();
                var wireframeIsOn = wireframe == 1;

                if (wireframeIsOn) {
                    if (WireframeEnabledCheckBox.IsChecked == true) {
                        WireframeEnabledCheckBox.IsChecked = false;
                    }
                    else {
                        // fix desync from restarting game/trainer
                        SetWireframe(false);
                    }
                }
                else {
                    if (WireframeEnabledCheckBox.IsChecked == true) {
                        WireframeEnabledCheckBox.IsChecked = false;
                    }
                    else {
                        WireframeEnabledCheckBox.IsChecked = true;
                    }
                }
            });
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e) {
            mem = new Mem();

            if (!mem.OpenProcess("MCC-Win64-Shipping")) {
                throw new Exception("Could not open Halo MCC process");
            }

            var controller = new Controller(UserIndex.One);

            if (!controller.IsConnected) {
                throw new Exception("No gamepad detected");
            }

            var keysHeld = new List<GamepadKeyCode>();

            while (true) {
                controller.GetKeystroke(DeviceQueryType.Gamepad, out Keystroke input);

                if (input.Flags == KeyStrokeFlags.KeyDown) {
                    keysHeld.Add(input.VirtualKey);

                    bool firedHotkey = false;

                    // if at least one tickrate hotkey is set
                    if (!(TickrateHotkeys[0] == GamepadKeyCode.None && TickrateHotkeys[1] == GamepadKeyCode.None)) {
                        bool hotkey0 = TickrateHotkeys[0] == GamepadKeyCode.None ? true : keysHeld.Contains(TickrateHotkeys[0]);
                        bool hotkey1 = TickrateHotkeys[1] == GamepadKeyCode.None ? true : keysHeld.Contains(TickrateHotkeys[1]);

                        if (hotkey0 && hotkey1) {
                            ToggleTickrate();
                            firedHotkey = true;
                        }
                    }

                    // if at least one wireframe hotkey is set
                    if (!(WireframeHotkeys[0] == GamepadKeyCode.None && WireframeHotkeys[1] == GamepadKeyCode.None)) {
                        bool hotkey0 = WireframeHotkeys[0] == GamepadKeyCode.None ? true : keysHeld.Contains(WireframeHotkeys[0]);
                        bool hotkey1 = WireframeHotkeys[1] == GamepadKeyCode.None ? true : keysHeld.Contains(WireframeHotkeys[1]);

                        if (hotkey0 && hotkey1) {
                            ToggleWireframe();
                            firedHotkey = true;
                        }
                    }

                    if (firedHotkey) {
                        keysHeld.Remove(input.VirtualKey);
                    }
                }

                else if (input.Flags == KeyStrokeFlags.KeyUp) {
                    keysHeld.Remove(input.VirtualKey);
                }

                Thread.Sleep(1);
            }
        }
    }
}
