// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;

namespace SmartHomeEmulator.Base
{
    /// <summary>
    /// Base model for a home device.  Equivalent to Android's HomeDevice.
    /// </summary>
    public class HomeDevice
    {
        // ---- Well-known property names ----
        public const string PROP_ADDR      = "0.address";
        public const string PROP_AREA      = "0.area";
        public const string PROP_NAME      = "0.name";
        public const string PROP_IS_SLAVE  = "0.is_slave";
        public const string PROP_ONOFF     = "0.on_off";
        public const string PROP_CONNECTED = "0.connected";
        public const string PROP_TYPE      = "0.type";

        public static class Type
        {
            public const int UNKNOWN       = 0;
            public const int LIGHT         = 1;
            public const int DOOR_LOCK     = 2;
            public const int VENTILATION   = 3;
            public const int GAS_VALVE     = 4;
            public const int HOUSE_METER   = 5;
            public const int CURTAIN       = 6;
            public const int THERMOSTAT    = 7;
            public const int BATCH_SWITCH  = 8;
            public const int SENSOR        = 9;
            public const int AIRCONDITIONER = 10;
            public const int POWER_SAVER   = 11;
        }

        public static class Area
        {
            public const int UNKNOWN      = 0;
            public const int ENTERANCE    = 1;
            public const int LIVING_ROOM  = 2;
            public const int MAIN_ROOM    = 3;
            public const int OTHER_ROOM   = 4;
            public const int KITCHEN      = 5;
        }

        public static class Error
        {
            public const int UNKNOWN = 0;
        }

        // ---- Callbacks ----
        public delegate void PropertyChangedHandler(HomeDevice device, IList<PropertyValue> props);
        public delegate void ErrorOccurredHandler(HomeDevice device, int error);

        public event PropertyChangedHandler PropertyChanged;
        public event ErrorOccurredHandler   ErrorOccurred;

        // ---- Internal context ----
        private readonly DeviceContextBase _dc;

        public HomeDevice(DeviceContextBase dc)
        {
            _dc = dc;
        }

        public DeviceContextBase Dc => _dc;

        public string GetAddress() => GetProperty<string>(PROP_ADDR) ?? "";

        public T GetProperty<T>(string name)
        {
            return _dc.GetReadPropertyMap().Get<T>(name);
        }

        public PropertyValue GetProperty(string name)
        {
            return _dc.GetReadPropertyMap().Get(name);
        }

        public void NotifyPropertyChanged(IList<PropertyValue> props)
        {
            PropertyChanged?.Invoke(this, props);
        }

        public void NotifyErrorOccurred(int error)
        {
            ErrorOccurred?.Invoke(this, error);
        }

        public override string ToString()
        {
            string name = GetProperty<string>(PROP_NAME) ?? GetAddress();
            return $"HomeDevice[{name}]";
        }
    }
}
