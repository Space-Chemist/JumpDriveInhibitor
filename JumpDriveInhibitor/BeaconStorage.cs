using Sandbox.ModAPI;
using VRage.Game;
using IMyBeacon = Sandbox.ModAPI.Ingame.IMyBeacon;
namespace JumpDriveInhibitor
{
    
    public class BeaconStorage
    {
       
        public IMyBeacon Beacon { get; set; }
        
        public MyParticleEffect Effect { get; set; }
        
        public int RingRotation { get; set; }
        
        public bool Once { get; set; }
    }
}