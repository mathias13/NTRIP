using System;
using System.Configuration;

namespace NTRIP.Settings
{
    public class NTRIPUser : ConfigurationElement
    {
        private static ConfigurationProperty _userName =
            new ConfigurationProperty("UserName", typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _userPassword =
            new ConfigurationProperty("UserPassword", typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);
        
        public NTRIPUser()
        {
            base.Properties.Add(_userName);
            base.Properties.Add(_userPassword);
        }

        public NTRIPUser(string userName, string userPassword): this()
        {
            this[_userName] = userName;
            this[_userPassword]=userPassword;
        }

        [ConfigurationProperty("UserName", IsRequired = true)]
        public string UserName
        {
            get { return (string)this[_userName]; }
        }

        [ConfigurationProperty("UserPassword", IsRequired = true)]
        public string UserPassword
        {
            get { return (string)this[_userPassword]; }
        }    
    }
}
