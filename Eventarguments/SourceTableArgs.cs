using System;

namespace NTRIP.Eventarguments
{
    public class SourceTableArgs
    {
        private MountPoint[] _mountPoints;

        public SourceTableArgs(MountPoint[] mountPoints)
        {
            _mountPoints = mountPoints;
        }

        public MountPoint[] MountPoints
        {
            get { return _mountPoints; }
        }
    }
}
