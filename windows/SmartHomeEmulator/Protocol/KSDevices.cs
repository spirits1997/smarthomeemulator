// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using SmartHomeEmulator.Base;

namespace SmartHomeEmulator.Protocol
{
    public static class BoilerProps
    {
        private const string PROP_PREFIX = "blr.";
        public const string PROP_HOTWATER_TEMP    = PROP_PREFIX + "hotwater.temp";
        public const string PROP_HEATING_TEMP     = PROP_PREFIX + "heating.temp";
        public const string PROP_AWAY_MODE        = PROP_PREFIX + "away_mode";
    }

    public class KSBoiler : KSDeviceContextBase
    {
        public KSBoiler(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props)
        {
            if (IsSlave())
            {
                _rxProps.Put(HomeDevice.PROP_ONOFF, false);
                _rxProps.Put(BoilerProps.PROP_HOTWATER_TEMP, 40.0f);
                _rxProps.Put(BoilerProps.PROP_HEATING_TEMP, 35.0f);
                _rxProps.Put(BoilerProps.PROP_AWAY_MODE, false);
                _rxProps.Commit();
            }
        }

        protected override int ParseStatusReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            var props = GetReadPropertyMap();
            bool on = props.Get<bool>(HomeDevice.PROP_ONOFF);
            bool away = props.Get<bool>(BoilerProps.PROP_AWAY_MODE);
            int state = (on ? 0x01 : 0x00) | (away ? 0x02 : 0x00);
            int hw = (int)(props.Get<float>(BoilerProps.PROP_HOTWATER_TEMP, 40.0f));
            int ht = (int)(props.Get<float>(BoilerProps.PROP_HEATING_TEMP, 35.0f));
            SendPacket(CreatePacket(CMD_STATUS_RSP, 0x00, (byte)state, (byte)hw, (byte)ht));
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseStatusRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 4) return PARSE_ERROR_MALFORMED_PACKET;
            int error = packet.Data[0] & 0xFF;
            if (error != 0) { OnErrorOccurred(HomeDevice.Error.UNKNOWN); return PARSE_OK_ERROR_RECEIVED; }
            int state = packet.Data[1] & 0xFF;
            outProps.Put(HomeDevice.PROP_ONOFF, (state & 0x01) != 0);
            outProps.Put(BoilerProps.PROP_AWAY_MODE, (state & 0x02) != 0);
            outProps.Put(BoilerProps.PROP_HOTWATER_TEMP, (float)(packet.Data[2] & 0xFF));
            outProps.Put(BoilerProps.PROP_HEATING_TEMP, (float)(packet.Data[3] & 0xFF));
            outProps.Commit();
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseSingleControlReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            if (packet.Data.Length < 1) return PARSE_ERROR_MALFORMED_PACKET;
            int cmd = packet.Data[0] & 0xFF;
            bool on = (cmd & 0x01) != 0;
            outProps.Put(HomeDevice.PROP_ONOFF, on);
            outProps.Commit();
            ParseStatusReq(packet, outProps);
            return PARSE_OK_ACTION_PERFORMED;
        }
    }

    public static class GasValveProps
    {
        private const string PROP_PREFIX = "gv.";
        public const string PROP_VALVE_STATE = PROP_PREFIX + "valve.state";
    }

    public class KSGasValve : KSDeviceContextBase
    {
        public KSGasValve(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props)
        {
            if (IsSlave())
            {
                _rxProps.Put(HomeDevice.PROP_ONOFF, false);
                _rxProps.Commit();
            }
        }

        protected override int ParseStatusReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            bool on = GetReadPropertyMap().Get<bool>(HomeDevice.PROP_ONOFF);
            SendPacket(CreatePacket(CMD_STATUS_RSP, 0x00, (byte)(on ? 0x01 : 0x00)));
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseStatusRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 2) return PARSE_ERROR_MALFORMED_PACKET;
            outProps.Put(HomeDevice.PROP_ONOFF, (packet.Data[1] & 0x01) != 0);
            outProps.Commit();
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseSingleControlReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            if (packet.Data.Length < 1) return PARSE_ERROR_MALFORMED_PACKET;
            outProps.Put(HomeDevice.PROP_ONOFF, (packet.Data[0] & 0x01) != 0);
            outProps.Commit();
            ParseStatusReq(packet, outProps);
            return PARSE_OK_ACTION_PERFORMED;
        }
    }

    public static class VentilationProps
    {
        private const string PROP_PREFIX = "vent.";
        public const string PROP_FAN_LEVEL = PROP_PREFIX + "fan.level";
        public const string PROP_MAX_FAN_LEVEL = PROP_PREFIX + "fan.max_level";
    }

    public class KSVentilation : KSDeviceContextBase
    {
        public KSVentilation(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props)
        {
            if (IsSlave())
            {
                _rxProps.Put(HomeDevice.PROP_ONOFF, false);
                _rxProps.Put(VentilationProps.PROP_FAN_LEVEL, 0);
                _rxProps.Put(VentilationProps.PROP_MAX_FAN_LEVEL, 3);
                _rxProps.Commit();
            }
        }

        protected override int ParseStatusReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            var props = GetReadPropertyMap();
            bool on = props.Get<bool>(HomeDevice.PROP_ONOFF);
            int level = props.Get<int>(VentilationProps.PROP_FAN_LEVEL);
            SendPacket(CreatePacket(CMD_STATUS_RSP, 0x00, (byte)(on ? (level & 0x0F) | 0x10 : 0x00)));
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseStatusRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 2) return PARSE_ERROR_MALFORMED_PACKET;
            int state = packet.Data[1] & 0xFF;
            outProps.Put(HomeDevice.PROP_ONOFF, (state & 0x10) != 0);
            outProps.Put(VentilationProps.PROP_FAN_LEVEL, state & 0x0F);
            outProps.Commit();
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseSingleControlReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            if (packet.Data.Length < 1) return PARSE_ERROR_MALFORMED_PACKET;
            int ctrl = packet.Data[0] & 0xFF;
            outProps.Put(HomeDevice.PROP_ONOFF, (ctrl & 0x10) != 0);
            outProps.Put(VentilationProps.PROP_FAN_LEVEL, ctrl & 0x0F);
            outProps.Commit();
            ParseStatusReq(packet, outProps);
            return PARSE_OK_ACTION_PERFORMED;
        }
    }

    public static class DoorLockProps
    {
        private const string PROP_PREFIX = "dl.";
        public const string PROP_LOCKED = PROP_PREFIX + "locked";
    }

    public class KSDoorLock : KSDeviceContextBase
    {
        public KSDoorLock(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props)
        {
            if (IsSlave())
            {
                _rxProps.Put(HomeDevice.PROP_ONOFF, false);
                _rxProps.Put(DoorLockProps.PROP_LOCKED, true);
                _rxProps.Commit();
            }
        }

        protected override int ParseStatusReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            bool locked = GetReadPropertyMap().Get<bool>(DoorLockProps.PROP_LOCKED, true);
            SendPacket(CreatePacket(CMD_STATUS_RSP, 0x00, (byte)(locked ? 0x01 : 0x00)));
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseStatusRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 2) return PARSE_ERROR_MALFORMED_PACKET;
            outProps.Put(DoorLockProps.PROP_LOCKED, (packet.Data[1] & 0x01) != 0);
            outProps.Commit();
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseSingleControlReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            // Door lock: can only lock (1) or open once (0→auto lock)
            if (packet.Data.Length < 1) return PARSE_ERROR_MALFORMED_PACKET;
            bool lock_ = (packet.Data[0] & 0x01) != 0;
            outProps.Put(DoorLockProps.PROP_LOCKED, lock_);
            outProps.Commit();
            ParseStatusReq(packet, outProps);
            return PARSE_OK_ACTION_PERFORMED;
        }
    }

    public static class BatchSwitchProps
    {
        private const string PROP_PREFIX = "bs.";
        public const string PROP_SWITCH_STATES = PROP_PREFIX + "switch.states";
    }

    public class KSBatchSwitch : KSDeviceContextBase
    {
        public KSBatchSwitch(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props)
        {
            if (IsSlave())
            {
                _rxProps.Put(HomeDevice.PROP_ONOFF, false);
                _rxProps.Put(BatchSwitchProps.PROP_SWITCH_STATES, 0);
                _rxProps.Commit();
            }
        }

        protected override int ParseStatusReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            int states = GetReadPropertyMap().Get<int>(BatchSwitchProps.PROP_SWITCH_STATES);
            SendPacket(CreatePacket(CMD_STATUS_RSP, 0x00, (byte)(states & 0xFF)));
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseStatusRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 2) return PARSE_ERROR_MALFORMED_PACKET;
            int states = packet.Data[1] & 0xFF;
            outProps.Put(BatchSwitchProps.PROP_SWITCH_STATES, states);
            outProps.Put(HomeDevice.PROP_ONOFF, states != 0);
            outProps.Commit();
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseSingleControlReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            if (packet.Data.Length < 1) return PARSE_ERROR_MALFORMED_PACKET;
            int states = packet.Data[0] & 0xFF;
            outProps.Put(BatchSwitchProps.PROP_SWITCH_STATES, states);
            outProps.Put(HomeDevice.PROP_ONOFF, states != 0);
            outProps.Commit();
            ParseStatusReq(packet, outProps);
            return PARSE_OK_ACTION_PERFORMED;
        }
    }

    public static class HouseMeterProps
    {
        private const string PROP_PREFIX = "hm.";
        public const string PROP_METER_VALUE     = PROP_PREFIX + "meter.value";
        public const string PROP_REALTIME_VALUE  = PROP_PREFIX + "realtime.value";
    }

    public class KSHouseMeter : KSDeviceContextBase
    {
        public KSHouseMeter(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props)
        {
            if (IsSlave())
            {
                _rxProps.Put(HouseMeterProps.PROP_METER_VALUE, 0.0f);
                _rxProps.Put(HouseMeterProps.PROP_REALTIME_VALUE, 0.0f);
                _rxProps.Commit();
            }
        }

        protected override int ParseStatusReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            // Return 4-byte meter value (dummy 0 for now)
            SendPacket(CreatePacket(CMD_STATUS_RSP, 0x00, 0x00, 0x00, 0x00, 0x00));
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseStatusRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 5) return PARSE_ERROR_MALFORMED_PACKET;
            int error = packet.Data[0] & 0xFF;
            if (error != 0) { OnErrorOccurred(HomeDevice.Error.UNKNOWN); return PARSE_OK_ERROR_RECEIVED; }
            int raw = ((packet.Data[1] & 0xFF) << 24) | ((packet.Data[2] & 0xFF) << 16)
                    | ((packet.Data[3] & 0xFF) << 8)  |  (packet.Data[4] & 0xFF);
            outProps.Put(HouseMeterProps.PROP_METER_VALUE, (float)raw);
            outProps.Commit();
            return PARSE_OK_STATE_UPDATED;
        }
    }

    public static class PowerSaverProps
    {
        private const string PROP_PREFIX = "ps.";
        public const string PROP_STANDBY_CUT = PROP_PREFIX + "standby.cut";
    }

    public class KSPowerSaver : KSDeviceContextBase
    {
        public KSPowerSaver(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props)
        {
            if (IsSlave())
            {
                _rxProps.Put(HomeDevice.PROP_ONOFF, false);
                _rxProps.Put(PowerSaverProps.PROP_STANDBY_CUT, false);
                _rxProps.Commit();
            }
        }

        protected override int ParseStatusReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            var props = GetReadPropertyMap();
            bool on = props.Get<bool>(HomeDevice.PROP_ONOFF);
            bool cut = props.Get<bool>(PowerSaverProps.PROP_STANDBY_CUT);
            SendPacket(CreatePacket(CMD_STATUS_RSP, 0x00, (byte)((on ? 0x01 : 0x00) | (cut ? 0x02 : 0x00))));
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseStatusRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 2) return PARSE_ERROR_MALFORMED_PACKET;
            int state = packet.Data[1] & 0xFF;
            outProps.Put(HomeDevice.PROP_ONOFF, (state & 0x01) != 0);
            outProps.Put(PowerSaverProps.PROP_STANDBY_CUT, (state & 0x02) != 0);
            outProps.Commit();
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseSingleControlReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            if (packet.Data.Length < 1) return PARSE_ERROR_MALFORMED_PACKET;
            int state = packet.Data[0] & 0xFF;
            outProps.Put(HomeDevice.PROP_ONOFF, (state & 0x01) != 0);
            outProps.Put(PowerSaverProps.PROP_STANDBY_CUT, (state & 0x02) != 0);
            outProps.Commit();
            ParseStatusReq(packet, outProps);
            return PARSE_OK_ACTION_PERFORMED;
        }
    }

    public static class CurtainProps
    {
        private const string PROP_PREFIX = "ctn.";
        public const string PROP_OPEN_LEVEL = PROP_PREFIX + "open.level";
        public const string PROP_MAX_LEVEL  = PROP_PREFIX + "open.max_level";
    }

    public class KSCurtain : KSDeviceContextBase
    {
        public KSCurtain(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props)
        {
            if (IsSlave())
            {
                _rxProps.Put(HomeDevice.PROP_ONOFF, false);
                _rxProps.Put(CurtainProps.PROP_OPEN_LEVEL, 0);
                _rxProps.Put(CurtainProps.PROP_MAX_LEVEL, 100);
                _rxProps.Commit();
            }
        }

        protected override int ParseStatusReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            int level = GetReadPropertyMap().Get<int>(CurtainProps.PROP_OPEN_LEVEL);
            SendPacket(CreatePacket(CMD_STATUS_RSP, 0x00, (byte)(level & 0xFF)));
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseStatusRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 2) return PARSE_ERROR_MALFORMED_PACKET;
            int level = packet.Data[1] & 0xFF;
            outProps.Put(CurtainProps.PROP_OPEN_LEVEL, level);
            outProps.Put(HomeDevice.PROP_ONOFF, level > 0);
            outProps.Commit();
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseSingleControlReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            if (packet.Data.Length < 1) return PARSE_ERROR_MALFORMED_PACKET;
            int level = packet.Data[0] & 0xFF;
            outProps.Put(CurtainProps.PROP_OPEN_LEVEL, level);
            outProps.Put(HomeDevice.PROP_ONOFF, level > 0);
            outProps.Commit();
            ParseStatusReq(packet, outProps);
            return PARSE_OK_ACTION_PERFORMED;
        }
    }

    public static class AirConditionerProps
    {
        private const string PROP_PREFIX = "ac.";
        public const string PROP_MODE          = PROP_PREFIX + "mode";
        public const string PROP_FAN_LEVEL     = PROP_PREFIX + "fan.level";
        public const string PROP_SET_TEMP      = PROP_PREFIX + "temperature.setting";
        public const string PROP_CURRENT_TEMP  = PROP_PREFIX + "temperature.current";
    }

    public class KSAirConditioner : KSDeviceContextBase
    {
        public KSAirConditioner(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props)
        {
            if (IsSlave())
            {
                _rxProps.Put(HomeDevice.PROP_ONOFF, false);
                _rxProps.Put(AirConditionerProps.PROP_MODE, 0);
                _rxProps.Put(AirConditionerProps.PROP_FAN_LEVEL, 0);
                _rxProps.Put(AirConditionerProps.PROP_SET_TEMP, 24.0f);
                _rxProps.Put(AirConditionerProps.PROP_CURRENT_TEMP, 24.0f);
                _rxProps.Commit();
            }
        }

        protected override int ParseStatusReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            var props = GetReadPropertyMap();
            bool on    = props.Get<bool>(HomeDevice.PROP_ONOFF);
            int  mode  = props.Get<int>(AirConditionerProps.PROP_MODE);
            int  fan   = props.Get<int>(AirConditionerProps.PROP_FAN_LEVEL);
            int  setT  = (int)props.Get<float>(AirConditionerProps.PROP_SET_TEMP, 24.0f);
            int  curT  = (int)props.Get<float>(AirConditionerProps.PROP_CURRENT_TEMP, 24.0f);
            SendPacket(CreatePacket(CMD_STATUS_RSP, 0x00,
                (byte)((on ? 0x80 : 0x00) | (mode & 0x0F)),
                (byte)(fan & 0x0F),
                (byte)setT,
                (byte)curT));
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseStatusRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 5) return PARSE_ERROR_MALFORMED_PACKET;
            int error = packet.Data[0] & 0xFF;
            if (error != 0) { OnErrorOccurred(HomeDevice.Error.UNKNOWN); return PARSE_OK_ERROR_RECEIVED; }
            int status = packet.Data[1] & 0xFF;
            outProps.Put(HomeDevice.PROP_ONOFF, (status & 0x80) != 0);
            outProps.Put(AirConditionerProps.PROP_MODE, status & 0x0F);
            outProps.Put(AirConditionerProps.PROP_FAN_LEVEL, packet.Data[2] & 0x0F);
            outProps.Put(AirConditionerProps.PROP_SET_TEMP, (float)(packet.Data[3] & 0xFF));
            outProps.Put(AirConditionerProps.PROP_CURRENT_TEMP, (float)(packet.Data[4] & 0xFF));
            outProps.Commit();
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseSingleControlReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            if (packet.Data.Length < 1) return PARSE_ERROR_MALFORMED_PACKET;
            int ctrl = packet.Data[0] & 0xFF;
            outProps.Put(HomeDevice.PROP_ONOFF, (ctrl & 0x80) != 0);
            outProps.Put(AirConditionerProps.PROP_MODE, ctrl & 0x0F);
            if (packet.Data.Length >= 3)
            {
                outProps.Put(AirConditionerProps.PROP_FAN_LEVEL, packet.Data[1] & 0x0F);
                outProps.Put(AirConditionerProps.PROP_SET_TEMP, (float)(packet.Data[2] & 0xFF));
            }
            outProps.Commit();
            ParseStatusReq(packet, outProps);
            return PARSE_OK_ACTION_PERFORMED;
        }
    }
}
