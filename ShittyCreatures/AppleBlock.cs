using Engine;
using Engine.Graphics;

namespace Game
{
	public class AppleBlock : ShittyTexturesFlat
	{
		public const int Index = 408;

		public AppleBlock() : base("Textures/alimentos/manzana")
		{
			this.BlockIndex = Index;
		}
	}
}
