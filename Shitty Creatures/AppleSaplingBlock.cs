using System;
using System.Collections.Generic;

namespace Game
{
	public class AppleSaplingBlock : ShittyTexturesFlat
	{
		public static int Index = 417;

		public AppleSaplingBlock() : base("Textures/alimentos/arbol joven manzano")
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