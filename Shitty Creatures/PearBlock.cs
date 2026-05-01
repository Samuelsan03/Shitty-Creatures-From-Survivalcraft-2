using Engine;
using Engine.Graphics;

namespace Game
{
	public class PearBlock : ShittyTexturesFlat
	{
		public const int Index = 409;

		public PearBlock() : base("Textures/alimentos/pera")
		{
			this.BlockIndex = Index;
		}
	}
}