using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemBigStonePoisonChunkBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[] { BigStonePoisonChunkBlock.Index };
			}
		}

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		private bool m_initialized;
		private Action<Attackment> m_bodyAttackedHandler;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_initialized = true;
		}

		public override void OnEntityAdded(Entity entity)
		{
			base.OnEntityAdded(entity);

			// Solo suscribirse si está inicializado y la entidad tiene un cuerpo
			if (m_initialized)
			{
				ComponentBody body = entity.FindComponent<ComponentBody>();
				if (body != null)
				{
					// Suscribir el evento Attacked (Action<Attackment>)
					body.Attacked = (Action<Attackment>)Delegate.Combine(body.Attacked, m_bodyAttackedHandler);
				}
			}
		}

		public override void OnEntityRemoved(Entity entity)
		{
			base.OnEntityRemoved(entity);

			// Limpiar el evento al eliminar la entidad para evitar memory leaks
			ComponentBody body = entity.FindComponent<ComponentBody>();
			if (body != null && m_bodyAttackedHandler != null)
			{
				body.Attacked = (Action<Attackment>)Delegate.Remove(body.Attacked, m_bodyAttackedHandler);
			}
		}

		public SubsystemBigStonePoisonChunkBlockBehavior()
		{
			// Inicializar el handler una sola vez con el tipo correcto Action<Attackment>
			m_bodyAttackedHandler = new Action<Attackment>(HandleBodyAttacked);
		}

		private void HandleBodyAttacked(Attackment attackment)
		{
			// Verificar que el ataque venga de un proyectil
			ProjectileAttackment projectileAttack = attackment as ProjectileAttackment;
			if (projectileAttack == null)
				return;

			// Verificar que el proyectil no sea nulo y sea específicamente la roca venenosa
			if (projectileAttack.Projectile == null || projectileAttack.Projectile.Value == 0)
				return;

			if (Terrain.ExtractContents(projectileAttack.Projectile.Value) != BigStonePoisonChunkBlock.Index)
				return;

			// Si llegamos aquí, la roca LE PEGÓ DE VERDAD a esta entidad. Aplicar veneno.
			// Usar attackment.Target (que es Entity) en lugar de AttackedEntity
			ComponentCreature creature = attackment.Target?.FindComponent<ComponentCreature>();
			if (creature != null)
			{
				ApplyPoisonToCreature(creature);
			}
		}

		private void ApplyPoisonToCreature(ComponentCreature creature)
		{
			if (creature == null)
				return;

			ComponentPoisonInfected componentPoisonInfected = creature.Entity.FindComponent<ComponentPoisonInfected>();
			ComponentPlayer componentPlayer = creature as ComponentPlayer;

			if (componentPlayer != null)
			{
				// Para jugadores: usar sistema de enfermedad
				if (!componentPlayer.ComponentSickness.IsSick)
				{
					componentPlayer.ComponentSickness.StartSickness();
					if (componentPoisonInfected != null)
					{
						componentPlayer.ComponentSickness.m_sicknessDuration = 15f - componentPoisonInfected.PoisonResistance;
					}
				}
			}
			else if (componentPoisonInfected != null && !componentPoisonInfected.IsInfected)
			{
				// Para otras criaturas: usar sistema de infección de veneno
				componentPoisonInfected.StartInfect(15f);
			}
		}

		public void Update(float dt)
		{
			// Ya no necesitamos buscar proyectiles ni áreas.
			// El veneno se aplica automáticamente y de forma precisa a través del evento Attacked del motor.
		}
	}
}
