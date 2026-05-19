using System;
using System.Collections.Generic;

namespace Game
{
	public class PearSaplingBlock : ShittyTexturesFlat
	{
		public static int Index = 497;

		public PearSaplingBlock() : base("Textures/alimentos/arbol joven peral")
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