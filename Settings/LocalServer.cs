using System;
using System.Configuration;

namespace NTRIP.Settings
{
    public class LocalServer : ConfigurationElement
    {        
        private static ConfigurationProperty _name =
            new ConfigurationProperty("Name", typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _dllPath =
            new ConfigurationProperty("DLLPath", typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _className =
            new ConfigurationProperty("ClassName", typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);
                 
        private static ConfigurationProperty _mountPoint =
            new ConfigurationProperty("MountPoint", typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);

        private static ConfigurationProperty _constructorArgs =
            new ConfigurationProperty("ConstructorArguments", typeof(string), string.Empty, ConfigurationPropertyOptions.IsRequired);

        public LocalServer()
        {
            base.Properties.Add(_name);
            base.Properties.Add(_dllPath);
            base.Properties.Add(_className);
            base.Properties.Add(_mountPoint);
            base.Properties.Add(_constructorArgs);
        }

        public LocalServer(string name, string dllPath, string className, string mountPoint, string constructorArgs):this()
        {
            this[_name] = name;
            this[_dllPath] = dllPath;
            this[_className] = className;
            this[_mountPoint] = mountPoint;
            this[_constructorArgs] = constructorArgs;
        }

        [ConfigurationProperty("Name", IsRequired = true)]
        public string Name
        {
            get { return (string)this[_name]; }
        }

        [ConfigurationProperty("DLLPath", IsRequired = true)]
        public string DLLPath
        {
            get { return (string)this[_dllPath]; }
        }

        [ConfigurationProperty("ClassName", IsRequired = true)]
        public string ClassName
        {
            get { return (string)this[_className]; }
        }
        [ConfigurationProperty("MountPoint", IsRequired = true)]
        public string MountPoint
        {
            get { return (string)this[_mountPoint]; }
        }

        [ConfigurationProperty("ConstructorArguments", IsRequired = true)]
        public string ConstructorArguments
        {
            get { return (string)this[_constructorArgs]; }
        }
    }
}
