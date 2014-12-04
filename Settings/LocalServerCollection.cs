using System;
using System.Configuration;

namespace NTRIP.Settings
{
    [ConfigurationCollection(typeof(LocalServer), AddItemName = "LocalServer",
            CollectionType = ConfigurationElementCollectionType.BasicMap)]

    public class LocalServerCollection : ConfigurationElementCollection
    {
        public void CreateExampleServer()
        {
            base.BaseAdd(new LocalServer("SBP", @"C:\SBP\SBP.dll", "SBPLocalServer", "ExampleName", "-P COM1"));
        }

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
