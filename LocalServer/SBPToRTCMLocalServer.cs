using System;
using System.Collections.Generic;
using RTCM;
using RTCM.MESSAGES;
using SwiftBinaryProtocol;
using SwiftBinaryProtocol.Eventarguments;
using SwiftBinaryProtocol.MessageStructs;

namespace NTRIP.LocalServerInstances
{
    public class SBPToRTCMLocalServer: ILocalServer, IDisposable
    {
        protected const float PRUNIT_GPS = 299792.458f;

        protected const float CLIGHT = 299792458.0f;

        protected const float FREQ1 = 1.57542E9f;

        protected const float LAMBDA1 = CLIGHT / FREQ1;

        private SBPReceiverSender _sbpReceiverSender;

        private Queue<ObservationHeader> _observationQueue = new Queue<ObservationHeader>();

        private List<ObservationHeader> _observationHeaders = new List<ObservationHeader>();

        private object _syncObject = new object();

        public SBPToRTCMLocalServer()
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
                    if (_observationHeaders.Count == observation.Count)
                    {
                        if (observation.Count > 0)
                        {
                            if (_observationHeaders[_observationHeaders.Count - 1].TimeOfWeek == observation.TimeOfWeek)
                                _observationHeaders.Add(observation);
                            else
                            {
                                _observationHeaders.Clear();
                                continue;
                            }
                        }
                        else
                            _observationHeaders.Add(observation);
                    }
                    else
                    {
                        _observationHeaders.Clear();
                        continue;
                    }

                    if(_observationHeaders[0].Total == _observationHeaders.Count)
                    {
                        List<SatteliteData1002> satteliteDataSet = new List<SatteliteData1002>();
                        foreach(ObservationHeader observationHeader in _observationHeaders)
                        {
                            foreach (Observation observationItem in observationHeader.Observations)
                            {
                                uint satID = (uint)observationItem.PRN;
                                if (satID > 200)
                                    satID -= 200;
                                bool l1CodeInd = false;
                                uint l1PseudoModulusAmbiguity = (uint)(observationItem.P / PRUNIT_GPS); ;
                                uint l1Pseudo = (uint)Math.Round((observationItem.P - (double)l1PseudoModulusAmbiguity * PRUNIT_GPS) / 0.02);
                                double prc = l1Pseudo * 0.02 + (double)l1PseudoModulusAmbiguity * PRUNIT_GPS;
                                double cp_pr = observationItem.L - prc / (CLIGHT / FREQ1);

                                if (Math.Abs(cp_pr) > 1000)
                                {
                                    //nm->lock_time = 0;
                                    //nm->carrier_phase -= (s32)cp_pr;
                                    cp_pr -= (int)cp_pr;
                                }
                                                                
                                int l1PhaseMinusPseudo = (int)Math.Round(cp_pr * (CLIGHT / FREQ1) / 0.0005);;
                                uint l1LockTimeInd = 0;
                                uint l1CNR = 0;
                                satteliteDataSet.Add(new SatteliteData1002(satID, l1CodeInd, l1Pseudo, l1PhaseMinusPseudo, l1LockTimeInd, l1PseudoModulusAmbiguity, l1CNR));
                            }
                        }
                        Message_1002 message = new Message_1002(satteliteDataSet.ToArray(),
                            13,
                            (uint)(_observationHeaders[0].TimeOfWeek * 1e3),
                            true,
                            (uint)satteliteDataSet.Count,
                            false,
                            0);

                        byte[] messageBytes = new byte[message.ByteLength];
                        message.GetBytes(ref messageBytes);
                        byte[] completeMessage = HeaderUtil.AddRTCMHeader(messageBytes);
                        return completeMessage;
                    }


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
