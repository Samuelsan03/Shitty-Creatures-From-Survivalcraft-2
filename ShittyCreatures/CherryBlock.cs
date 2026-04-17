using Engine;
using Engine.Graphics;

namespace Game
{
	public class CherryBlock : ShittyTexturesFlat
	{
		public const int Index = 411;

		public CherryBlock() : base("Textures/alimentos/cereza")
		{
			this.BlockIndex = Index;
		}
	}
}