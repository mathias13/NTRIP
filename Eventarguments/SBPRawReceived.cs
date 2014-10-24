using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRIP.Eventarguments
{
    public class SBPRawReceived : EventArgs
    {
        private byte[] _message;

        public SBPRawReceived(byte[] message)
        {
            _message = message;
        }

        public byte[] Message
        {
            get { return _message; }
        }
    }
}
