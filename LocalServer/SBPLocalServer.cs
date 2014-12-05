using System;
using System.Collections.Generic;
using RTCM;
using RTCM.MESSAGES;
using SwiftBinaryProtocol;
using SwiftBinaryProtocol.Eventarguments;
using SwiftBinaryProtocol.MessageStructs;

namespace NTRIP.LocalServerInstances
{
    public class SBPLocalServer : ILocalServer, IDisposable
    {
        private SBPReceiverSender _sbpReceiverSender;

        private Queue<ObservationHeader> _observationQueue = new Queue<ObservationHeader>();

        private List<ObservationHeader> _observationHeaders = new List<ObservationHeader>();

        private object _syncObject = new object();

        public SBPLocalServer()
        {
            _sbpReceiverSender = new SBPReceiverSender("COM4", 1000000);
            _sbpReceiverSender.ReceivedMessageEvent += _sbpReceiverSender_ReceivedMessageEvent;
        }

        public void Dispose()
        {
            _sbpReceiverSender.Dispose();
        }

        private void _sbpReceiverSender_ReceivedMessageEvent(object sender, SBPMessageEventArgs e)
        {
            if (e.MessageType == SBP_Enums.MessageTypes.OBS_HDR)
                lock (_syncObject)
                    _observationQueue.Enqueue((ObservationHeader)e.Data);
        }

        public byte[] GetBytes()
        {
            if (_observationQueue.Count < 1)
                return new byte[0];

            List<byte> bytes = new List<byte>();

            lock(_syncObject)
            {
                while(_observationQueue.Count > 0)
                {
                    ObservationHeader observation = _observationQueue.Dequeue();
                    bytes.AddRange(observation.Data);
                }
            }

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
