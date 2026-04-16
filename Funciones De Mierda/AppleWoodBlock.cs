using Game;

namespace Game
{
	/// <summary>
	/// Bloque de madera de manzano utilizando la textura personalizada.
	/// </summary>
	public class AppleWoodBlock : ShittyWoodBlock
	{
		public AppleWoodBlock() : base(1, 0) // Ajusta los slots según tu textura
		{
		}

		public static int Index = 414;
	}
}