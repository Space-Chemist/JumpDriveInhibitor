using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Lights;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.Entity;
using VRage.Utils;
using IMyBeacon = Sandbox.ModAPI.Ingame.IMyBeacon;
using IMyJumpDrive = Sandbox.ModAPI.Ingame.IMyJumpDrive;

namespace JumpDriveInhibitor
{
    [MySessionComponentDescriptor(VRage.Game.Components.MyUpdateOrder.BeforeSimulation)]
    public class Animator : MySessionComponentBase
    {
        MyParticleEffect effect;
        MyEntitySubpart subpart;
        private readonly Dictionary<long, IMyCubeGrid> _grids = new Dictionary<long, IMyCubeGrid>();
        private const string SUBPART_NAME = "Crystal"; // dummy name without the "subpart_" prefix
        private const float DEGREES_PER_TICK = 1.5f; // rotation per tick in degrees (60 ticks per second)
        private const float ACCELERATE_PERCENT_PER_TICK = 0.05f; // aceleration percent of "DEGREES_PER_TICK" per tick.
        private const float DEACCELERATE_PERCENT_PER_TICK = 0.01f; // deaccleration percent of "DEGREES_PER_TICK" per tick.
        private readonly Vector3 ROTATION_AXIS = Vector3.Up; // rotation axis for the subpart, you can do new Vector3(0.0f, 0.0f, 0.0f) for custom values
        private const float MAX_DISTANCE_SQ = 1000 * 1000; // player camera must be under this distance (squared) to see the subpart spinning
        private bool subpartFirstFind = true;
        private List<BeaconStorage> store;
        private Matrix subpartLocalMatrix; // keeping the matrix here because subparts are being re-created on paint, resetting their orientations
        private float targetSpeedMultiplier; // used for smooth transition

        
        
        

        public override void LoadData()
        {
            MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;

            _grids.Clear();
        }

        private void EntityAdded(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;

            if (grid != null)
            {
                _grids.Add(grid.EntityId, grid);
                grid.OnMarkForClose += GridMarkedForClose;
                grid.OnBlockAdded += addToStore;
                grid.OnBlockRemoved += removeFromStore;
            }
        }

        private void GridMarkedForClose(IMyEntity ent)
        {
            _grids.Remove(ent.EntityId);
        }


        private void removeFromStore(IMySlimBlock block)
        {
            if (!block.BlockDefinition.Id.SubtypeName.Equals("JumpInhibitor")) return;
            foreach (var beaconStorage in store)
            {
                if (beaconStorage.Beacon.EntityId == (block as IMyBeacon).EntityId)
                {
                    store.Remove(beaconStorage);
                }  
            }
        }
        
        private void addToStore(IMySlimBlock block)
        {
            if (!block.BlockDefinition.Id.SubtypeName.Equals("JumpInhibitor")) return;
            var bs = new BeaconStorage()
            {
                Beacon = (block as IMyBeacon),
                Effect = effect,
                RingRotation = 0
            };
            store.Add(bs);
        }
        
        public override void UpdateBeforeSimulation()
        {
            try
            {
                
                foreach (var beaconStorage in store)
                {
                    
                    var entity = beaconStorage.Beacon as MyEntity;

                    bool shouldSpin = beaconStorage.Beacon.IsWorking; // if block is functional and enabled and powered.

                    if (beaconStorage.Beacon == null || !beaconStorage.Beacon.Enabled || !beaconStorage.Beacon.IsWorking ||
                        !beaconStorage.Beacon.IsFunctional)
                    {
                        if (entity.TryGetSubpart(SUBPART_NAME, out subpart))
                        {
                            subpart.SetEmissiveParts("EmissiveSpotlight", Color.DarkRed, 1.0f);
                            effect?.Stop();
                        }    
                    }    
                    
                    switch (shouldSpin)
                    {
                        case false when Math.Abs(targetSpeedMultiplier) < 0.00001f:
                            return;
                        case true when targetSpeedMultiplier < 1:
                            targetSpeedMultiplier = Math.Min(targetSpeedMultiplier + ACCELERATE_PERCENT_PER_TICK, 1);
                            break;
                        case false when targetSpeedMultiplier > 0:
                            targetSpeedMultiplier = Math.Max(targetSpeedMultiplier - DEACCELERATE_PERCENT_PER_TICK, 0);
                            break;
                    }


                    var camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation; // local machine camera position

                    if(Vector3D.DistanceSquared(camPos, beaconStorage.Beacon.GetPosition()) > MAX_DISTANCE_SQ)
                        return;
                    
                    
                    if(entity.TryGetSubpart(SUBPART_NAME, out subpart)) // subpart does not exist when block is in build stage
                    {
                        //MyAPIGateway.Utilities.ShowNotification(subpart.ToString());
                        if(subpartFirstFind) // first time the subpart was found
                        {
                            subpartFirstFind = false;
                            subpartLocalMatrix = subpart.PositionComp.LocalMatrix;
                        }

                        if(targetSpeedMultiplier > 0)
                        {
                            subpartLocalMatrix *= Matrix.CreateFromAxisAngle(ROTATION_AXIS, MathHelper.ToRadians(targetSpeedMultiplier * DEGREES_PER_TICK));
                            subpartLocalMatrix = Matrix.Normalize(subpartLocalMatrix); // normalize to avoid any rotation inaccuracies over time resulting in weird scaling
                        }

                        subpart.PositionComp.LocalMatrix = subpartLocalMatrix;
                    }
                    //entity.SetEmissiveParts("Emissive", Color.White, 0f);
                    //entity.SetEmissiveParts("EmissiveSpotlight", Color.DarkGreen, 1.0f);
                    subpart.SetEmissiveParts("EmissiveSpotlight", Color.LimeGreen, beaconStorage.Beacon.Radius/6000);
                    
                    if (beaconStorage.Effect == null)
                    {
                        var e = beaconStorage.Effect;
                        MyParticlesManager.TryCreateParticleEffect( "ExhaustElectricSmall" , subpartLocalMatrix,  out e);
                        e.WorldMatrix = beaconStorage.Beacon.WorldMatrix;
                    }
                    
                    beaconStorage.Effect.UserScale = beaconStorage.Beacon.Radius/6000;
                    beaconStorage.Effect.UserEmitterScale = beaconStorage.Beacon.Radius/6000;
                    beaconStorage.Effect.Play();
                    
                    MyEntitySubpart subpart2;
                    
                    if (subpart.TryGetSubpart("Ring", out subpart2))
                    {
                        beaconStorage.RingRotation -= 1;
                        var rotationMatrix = Matrix.CreateRotationY(( beaconStorage.Beacon.Radius/6000) * beaconStorage.RingRotation);
                        subpart2.PositionComp.SetLocalMatrix(ref rotationMatrix, null, true);
                    }
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification(
                        $"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000,
                        MyFontEnum.Red);
            }

        }
    }

    public class Entity
    {
    }
}