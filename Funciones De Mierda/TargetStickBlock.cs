using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class TargetStickBlock : Armas
	{
		public TargetStickBlock() : base("Models/Vara", "Textures/Items/Vara")
		{
		}

		// Método para obtener el nombre mostrado - usa LanguageControl
		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName;
			if (LanguageControl.TryGetBlock("TargetStickBlock:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Targeting Stick"; // Valor por defecto en inglés
		}

		// Método para obtener la descripción
		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("TargetStickBlock:0", "Description", out description))
			{
				return description;
			}
			return "A specialized stick used to command allied creatures. When pointed at an enemy, it signals all nearby player-allied creatures to focus their attacks on the target. Useful for coordinating attacks and managing your creature army in battle."; // Valor por defecto en inglés
		}

		public const int Index = 329;
	}
}
