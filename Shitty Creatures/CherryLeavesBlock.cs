using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class CherryLeavesBlock : ShittyLeavesBlock
	{
		public CherryLeavesBlock() : base()
		{
		}

		public static int Index = 438;

		public override int GetFaceTextureSlot(int face, int value)
		{
			return 11; // Ajusta según tu atlas de texturas
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