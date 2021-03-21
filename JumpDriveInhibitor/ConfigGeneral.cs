using System;
using System.Xml.Serialization;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Utils;

namespace JumpDriveInhibitor
{
	[ProtoContract]
	[Serializable]
    public class ConfigGeneral
    {
		[ProtoMember(1)]
        public float MaxRadius { get; set; }

        [ProtoMember(2)]
        public float MaxPowerDrain { get; set; }

        /// <summary>
        /// Make a copy of all values within <see cref="ConfigGeneral"/>.
        /// </summary>
        internal ConfigGeneral Clone()
        {
            return new ConfigGeneral
            {
                MaxRadius = MaxRadius,
                MaxPowerDrain = MaxPowerDrain
            };
        }
	}
}