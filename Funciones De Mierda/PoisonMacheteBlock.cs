using System;
using Engine;
using Game;
using TemplatesDatabase;

namespace Game
{
	public class PoisonMacheteBlock : MacheteBlock
	{
		public PoisonMacheteBlock() : base(47, 9)
		{
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName;
			if (LanguageControl.TryGetBlock("PoisonMacheteBlock:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Poison Machete";
		}

		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("PoisonMacheteBlock:0", "Description", out description))
			{
				return description;
			}
			return "A machete coated with a lethal toxin that poisons enemies upon striking them.";
		}

		public static int Index = 327;
	}
}
