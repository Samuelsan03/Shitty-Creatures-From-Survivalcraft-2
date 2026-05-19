using System;
using System.Collections.Generic;

namespace Game
{
	public class OrangeSaplingBlock : ShittyTexturesFlat
	{
		public static int Index = 493;

		public OrangeSaplingBlock() : base("Textures/alimentos/arbol joven naranjo")
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