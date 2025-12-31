using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;
using GameEntitySystem;

namespace Game
{
	public class SubsystemBigStonePoisonChunkBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private Game.Random m_random = new Game.Random();
		private List<Projectile> m_activeProjectiles = new List<Projectile>();

		// CONFIGURACIÓN SIMPLE
		private const float PoisonRadius = 3.5f;
		private const float ImpactDamage = 0.3f;
		private const float PoisonIntensity = 180f;
		private const float KnockbackForce = 8f;

		public override int[] HandledBlocks
		{
			get { return new int[] { BigStonePoisonChunkBlock.Index }; }
		}

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			int blockId = Terrain.ExtractContents(projectile.Value);
			if (blockId == BigStonePoisonChunkBlock.Index)
			{
				// Sin humo, sin partículas en vuelo
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
				m_activeProjectiles.Add(projectile);
			}
		}

		public void Update(float dt)
		{
			for (int i = m_activeProjectiles.Count - 1; i >= 0; i--)
			{
				Projectile projectile = m_activeProjectiles[i];

				if (projectile.ToRemove)
				{
					// Solo aplicar veneno al impactar, sin efectos visuales extra
					ApplyPoisonToNearbyEntities(projectile.Position);
					m_activeProjectiles.RemoveAt(i);
				}
			}
		}

		private void ApplyPoisonToNearbyEntities(Vector3 center)
		{
			if (m_subsystemBodies == null) return;

			float poisonRadiusSquared = PoisonRadius * PoisonRadius;

			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body.Entity == null) continue;

				Vector3 offset = body.Position - center;
				float distanceSquared = offset.LengthSquared();

				if (distanceSquared <= poisonRadiusSquared)
				{
					// 1. Daño moderado por impacto
					ComponentHealth health = body.Entity.FindComponent<ComponentHealth>();
					if (health != null && ImpactDamage > 0)
					{
						string deathCause = GetDeathCause();
						health.Injure(ImpactDamage, null, false, deathCause);
					}

					// 2. Aplicar efecto de veneno
					ApplyPoisonEffect(body.Entity, PoisonIntensity);

					// 3. Empuje moderado
					float distance = MathUtils.Sqrt(distanceSquared);
					if (distance > 0.1f)
					{
						Vector3 forceDirection = offset / distance;
						forceDirection.Y += 0.15f;

						float forceMultiplier = 1f - (distance / PoisonRadius);
						float explosionForce = KnockbackForce * forceMultiplier;

						body.ApplyImpulse(forceDirection * explosionForce);
					}
				}
			}
		}

		private void ApplyPoisonEffect(Entity entity, float poisonIntensity)
		{
			ComponentCreature creature = entity.FindComponent<ComponentCreature>();
			if (creature == null) return;

			// Para jugadores
			ComponentPlayer player = creature as ComponentPlayer;
			if (player != null)
			{
				if (!player.ComponentSickness.IsSick)
				{
					player.ComponentSickness.StartSickness();
					player.ComponentSickness.m_sicknessDuration = poisonIntensity;
				}
				return;
			}

			// Para NPCs
			ComponentPoisonInfected poisonInfected = entity.FindComponent<ComponentPoisonInfected>();
			if (poisonInfected != null)
			{
				if (!poisonInfected.IsInfected)
				{
					poisonInfected.StartInfect(poisonIntensity);
				}
			}
		}

		private string GetDeathCause()
		{
			string deathCause;
			if (LanguageControl.TryGet(out deathCause, "Messages", "DeathByPoisonRock"))
			{
				return deathCause;
			}
			return "Poisoned by toxic giant rock";
		}
	}
}
