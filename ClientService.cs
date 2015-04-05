using NTRIP.Eventarguments;
using NTRIP.Settings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NTRIP
{
    public class ClientService
    {
        #region Events

        public event EventHandler<ConnectionExceptionArgs> ConnectionExceptionEvent;

        public event EventHandler<StreamReceivedArgs> StreamDataReceivedEvent;

        public event EventHandler<SourceTableArgs> SourceTableReceivedEvent;
                
        #endregion

        #region Private Variables
        
        private Thread _serverConnectionThread;

        private bool _serverConnectionThreadStop = false;
        
        private bool _serverConnectedAndStreaming = false;
        
        private TcpClient _tcpClient;

        private Thread _serviceWorkThread;

        private bool _serviceWorkThreadStop = false;
        
        private object _syncObject = new object();

        private IPAddress _ipAdress = null;

        private ClientSettings _settings;
        
        private bool _connect = false;

        private bool _tcpClientConnected = false;

        private bool _disconnect = false;
        
        private int ALIVE_CHECK_TIMEOUT = 30;

        #endregion

        public ClientService(ClientSettings settings)
        {
            _settings = settings;
            
            _serverConnectionThread = new Thread(new ThreadStart(ServerConnectionThread));
            _serverConnectionThread.Start();

            _serviceWorkThread = new Thread(new ThreadStart(ServiceWorkThread));
            _serviceWorkThread.Start();

        }

        public void Dispose()
        {
            _serverConnectionThreadStop = true;
            _serverConnectionThread.Join();
            
            _serviceWorkThreadStop = true;
            _serviceWorkThread.Join();

            if (_tcpClient != null)
                _tcpClient.Close();
        }

        #region Thread Methods

        private void ServerConnectionThread()
        {
            while (!_serverConnectionThreadStop)
            {
                if (_disconnect)
                {
                    lock (_syncObject)
                    {
                        _tcpClient.Close();
                        _tcpClient = null;
                        _tcpClientConnected = false;
                        _serverConnectedAndStreaming = false;
                        _disconnect = false;
                    }
                }

                if (!_connect)
                {
                    Thread.Sleep(200);
                    continue;
                }

                if (_tcpClient == null)
                    _tcpClient = new TcpClient();

                if (_ipAdress == null)
                {
                    if (!IPAddress.TryParse(_settings.IPorHost, out _ipAdress))
                    {
                        IPHostEntry ipAdresses;
                        try
                        {
                            ipAdresses = Dns.GetHostEntry(_settings.IPorHost);
                        }
                        catch(SocketException e)
                        {
                            OnConnectionException(e, ConnectionFailure.NoInternetConnection);
                            continue;
                        }
                        if (ipAdresses.AddressList.Length < 1)
                            throw new Exception(String.Format("No valid ip found for: {0}", _settings.IPorHost));

                        _ipAdress = ipAdresses.AddressList[0];
                    }
                }
                try
                {
                    if (!_tcpClientConnected)
                        _tcpClient.Connect(_ipAdress, _settings.PortNumber);

                    _tcpClientConnected = true;

                    byte[] authBytes = Encoding.ASCII.GetBytes(String.Format("{0}:{1}", _settings.NTRIPUser.UserName, _settings.NTRIPUser.UserPassword));
                    string auth = Convert.ToBase64String(authBytes);

                    string msg = String.Format(
                        "GET /{1} HTTP/1.0{0}"
                        + "User-Agent: NTRIP NolgardenNTRIPClient/20141010{0}"
                        + "Accept: */*{0}"
                        + "Connection: close{0}"
                        + "Authorization: Basic {2}{0}",
                            Constants.CRLF, _settings.NTRIPMountPoint, auth);

                    byte[] msgBytes = Encoding.ASCII.GetBytes(msg);

                    _tcpClient.GetStream().Write(msgBytes, 0, msgBytes.Length);

                    DateTime timeOut = DateTime.Now.AddSeconds(30.0);
                    bool responseComplete = false;
                    string msgReturned = String.Empty;
                    while (!responseComplete)
                    {
                        if (_tcpClient.Available > 0)
                        {
                            byte[] receiveByte = new byte[_tcpClient.Available];
                            _tcpClient.GetStream().Read(receiveByte, 0, receiveByte.Length);
                            msgReturned += Encoding.ASCII.GetString(receiveByte);
                        }

                        if (msgReturned.Contains(Constants.CRLF))
                        {
                            if (msgReturned.Contains(Constants.SOURCE200OK))
                            {
                                if (msgReturned.Contains(Constants.ENDSOURCETABLE))
                                    responseComplete = true;
                            }
                            else
                                responseComplete = true;
                        }

                        if (DateTime.Now > timeOut)
                            break;
                    }

                    if (responseComplete)
                    {
                        _connect = false;
                        if (msgReturned.Contains(Constants.ICY200OK))
                            _serverConnectedAndStreaming = true;
                        else if (msgReturned.Contains(Constants.UNAUTHORIZED))
                        {
                            _serverConnectedAndStreaming = false;
                            OnConnectionException(null, ConnectionFailure.Unauthorized);
                        }
                        else if (msgReturned.Contains(Constants.SOURCE200OK))
                        {
                            if (_settings.NTRIPMountPoint != String.Empty)
                                OnConnectionException(null, ConnectionFailure.MountpointNotValid);

                            msgReturned = msgReturned.Replace(Constants.SOURCE200OK + Constants.CRLF, String.Empty);
                            List<MountPoint> mountPoints = new List<MountPoint>();
                            string[] mountPointStrings = msgReturned.Split(new string[] { Constants.CRLF }, StringSplitOptions.None);
                            foreach (string mountPointString in mountPointStrings)
                            {
                                if (mountPointString.StartsWith("STR"))
                                {
                                    string[] fields = mountPointString.Split(new char[] { ';' });
                                    mountPoints.Add(new MountPoint(fields[1],
                                                        fields[2],
                                                        fields[3],
                                                        (CarrierEnum)Convert.ToInt32(fields[5]),
                                                        fields[6],
                                                        Convert.ToSingle(fields[9], new CultureInfo("en-US")),
                                                        Convert.ToSingle(fields[10], new CultureInfo("en-US"))));
                                }

                            }

                            OnSourceTableReceived(mountPoints.ToArray());

                            _serverConnectedAndStreaming = false;
                        }
                    }
                    else
                    {
                        _connect = false;
                        OnConnectionException(new Exception("Incomplete or no answer from Caster"), ConnectionFailure.NoAnswer);
                    }
                }
                catch (Exception e)
                {
                    _connect = false;
                    _tcpClient.Close();
                    _tcpClientConnected = false;
                    OnConnectionException(e, ConnectionFailure.UnhandledException);
                }
                                
            }
        }

        private void ServiceWorkThread()
        {
            DateTime aliveCheckTimeout = DateTime.MinValue;
            List<byte> messageBytes = new List<byte>();
            Queue<byte> byteStream = new Queue<byte>();
            while (!_serviceWorkThreadStop)
            {
                if (!_serverConnectedAndStreaming)
                {
                    Thread.Sleep(20);
                    continue;
                }
                else
                    Thread.Sleep(0);

                try
                {
                    lock (_syncObject)
                    {
                        if (aliveCheckTimeout < DateTime.Now)
                        {
                            try
                            {
                                if(0 > _tcpClient.Client.Send(new byte[0]))
                                {
                                    OnConnectionException(new Exception("Lost connection to NTRIP Server"), ConnectionFailure.ConnectionTerminated);
                                    continue;
                                }
                            }
                            catch (SocketException e)
                            {
                                OnConnectionException(e, ConnectionFailure.ConnectionTerminated);
                                continue;
                            }
                            aliveCheckTimeout = DateTime.Now.AddSeconds((double)ALIVE_CHECK_TIMEOUT);
                        }

                        byte[] buffer = new byte[1024];
                        int bytesReceived = _tcpClient.GetStream().Read(buffer, 0, buffer.Length);
                        if (bytesReceived < 1)
                            Thread.Sleep(1);
                        else
                        {
                            byte[] dataStream = new byte[bytesReceived];
                            Array.Copy(buffer, dataStream, bytesReceived);
                            OnStreamDataReceived(dataStream, _settings.NTRIPMountPoint);
                            aliveCheckTimeout = DateTime.Now.AddSeconds((double)ALIVE_CHECK_TIMEOUT);                            
                        }
                    }
                }
                catch (Exception e)
                {
                    OnConnectionException(e, ConnectionFailure.UnhandledException);
                }
            }
        }

        #endregion

        #region ProtectedMethods

        protected void OnConnectionException(Exception e, ConnectionFailure connectionFailure)
        {
            _serverConnectedAndStreaming = false;

            if (ConnectionExceptionEvent != null)
                ConnectionExceptionEvent.Invoke(this, new ConnectionExceptionArgs(e, connectionFailure));
        }

        protected void OnStreamDataReceived(byte[] data, string mountPoint)
        {
            if (StreamDataReceivedEvent != null)
                StreamDataReceivedEvent.Invoke(this, new StreamReceivedArgs(data, mountPoint));
        }

        protected void OnSourceTableReceived(MountPoint[] mountPoints)
        {
            if (SourceTableReceivedEvent != null)
                SourceTableReceivedEvent.Invoke(this, new SourceTableArgs(mountPoints));
        }

        #endregion

        #region Public Methods

        public void Connect()
        {
            _connect = true;
        }

        public void Connect(string mounpoint)
        {
            _settings.NTRIPMountPoint = mounpoint;
            this.Connect();            
        }
        
        public void Disconnect()
        {
            _disconnect = true;
        }

        #endregion

        #region Public Properties

        public bool IsConnected
        {
            get { return _serverConnectedAndStreaming; }
        }

        #endregion
    }
}
