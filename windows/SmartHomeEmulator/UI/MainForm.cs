// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using SmartHomeEmulator.Base;
using SmartHomeEmulator.Network;
using SmartHomeEmulator.Protocol;
using SmartHomeEmulator.Session;

namespace SmartHomeEmulator.UI
{
    public partial class MainForm : Form
    {
        private readonly HomeNetwork _homeNetwork = new HomeNetwork();

        public MainForm()
        {
            InitializeComponent();
            this.Text = "Smart Home Emulator";
            this.MinimumSize = new Size(800, 600);

            RefreshPortList();
            InitBaudrateList();
            InitProtocolList();
            InitModeList();
            InitDefaultDevices();

            _homeNetwork.StateChanged += OnNetworkStateChanged;
            UpdateUIState(HomeNetwork.State.Stopped);
        }

        // ---- Port list ----

        private void RefreshPortList()
        {
            string selected = comboPort.SelectedItem?.ToString();
            comboPort.Items.Clear();
            foreach (string p in Session.ComNetworkSession.GetPortNames())
                comboPort.Items.Add(p);
            if (comboPort.Items.Count > 0)
            {
                int idx = comboPort.Items.IndexOf(selected);
                comboPort.SelectedIndex = idx >= 0 ? idx : 0;
            }
        }

        // ---- Baudrate list ----

        private static readonly int[] BaudrateValues =
        {
            1200, 2400, 4800, 9600, 14400, 19200, 38400, 57600, 115200
        };

        private void InitBaudrateList()
        {
            comboBaudrate.Items.Clear();
            foreach (int b in BaudrateValues)
                comboBaudrate.Items.Add(b.ToString());
            comboBaudrate.SelectedItem = "9600"; // default
        }

        // ---- Protocol list ----
        private void InitProtocolList()
        {
            comboProtocol.Items.Clear();
            comboProtocol.Items.Add("KS X 4506 / KD");
            comboProtocol.SelectedIndex = 0;
        }

        // ---- Mode list ----
        private void InitModeList()
        {
            comboMode.Items.Clear();
            comboMode.Items.Add("Slave (Device Emulator)");
            comboMode.Items.Add("Master (Controller)");
            comboMode.SelectedIndex = 0; // slave by default, same as Android app
        }

        // ---- Default devices ----

        // Mode selection (Slave = device emulator, Master = controller)
#pragma warning disable 414
        private bool _isSlave = true;
#pragma warning restore 414

        private void InitDefaultDevices()
        {
            // Add a default set of emulated devices (slave mode)
            // User can modify this list via the UI
            AddDefaultDevice("36.1F", HomeDevice.PROP_IS_SLAVE, true, "Thermostat Group");
            for (int i = 1; i <= 4; i++)
                AddDefaultDevice($"36.{i:X2}", HomeDevice.PROP_IS_SLAVE, true, $"Thermostat {i}");

            AddDefaultDevice("0E.1F", HomeDevice.PROP_IS_SLAVE, true, "Light Group");
            for (int i = 1; i <= 3; i++)
                AddDefaultDevice($"0E.{i:X2}", HomeDevice.PROP_IS_SLAVE, true, $"Light {i}");

            AddDefaultDevice("12.01", HomeDevice.PROP_IS_SLAVE, true, "Gas Valve");
            AddDefaultDevice("33.01", HomeDevice.PROP_IS_SLAVE, true, "Batch Switch");
            AddDefaultDevice("35.01", HomeDevice.PROP_IS_SLAVE, true, "Boiler");
            AddDefaultDevice("32.1F", HomeDevice.PROP_IS_SLAVE, true, "Ventilation Group");
        }

        private void AddDefaultDevice(string addr, string isSlaveKey, bool isSlave, string name)
        {
            var props = new Dictionary<string, object>
            {
                [HomeDevice.PROP_ADDR]     = addr,
                [HomeDevice.PROP_IS_SLAVE] = isSlave,
                [HomeDevice.PROP_NAME]     = name,
                [HomeDevice.PROP_AREA]     = HomeDevice.Area.UNKNOWN,
            };
            _homeNetwork.AddDevice(props);
        }

        // ---- Start / Stop ----

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (_homeNetwork.CurrentState == HomeNetwork.State.Running)
                return;

            if (comboPort.SelectedItem == null)
            {
                MessageBox.Show("Please select a COM port.", "No port selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string portName = comboPort.SelectedItem.ToString();
            int    baudRate = int.TryParse(comboBaudrate.SelectedItem?.ToString(), out int b) ? b : 9600;

            var session = new Session.ComNetworkSession(portName, baudRate);
            bool started = _homeNetwork.Start(session);
            if (!started)
            {
                MessageBox.Show($"Failed to open port {portName}.\nCheck that the device is connected and the port is not in use.",
                    "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _homeNetwork.Stop();
        }

        private void btnRefreshPorts_Click(object sender, EventArgs e)
        {
            RefreshPortList();
        }

        // ---- Network state callback ----

        private void OnNetworkStateChanged(HomeNetwork.State state)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => OnNetworkStateChanged(state))); return; }
            UpdateUIState(state);
        }

        private void UpdateUIState(HomeNetwork.State state)
        {
            bool running = state == HomeNetwork.State.Running;
            bool stopped = state == HomeNetwork.State.Stopped || state == HomeNetwork.State.Error;

            btnStart.Enabled  = stopped;
            btnStop.Enabled   = running;
            comboPort.Enabled    = stopped;
            comboBaudrate.Enabled= stopped;
            comboProtocol.Enabled= stopped;
            comboMode.Enabled    = stopped;
            btnRefreshPorts.Enabled = stopped;

            string stateText;
            Color  stateColor;
            switch (state)
            {
                case HomeNetwork.State.Running:
                    stateText  = "● RUNNING"; stateColor = Color.Green; break;
                case HomeNetwork.State.Starting:
                    stateText  = "◌ STARTING..."; stateColor = Color.Orange; break;
                case HomeNetwork.State.Error:
                    stateText  = "✖ ERROR"; stateColor = Color.Red; break;
                default:
                    stateText  = "○ STOPPED"; stateColor = Color.Gray; break;
            }
            lblStatus.Text      = stateText;
            lblStatus.ForeColor = stateColor;

            if (running) RefreshDeviceList();
        }

        // ---- Device list ----

        private void RefreshDeviceList()
        {
            listDevices.Items.Clear();
            foreach (var device in _homeNetwork.GetAllDevices())
            {
                string addr  = device.GetAddress();
                string name  = device.GetProperty<string>(HomeDevice.PROP_NAME) ?? addr;
                bool   on    = device.GetProperty<bool>(HomeDevice.PROP_ONOFF);
                bool   conn  = device.GetProperty<bool>(HomeDevice.PROP_CONNECTED);
                string state = on ? "ON" : "OFF";

                var item = new ListViewItem(new[] { name, addr, state, conn ? "Connected" : "-" });
                item.Tag = device;
                listDevices.Items.Add(item);
            }
        }

        private void listDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listDevices.SelectedItems.Count == 0)
            {
                lblDeviceDetail.Text = "";
                return;
            }
            var device = listDevices.SelectedItems[0].Tag as HomeDevice;
            if (device == null) return;
            ShowDeviceDetail(device);
        }

        private void ShowDeviceDetail(HomeDevice device)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Name:    {device.GetProperty<string>(HomeDevice.PROP_NAME) ?? device.GetAddress()}");
            sb.AppendLine($"Address: {device.GetAddress()}");
            sb.AppendLine($"On/Off:  {device.GetProperty<bool>(HomeDevice.PROP_ONOFF)}");

            // Show thermostat specific info if available
            var props = device.Dc.GetReadPropertyMap();
            if (props.ContainsKey(ThermostatProps.PROP_SETTING_TEMPERATURE))
            {
                sb.AppendLine($"Set Temp:     {props.Get<float>(ThermostatProps.PROP_SETTING_TEMPERATURE):F1}°C");
                sb.AppendLine($"Current Temp: {props.Get<float>(ThermostatProps.PROP_CURRENT_TEMPERATURE):F1}°C");
                long states = props.Get<long>(ThermostatProps.PROP_FUNCTION_STATES);
                sb.AppendLine($"Heating:      {(states & ThermostatProps.Function.HEATING) != 0}");
                sb.AppendLine($"Outing:       {(states & ThermostatProps.Function.OUTING_SETTING) != 0}");
            }
            if (props.ContainsKey(LightProps.PROP_CUR_DIM_LEVEL))
            {
                sb.AppendLine($"Dim Level: {props.Get<int>(LightProps.PROP_CUR_DIM_LEVEL)}");
            }
            if (props.ContainsKey(VentilationProps.PROP_FAN_LEVEL))
            {
                sb.AppendLine($"Fan Level: {props.Get<int>(VentilationProps.PROP_FAN_LEVEL)}");
            }
            if (props.ContainsKey(ThermostatProps.PROP_SETTING_TEMPERATURE))
            {
                // already handled above
            }

            lblDeviceDetail.Text = sb.ToString();
        }

        // ---- Log ----

        public void AppendLog(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => AppendLog(text))); return; }
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {text}{Environment.NewLine}");
            // Auto-scroll
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        private void btnClearLog_Click(object sender, EventArgs e) => txtLog.Clear();

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _homeNetwork.Stop();
        }
    }
}
