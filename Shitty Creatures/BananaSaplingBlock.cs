using System;
using System.Collections.Generic;

namespace Game
{
	public class BananaSaplingBlock : ShittyTexturesFlat
	{
		public static int Index;

		public BananaSaplingBlock() : base("Textures/alimentos/arbol joven banano")
		{
		}

		public override void Initialize()
		{
			base.Initialize();
			Index = this.BlockIndex;
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(BlockIndex, 0, 0);
		}
	}
}