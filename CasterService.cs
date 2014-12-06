using NTRIP.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Text;

namespace NTRIP
{
    public class CasterService : IDisposable
    {
        #region Variables

        private const int PORT_NUMBER = 5000;
        
        private TcpListener _tcpListener;

        private Thread _listeningThread;

        private bool _stopListening = false;

        private object _syncObject = new object();

        private List<NtripClientContext> _clients = new List<NtripClientContext>();

        private IDictionary<int, MountPoint> _mountPoints = new Dictionary<int, MountPoint>();

        private IDictionary<string, string> _users = new Dictionary<string, string>();

        private string _casterPassword = "nolgardenCaster";
        
        #endregion

        #region ctor

        public CasterService(CasterSettings settings)
        {
            _tcpListener = new TcpListener(IPAddress.Any, settings.PortNumber);
            int i = 0;
            foreach(NTRIPMountPoint mountPoint in settings.NTRIPMountPoints)
            {
                _mountPoints.Add(i, new MountPoint(mountPoint.Name, mountPoint.Format, mountPoint.Format, mountPoint.Carrier, mountPoint.NavSystem, mountPoint.Latitude, mountPoint.Longitude));
                i++;
            }

            foreach (NTRIPUser user in settings.NTRIPUsers)
                _users.Add(new KeyValuePair<string, string>(user.UserName, user.UserPassword));


            foreach (LocalServer localServer in settings.LocalServers)
            {
                if (!File.Exists(localServer.DLLPath))
                    throw new FileNotFoundException("Couldn't find dll", localServer.DLLPath);

                Assembly serverAssembly = Assembly.LoadFile(localServer.DLLPath);
                Type serverType = serverAssembly.GetType(localServer.ClassName);
                if (serverType == null)
                    throw new NullReferenceException(String.Format("ClassName: {0} doesn't exist in {1}", localServer.ClassName, localServer.DLLPath));
                else if (!CheckILocalServer(serverType))
                    throw new Exception(String.Format("Class: {0} doesn't implement ILocalServer", serverType.Name));

                ILocalServer serverInstance = null;

                foreach (ConstructorInfo constructor in serverType.GetConstructors())
                {
                    ParameterInfo[] parameter = constructor.GetParameters();
                    if (parameter.Length > 0)
                        if (parameter[0].ParameterType == typeof(String))
                            serverInstance = (ILocalServer)Activator.CreateInstance(serverType, localServer.ConstructorArguments);
                }

                //If type doesn't contain constructor with arguments create it using no parameters
                if (serverInstance == null)
                    serverInstance = (ILocalServer)Activator.CreateInstance(serverType);

                NtripClientContext piksi = new NtripClientContext(serverInstance, GetMountPoint(localServer.MountPoint));
                piksi.ClientType = ClientType.NTRIP_ServerLocal;
                _clients.Add(piksi);
            }
        }

        public CasterService(CasterSettings settings, IDictionary<string, ILocalServer> servers):this(settings)
        {
            foreach (KeyValuePair<string, ILocalServer> server in servers)
            {
                NtripClientContext piksi = new NtripClientContext(server.Value, GetMountPoint(server.Key));
                piksi.ClientType = ClientType.NTRIP_ServerLocal;
                _clients.Add(piksi);
            }
        }

        #endregion

        #region Public Methods

        public void StartListening()
        {
            _stopListening = false;
            _listeningThread = new Thread(new ThreadStart(ListeningThread));
            _listeningThread.Start();
        }

        public void StopListening()
        {
            _stopListening = true;
        }

        public void Dispose()
        {
            StopListening();
            if (_listeningThread != null)
                _listeningThread.Join(TimeSpan.FromSeconds(20.0));

        }

        #endregion

        #region Private Methods

        private int GetMountPoint(string mountPoint)
        {
            foreach (KeyValuePair<int, MountPoint> item in _mountPoints)
                if (item.Value.Name.ToLower() == mountPoint.ToLower())
                    return item.Key;

            return -1;
        }

        private void ListeningThread()
        {
            try
            {
                _tcpListener.Start();
                while (!_stopListening)
                {
                    if (_tcpListener.Pending())
                        _clients.Add(new NtripClientContext(_tcpListener.AcceptTcpClient()));

                    bool noActivity = true;
                    for (int i = 0; i < _clients.Count; i++)
                    {
                        _clients[i].ByteStorage.AddRange(_clients[i].ProcessIncoming());

                        if (_clients[i].RemoveFlag)
                        {
                            _clients.RemoveAt(i);
                            continue;
                        }
                        
                        List<string> messageLines = new List<string>();

                        if (_clients[i].Agent != String.Empty && _clients[i].ValidUser && _clients[i].ClientType != ClientType.Unknown)
                        {
                            if (_clients[i].ClientType == ClientType.NTRIP_ServerLocal || _clients[i].ClientType == ClientType.NTRIP_Server)
                            {
                                if(_clients[i].ClientType == ClientType.NTRIP_ServerLocal)
                                    _clients[i].ByteStorage.AddRange(_clients[i].LocalDelegate.Invoke());
                                else if(_clients[i].ClientType == ClientType.NTRIP_Server)
                                    _clients[i].ByteStorage.AddRange(_clients[i].ProcessIncoming());

                                if (_clients[i].ByteStorage.Count > 0)
                                    noActivity = false;

                                if ((_clients[i].ClientType == ClientType.NTRIP_ServerLocal || _clients[i].ClientType == ClientType.NTRIP_Server) &&_clients[i].ByteStorage.Count > 0)
                                {
                                    foreach (NtripClientContext client in _clients)
                                        if (client.ClientType == ClientType.NTRIP_Client)
                                            if (client.MountPoint == _clients[i].MountPoint)
                                                client.SendMessage(_clients[i].ByteStorage.ToArray());

                                    _clients[i].ByteStorage.Clear();
                                }

                            }
                        }
                        else
                        {
                            if (_clients[i].ByteStorage.Count < 1)
                                continue;
                            string message = Encoding.ASCII.GetString(_clients[i].ByteStorage.ToArray());
                            if (!message.EndsWith(Constants.CRLF))
                                message = message.Substring(0, message.LastIndexOf(Constants.CRLF) + 4);

                            messageLines.AddRange(message.Split(new string[] { Constants.CRLF }, int.MaxValue, StringSplitOptions.None));

                            _clients[i].ByteStorage.RemoveRange(0, Encoding.ASCII.GetByteCount(message));

                            if (messageLines.Count < 1)
                                noActivity = false;

                            if (_clients[i].ClientType == ClientType.Unknown && messageLines.Count > 0)
                            {
                                if (messageLines[0].StartsWith("GET "))
                                {
                                    _clients[i].ClientType = ClientType.NTRIP_Client;
                                    string[] stringSplit = messageLines[0].Substring(4).Split(' ');
                                    _clients[i].MountPoint = GetMountPoint(stringSplit[0].Remove(0,1));
                                    if (_clients[i].MountPoint < 0)
                                    {
                                        _clients[i].SendMessage(Encoding.ASCII.GetBytes(Constants.SOURCE200OK + Constants.CRLF));
                                        _clients[i].SendMessage(Encoding.ASCII.GetBytes(GenerateSourceTable()));
                                        _clients[i].ClientType = ClientType.Unknown;
                                        continue;
                                    }

                                    messageLines.RemoveAt(0);
                                }
                                else if (messageLines[0].StartsWith("SOURCE "))
                                {
                                    _clients[i].ClientType = ClientType.NTRIP_Server;
                                    string[] stringSplit = messageLines[0].Substring(4).Split(' ');
                                    if (stringSplit[0] != _casterPassword)
                                    {
                                        _clients[i].SendMessage(Encoding.ASCII.GetBytes(Constants.BAD_PASS + Constants.CRLF));
                                        _clients[i].RemoveFlag = true;
                                        continue;
                                    }

                                    _clients[i].SendMessage(Encoding.ASCII.GetBytes(Constants.ICY200OK + Constants.CRLF));
                                    _clients[i].ValidUser = true;

                                    messageLines.RemoveAt(0);
                                }
                                else
                                    _clients[i].RemoveFlag = true;
                            }

                            if ((_clients[i].Agent == String.Empty && _clients[i].ClientType != ClientType.Unknown) && messageLines.Count > 0)
                            {
                                if (messageLines[0].StartsWith("User-Agent: NTRIP ") || messageLines[0].StartsWith("Source-Agent: NTRIP "))
                                {
                                    messageLines[0] = messageLines[0].Replace("User-Agent: NTRIP ", "");
                                    messageLines[0] = messageLines[0].Replace("Source-Agent: NTRIP ", "");
                                    _clients[i].Agent = messageLines[0];
                                }
                                else
                                    _clients[i].RemoveFlag = true;

                                messageLines.RemoveAt(0);
                            }

                            if (_clients[i].Agent != String.Empty && !_clients[i].ValidUser && _clients[i].ClientType == ClientType.NTRIP_Client && messageLines.Count > 0)
                            {
                                while (messageLines[0].Contains("Accept:") || messageLines[0].Contains("Connection:"))
                                    messageLines.RemoveAt(0);
                                string[] stringSplit = messageLines[0].Split(' ');
                                if (stringSplit[0] == "Authorization:" && stringSplit[1] == "Basic")
                                {
                                    //TODO change user not correct
                                    byte[] userPassBytes = Convert.FromBase64String(stringSplit[2]);
                                    string[] userPass = Encoding.ASCII.GetString(userPassBytes).Split(':');
                                    if(userPass.Length != 2)
                                    {
                                        _clients[i].SendMessage(Encoding.ASCII.GetBytes(Constants.UNAUTHORIZED + Constants.CRLF));
                                        _clients[i].RemoveFlag = true;
                                        continue;
                                    }
                                    else if (!_users.ContainsKey(userPass[0]))
                                    {
                                        _clients[i].SendMessage(Encoding.ASCII.GetBytes(Constants.UNAUTHORIZED + Constants.CRLF));
                                        _clients[i].RemoveFlag = true;
                                        continue;
                                    }
                                    else if (_users[userPass[0]] != userPass[1])
                                    {
                                        _clients[i].SendMessage(Encoding.ASCII.GetBytes(Constants.UNAUTHORIZED + Constants.CRLF));
                                        _clients[i].RemoveFlag = true;
                                        continue;
                                    }
                                    else
                                    {
                                        _clients[i].ValidUser = true;
                                        _clients[i].SendMessage(Encoding.ASCII.GetBytes(Constants.ICY200OK + Constants.CRLF));
                                    }
                                }
                                else
                                {
                                    if(_clients[i].AuthenticationNotified)
                                        _clients[i].RemoveFlag = true;
                                    else
                                    {
                                        _clients[i].SendMessage(Encoding.ASCII.GetBytes(Constants.UNAUTHORIZED + Constants.CRLF));
                                        _clients[i].AuthenticationNotified = true;
                                        _clients[i].ClientType = ClientType.Unknown;
                                        _clients[i].Agent = String.Empty;
                                    }
                                    continue;
                                }
                            }

                            if(_clients[i].Timeout)
                                _clients[i].RemoveFlag = true;
                        }

                    }

                    if (noActivity)
                        Thread.Sleep(1);

                }
                _tcpListener.Stop();
                foreach (NtripClientContext client in _clients)
                    client.Dispose();
            }
            catch (Exception e)
            {
                //TODO create event for this exception
                if (!_stopListening)
                    StartListening();
            }
        }

        private string GenerateSourceTable()
        {
            string mountpoints = String.Empty;
            foreach(MountPoint mountpoint in _mountPoints.Values)
            {
                mountpoints += "STR;";
                mountpoints += mountpoint.Name + ";";
                mountpoints += mountpoint.Location + ";";
                mountpoints += mountpoint.Format + ";";
                mountpoints += ";";
                mountpoints += ((int)mountpoint.Carrier).ToString() + ";";
                mountpoints += mountpoint.NavSystem + ";";
                mountpoints += "Nolgarden RTK;";
                mountpoints += ";";
                mountpoints += mountpoint.Latitude.ToString("0.00") + ";";
                mountpoints += mountpoint.Longitude.ToString("0.00") + ";";
                mountpoints += "0;";
                mountpoints += "0;";
                mountpoints += ";";
                mountpoints += ";";
                mountpoints += "B;";
                mountpoints += "N;";
                mountpoints += "9600;";
                mountpoints += ";";
                mountpoints += Constants.CRLF;

            }
            string msg = String.Empty;
            msg += "Server: Nolgarden NTRIP Caster/1.0" + Constants.CRLF;
            msg += "Content-Type: text/plain" + Constants.CRLF;
            msg += "Content-Length: " + mountpoints.Length.ToString() + Constants.CRLF;
            msg += mountpoints;
            msg += "ENDSOURCETABLE" + Constants.CRLF;

            return msg;
        }

        private bool CheckILocalServer(Type serverType)
        {
            foreach (Type type in serverType.GetInterfaces()) 
                if (type == typeof(ILocalServer)) 
                    return true;  
            
            return false;
        }
        
        #endregion
    }
}
