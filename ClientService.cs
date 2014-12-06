using NTRIP.Eventarguments;
using System;
using System.Collections.Generic;
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
                
        #endregion

        #region Private Variables
        
        private Thread _serverConnectionThread;

        private bool _serverConnectionThreadStop = false;
        
        private bool _serverConnectionAvailable = false;

        private AutoResetEvent _serverConnectionLostEvent = new AutoResetEvent(false);

        private TcpClient _tcpClient;

        private Thread _serviceWorkThread;

        private bool _serviceWorkThreadStop = false;
        
        private object _syncObject = new object();

        private IPAddress _ipAdress;

        private int _tcpPort;
        
        private DateTime _lastHelloMessage = DateTime.MinValue;

        private bool _connect = false;

        private string _mountPoint;

        private string _user;

        private string _password;


        #endregion

        public ClientService(int tcpPort, IPAddress ipAdress, string mountPoint, string user, string password)
        {
            _tcpPort = tcpPort;
            _ipAdress = ipAdress;
            _mountPoint = mountPoint;
            _user = user;
            _password = password;

            _serverConnectionThread = new Thread(new ThreadStart(ServerConnectionThread));
            _serverConnectionThread.Start();

            _serviceWorkThread = new Thread(new ThreadStart(ServiceWorkThread));
            _serviceWorkThread.Start();

        }

        public void Dispose()
        {
            _serverConnectionLostEvent.Set();
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
            DateTime nextConnection = DateTime.MinValue;
            while (!_serverConnectionThreadStop)
            {
                if (!_connect || nextConnection > DateTime.Now)
                {
                    Thread.Sleep(200);
                    continue;
                }
                Ping ping = new Ping();
                if (ping.Send(_ipAdress, 10000).Status == IPStatus.Success)
                {
                    if (_tcpClient == null)
                        _tcpClient = new TcpClient();

                    try
                    {
                        _tcpClient.Connect(_ipAdress, _tcpPort);
                        byte[] authBytes = Encoding.ASCII.GetBytes(String.Format("{0}:{1}", _user, _password));
                        string auth = Convert.ToBase64String(authBytes);

                        string msg = String.Format(
                            "GET /{1} HTTP/1.0{0}"
                            + "User-Agent: NTRIP NolgardenNTRIPClient/20141010{0}"
                            + "Accept: */*{0}"
                            + "Connection: close{0}"
                            + "Authorization: Basic {2}{0}",
                             Constants.CRLF, _mountPoint, auth);

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
                                responseComplete = true;

                            if (DateTime.Now > timeOut)
                                break;
                        }

                        if (responseComplete)
                        {
                            msgReturned = msgReturned.Replace(Constants.CRLF, String.Empty);
                            if (msgReturned.Contains(Constants.ICY200OK))
                                _serverConnectionAvailable = true;
                            else if (msgReturned.Contains(Constants.UNAUTHORIZED))
                            {
                                _connect = false;
                                nextConnection = DateTime.Now.AddSeconds(30.0);
                                OnConnectionException(null, ConnectionFailure.Unauthorized);
                            }
                            else if (msgReturned.Contains(Constants.SOURCE200OK))
                            {
                                _connect = false;
                                nextConnection = DateTime.Now.AddSeconds(30.0);
                                OnConnectionException(null, ConnectionFailure.MountpointNotValid);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        OnConnectionException(e, ConnectionFailure.UnhandledException);
                    }
                }
                else
                    OnConnectionException(new Exception("NTRIP Server not responding"), ConnectionFailure.UnhandledException);

                if (_serverConnectionAvailable)
                {
                    _serverConnectionLostEvent.Reset();
                    _serverConnectionLostEvent.WaitOne(-1);
                }
            }
        }

        private void ServiceWorkThread()
        {
            bool preamableFound = false;
            List<byte> messageBytes = new List<byte>();
            int messageLength = Int32.MaxValue;
            Queue<byte> byteStream = new Queue<byte>();
            while (!_serviceWorkThreadStop)
            {
                if (!_serverConnectionAvailable)
                {
                    Thread.Sleep(20);
                    continue;
                }
                else
                    Thread.Sleep(0);

                try
                {
                    byte[] buffer = new byte[1024];
                    int bytesReceived = _tcpClient.GetStream().Read(buffer, 0, buffer.Length);
                    if (bytesReceived < 1)
                        Thread.Sleep(1);
                    else
                        for (int i = 0; i < bytesReceived; i++)
                            byteStream.Enqueue(buffer[i]);

                    while (byteStream.Count > 1)
                    {
                        //TODO Fix this it's not the NTRIP service buisness to decode the stream
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
            if (ConnectionExceptionEvent != null)
                ConnectionExceptionEvent.Invoke(this, new ConnectionExceptionArgs(e, connectionFailure));
        }

        #endregion

        #region Public Methods

        public void StartConnecting()
        {
            _connect = true;
            if(!_serverConnectionAvailable)
                _serverConnectionLostEvent.Set();
        }

        public void StartConnecting(string mounpoint)
        {
            _mountPoint = mounpoint;
            this.StartConnecting();            
        }

        public void StopConnecting()
        {
            _connect = false;
        }

        #endregion

        #region Public Properties

        public bool IsConnected
        {
            get { return _serverConnectionAvailable; }
        }

        #endregion
    }
}
