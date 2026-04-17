using Engine;
using Engine.Graphics;

namespace Game
{
	public class OrangeBlock : ShittyTexturesFlat
	{
		public const int Index = 410;

		public OrangeBlock() : base("Textures/alimentos/naranja")
		{
			this.BlockIndex = Index;
		}
	}
}