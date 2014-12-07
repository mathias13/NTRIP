using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRIP
{
    public class Constants
    {
        public const string ICY200OK = "ICY 200 OK";

        public const string SOURCE200OK = "SOURCETABLE 200 OK";

        public const string ENDSOURCETABLE = "ENDSOURCETABLE";

        public const string BAD_PASS = "ERROR - Bad Password";

        public const string UNAUTHORIZED = "HTTP/1.0 401 Unauthorized";

        public const string CRLF = "\r\n";
    }
}
