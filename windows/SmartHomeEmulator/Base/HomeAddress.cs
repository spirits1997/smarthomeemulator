// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

namespace SmartHomeEmulator.Base
{
    public abstract class HomeAddress
    {
        public abstract string DeviceAddress { get; }
        public override string ToString() => DeviceAddress;
    }
}
