// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

using SmartHomeEmulator.Base;

namespace SmartHomeEmulator.Protocol
{
    /// <summary>
    /// KS X 4506 main protocol context – routes parsed packets to device contexts.
    /// Equivalent to Android's KSMainContext.
    /// </summary>
    public class KSMainContext : MainContext
    {
        private const string Tag = "KSMainContext";

        protected readonly Dictionary<int, Func<KSMainContext, Dictionary<string, object>, KSDeviceContextBase>>
            _addressToFactory = new Dictionary<int, Func<KSMainContext, Dictionary<string, object>, KSDeviceContextBase>>();

        private DeviceDiscovery _discovery;
        private readonly ConcurrentDictionary<string, HomeDevice> _virtualDevices
            = new ConcurrentDictionary<string, HomeDevice>();

        public KSMainContext()
        {
            RegisterDefaultDevices();
        }

        protected virtual void RegisterDefaultDevices()
        {
            _addressToFactory[0x02] = (ctx, props) => new KSAirConditioner(ctx, props);
            _addressToFactory[0x33] = (ctx, props) => new KSBatchSwitch(ctx, props);
            _addressToFactory[0x35] = (ctx, props) => new KSBoiler(ctx, props);
            _addressToFactory[0x13] = (ctx, props) => new KSCurtain(ctx, props);
            _addressToFactory[0x31] = (ctx, props) => new KSDoorLock(ctx, props);
            _addressToFactory[0x12] = (ctx, props) => new KSGasValve(ctx, props);
            _addressToFactory[0x30] = (ctx, props) => new KSHouseMeter(ctx, props);
            _addressToFactory[0x0E] = (ctx, props) => new KSLight(ctx, props);
            _addressToFactory[0x39] = (ctx, props) => new KSPowerSaver(ctx, props);
            _addressToFactory[0x34] = (ctx, props) => new KSSecurityExpansion(ctx, props);
            _addressToFactory[0x36] = (ctx, props) => new KSThermostat(ctx, props);
            _addressToFactory[0x32] = (ctx, props) => new KSVentilation(ctx, props);
        }

        // ---- MainContext overrides ----

        public override HomeDevice CreateDevice(Dictionary<string, object> defaultProps)
        {
            int devId = GetDeviceIdFromProps(defaultProps);
            KSDeviceContextBase dc;
            if (_addressToFactory.TryGetValue(devId, out var factory))
                dc = factory(this, defaultProps);
            else
                dc = new KSUnknownDevice(this, defaultProps);

            var device = new HomeDevice(dc);
            dc.SetDevice(device);
            return device;
        }

        public override bool AddDevice(HomeDevice device)
        {
            string addr = device.GetAddress();

            // If a virtual placeholder existed, remove it
            _virtualDevices.TryRemove(addr, out _);

            bool added = base.AddDevice(device);
            if (!added) return false;

            var dc     = (KSDeviceContextBase)device.Dc;
            int devId  = dc.GetDeviceId();
            int subId  = dc.GetDeviceSubId().Value();
            int singleNibble = subId & 0x0F;
            int groupNibble  = subId & 0xF0;

            if (singleNibble == 0x0F)
            {
                // This is a "full" group device – wire up any already-added children
                for (int i = 1; i <= 0xE; i++)
                {
                    string childAddr = new KSAddress(devId, groupNibble | i).ToAddressString();
                    var    child     = GetDevice(childAddr);
                    if (child != null) dc.AddChild(child.Dc);
                }
            }
            else
            {
                // This is a single device – find or create parent
                string parentAddr = new KSAddress(devId, groupNibble | 0x0F).ToAddressString();
                HomeDevice parent = GetDevice(parentAddr);
                if (parent == null) _virtualDevices.TryGetValue(parentAddr, out parent);
                if (parent == null)
                {
                    var parentProps = new Dictionary<string, object>
                    {
                        [HomeDevice.PROP_ADDR]     = parentAddr,
                        [HomeDevice.PROP_AREA]     = HomeDevice.Area.UNKNOWN,
                        [HomeDevice.PROP_NAME]     = "Virtual " + parentAddr,
                        [HomeDevice.PROP_IS_SLAVE] = device.Dc.IsSlave(),
                    };
                    parent = CreateDevice(parentProps);
                    _virtualDevices[parentAddr] = parent;
                }
                parent.Dc.AddChild(dc);
            }

            return true;
        }

        public override void RemoveDevice(HomeDevice device)
        {
            var dc     = (KSDeviceContextBase)device.Dc;
            int subId  = dc.GetDeviceSubId().Value();

            if ((subId & 0x0F) == 0x0F)
                dc.RemoveAllChildren();
            else
            {
                int devId      = dc.GetDeviceId();
                string parent  = new KSAddress(devId, subId | 0x0F).ToAddressString();
                var parentDev  = GetDevice(parent);
                parentDev?.Dc.RemoveChild(dc);
            }

            _virtualDevices.TryRemove(device.GetAddress(), out _);
            base.RemoveDevice(device);
        }

        public override DeviceDiscovery GetDeviceDiscovery()
        {
            if (_discovery == null) _discovery = new DeviceDiscovery();
            return _discovery;
        }

        // ---- Packet parsing ----

        protected override int ParsePacket(byte[] buffer, int offset, int length)
        {
            // Skip until we find STX
            if ((buffer[offset] & 0xFF) != KSPacket.STX)
            {
                Debug.WriteLine($"[{Tag}] Skipping non-STX byte 0x{buffer[offset]:X2}");
                return 1;  // consume this byte and try next
            }

            int packetSize = KSPacket.EnsureSize(buffer, offset, length);
            if (packetSize < 0) return 0;  // need more data

            var packet = new KSPacket();
            if (!packet.Parse(buffer, offset, length))
            {
                Debug.WriteLine($"[{Tag}] Bad packet checksum, skipping");
                return 1;  // skip STX and look for next
            }

            Debug.WriteLine("RX: " + BitConverter.ToString(buffer, offset, packetSize).Replace("-", " "));

            ParsePacketInDeviceContexts(packet);

            return packetSize;
        }

        private void ParsePacketInDeviceContexts(KSPacket packet)
        {
            var address = new KSAddress(packet.DeviceId, packet.DeviceSubId);
            string devAddr = address.DeviceAddress;

            var device = GetDevice(devAddr);
            if (device == null) _virtualDevices.TryGetValue(devAddr, out device);

            if (device != null)
            {
                device.Dc.ParsePacket(packet);

                if (device.Dc.IsMaster())
                {
                    foreach (var child in device.Dc.GetChildren())
                        child.ParsePacket(packet);
                }
            }
        }

        // ---- Helpers ----

        private static int GetDeviceIdFromProps(Dictionary<string, object> props)
        {
            if (props.TryGetValue(HomeDevice.PROP_ADDR, out var addrObj))
            {
                string addr = addrObj?.ToString() ?? "";
                return new KSAddress(addr).GetDeviceId();
            }
            return 0;
        }
    }

    /// <summary>Placeholder context for unknown device IDs.</summary>
    internal class KSUnknownDevice : KSDeviceContextBase
    {
        public KSUnknownDevice(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props) { }
    }

    /// <summary>Security expansion placeholder (no specific protocol handling).</summary>
    internal class KSSecurityExpansion : KSDeviceContextBase
    {
        public KSSecurityExpansion(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props) { }
    }
}
