// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SmartHomeEmulator.Streams
{
    /// <summary>Continuously reads bytes from InputStream and delivers them to the callback.</summary>
    public class StreamRxThread
    {
        private const string Tag = "StreamRxThread";
        private const int ChunkSize = 256;

        private readonly System.IO.Stream _inputStream;
        private readonly IStreamCallback  _callback;
        private Thread   _thread;
        private volatile bool _running;

        public StreamRxThread(System.IO.Stream inputStream, IStreamCallback callback)
        {
            _inputStream = inputStream;
            _callback    = callback;
        }

        public void Start()
        {
            _running = true;
            _thread  = new Thread(Run) { IsBackground = true, Name = "StreamRxThread" };
            _thread.Start();
        }

        public void RequestStop()
        {
            _running = false;
            // Interrupt the blocking read by closing or just letting timeout fire.
            try { _inputStream.Close(); } catch { }
            _thread?.Join(1000);
            _thread = null;
        }

        private void Run()
        {
            byte[] buf = new byte[ChunkSize];
            try
            {
                while (_running)
                {
                    int n = _inputStream.Read(buf, 0, ChunkSize);
                    if (n <= 0)
                    {
                        if (_running)
                        {
                            Debug.WriteLine($"[{Tag}] stream ended unexpectedly");
                            _callback.OnErrorOccurred();
                        }
                        break;
                    }

                    byte[] packet = new byte[n];
                    Array.Copy(buf, packet, n);
                    _callback.OnPacketReceived(packet, n);
                }
            }
            catch (Exception ex)
            {
                if (_running)
                {
                    Debug.WriteLine($"[{Tag}] error: {ex.Message}");
                    _callback.OnErrorOccurred();
                }
            }
            Debug.WriteLine($"[{Tag}] exited");
        }
    }
}
