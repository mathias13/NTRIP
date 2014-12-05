using System;

namespace NTRIP
{
    public delegate byte[] GetBytesLocalServer();

    public interface ILocalServer
    {
        byte[] GetBytes();

        GetBytesLocalServer GetBytesDelegate
        {
            get;
        }

        Action GetDisposeDelegate
        {
            get;
        }
    }
}
