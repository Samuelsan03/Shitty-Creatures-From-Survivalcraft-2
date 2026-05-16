using Game;

namespace Game
{
	public class RottenWatermelonBlock : BaseWatermelonBlock
	{
		public static int Index = 431;

		public RottenWatermelonBlock()
			: base(isRotten: true)
		{
		}

		public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
		{
			isEnd = false;
			return false;
		}
	}
}