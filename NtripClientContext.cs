using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Utilities.Log;

namespace NTRIP
{
    public enum ClientType
    {
        Unknown,
        NTRIP_Client,
        NTRIP_Server,
        NTRIP_ServerLocal
    }

    public class NtripClientContext : IDisposable
    {
        #region Private Variables

        private TcpClient _tcpClient;

        private ClientType _clientType = ClientType.Unknown;

        private bool _removeFlag = false;

        private int _mountPoint = -1;

        private bool _validUser = false;

        private bool _authenticationNotified = false;

        private string _agent = String.Empty;

        private DateTime _timeout;

        private const int TIMEOUT_DELAY = 60;

        private GetBytesLocalServer _localServerDelegate = null;

        private Action _localDisposeDelegate = null;

        #endregion

        #region ctor

        public NtripClientContext(TcpClient client)
        {
            _tcpClient = client;
            _timeout = DateTime.Now.AddSeconds((double)TIMEOUT_DELAY);
        }

        public NtripClientContext(ILocalServer localServer, int mountPoint) : this(null)
        {
            _clientType = NTRIP.ClientType.NTRIP_ServerLocal;
            _localServerDelegate = localServer.GetBytesDelegate;
            _localDisposeDelegate = localServer.GetDisposeDelegate;
            _validUser = true;
            _agent = "NTRIP Local";
            _mountPoint = mountPoint;
        }

        #endregion

        #region Properties

        public ClientType ClientType
        {
            get { return _clientType; }
            set { _clientType = value; }
        }

        public int MountPoint
        {
            get { return _mountPoint; }
            set { _mountPoint = value; }
        }

        public bool RemoveFlag
        {
            get { return _removeFlag; }
            set { _removeFlag = value; }
        }

        public bool ValidUser
        {
            get { return _validUser; }
            set { _validUser = value; }
        }

        public bool Timeout
        {
            get { return DateTime.Now > _timeout; }
        }

        public string Agent
        {
            get { return _agent; }
            set { _agent = value; }
        }

        public GetBytesLocalServer LocalDelegate
        {
            get { return _localServerDelegate; }
        }

        public List<byte> ByteStorage = new List<byte>();

        public bool AuthenticationNotified
        {
            get { return _authenticationNotified; }
            set { _authenticationNotified = value; }
        }

        #endregion

        #region Methods

        public void SendMessage(byte[] bytes)
        {
            try
            {
                _tcpClient.GetStream().Write(bytes, 0, bytes.Length);
            }
            catch(Exception e)
            {
                Log.Error(e);
                if (!_tcpClient.Connected)
                    _removeFlag = true;
            }
        }

        public void Remove()
        {
            _tcpClient.Close();
        }

        public byte[] ProcessIncoming()
        {
            byte[] bytes = new byte[0];
            if (_clientType != NTRIP.ClientType.NTRIP_ServerLocal)
            {
                try
                {
                    if (_tcpClient.Available > 0)
                    {
                        bytes = new byte[_tcpClient.Available];
                        _tcpClient.GetStream().Read(bytes, 0, _tcpClient.Available);
                    }

                }
                catch (Exception e)
                {
                    Log.Error(e);
                    if (!_tcpClient.Connected)
                        _removeFlag = true;
                }
            }
            return bytes;
        }

        public void Dispose()
        {
            if (_localDisposeDelegate != null)
                _localDisposeDelegate.Invoke();
        }

        #endregion


    }
}
