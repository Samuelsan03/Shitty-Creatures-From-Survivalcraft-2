using System;
using Game;

namespace Game
{
	// Token: 0x020000B2 RID: 178
	public class PoisonInjury : Injury
	{
		// Token: 0x06000571 RID: 1393 RVA: 0x00022E56 File Offset: 0x00021056
		public PoisonInjury(float amount, ComponentCreature attacker) : base(amount, attacker, false, "Poisoned")
		{
		}
	}
}
