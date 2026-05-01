using Engine;
using Engine.Graphics;

namespace Game
{
	public class BlueberryBlock : ShittyTexturesFlat
	{
		public const int Index = 411;

		public BlueberryBlock() : base("Textures/alimentos/arandanos")
		{
			this.BlockIndex = Index;
		}
	}
}