using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRIP
{
    public enum CarrierEnum
    {
        NO = 0,
        L1 = 1,
        L1_L2 = 2
    }

    public struct MountPoint
    {
        private string _name;

        private string _location;

        private string _format;

        private CarrierEnum _carrier;

        private string _navSystem;

        private float _latitude;

        private float _longitude;

        public MountPoint(string name, string location, string format, CarrierEnum carrier, string navSystem, float latitude, float longitude)
        {
            _name = name;
            _location = location;
            _format = format;
            _carrier = carrier;
            _navSystem = navSystem;
            _latitude = latitude;
            _longitude = longitude;
        }

        public string Name
        {
            get { return _name; }
        }

        public string Location
        {
            get { return _location; }
        }

        public string Format
        {
            get { return _format; }
        }

        public CarrierEnum Carrier
        {
            get { return _carrier; }
        }

        public string NavSystem
        {
            get { return _navSystem; }
        }

        public float Latitude
        {
            get { return _latitude; }
        }

        public float Longitude
        {
            get { return _longitude; }
        }
    }
}
