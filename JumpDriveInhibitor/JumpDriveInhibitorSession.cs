using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Utils;

namespace JumpDriveInhibitor
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class JumpDriveInhibitorSession : MySessionComponentBase
    {

        public override void BeforeStart()
        {
            NetworkService.NetworkInit();
        }

        protected override void UnloadData()
        {
            NetworkService.NetworkEnd();
        }
    }
}