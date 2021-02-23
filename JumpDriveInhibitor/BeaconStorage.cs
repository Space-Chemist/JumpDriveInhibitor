using Sandbox.ModAPI;
using VRage.Game;

namespace JumpDriveInhibitor
{
    public class BeaconStorage
    {
        public IMyBeacon Beacon { get; set; }
        
        public MyParticleEffect Effect { get; set; }
        
        public int RingRotation { get; set; }
    }
}