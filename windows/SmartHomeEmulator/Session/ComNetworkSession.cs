// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace SmartHomeEmulator.Session
{
    /// <summary>
    /// Windows COM port implementation of NetworkSessionAdapter.
    /// Equivalent to Android's UsbNetworkSession but uses System.IO.Ports.SerialPort.
    /// </summary>
    public class ComNetworkSession : NetworkSessionAdapter
    {
        private const string Tag = "ComNetworkSession";
        private const int WriteDelayMs = 10; // small delay before each write (matches Android HACK)

        private readonly string _portName;
        private readonly int    _baudRate;
        private SerialPort      _serialPort;
        private Thread          _readThread;
        private volatile bool   _running;

        public ComNetworkSession(string portName, int baudRate)
        {
            _portName = portName;
            _baudRate = baudRate;
        }

        protected override bool OnOpen()
        {
            try
            {
                _serialPort = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout  = 500,
                    WriteTimeout = 200,
                    ReadBufferSize  = 4096,
                    WriteBufferSize = 4096,
                };
                _serialPort.Open();

                _running = true;
                _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "ComRxThread" };
                _readThread.Start();

                Debug.WriteLine($"[{Tag}] Opened port {_portName} at {_baudRate} bps");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{Tag}] Open failed: {ex.Message}");
                _serialPort?.Dispose();
                _serialPort = null;
                return false;
            }
        }

        protected override void OnClose()
        {
            _running = false;

            try { _readThread?.Join(1000); } catch { }
            _readThread = null;

            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                    _serialPort.Close();
                _serialPort?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{Tag}] Close error: {ex.Message}");
            }
            _serialPort = null;

            Debug.WriteLine($"[{Tag}] Closed port {_portName}");
        }

        protected internal override void OnWrite(byte[] data)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;
                Thread.Sleep(WriteDelayMs); // small delay to avoid split packets (same as Android)
                _serialPort.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{Tag}] Write error: {ex.Message}");
            }
        }

        private void ReadLoop()
        {
            const int BufSize = 256;
            byte[] buf = new byte[BufSize];

            while (_running)
            {
                try
                {
                    if (_serialPort == null || !_serialPort.IsOpen) break;

                    int bytesRead = _serialPort.Read(buf, 0, BufSize);
                    if (bytesRead > 0)
                    {
                        byte[] data = new byte[bytesRead];
                        Array.Copy(buf, data, bytesRead);
                        PutData(data);
                    }
                }
                catch (TimeoutException)
                {
                    // No data available – normal, keep looping
                }
                catch (Exception ex)
                {
                    if (_running)
                        Debug.WriteLine($"[{Tag}] Read error: {ex.Message}");
                    break;
                }
            }

            Debug.WriteLine($"[{Tag}] Read loop exited");
        }

        /// <summary>Returns available COM port names on the system.</summary>
        public static string[] GetPortNames()
        {
            return SerialPort.GetPortNames();
        }
    }
}
