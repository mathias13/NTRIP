using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SwiftBinaryProtocol;
using SwiftBinaryProtocol.Eventarguments;
using SwiftBinaryProtocol.MessageStructs;

namespace NTRIP.LocalServer
{
    public class SBPRawLocalServer : ILocalServer, IDisposable
    {
        private SBPRawReceiverSender _sbpReceiverSender;

        private Queue<byte[]> _messageQueue = new Queue<byte[]>();

        private object _syncObject = new object();

        public SBPRawLocalServer()
        {
            _sbpReceiverSender = new SBPRawReceiverSender("COM4", 1000000);
            _sbpReceiverSender.ReceivedRawMessageEvent += _sbpReceiverSender_ReceivedRawMessageEvent;
        }

        public void Dispose()
        {
            _sbpReceiverSender.Dispose();
        }

        private void _sbpReceiverSender_ReceivedRawMessageEvent(object sender, SBPRawMessageEventArgs e)
        {
            if (e.MessageType == SBP_Enums.MessageTypes.OBS_HDR || e.MessageType == SBP_Enums.MessageTypes.BASEPOS)
                lock (_syncObject)
                    _messageQueue.Enqueue(e.Message);
        }
        
        public byte[] GetBytes()
        {
            if (_messageQueue.Count < 1)
                return new byte[0];

            List<byte> bytes = new List<byte>();

            lock(_syncObject)
                while (_messageQueue.Count > 0)
                    bytes.AddRange(_messageQueue.Dequeue());

            return bytes.ToArray();
        }
        
        public GetBytesLocalServer GetBytesDelegate
        {
            get { return new GetBytesLocalServer(this.GetBytes); }
        }
    
        public Action GetDisposeDelegate
        {
            get { return new Action(this.Dispose); }
        }
    }
}
