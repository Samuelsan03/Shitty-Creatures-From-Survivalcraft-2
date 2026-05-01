using Engine;
using Engine.Graphics;

namespace Game
{
	public class AK48BulletBlock : ShittyTexturesFlat
	{
		public const int Index = 404; // Índice único para la bala. Debe coincidir con el CSV.

		public AK48BulletBlock() : base("Textures/Armas De Mierda/308CL")
		{
			this.BlockIndex = Index;
		}
	}
}
