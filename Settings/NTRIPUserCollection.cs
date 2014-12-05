using System;
using System.Configuration;

namespace NTRIP.Settings
{
    [ConfigurationCollection(typeof(NTRIPUser), AddItemName = "NTRIPUser",
            CollectionType = ConfigurationElementCollectionType.BasicMap)]

    public class NTRIPUserCollection : ConfigurationElementCollection
    {
        public void CreateExampleUser()
        {
            base.BaseAdd(new NTRIPUser("ExAmPlEuSeR", "ExAmPlEpAsSwOrD"));
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new NTRIPUser();
        }
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((NTRIPUser)element).UserName;
        }

        new public NTRIPUser this[string userName]
        {
            get { return (NTRIPUser)BaseGet(userName); }
        }
    }
}
