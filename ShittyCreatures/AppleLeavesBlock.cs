using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	/// <summary>
	/// Bloque de hojas de manzano utilizando la textura personalizada.
	/// </summary>
	public class AppleLeavesBlock : ShittyLeavesBlock
	{
		public AppleLeavesBlock() : base()
		{
		}

		public static int Index = 415;

		public override int GetFaceTextureSlot(int face, int value)
		{
			return 2; // Ajusta según tu atlas de texturas
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