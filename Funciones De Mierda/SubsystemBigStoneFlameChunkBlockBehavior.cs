using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;
using GameEntitySystem;

namespace Game
{
	public class SubsystemBigStoneFlameChunkBlockBehavior : SubsystemBlockBehavior, IUpdateable
	{
		private SubsystemFireBlockBehavior m_subsystemFireBlockBehavior;
		private SubsystemBodies m_subsystemBodies;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAmbientSounds m_subsystemAmbientSounds;
		private Game.Random m_random = new Game.Random();
		private List<Projectile> m_activeProjectiles = new List<Projectile>();

		// CONFIGURACIÓN BALANCEADA
		private const float FireDamageRadius = 4f;          // Radio moderado
		private const float ImpactDamage = 3f;              // Daño moderado (no insta-kill)
		private const float BurnDuration = 30f;             // 30 segundos de fuego
		private const float KnockbackForce = 10f;           // Empuje moderado

		public override int[] HandledBlocks
		{
			get { return new int[] { BigStoneFlameChunkBlock.Index }; }
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
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemAmbientSounds = base.Project.FindSubsystem<SubsystemAmbientSounds>(true);
			m_subsystemFireBlockBehavior = base.Project.FindSubsystem<SubsystemFireBlockBehavior>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			int blockId = Terrain.ExtractContents(projectile.Value);
			if (blockId == BigStoneFlameChunkBlock.Index)
			{
				m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new SmokeTrailParticleSystem(25, 2.5f, float.MaxValue, Color.White));
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
					// Crear fuego al impactar
					CreateFireAtImpact(projectile.Position);
					// Aplicar daño balanceado
					DamageNearbyEntities(projectile.Position);
					m_activeProjectiles.RemoveAt(i);
				}
			}
		}

		private void CreateFireAtImpact(Vector3 position)
		{
			try
			{
				int x = Terrain.ToCell(position.X);
				int y = Terrain.ToCell(position.Y);
				int z = Terrain.ToCell(position.Z);

				// Crear fuego en área moderada
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dz = -1; dz <= 1; dz++)
					{
						int fireX = x + dx;
						int fireY = y;
						int fireZ = z + dz;

						if (fireY >= 0 && fireY < 256)
						{
							// Probabilidad de crear fuego (no siempre en todas las celdas)
							if (m_random.Float(0f, 1f) < 0.7f)
							{
								m_subsystemFireBlockBehavior.SetCellOnFire(fireX, fireY, fireZ, 1f);
							}
						}
					}
				}
			}
			catch { }
		}

		private void DamageNearbyEntities(Vector3 center)
		{
			if (m_subsystemBodies == null) return;

			float fireRadiusSquared = FireDamageRadius * FireDamageRadius;

			foreach (ComponentBody body in m_subsystemBodies.Bodies)
			{
				if (body.Entity == null) continue;

				Vector3 offset = body.Position - center;
				float distanceSquared = offset.LengthSquared();

				if (distanceSquared <= fireRadiusSquared)
				{
					// 1. Daño moderado por impacto (no insta-kill)
					ComponentHealth health = body.Entity.FindComponent<ComponentHealth>();
					if (health != null && ImpactDamage > 0)
					{
						// Obtener causa de muerte desde Messages
						string deathCause = GetDeathCause();
						health.Injure(ImpactDamage, null, false, deathCause);
					}

					// 2. Establecer en fuego por 30 segundos
					ComponentOnFire onFire = body.Entity.FindComponent<ComponentOnFire>();
					if (onFire != null)
					{
						onFire.SetOnFire(null, BurnDuration);
					}

					// 3. Empuje moderado
					float distance = MathUtils.Sqrt(distanceSquared);
					if (distance > 0.1f)
					{
						Vector3 forceDirection = offset / distance;
						forceDirection.Y += 0.2f;

						float forceMultiplier = 1f - (distance / FireDamageRadius);
						float explosionForce = KnockbackForce * forceMultiplier;

						body.ApplyImpulse(forceDirection * explosionForce);
					}
				}
			}
		}

		private string GetDeathCause()
		{
			// Intentar obtener causa de muerte desde Messages
			string deathCause;
			if (LanguageControl.TryGet(out deathCause, "Messages", "DeathByFlamingRock"))
			{
				return deathCause;
			}

			// Fallback único: "Incinerated by flaming giant rock"
			return "Incinerated by flaming giant rock";
		}
	}
}
