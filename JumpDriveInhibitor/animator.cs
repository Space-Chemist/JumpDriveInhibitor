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
        private List<IMyBeacon> beaconlist = new List<IMyBeacon>();
        private const string SUBPART_NAME = "Crystal"; // dummy name without the "subpart_" prefix
        private const float DEGREES_PER_TICK = 1.5f; // rotation per tick in degrees (60 ticks per second)
        private const float ACCELERATE_PERCENT_PER_TICK = 0.05f; // aceleration percent of "DEGREES_PER_TICK" per tick.
        private const float DEACCELERATE_PERCENT_PER_TICK = 0.01f; // deaccleration percent of "DEGREES_PER_TICK" per tick.
        private readonly Vector3 ROTATION_AXIS = Vector3.Up; // rotation axis for the subpart, you can do new Vector3(0.0f, 0.0f, 0.0f) for custom values
        private const float MAX_DISTANCE_SQ = 1000 * 1000; // player camera must be under this distance (squared) to see the subpart spinning
        private int RotationTime { get; set; }
        private bool once { get; set; } = false;
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
                grid.OnBlockIntegrityChanged += Fuckeveryone;
            }
        }

        private void GridMarkedForClose(IMyEntity ent)
        {
            _grids.Remove(ent.EntityId);
        }


        private void removeFromStore(IMySlimBlock block)
        {
            if (block.BlockDefinition.Id.SubtypeName.Equals("JumpInhibitor"))
            {
                store.Remove();
            }
        }
        
        private void addToStore(IMySlimBlock block)
        {
            if (block.BlockDefinition.Id.SubtypeName.Equals("JumpInhibitor"))
            {
                store.Add();
            }
        }
        
        private void Fuckeveryone(IMySlimBlock fuck)
        {
            var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(fuck.CubeGrid);
            gts.GetBlocksOfType(beaconlist,
                block => { return block.BlockDefinition.SubtypeName.Equals("JumpInhibitor"); });
            //MyAPIGateway.Utilities.ShowNotification("let me die");
            //MyAPIGateway.Utilities.ShowNotification(beaconlist.Count.ToString());
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                
                foreach (var _beacon in beaconlist)
                {
                    
                    var entity = _beacon as MyEntity;

                    bool shouldSpin = _beacon.IsWorking; // if block is functional and enabled and powered.

                    if (_beacon == null || !_beacon.Enabled || !_beacon.IsWorking ||
                        !_beacon.IsFunctional)
                    {
                        if (entity.TryGetSubpart(SUBPART_NAME, out subpart))
                        {
                            subpart.SetEmissiveParts("EmissiveSpotlight", Color.DarkRed, 1.0f);
                            effect.Stop();
                            once = false;
                        }    
                    }    
                    
                    if (!shouldSpin && Math.Abs(targetSpeedMultiplier) < 0.00001f)
                    {
                        return;
                    }    
                        

                    if(shouldSpin && targetSpeedMultiplier < 1)
                    {
                        targetSpeedMultiplier = Math.Min(targetSpeedMultiplier + ACCELERATE_PERCENT_PER_TICK, 1);
                    }
                    else if(!shouldSpin && targetSpeedMultiplier > 0)
                    {
                        targetSpeedMultiplier = Math.Max(targetSpeedMultiplier - DEACCELERATE_PERCENT_PER_TICK, 0);
                    }

                    var camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation; // local machine camera position

                    if(Vector3D.DistanceSquared(camPos, _beacon.GetPosition()) > MAX_DISTANCE_SQ)
                        return;
                    
                    
                    
                    
                 
                    //MyAPIGateway.Utilities.ShowNotification(bp.ToString(), 5000);
                    

                    
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
                    subpart.SetEmissiveParts("EmissiveSpotlight", Color.LimeGreen, _beacon.Radius/6000);
                    
                    if (!once)
                    {
                        MyParticlesManager.TryCreateParticleEffect( "ExhaustElectricSmall" , subpartLocalMatrix,  out effect);
                        
                        effect.WorldMatrix = _beacon.WorldMatrix;
                        effect.Play();
                        once = true;
                    }
                    effect.UserScale = _beacon.Radius/6000;
                    effect.UserEmitterScale = _beacon.Radius/6000;
                    MyEntitySubpart subpart2;
                    
                    if (subpart.TryGetSubpart("Ring", out subpart2))
                    {
                        RotationTime += 1;
                        var rotationMatrix = Matrix.CreateRotationY((_beacon.Radius/6000) * RotationTime);
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