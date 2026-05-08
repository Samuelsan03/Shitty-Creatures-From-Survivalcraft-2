using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class OrangeLeavesBlock : ShittyLeavesBlock
	{
		public OrangeLeavesBlock() : base()
		{
		}

		public static int Index = 419;

		public override int GetFaceTextureSlot(int face, int value)
		{
			return 8; // Ajusta según tu atlas de texturas
		}

		public override Color GetLeavesBlockColor(int value, Terrain terrain, int x, int y, int z)
		{
			return Color.White;
		}

		public override Color GetLeavesItemColor(int value, DrawBlockEnvironmentData environmentData)
		{
			return Color.White;
		}
	}
}