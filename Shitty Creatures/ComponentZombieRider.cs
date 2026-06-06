// ComponentZombieRider.cs
using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentZombieRider : ComponentRider
	{
		// Evita que los jugadores monten monturas zombi si no está permitido
		public override float ScoreMount(ComponentMount componentMount, float maxDistance)
		{
			// Verificar si este jinete es un jugador
			ComponentPlayer player = Entity.FindComponent<ComponentPlayer>();
			if (player == null)
				return base.ScoreMount(componentMount, maxDistance);

			// Verificar si la montura es un zombi
			ComponentZombieMount zombieMount = componentMount as ComponentZombieMount;
			if (zombieMount == null)
				zombieMount = componentMount.Entity.FindComponent<ComponentZombieMount>();

			if (zombieMount != null && !zombieMount.CanPlayersRide)
			{
				// Mostrar mensaje al jugador
				player.ComponentGui?.DisplaySmallMessage("No puedes montar esta montura zombi", Color.White, false, true);
				return -1f;
			}

			return base.ScoreMount(componentMount, maxDistance);
		}

		// Doble bloqueo por si el jugador intenta montar de otra forma
		public override void StartMounting(ComponentMount componentMount)
		{
			ComponentPlayer player = Entity.FindComponent<ComponentPlayer>();
			if (player == null)
			{
				base.StartMounting(componentMount);
				return;
			}

			ComponentZombieMount zombieMount = componentMount as ComponentZombieMount;
			if (zombieMount == null)
				zombieMount = componentMount.Entity.FindComponent<ComponentZombieMount>();

			if (zombieMount != null && !zombieMount.CanPlayersRide)
			{
				player.ComponentGui?.DisplaySmallMessage("No puedes montar esta montura zombi", Color.White, false, true);
				return;
			}

			base.StartMounting(componentMount);
		}
	}
}