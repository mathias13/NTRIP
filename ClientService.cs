using RTCM;
using RTCM.MESSAGES;
using NTRIP.Eventarguments;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Utilities.Log;
using SwiftBinaryProtocol;

namespace NTRIP
{
    public enum GNSSStream
    {
        RTCM,
        SBP,
        SBPRaw
    }

    public class ClientService
    {
        #region Events

        public event EventHandler<ConnectionExceptionArgs> ConnectionExceptionEvent;

        public event EventHandler<RTCMReceived> RTCMReceivedEvent;

        public event EventHandler<SBPRawReceived> SBPRawReceivedEvent;
        
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

        private GNSSStream _gnssStream;

        #endregion

        public ClientService(int tcpPort, IPAddress ipAdress, string mountPoint, string user, string password, GNSSStream gnssStream)
        {
            _tcpPort = tcpPort;
            _ipAdress = ipAdress;
            _mountPoint = mountPoint;
            _user = user;
            _password = password;
            _gnssStream = gnssStream;

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
                        Log.Error(e);
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
            while (!_serviceWorkThreadStop)
            {
                if (!_serverConnectionAvailable)
                {
                    Thread.Sleep(20);
                    continue;
                }
                else
                    Thread.Sleep(20);

                try
                {
                    NetworkStream stream = _tcpClient.GetStream();
                    switch(_gnssStream)
                    {
                        case GNSSStream.RTCM:
                            if(preamableFound)
                            {
                                if(messageBytes.Count < 3 && stream.DataAvailable)
                                {
                                    messageBytes.Add(HeaderUtil.RTCM_PREAMBLE);
                                    messageBytes.Add((byte)stream.ReadByte());
                                    messageBytes.Add((byte)stream.ReadByte());
                                    messageLength = (int)BitUtil.GetUnsigned(messageBytes.ToArray(), 14, 10);
                                }

                                if(messageBytes.Count < messageLength + 6)
                                {
                                    if(stream.DataAvailable)
                                        messageBytes.Add((byte)stream.ReadByte());
                                }
                                else
                                {
                                    object message = null;
                                    int messageType = (int)BitUtil.GetUnsigned(messageBytes.ToArray(), 24,12);
                                    if (HeaderUtil.CheckAndRemoveRTCMHeader(ref messageBytes))
                                    {
                                        MessageEnum.Messages messageTypeEnum = MessageEnum.Messages.Unknown;
                                        if (Enum.IsDefined(typeof(MessageEnum.Messages), messageType))
                                            messageTypeEnum = (MessageEnum.Messages)messageType;
                                        switch (messageTypeEnum)
                                        {
                                            case MessageEnum.Messages.MSG_1002:
                                                message = new Message_1002(messageBytes.ToArray());
                                                break;

                                            case MessageEnum.Messages.Unknown:
                                                messageBytes.Clear();
                                                continue;
                                        }
                                        OnRTCMReceived(messageTypeEnum, message);
                                    }
                                    else
                                        ;//TODO trigger event;
                                }
                            }
                            else
                            {
                                if(stream.DataAvailable)
                                {
                                    if((byte)stream.ReadByte() == HeaderUtil.RTCM_PREAMBLE)
                                        preamableFound = true;
                                }
                                else
                                    Thread.Sleep(20);
                            }
                            break;

                        case GNSSStream.SBPRaw:
                            if (preamableFound)
                            {
                                if (messageBytes.Count == 0)
                                    messageBytes.Add(SBPReceiverSender.PREAMBLE);

                                while (messageBytes.Count < 6 && stream.DataAvailable)
                                    messageBytes.Add((byte)stream.ReadByte());

                                while (messageBytes.Count < ((int)messageBytes[5] + 8) && messageBytes.Count >= 6 && stream.DataAvailable)
                                    messageBytes.Add((byte)stream.ReadByte());

                                if (messageBytes.Count == ((int)messageBytes[5] + 8))
                                {
                                    List<byte> crcBytes = new List<byte>();
                                    for (int i = 1; i < messageBytes.Count - 2; i++)
                                        crcBytes.Add(messageBytes[i]);

                                    ushort crc = Crc16CcittKermit.ComputeChecksum(crcBytes.ToArray());
                                    byte[] crcSumBytes = new byte[2] { messageBytes[messageBytes.Count - 2], messageBytes[messageBytes.Count - 1] };
                                    ushort crcInMessage = BitConverter.ToUInt16(crcSumBytes, 0);
                                    if (crc == crcInMessage)
                                        OnSBPRawReceived(messageBytes.ToArray());
                                    else
                                        ;//TODO Trigger event
                                    messageBytes.Clear();
                                    preamableFound = false;
                                }
                            }
                            else
                                if(stream.DataAvailable)
                                    if ((byte)stream.ReadByte() == SBPReceiverSender.PREAMBLE)
                                        preamableFound = true;
                            break;

                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
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

        protected void OnRTCMReceived(MessageEnum.Messages msgType, object message)
        {
            if (RTCMReceivedEvent != null)
                RTCMReceivedEvent.Invoke(this, new RTCMReceived(msgType, message));
        }

        protected void OnSBPRawReceived(byte[] message)
        {
            if (SBPRawReceivedEvent != null)
                SBPRawReceivedEvent.Invoke(this, new SBPRawReceived(message));
        }

        //protected void OnServerMessage(ServerMessage message)
        //{
        //    ServerMessageEventArgs e = new ServerMessageEventArgs(message);
        //    ServerMessageEventHandler handler = ServerMessageEvent;
        //    if (handler != null)
        //        handler.Invoke(this, e);
        //}

        //protected void OnServerConnectedChanged(bool isConnected)
        //{
        //    _serverConnectionAvailable = isConnected;
        //    if (!isConnected)
        //        _serverConnectionLostEvent.Set();

        //    ServerConnectedChangedEventHandler handler = ServerConnectedChangedEvent;
        //    if (handler != null)
        //        handler.Invoke(this, isConnected);
        //}

        #endregion

        #region Public Methods

        public void SendServerMessage(ISendMessage message)
        {
            lock (_syncObject)
                ;
        }

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
