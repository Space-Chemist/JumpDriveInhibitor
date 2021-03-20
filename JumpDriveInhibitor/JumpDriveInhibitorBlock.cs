using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Utils;
using IMyBeacon = Sandbox.ModAPI.Ingame.IMyBeacon;
using IMyJumpDrive = Sandbox.ModAPI.Ingame.IMyJumpDrive;

namespace JumpDriveInhibitor
{
    [MyEntityComponentDescriptor(typeof(Sandbox.Common.ObjectBuilders.MyObjectBuilder_Beacon), false, new string[] { "JumpInhibitor", "JumpInhibitorSmall" })]
    public class JumpDriveInhibitorBlock : MyGameLogicComponent
    {
        private VRage.ObjectBuilders.MyObjectBuilder_EntityBase _objectBuilder;
        private IMyBeacon _beacon;
        private IMyEntity _entity;
        private bool _logicEnabled;
        MyParticleEffect effect;
        MyEntitySubpart subpart;
        private const string SUBPART_NAME = "Crystal"; // dummy name without the "subpart_" prefix
        private const float DEGREES_PER_TICK = 1.5f; // rotation per tick in degrees (60 ticks per second)
        private const float ACCELERATE_PERCENT_PER_TICK = 0.05f; // aceleration percent of "DEGREES_PER_TICK" per tick.
        private const float DEACCELERATE_PERCENT_PER_TICK = 0.01f; // deaccleration percent of "DEGREES_PER_TICK" per tick.
        private readonly Vector3 ROTATION_AXIS = Vector3.Up; // rotation axis for the subpart, you can do new Vector3(0.0f, 0.0f, 0.0f) for custom values
        private const float MAX_DISTANCE_SQ = 1000 * 1000; // player camera must be under this distance (squared) to see the subpart spinning
        private bool subpartFirstFind = true;
        private Matrix subpartLocalMatrix; // keeping the matrix here because subparts are being re-created on paint, resetting their orientations
        private float targetSpeedMultiplier; // used for smooth transition
        private List<BeaconStorage> store;
        
        public override void Init(VRage.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder)
        {
            
            _objectBuilder = objectBuilder;
            _beacon = (Entity as IMyBeacon);
           

            if (_beacon != null && _beacon.BlockDefinition.SubtypeId.Equals("JumpInhibitor"))
            {
                _logicEnabled = true;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            }
            

            if (_beacon != null && _beacon.BlockDefinition.SubtypeId.Equals("JumpInhibitorSmall"))
            {
                _logicEnabled = true;
                Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            }
            
            
            if (_beacon != null && _beacon.BlockDefinition.SubtypeId.Equals("JumpInhibitor"))
            {
                store = new List<BeaconStorage>(); 
                
                var bs = new BeaconStorage()
                {
                    Beacon = _beacon,
                    Effect = effect,
                    RingRotation = 0,
                    Once = false
                };
                store.Add(bs);
            }
            
            Settings.LoadSettings();
            
            var b = _beacon as MyCubeBlock;
            if (b != null)
            {
                var def = b.BlockDefinition as MyBeaconDefinition;
                MyDefinitionId id = new MyDefinitionId(typeof(MyObjectBuilder_FlareDefinition), def.Flare);
                MyFlareDefinition flareDefinition = MyDefinitionManager.Static.GetDefinition(id) as MyFlareDefinition;
                
                flareDefinition.Intensity = 0;
                
                def.MaxBroadcastRadius = Settings.General.MaxRadius;
                def.MaxBroadcastPowerDrainkW = Settings.General.MaxPowerDrainInKw;
                MyLog.Default.WriteLine("12345");
            }
            MyAPIGateway.Entities.OnEntityRemove += Removal;
        }

        private void Removal(IMyEntity obj)
        {
            IMyCubeBlock block = obj as IMyCubeBlock;
            if (block != null)
            {
                if (block.BlockDefinition.SubtypeId.Equals("JumpInhibitor"))
                {
                    foreach (var b in store)
                    {
                        if (b.Beacon.EntityId == obj.EntityId)
                        {
                            store = new List<BeaconStorage>();
                            if (b.Effect != null)
                            {
                                b.Effect.Stop();
                            }    
                            
                            store.Remove(b);
                        }
                    }
                }    
            }
        }
        
        
        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();

               try
               {
                   foreach (var beaconStorage in store)
                   {
                       var entity = beaconStorage.Beacon as MyEntity;
                       bool shouldSpin = beaconStorage.Beacon.IsWorking; // if block is functional and enabled and powered.
                       
                       if (beaconStorage.Beacon == null || !beaconStorage.Beacon.Enabled || !beaconStorage.Beacon.IsWorking || !beaconStorage.Beacon.IsFunctional)
                       {
                           if (entity != null && entity.TryGetSubpart(SUBPART_NAME, out subpart))
                           {
                               subpart.SetEmissiveParts("EmissiveSpotlight", Color.DarkRed, 1.0f);
                               if (beaconStorage.Effect != null)
                               {
                                   beaconStorage.Effect.Stop();
                               }
                               beaconStorage.Once = false;
                               return;
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

                       if(Vector3D.DistanceSquared(camPos, beaconStorage.Beacon.GetPosition()) > MAX_DISTANCE_SQ)
                           return;
                       
                       if(entity !=null && entity.TryGetSubpart(SUBPART_NAME, out subpart)) // subpart does not exist when block is in build stage
                       {
                           
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
                       
                       if (subpart != null)
                       {
                           subpart.SetEmissiveParts("EmissiveSpotlight", Color.LimeGreen, beaconStorage.Beacon.Radius/6000);
                       }

                       if (!beaconStorage.Once)
                       {
                           MyParticleEffect e;
                           MyParticlesManager.TryCreateParticleEffect( "ExhaustElectricSmall" , subpartLocalMatrix,  out e);
                           e.WorldMatrix = beaconStorage.Beacon.WorldMatrix;
                           beaconStorage.Effect = e;
                           beaconStorage.Once = true;
                       }

                       beaconStorage.Effect.UserScale = beaconStorage.Beacon.Radius/600;
                       beaconStorage.Effect.UserEmitterScale = beaconStorage.Beacon.Radius/600;
                       beaconStorage.Effect.Play();

                       MyEntitySubpart subpart2;
                       if (subpart != null && subpart.TryGetSubpart("Ring", out subpart2))
                       {
                           beaconStorage.RingRotation -= 1;
                           var rotationMatrix = Matrix.CreateRotationY(( beaconStorage.Beacon.Radius/600) * beaconStorage.RingRotation);
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


        public override void UpdateAfterSimulation10()
        {
            base.UpdateAfterSimulation10();
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
                       IMyCubeBlock block = e as IMyCubeBlock;
                       
                       
                       
                       if (block != null && block.SlimBlock.FatBlock != null &&
                           (block.SlimBlock.FatBlock as IMyJumpDrive) != null && block.SlimBlock.FatBlock.IsWorking &&
                           block.SlimBlock.FatBlock.IsFunctional &&
                           block.SlimBlock.FatBlock.BlockDefinition.ToString().Contains("MyObjectBuilder_JumpDrive"))
                       {
                           if (!((IMyJumpDrive) block.SlimBlock.FatBlock).Enabled) continue;
                           var damage = block.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 0.5f : 0.05f;
                           block.SlimBlock.DecreaseMountLevel(damage, null, true);
                           block.SlimBlock.ApplyAccumulatedDamage();
                           ((IMyJumpDrive) block.SlimBlock.FatBlock).Enabled = false;   
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