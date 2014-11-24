using System;
using System.Configuration;

namespace NTRIP.Settings
{
    [ConfigurationCollection(typeof(NTRIPMountPoint), AddItemName = "NTRIPMountPoint",
            CollectionType = ConfigurationElementCollectionType.BasicMap)]

    public class NTRIPMountPointCollection : ConfigurationElementCollection
    {
        public void CreateExampleMountPoint()
        {
            base.BaseAdd(new NTRIPMountPoint("ExampleName", "ExampleLocation", "RTCM", CarrierEnum.L1_L2, "GPS", 58.512585f, 13.854581f));
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new NTRIPMountPoint();
        }
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((NTRIPMountPoint)element).Name;
        }

        new public NTRIPMountPoint this[string name]
        {
            get { return (NTRIPMountPoint)BaseGet(name); }
        }
    }
}
