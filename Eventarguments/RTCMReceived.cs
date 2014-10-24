using System;
using RTCM.MESSAGES;

namespace NTRIP.Eventarguments
{
    public class RTCMReceived : EventArgs
    {
        private MessageEnum.Messages _msgType;

        private object _message;

        public RTCMReceived(MessageEnum.Messages msgType, object message)
        {
            _msgType = msgType;
            _message = message;
        }

        public MessageEnum.Messages MsgType
        {
            get { return _msgType; }
        }

        public object Message
        {
            get { return _message; }
        }
    }
}
