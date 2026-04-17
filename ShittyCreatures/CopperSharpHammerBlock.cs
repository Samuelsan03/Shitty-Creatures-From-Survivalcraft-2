using System;

namespace Game
{
	public class CopperSharpHammerBlock : SharpHammerBlock
	{
		public CopperSharpHammerBlock() : base(47, 79)
		{
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName = LanguageControl.GetBlock("CopperSharpHammerBlock", "DisplayName");
			return string.IsNullOrEmpty(displayName) ? "Copper Sharp Hammer" : displayName;
		}

		public override string GetDescription(int value)
		{
			string description = LanguageControl.GetBlock("CopperSharpHammerBlock", "Description");
			return string.IsNullOrEmpty(description) ? "The Copper Sharp Hammer is a powerful weapon. At first glance it looks like a simple work tool, but in reality its lethality surpasses that of the copper machete. Although it is not as durable as the machete, it is taller and far deadlier, capable of delivering devastating blows that make it one of the most dangerous copper-based weapons." : description;
		}

		public static int Index = 333;
	}
}