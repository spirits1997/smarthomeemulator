// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System.IO;

namespace SmartHomeEmulator.Session
{
    /// <summary>
    /// Abstraction for a network/serial communication session.
    /// Equivalent to Android's NetworkSession interface.
    /// </summary>
    public interface INetworkSession
    {
        bool Open();
        void Close();
        Stream InputStream { get; }
        Stream OutputStream { get; }
    }
}
