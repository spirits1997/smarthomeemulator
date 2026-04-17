// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;

namespace SmartHomeEmulator.Base
{
    public interface IHomePacket
    {
        string Address  { get; }
        int    Command  { get; }
        byte[] Data     { get; }

        bool Parse(byte[] buffer, int offset, int length);
        byte[] ToBytes();
    }

    /// <summary>Packet wrapper that carries extra metadata flags.</summary>
    public class HomePacketMeta
    {
        public IHomePacket Packet    { get; }
        public bool        SuppressLog { get; set; }

        public HomePacketMeta(IHomePacket packet) { Packet = packet; }
    }
}
