using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using IMyBeacon = Sandbox.ModAPI.Ingame.IMyBeacon;
using IMyJumpDrive = Sandbox.ModAPI.Ingame.IMyJumpDrive;

namespace JumpDriveInhibitor
{
    [MyEntityComponentDescriptor(typeof(Sandbox.Common.ObjectBuilders.MyObjectBuilder_Beacon), true, "Beacon", "JumpInhibitor", "JumpInhibitorSmall")]
    public class JumpDriveInhibitorBlock : MyGameLogicComponent
    {
        private VRage.ObjectBuilders.MyObjectBuilder_EntityBase _objectBuilder;
        private IMyBeacon _beacon;
        private IMyEntity _entity;
        private bool _logicEnabled;
        
        public override void Init(VRage.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder)
        {
            _objectBuilder = objectBuilder;
            
            _beacon = (Entity as IMyBeacon);

            if (_beacon != null && _beacon.BlockDefinition.SubtypeId.Equals("JumpInhibitor"))
            {
                _logicEnabled = true;
                Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            }

            if (_beacon != null && _beacon.BlockDefinition.SubtypeId.Equals("JumpInhibitorSmall"))
            {
                _logicEnabled = true;
                Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            }

            var b = _beacon as MyCubeBlock;
            if (b ==null)
             return;   

            var def = b.BlockDefinition as MyBeaconDefinition;
            MyDefinitionId id = new MyDefinitionId(typeof(MyObjectBuilder_FlareDefinition), def.Flare);
            MyFlareDefinition flareDefinition = MyDefinitionManager.Static.GetDefinition(id) as MyFlareDefinition;
            
            flareDefinition.Intensity = 0;

        }

        public override void UpdateAfterSimulation10()
        {
            try
            {
                if (!_logicEnabled || _beacon == null || !_beacon.Enabled || !_beacon.IsWorking ||
                    !_beacon.IsFunctional) return;

                List<IMyEntity> l;

                var sphere = new BoundingSphereD(((IMyBeacon) Entity).GetPosition(),
                    ((IMyBeacon) Entity).Radius);
                l = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

                var parentGrid = _beacon.CubeGrid;
                if (_entity == null)
                    _entity = MyAPIGateway.Entities.GetEntityById(parentGrid.EntityId);

                if (parentGrid == null || (!parentGrid.IsStatic &&
                                           (_entity.Physics == null || _entity.Physics.LinearVelocity.Length() != 0)))
                    return;

                foreach (var e in l)
                {
                    var grid = (e as IMyCubeGrid);

                    if (grid == null) continue;
                    var blocks = new List<IMySlimBlock>();
                    grid.GetBlocks(blocks, b =>
                        b != null && b.FatBlock != null &&
                        (b.FatBlock as IMyJumpDrive) != null && b.FatBlock.IsWorking &&
                        b.FatBlock.IsFunctional &&
                        b.FatBlock.BlockDefinition.ToString().Contains("MyObjectBuilder_JumpDrive"));

                    foreach (var b in blocks)
                    {
                        if (!((IMyJumpDrive) b.FatBlock).Enabled) continue;
                        var damage = grid.GridSizeEnum == MyCubeSize.Large ? 0.5f : 0.05f;
                        b.DecreaseMountLevel(damage, null, true);
                        b.ApplyAccumulatedDamage();
                        ((IMyJumpDrive) b.FatBlock).Enabled = false;
                    }
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage("Jump-drive Inhibitor", $"An error happened in the mod, see an admin:CODE 42: {e.Message}");
            }
            
        }
        
        public override VRage.ObjectBuilders.MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return _objectBuilder;
        }
    }
}