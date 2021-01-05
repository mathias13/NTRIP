using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRIP.Eventarguments
{
    public enum ConnectionFailure
    {
        UnhandledException,
        Unauthorized,
        MountpointNotValid,
        ConnectionTerminated,
        NoAnswer,
        NoInternetConnection
    }

    public class ConnectionExceptionArgs
    {
        private Exception _e;

        private ConnectionFailure _connectionFailure;

        public ConnectionExceptionArgs(Exception e, ConnectionFailure connectionFailure)
        {
            _e = e;
            _connectionFailure = connectionFailure;
        }

        public Exception Exception
        {
            get { return _e; }
        }

        public ConnectionFailure ConnectionFailure
        {
            get { return _connectionFailure; }
        }
    }
}
