using System;
using System.Configuration;

namespace NTRIP.Settings
{
    public class NTRIPMountPoint : ConfigurationElement
    {
        private static ConfigurationProperty _name =
            new ConfigurationProperty("Name", typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _location =
            new ConfigurationProperty("Location", typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _format =
            new ConfigurationProperty("Format", typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _carrier =
            new ConfigurationProperty("Carrier", typeof(CarrierEnum), CarrierEnum.NO, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _navSystem =
            new ConfigurationProperty("NavSystem", typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _longitude =
            new ConfigurationProperty("Longitude", typeof(Single), 0.0f, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _latitude =
            new ConfigurationProperty("Latitude", typeof(Single), 0.0f, ConfigurationPropertyOptions.IsRequired);

        public NTRIPMountPoint()
        {
            base.Properties.Add(_name);
            base.Properties.Add(_location);
            base.Properties.Add(_format);
            base.Properties.Add(_carrier);
            base.Properties.Add(_navSystem);
            base.Properties.Add(_longitude);
            base.Properties.Add(_latitude);
        }

        public NTRIPMountPoint(string name, string location, string format, CarrierEnum carrier, string navSystem, float longitude, float latitude)
            : this()
        {
            this[_name] = name;
            this[_location] = location;
            this[_format] = format;
            this[_carrier] = carrier;
            this[_navSystem] = navSystem;
            this[_longitude] = longitude;
            this[_latitude] = latitude;
        }

        [ConfigurationProperty("Name", IsRequired = true)]
        public string Name
        {
            get { return (string)this[_name]; }
        }

        [ConfigurationProperty("Location", IsRequired = true)]
        public string Location
        {
            get { return (string)this[_location]; }
        }
        
        [ConfigurationProperty("Format", IsRequired = true)]
        public string Format
        {
            get { return (string)this[_format]; }
        }

        [ConfigurationProperty("Carrier", IsRequired = true)]
        public CarrierEnum Carrier
        {
            get { return (CarrierEnum)this[_carrier]; }
        }

        [ConfigurationProperty("Longitude", IsRequired = true)]
        public float Longitude
        {
            get { return (float)this[_longitude]; }
        }

        [ConfigurationProperty("Latitude", IsRequired = true)]
        public float Latitude
        {
            get { return (float)this[_latitude]; }
        }
    }
}
