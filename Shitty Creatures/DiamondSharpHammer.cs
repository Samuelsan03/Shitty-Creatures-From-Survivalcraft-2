using System;

namespace Game
{
	// Token: 0x02000054 RID: 84
	public class DiamondSharpHammerBlock : SharpHammerBlock
	{
		// Token: 0x060001F8 RID: 504 RVA: 0x0000C4B5 File Offset: 0x0000A6B5
		public DiamondSharpHammerBlock() : base(47, 182)
		{
		}
		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName = LanguageControl.GetBlock("DiamondSharpHammerBlock", "DisplayName");
			return string.IsNullOrEmpty(displayName) ? "Diamond Sharp Hammer" : displayName;
		}

		public override string GetDescription(int value)
		{
			string description = LanguageControl.GetBlock("DiamondSharpHammerBlock", "Description");
			return string.IsNullOrEmpty(description) ? "The Diamond Sharp Hammer is similar to the diamond machete, but far more lethal and durable. Its unmatched resilience and devastating power make it an exceptional weapon, capable of enduring prolonged battles while delivering fatal strikes. Perhaps its greatest value lies in confronting bosses, where its strength and durability can turn the tide of combat." : description;
		}

		// Token: 0x040000EF RID: 239
		public static int Index = 335; // Cambia el índice según lo necesites
	}
}