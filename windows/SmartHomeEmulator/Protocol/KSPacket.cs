// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Diagnostics;
using System.Text;

using SmartHomeEmulator.Base;

namespace SmartHomeEmulator.Protocol
{
    /// <summary>
    /// KS X 4506 packet encoder/decoder.
    /// Wire format: [STX(F7)][DevId][SubId][Cmd][Len][Data...][XOR][ADD]
    /// Equivalent to Android's KSPacket.
    /// </summary>
    public class KSPacket : IHomePacket
    {
        public const int STX = 0xF7;

        public int    DeviceId    { get; set; }
        public int    DeviceSubId { get; set; }
        public int    CommandType { get; set; }
        public byte[] Data        { get; set; } = Array.Empty<byte>();

        // ---- IHomePacket ----
        public string Address => new KSAddress(DeviceId, DeviceSubId).DeviceAddress;
        public int    Command => CommandType;
        byte[] IHomePacket.Data => Data;

        // ---- Static helpers ----

        /// <summary>
        /// Checks if there are enough bytes in buffer[offset..] to hold a complete packet.
        /// Returns -1 if not enough, or the full packet size (including checksums) if enough.
        /// </summary>
        public static int EnsureSize(byte[] buf, int offset, int length)
        {
            int minSize = 7; // hdr + devId + subId + cmd + len + xor + add
            if (length < minSize) return -1;
            int dataLen = buf[offset + 4] & 0xFF;
            int fullSize = 7 + dataLen;   // 5 header bytes + data + 2 checksum bytes
            if (length < fullSize) return -1;
            return fullSize;
        }

        /// <summary>
        /// Validates XOR and ADD checksums of a packet at buffer[offset].
        /// Assumes EnsureSize already passed.
        /// </summary>
        public static bool Check(byte[] buf, int offset)
        {
            if ((buf[offset] & 0xFF) != STX) return false;

            int dataLen  = buf[offset + 4] & 0xFF;
            int xorPos   = offset + 5 + dataLen;
            int addPos   = xorPos + 1;
            int packetSz = 5 + dataLen;  // bytes that participate in checksum (before xor/add)

            int calXor = 0, calAdd = 0;
            for (int i = offset; i < offset + packetSz; i++)
            {
                int b  = buf[i] & 0xFF;
                calXor ^= b;
                calAdd += b;
            }
            calAdd += (buf[xorPos] & 0xFF);  // spec: add the xor byte to add-sum

            calXor &= 0xFF;
            calAdd &= 0xFF;

            if (calXor != (buf[xorPos] & 0xFF))
            {
                Debug.WriteLine($"[KSPacket] XOR mismatch: calc={calXor:X2} recv={buf[xorPos]:X2}");
                return false;
            }
            if (calAdd != (buf[addPos] & 0xFF))
            {
                Debug.WriteLine($"[KSPacket] ADD mismatch: calc={calAdd:X2} recv={buf[addPos]:X2}");
                return false;
            }
            return true;
        }

        // ---- IHomePacket ----

        public bool Parse(byte[] buffer, int offset, int length)
        {
            if (!Check(buffer, offset)) return false;

            DeviceId    = buffer[offset + 1] & 0xFF;
            DeviceSubId = buffer[offset + 2] & 0xFF;
            CommandType = buffer[offset + 3] & 0xFF;

            int dataLen = buffer[offset + 4] & 0xFF;
            Data = new byte[dataLen];
            if (dataLen > 0)
                Array.Copy(buffer, offset + 5, Data, 0, dataLen);

            return true;
        }

        public byte[] ToBytes()
        {
            int dataLen = Data?.Length ?? 0;
            int totalSize = 7 + dataLen;  // STX+devId+subId+cmd+len+data+xor+add
            byte[] buf = new byte[totalSize];

            buf[0] = (byte)STX;
            buf[1] = (byte)(DeviceId    & 0xFF);
            buf[2] = (byte)(DeviceSubId & 0xFF);
            buf[3] = (byte)(CommandType & 0xFF);
            buf[4] = (byte)(dataLen     & 0xFF);
            if (dataLen > 0) Array.Copy(Data, 0, buf, 5, dataLen);

            int packetSz = 5 + dataLen;
            int calXor   = 0, calAdd = 0;
            for (int i = 0; i < packetSz; i++)
            {
                int b  = buf[i] & 0xFF;
                calXor ^= b;
                calAdd += b;
            }
            buf[5 + dataLen] = (byte)(calXor & 0xFF);
            calAdd += (calXor & 0xFF);
            buf[6 + dataLen] = (byte)(calAdd & 0xFF);

            return buf;
        }

        public override string ToString()
        {
            return $"KSPacket[devId=0x{DeviceId:X2} subId=0x{DeviceSubId:X2} cmd=0x{CommandType:X2} data={BitConverter.ToString(Data ?? Array.Empty<byte>())}]";
        }
    }
}
