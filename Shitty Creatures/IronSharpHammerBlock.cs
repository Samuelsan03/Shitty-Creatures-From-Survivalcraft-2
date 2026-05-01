using System;

namespace Game
{
	// Token: 0x02000054 RID: 84
	public class IronSharpHammerBlock : SharpHammerBlock
	{
		// Token: 0x060001F8 RID: 504 RVA: 0x0000C4B5 File Offset: 0x0000A6B5
		public IronSharpHammerBlock() : base(47, 63)
		{
		}
		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName = LanguageControl.GetBlock("IronSharpHammerBlock", "DisplayName");
				return string.IsNullOrEmpty(displayName) ? "Iron Sharp Hammer" : displayName;
		}

		public override string GetDescription(int value)
		{
			string description = LanguageControl.GetBlock("IronSharpHammerBlock", "Description");
			return string.IsNullOrEmpty(description) ? "The Iron Sharp Hammer is similar to the iron machete, but slightly more lethal and a bit more durable. While it shares the same brutal design, its enhanced endurance and sharper impact make it a superior choice for those seeking a weapon that balances resilience with deadly efficiency." : description;
		}

		// Token: 0x040000EF RID: 239
		public static int Index = 334; // Cambia el índice según lo necesites
	}
}