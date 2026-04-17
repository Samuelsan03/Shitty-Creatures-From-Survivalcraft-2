using System;
using Game;

namespace Game
{
	public class PoisonInjury : Injury
	{
		public PoisonInjury(float amount, ComponentCreature attacker) : base(amount, attacker, false, GetPoisonedText())
		{
		}

		private static string GetPoisonedText()
		{
			// CORREGIDO: Usar categoría "Injury" y clave "Poisoned"
			return LanguageControl.Get("Injury", "Poisoned", "Poisoned");
		}
	}
}
