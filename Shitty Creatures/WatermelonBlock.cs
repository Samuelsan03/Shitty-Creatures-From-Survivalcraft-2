using Game;
using System.Collections.Generic;

namespace Game
{
	public class WatermelonBlock : BaseWatermelonBlock
	{
		public static int Index = 536;

		public WatermelonBlock()
			: base(isRotten: false)
		{
		}

		public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
		{
			isEnd = false;
			return false;
		}
	}
}