// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

namespace SmartHomeEmulator.Streams
{
    public interface IStreamCallback
    {
        void OnPacketReceived(byte[] data, int length);
        void OnErrorOccurred();
    }
}
