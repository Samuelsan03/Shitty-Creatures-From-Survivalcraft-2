using System;

namespace Game
{
	// Token: 0x02000054 RID: 84
	public class LavaSharpHammerBlock : SharpHammerBlock
	{
		// Token: 0x060001F8 RID: 504 RVA: 0x0000C4B5 File Offset: 0x0000A6B5
		public LavaSharpHammerBlock() : base(47, 158)
		{
		}
		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName = LanguageControl.GetBlock("LavaSharpHammerBlock", "DisplayName");
			return string.IsNullOrEmpty(displayName) ? "Lava Sharp Hammer" : displayName;
		}

		public override string GetDescription(int value)
		{
			string description = LanguageControl.GetBlock("LavaSharpHammerBlock", "Description");
			return string.IsNullOrEmpty(description) ? "The Lava Sharp Hammer is similar to the lava machete, but far more lethal and durable. Its blazing core and molten edge make it a weapon of pure destruction, capable of enduring the fiercest battles while delivering catastrophic strikes. It is not just stronger than the machete—it is a true infernal tool of war. This hammer is 100% extremely useful for slaying bosses, a weapon that radiates danger and promise. Players will feel its fiery potential, knowing that when the time comes, it may be the decisive force that turns impossible fights into victories." : description;
		}

		// Token: 0x040000EF RID: 239
		public static int Index = 336; // Cambia el índice según lo necesites
	}
}