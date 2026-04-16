using Engine;
using Engine.Graphics;

namespace Game
{
	public class BananaBlock : ShittyTexturesFlat
	{
		public const int Index = 413;

		public BananaBlock() : base("Textures/alimentos/Banana")
		{
			this.BlockIndex = Index;
		}
	}
}