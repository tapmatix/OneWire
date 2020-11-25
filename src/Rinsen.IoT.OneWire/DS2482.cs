using System;
using System.Collections.Generic;
using System.Device.I2c;
using System.Linq;
using static Rinsen.IoT.OneWire.DS2482Channel;

namespace Rinsen.IoT.OneWire
{
    public abstract class DS2482 : IDisposable
    {
        protected IList<DS2482Channel> Channels = new List<DS2482Channel>();
        private bool _disposeI2cDevice;
        private Dictionary<byte, Type> _oneWireDeviceTypes = new Dictionary<byte, Type>();
        private List<IOneWireDevice> _oneWireDevices = new List<IOneWireDevice>();
        private bool _initialized = false;

        public I2cDevice I2cDevice { get; }

        public DS2482(I2cDevice i2cDevice, bool disposeI2cDevice)
        {
            I2cDevice = i2cDevice;
            _disposeI2cDevice = disposeI2cDevice;

            AddDeviceType<DS18S20>(0x10);
            AddDeviceType<DS18B20>(0x28);
        }

        public abstract bool IsCorrectChannelSelected(OneWireChannel channel);

        public abstract void SetSelectedChannel(OneWireChannel channel);

        public void AddDeviceType<T>(byte familyCode) where T : IOneWireDevice
        {
            _oneWireDeviceTypes.Add(familyCode, typeof(T));
        }

        public IReadOnlyCollection<IOneWireDevice> GetAllDevices()
        {
            InitializeDevices();

            return _oneWireDevices;
        }

        private void InitializeDevices()
        {
            if (!_initialized)
            {
                foreach (var channel in Channels)
                {
                    _oneWireDevices.AddRange(channel.GetConnectedOneWireDevices(_oneWireDeviceTypes));
                }

                _initialized = true;
            }
        }

        public IReadOnlyCollection<T> GetDevices<T>() where T : IOneWireDevice
        {
            InitializeDevices();

            return _oneWireDevices.OfType<T>().ToArray();
        }

        public bool OneWireReset()
        {
            //Clear list to allow for detection of removed devices and avoid duplicates
            _oneWireDevices.Clear();
            // Clear initialized flag to ensure that the next call to GetDevices initiates a new search
            _initialized = false;
            // As OneWireReset does not take a channel identifier as parameter, we have to reset all channels.
            // Unfortunately this forces us to aggregate the result to avoid breaking the interface.
            // While this is ok for the 1 channel version, it is not helpful dealing with the 8 channel chip.
            // I would suggest to change the interface in the long run.

            //This returns true if devices where found on at least one channel.
            //return Channels.Select(c => c.Reset()).Any();
            return Channels.First().Reset();
        }

        public void EnableStrongPullup()
        {
            var configuration = new byte();
            configuration |= 1 << 2;
            configuration |= 1 << 7;
            configuration |= 1 << 5;
            configuration |= 1 << 4;

            I2cDevice.Write(new byte[] { FunctionCommand.WRITE_CONFIGURATION, configuration });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (I2cDevice != null && !_disposeI2cDevice)
                    I2cDevice.Dispose();
            }
        }
    }
}
