using System;

namespace NTRIP.Eventarguments
{
    public class StreamReceivedArgs : EventArgs
    {
        private byte[] _streamData;

        private string _mountPoint;

        public StreamReceivedArgs(byte[] data, string mountPoint)
        {
            _streamData = data;
            _mountPoint = mountPoint;
        }

        public byte[] DataStream
        {
            get { return _streamData; }
        }

        public string MountPoint
        {
            get { return _mountPoint; }
        }
    }
}
