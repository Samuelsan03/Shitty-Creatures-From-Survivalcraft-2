using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class RemoteControlBlock : ShittyTexturesFlat
	{
		public RemoteControlBlock() : base("Textures/Items/control remoto")
		{
			this.BlockIndex = 400;
		}

		public static int Index { get; internal set; }
	}
}
