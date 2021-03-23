using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JumpDriveInhibitor;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using VRage.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRageMath;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Entity;
using VRage.Library.Utils;
using VRage.Utils;
using IMyBeacon = Sandbox.ModAPI.Ingame.IMyBeacon;
using IMyJumpDrive = Sandbox.ModAPI.Ingame.IMyJumpDrive;
using IMySlimBlock = VRage.Game.ModAPI.Ingame.IMySlimBlock;

namespace JumpDriveInhibitor
{
    [MyEntityComponentDescriptor(typeof(Sandbox.Common.ObjectBuilders.MyObjectBuilder_Beacon), false, new string[] { "JumpInhibitor", "JumpInhibitorSmall" })]
    public class JumpDriveInhibitorBlock : MyGameLogicComponent
    {
        private VRage.ObjectBuilders.MyObjectBuilder_EntityBase _objectBuilder;
        private IMyBeacon _beacon;
        private IMyEntity _entity;
        private bool _logicEnabled;
        private bool _start = true;
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
        private List<BeaconStorage> store = new List<BeaconStorage>();
        public static JumpDriveInhibitorBlock Instance;
        public float size;

        public override void Init(VRage.ObjectBuilders.MyObjectBuilder_EntityBase objectBuilder)
        {
            
            _objectBuilder = objectBuilder;
            _beacon = (Entity as IMyBeacon);
            Instance = this;

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
                var bs = new BeaconStorage()
                {
                    Beacon = _beacon,
                    Effect = effect,
                    RingRotation = 0,
                    Once = false
                };
                store.Add(bs);
            }

            var b = _beacon as MyCubeBlock;
            if (b != null)
            {
                var def = b.BlockDefinition as MyBeaconDefinition;
                MyDefinitionId id = new MyDefinitionId(typeof(MyObjectBuilder_FlareDefinition), def.Flare);
                MyFlareDefinition flareDefinition = MyDefinitionManager.Static.GetDefinition(id) as MyFlareDefinition;
                
                flareDefinition.Intensity = 0;
            }

            
        }
        
        public void updateDef(string message)
        {
            var b = _beacon as MyCubeBlock;
            if (b != null)
            {
                var def = b.BlockDefinition as MyBeaconDefinition;
                var config = message.Split( '-' );
                var rad = config[0];
                var pow = config[1];
                def.MaxBroadcastRadius = Int32.Parse(rad);
                def.MaxBroadcastPowerDrainkW = Int32.Parse(pow);
            }
        }

        private void Setup()
        {
            Settings.LoadSettings();

            if (MyAPIGateway.Session.IsServer)
            {
                var b = _beacon as MyCubeBlock;
                if (b != null)
                {
                    var def = b.BlockDefinition as MyBeaconDefinition;
               
                    def.MaxBroadcastRadius = Settings.General.MaxRadius;
                    def.MaxBroadcastPowerDrainkW = Settings.General.MaxPowerDrain;
                }
            }    
            
        }
        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();

               try
               {
                   base.UpdateBeforeSimulation();
                   
                   List<BeaconStorage> temp = store.ToList();
                   foreach (var beaconStorage in temp)
                   {
                       var entity = beaconStorage.Beacon as MyEntity;
                       bool shouldSpin = beaconStorage.Beacon.IsWorking; // if block is functional and enabled and powered.

                       if (beaconStorage.Beacon.CubeGrid == null && beaconStorage.Effect != null)
                       {
                           beaconStorage.Effect.Stop();
                           store.Remove(beaconStorage);
                       }    
                       
                       if (beaconStorage.Beacon == null || !beaconStorage.Beacon.Enabled || !beaconStorage.Beacon.IsWorking || !beaconStorage.Beacon.IsFunctional)
                       {
                           if (entity != null && entity.TryGetSubpart(SUBPART_NAME, out subpart))
                           {
                               subpart.SetEmissiveParts("EmissiveSpotlight", Color.DarkRed, 1.0f);
                           }
                           
                           if (beaconStorage.Effect != null)
                           {
                               beaconStorage.Effect.Stop();
                           }
                           beaconStorage.Once = false;
                           return;
                           
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
                       
                       if (beaconStorage.Beacon.Radius < 600)
                       {
                           size = 0.75f;
                       }
                       else if (beaconStorage.Beacon.Radius > 600 && beaconStorage.Beacon.Radius < 3000)
                       {
                           size = beaconStorage.Beacon.Radius / 1000;
                       }
                       else
                       {
                           size = 3.0f;
                       }

                       if (!beaconStorage.Once)
                       {
                           MyParticleEffect e;
                           MyParticlesManager.TryCreateParticleEffect( "ExhaustElectricSmall" , subpartLocalMatrix,  out e);
                           e.WorldMatrix = beaconStorage.Beacon.WorldMatrix;
                           beaconStorage.Effect = e;
                           beaconStorage.Once = true;
                           beaconStorage.Effect.UserScale = size;
                           beaconStorage.Effect.UserEmitterScale = size;
                           beaconStorage.Effect.Play();
                       }
                       
                       
                       if (beaconStorage.Effect != null)
                       {
                           beaconStorage.Effect.UserScale = size;
                           beaconStorage.Effect.UserEmitterScale = size;
                           beaconStorage.Effect.WorldMatrix = beaconStorage.Beacon.WorldMatrix;
                           beaconStorage.Effect.Update();
                       }    

                       

                       MyEntitySubpart subpart2;
                       if (subpart != null && subpart.TryGetSubpart("Ring", out subpart2))
                       {
                           beaconStorage.RingRotation -= 1;
                           var rotationMatrix = Matrix.CreateRotationY(( beaconStorage.Beacon.Radius/600) * beaconStorage.RingRotation);
                           subpart2.PositionComp.SetLocalMatrix(ref rotationMatrix, null, true);
                       }
                      
                   }
               }
               catch (Exception ex)
               {
                   MyLog.Default.WriteLine($"Error in Jump Inhibitor {ex}");
               }
        }


        public override void UpdateAfterSimulation10()
        {
            base.UpdateAfterSimulation10();
               try
               {
                   Setup();
                   if (!_logicEnabled || _beacon == null || !_beacon.Enabled || !_beacon.IsWorking ||
                       !_beacon.IsFunctional) return;
                   
                   List<IMyEntity> l;

                   var sphere = new BoundingSphereD(((IMyBeacon) Entity).GetPosition(),
                       ((IMyBeacon) Entity).Radius);
                   l = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

                   var parentGrid = _beacon.CubeGrid;
                   if (_entity == null)
                       _entity = MyAPIGateway.Entities.GetEntityById(parentGrid.EntityId);

                   if (parentGrid == null)
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
               catch (Exception ex)
               {
                   MyLog.Default.WriteLine($"Error in Jump Inhibitor {ex}");
               }

        }
        public override void Close()
        {
            List<BeaconStorage> temp = store.ToList();
            foreach (var b in temp)
            {
                if (b.Beacon.EntityId == base.Entity.EntityId)
                {
                    if (b.Effect != null)
                    {
                        b.Effect.Stop();
                    }    
                            
                    store.Remove(b);
                }
            }
            base.Close();

        }

        public override VRage.ObjectBuilders.MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return _objectBuilder;
        }
    }
}