using Engine;
using Engine.Graphics;

namespace Game
{
	public class AppleBlock : ShittyTexturesFlat
	{
		public const int Index = 415;

		public AppleBlock() : base("Textures/alimentos/manzana")
		{
		}

		public override int GetDamageDestructionValue(int value)
		{
			// Cuando la manzana se pudre completamente, se convierte en manzana podrida
			int rottenAppleIndex = BlocksManager.GetBlockIndex("RottenAppleBlock");
			if (rottenAppleIndex >= 0)
				return Terrain.MakeBlockValue(rottenAppleIndex);
			return base.GetDamageDestructionValue(value); // fallback
		}
	}
}
