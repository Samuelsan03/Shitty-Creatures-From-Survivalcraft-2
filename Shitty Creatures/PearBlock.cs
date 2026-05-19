using Engine;
using Engine.Graphics;

namespace Game
{
	public class PearBlock : ShittyTexturesFlat
	{
		public const int Index = 497;

		public PearBlock() : base("Textures/alimentos/pera")
		{
		}

		public override int GetDamageDestructionValue(int value)
		{
			int rottenPearIndex = BlocksManager.GetBlockIndex("RottenPearBlock");
			if (rottenPearIndex >= 0)
				return Terrain.MakeBlockValue(rottenPearIndex);
			return base.GetDamageDestructionValue(value);
		}
	}
}