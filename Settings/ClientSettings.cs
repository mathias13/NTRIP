using System;
using System.Configuration;

namespace NTRIP.Settings
{   
    public class ClientSettings : ConfigurationSection
    {
        private static ConfigurationProperty _ipOrHost =
            new ConfigurationProperty("IPorHost", typeof(string), String.Empty, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _portNumber =
            new ConfigurationProperty("PortNumber", typeof(Int32), 5000, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _ntripUser =
            new ConfigurationProperty("NTRIPUser", typeof(NTRIPUser), null, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _ntripMountPoint =
            new ConfigurationProperty("NTRIPMountPoint", typeof(string), null, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _localServers =
            new ConfigurationProperty("LocalServers", typeof(LocalServerCollection), null, ConfigurationPropertyOptions.IsRequired);

        public ClientSettings()
        {
            base.Properties.Add(_ipOrHost);
            base.Properties.Add(_portNumber);
            base.Properties.Add(_ntripUser);
            base.Properties.Add(_ntripMountPoint);
        }

        [ConfigurationProperty("IPorHost", IsRequired = true)]
        public string IPorHost
        {
            get { return (string)this[_ipOrHost]; }
            set { this[_ipOrHost] = value; }
        }

        [ConfigurationProperty("PortNumber", IsRequired = true)]
        public int PortNumber
        {
            get { return (int)this[_portNumber]; }
            set { this[_portNumber] = value; }
        }

        [ConfigurationProperty("NTRIPUser", IsRequired = true)]
        public NTRIPUser NTRIPUser
        {
            get { return (NTRIPUser)this[_ntripUser]; }
            set { this[_ntripUser] = value; }
        }

        [ConfigurationProperty("NTRIPMountPoint", IsRequired = true)]
        public string NTRIPMountPoint
        {
            get { return (string)this[_ntripMountPoint]; }
            set { this[_ntripMountPoint] = value; }
        }

        [ConfigurationProperty("LocalServers", IsRequired = true)]
        public LocalServerCollection LocalServers
        {
            get { return (LocalServerCollection)this[_localServers]; }
            set { this[_localServers] = value; }
        }
    }
}
