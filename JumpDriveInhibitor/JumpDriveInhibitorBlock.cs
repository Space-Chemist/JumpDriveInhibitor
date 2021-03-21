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
using VRage.Utils;
using IMyBeacon = Sandbox.ModAPI.Ingame.IMyBeacon;
using IMyJumpDrive = Sandbox.ModAPI.Ingame.IMyJumpDrive;
using IMySlimBlock = VRage.Game.ModAPI.Ingame.IMySlimBlock;

namespace JumpDriveInhibitor
{
    [MyEntityComponentDescriptor(typeof(Sandbox.Common.ObjectBuilders.MyObjectBuilder_Beacon), false, new string[] { "JumpInhibitor", "JumpInhibitorSmall" })]
    public class JumpDriveInhibitorBlock : MyGameLogicComponent
    {
        #region variables and constants
        private const string ConfigSpeedPattern = @"^(?<command>/configspeed)(?:\s+(?<config>((ResetAll)|(LargeShipMaxSpeed)|(LargeShipSpeed)|(LargeShip)|(Large)|(SmallShipMaxSpeed)|(SmallShipSpeed)|(SmallShip)|(Small)|(ThrustRatio)|(EnableThrustRatio)|(LockThrustRatio)|(MaxAllSpeed)|(MissileMinSpeed)|(MissileMin)|(MissileMaxSpeed)|(MissileMax)|(autopilotspeed)|(autopilotlimit)|(autopilot)|(remoteautopilotlimit)|(remoteautopilotspeed)|(remoteautopilot)|(remotecontrolmaxspeed)|(containerdropdeployheight)|(containerdeployheight)|(dropdeployheight)|(dropheight)|(respawnshipdeployheight)|(respawndeployheight)|(respawnheight)))(?:\s+(?<value>.+))?)?";

        private const string ShortSpeedPattern = @"^(?<command>(/maxspeed))(?:\s+(?<value>.+))";
        
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
        private bool _isInitialized;
        private bool _isClientRegistered;
        private bool _isServerRegistered;
        private readonly Action<byte[]> _messageHandler = new Action<byte[]>(HandleMessage);
        public static JumpDriveInhibitorBlock Instance;
        
        public ConfigGeneral DefaultDefinitionValues;
        /// <summary>
        /// The current values that are stored and read into the game.
        /// </summary>
        public ConfigGeneral ConfigGeneralComponent;

        /// <summary>
        /// The previous values before we start changing them.
        /// </summary>
        public ConfigGeneral OldConfigGeneral;
        
        /// <summary>
        /// Indicates the stage of the settings if we have changed any.
        /// </summary>
        public bool IsModified;
        #endregion
        
        
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

        private void Setup()
        {
            try
            {
                var b = base.Entity as MyCubeBlock;
                // This Variables are already loaded by this point, but unaccessible because we need Utilities.

                // Need to create the Utilities, as it isn't yet created by the game at this point.
                //MyModAPIHelper.OnSessionLoaded();
                if (b != null)
                {
                    var def = b.BlockDefinition as MyBeaconDefinition;
                    if (def != null)
                    {
                        if (MyAPIGateway.Utilities == null)
                         MyAPIGateway.Utilities = MyAPIUtilities.Static;
                        //    MyAPIGateway.Utilities = new MyAPIUtilities();

                        DefaultDefinitionValues = new ConfigGeneral
                        {
                           MaxRadius = def.MaxBroadcastRadius,
                           MaxPowerDrain = def.MaxBroadcastPowerDrainkW,
                        };

                        // Load the speed on both server and client.
                        string xmlValue;
                        if (MyAPIGateway.Utilities.GetVariable("ConfigGeneral", out xmlValue))
                        {
                           ConfigGeneralComponent = MyAPIGateway.Utilities.SerializeFromXML<ConfigGeneral>(xmlValue);
                           if (ConfigGeneralComponent != null)
                           {
                               // Apply settings.
                               if (ConfigGeneralComponent.MaxRadius > 0)
                                   def.MaxBroadcastRadius = (float)ConfigGeneralComponent.MaxRadius;
                               if (ConfigGeneralComponent.MaxPowerDrain > 0)
                                   def.MaxBroadcastPowerDrainkW = (float)ConfigGeneralComponent.MaxPowerDrain;

                               OldConfigGeneral = ConfigGeneralComponent.Clone();
                               return;
                           }
                        }

                        // creates a new EnvironmentComponent if one was not found in the game Variables.
                        ConfigGeneralComponent = new ConfigGeneral
                        {
                           MaxRadius = def.MaxBroadcastRadius,
                           MaxPowerDrain = def.MaxBroadcastPowerDrainkW
                        };
                        OldConfigGeneral = ConfigGeneralComponent.Clone();
                    }
                }
            }
            catch (Exception ex)
            {
                VRage.Utils.MyLog.Default.WriteLine("configuration error in jump inhibitor " + ex.Message);

                // The Loggers doesn't actually exist yet, as Init is called before UpdateBeforeSimulation.
                // TODO: should rework the code to change this.
                //ClientLogger.WriteException(ex);
                //ServerLogger.WriteException(ex);
            }
        }
        
        private void InitClient()
        {
            _isInitialized = true; // Set this first to block any other calls from UpdateAfterSimulation().
            _isClientRegistered = true;

            MyAPIGateway.Utilities.MessageEntered += GotMessage;

            if (MyAPIGateway.Multiplayer.MultiplayerActive && !_isServerRegistered) // if not the server, also need to register the messagehandler.
            {
                
                MyAPIGateway.Multiplayer.RegisterMessageHandler(SpeedConsts.ConnectionId, _messageHandler);
            }
        }

        private void InitServer()
        {
            _isInitialized = true; // Set this first to block any other calls from UpdateAfterSimulation().
            _isServerRegistered = true;
            MyAPIGateway.Multiplayer.RegisterMessageHandler(SpeedConsts.ConnectionId, _messageHandler);
            //MyAPIGateway.Entities.OnEntityAdd += Entities_OnEntityAdd;
        }
        
        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();

               try
               {
                   if (_start)
                   {
                       Setup();
                       _start = false;
                   }
                   
                   //VRage.Utils.MyLog.Default.WriteLine("##Mod## ConfigurableSpeed UpdateBeforeSimulation");
                   if (Instance == null)
                       Instance = this;

                   // This needs to wait until the MyAPIGateway.Session.Player is created, as running on a Dedicated server can cause issues.
                   // It would be nicer to just read a property that indicates this is a dedicated server, and simply return.
                   if (!_isInitialized && MyAPIGateway.Session != null && MyAPIGateway.Session.Player != null)
                   {
                       if (MyAPIGateway.Session.OnlineMode.Equals(MyOnlineModeEnum.OFFLINE)) // pretend single player instance is also server.
                           InitServer();
                       if (!MyAPIGateway.Session.OnlineMode.Equals(MyOnlineModeEnum.OFFLINE) && MyAPIGateway.Multiplayer.IsServer && !MyAPIGateway.Utilities.IsDedicated)
                           InitServer();
                       InitClient();
                   }

                   // Dedicated Server.
                   if (!_isInitialized && MyAPIGateway.Utilities != null && MyAPIGateway.Multiplayer != null
                       && MyAPIGateway.Session != null && MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer.IsServer)
                   {
                       InitServer();
                       return;
                   }

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

                       if (!beaconStorage.Once)
                       {
                           MyParticleEffect e;
                           MyParticlesManager.TryCreateParticleEffect( "ExhaustElectricSmall" , subpartLocalMatrix,  out e);
                           e.WorldMatrix = beaconStorage.Beacon.WorldMatrix;
                           beaconStorage.Effect = e;
                           beaconStorage.Once = true;
                           beaconStorage.Effect.UserScale = beaconStorage.Beacon.Radius/600;
                           beaconStorage.Effect.UserEmitterScale = beaconStorage.Beacon.Radius/600;
                           beaconStorage.Effect.Play();
                       }

                       if (beaconStorage.Effect != null)
                       {
                           beaconStorage.Effect.UserScale = beaconStorage.Beacon.Radius/600;
                           beaconStorage.Effect.UserEmitterScale = beaconStorage.Beacon.Radius/600;
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
        
        protected override void UnloadData()
        {
            if (_isClientRegistered)
            {
                if (MyAPIGateway.Utilities != null)
                {
                    MyAPIGateway.Utilities.MessageEntered -= GotMessage;
                }

                if (!_isServerRegistered) // if not the server, also need to unregister the messagehandler.
                {
                    
                    MyAPIGateway.Multiplayer.UnregisterMessageHandler(SpeedConsts.ConnectionId, _messageHandler);
                }

                
            }

            if (_isServerRegistered)
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(SpeedConsts.ConnectionId, _messageHandler);
                //MyAPIGateway.Entities.OnEntityAdd -= Entities_OnEntityAdd;
            }

            base.UnloadData();
        }

        public override void SaveData()
        {
            if (_isServerRegistered)
            {
                // Only save the speed back to the server duruing world save.
                var xmlValue = MyAPIGateway.Utilities.SerializeToXML(ConfigGeneralComponent);
                MyAPIGateway.Utilities.SetVariable("MidspaceEnvironmentComponent", xmlValue);
            }

            base.SaveData();
        }
        
        private static void HandleMessage(byte[] message)
        {
            ConnectionHelper.ProcessData(message);
        }

        private void GotMessage(string messageText, ref bool sendToOthers)
        {
            try
            {
                // here is where we nail the echo back on commands "return" also exits us from processMessage
                if (ProcessMessage(messageText)) { sendToOthers = false; }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($"Error in Jump Inhibitor {ex}");
            }
        }
        
        private bool ProcessMessage(string messageText)
        {
            #region configspeed

            if (MyAPIGateway.Session.Player.PromoteLevel == MyPromoteLevel.Admin)
            {
                Match match = Regex.Match(messageText, ConfigSpeedPattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    MessageConfig.SendMessage(match.Groups["config"].Value, match.Groups["value"].Value);
                    return true;
                }

                match = Regex.Match(messageText, ShortSpeedPattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    MessageConfig.SendMessage("MaxAllSpeed", match.Groups["value"].Value);
                    return true;
                }
            }

            #endregion configspeed

            // it didnt start with help or anything else that matters so return false and get us out of here;
            return false;
        }

        public override void Close()
        {
            List<BeaconStorage> temp = store.ToList();
            MyVisualScriptLogicProvider.SendChatMessage( "got here ");
            MyVisualScriptLogicProvider.SendChatMessage(base.Entity.EntityId.ToString(), "42: ");
            foreach (var b in temp)
            {
                if (b.Beacon.EntityId == base.Entity.EntityId)
                {
                    MyVisualScriptLogicProvider.SendChatMessage( "block = beacon ");
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