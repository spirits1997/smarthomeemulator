// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SmartHomeEmulator.Streams
{
    /// <summary>
    /// Queues outgoing HomePacket byte arrays and writes them to the OutputStream.
    /// </summary>
    public class StreamTxThread
    {
        private const string Tag = "StreamTxThread";

        private readonly System.IO.Stream _outputStream;
        private readonly IStreamCallback  _callback;
        private readonly BlockingCollection<byte[]> _queue
            = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>(), 256);
        private Thread _thread;
        private volatile bool _running;

        public StreamTxThread(System.IO.Stream outputStream, IStreamCallback callback)
        {
            _outputStream = outputStream;
            _callback     = callback;
        }

        public void Start()
        {
            _running = true;
            _thread  = new Thread(Run) { IsBackground = true, Name = "StreamTxThread" };
            _thread.Start();
        }

        public void RequestStop()
        {
            _running = false;
            _queue.CompleteAdding();
            _thread?.Join(1000);
            _thread = null;
        }

        public void AddPacket(byte[] data)
        {
            if (!_running) return;
            try { _queue.TryAdd(data, 100); }
            catch (InvalidOperationException) { }
        }

        private void Run()
        {
            try
            {
                foreach (byte[] data in _queue.GetConsumingEnumerable())
                {
                    if (!_running) break;
                    try
                    {
                        _outputStream.Write(data, 0, data.Length);
                        _outputStream.Flush();
                    }
                    catch (Exception ex)
                    {
                        if (_running)
                        {
                            Debug.WriteLine($"[{Tag}] write error: {ex.Message}");
                            _callback.OnErrorOccurred();
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_running) Debug.WriteLine($"[{Tag}] error: {ex.Message}");
            }
            Debug.WriteLine($"[{Tag}] exited");
        }
    }
}
