using System;
using System.Configuration;

namespace NTRIP.Settings
{
    [ConfigurationCollection(typeof(LocalServer), AddItemName = "LocalServer",
            CollectionType = ConfigurationElementCollectionType.BasicMap)]

    public class LocalServerCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new LocalServer();
        }
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((LocalServer)element).Name;
        }

        new public LocalServer this[string name]

        {
            get { return (LocalServer)BaseGet(name); }
        }
    }
}
