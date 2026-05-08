using System;
using System.Collections.Generic;

namespace Game
{
	public class CherrySaplingBlock : ShittyTexturesFlat
	{
		public static int Index;

		public CherrySaplingBlock() : base("Textures/alimentos/arbol joven cerezo")
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