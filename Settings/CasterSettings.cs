using System;
using System.Configuration;

namespace NTRIP.Settings
{   
    public class CasterSettings : ConfigurationSection
    {
        private static ConfigurationProperty portNumber =
            new ConfigurationProperty("PortNumber", typeof(Int32), 5000, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty ntripUsers =
            new ConfigurationProperty("NTRIPUsers", typeof(NTRIPUserCollection), null, ConfigurationPropertyOptions.IsRequired);

        public CasterSettings()
        {
            base.Properties.Add(portNumber);
            base.Properties.Add(ntripUsers);
        }

        [ConfigurationProperty("PortNumber", IsRequired = true)]
        public int PortNumber
        {
            get { return (int)this[portNumber]; }
            set { this[portNumber] = value; }
        }

        [ConfigurationProperty("NTRIPUsers", IsRequired = true)]
        public NTRIPUserCollection NTRIPUsers
        {
            get { return (NTRIPUserCollection)this[ntripUsers]; }
            set { this[ntripUsers] = value; }
        }
    }
}
