using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentDeathSpawn : Component, IUpdateable
	{
		// Token: 0x170001B7 RID: 439
		// (get) Token: 0x06000C48 RID: 3144 RVA: 0x0004B760 File Offset: 0x00049960
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x06000C49 RID: 3145 RVA: 0x0004B764 File Offset: 0x00049964
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			this.m_componentHealth = base.Entity.FindComponent<ComponentHealth>(true);
			this.m_componentSpawn = base.Entity.FindComponent<ComponentSpawn>(true);

			// Cargar la lista de plantillas de entidades con probabilidades individuales
			string spawnTemplatesValue = valuesDictionary.GetValue<string>("DeathSpawnTemplates", "");
			if (!string.IsNullOrEmpty(spawnTemplatesValue))
			{
				// Formato esperado: "NPC1:0.5;NPC2:0.3;NPC3:0.8"
				string[] entries = spawnTemplatesValue.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

				foreach (string entry in entries)
				{
					string trimmedEntry = entry.Trim();
					if (!string.IsNullOrEmpty(trimmedEntry))
					{
						// Separar nombre de NPC y probabilidad
						string[] parts = trimmedEntry.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

						if (parts.Length >= 1)
						{
							string npcName = parts[0].Trim();
							float probability = 1.0f; // Probabilidad por defecto

							if (parts.Length >= 2)
							{
								if (!float.TryParse(parts[1].Trim(), out probability))
								{
									probability = 1.0f;
									Log.Warning($"Probabilidad inválida para {npcName}, usando 1.0 por defecto");
								}
							}

							// Verificar que la plantilla existe
							try
							{
								DatabaseManager.FindEntityValuesDictionary(npcName, true);
								this.m_spawnEntries.Add(new SpawnEntry
								{
									TemplateName = npcName,
									Probability = probability
								});
							}
							catch
							{
								Log.Warning($"Plantilla de entidad no encontrada: {npcName}");
							}
						}
					}
				}
			}

			// Cargar la probabilidad global (0-1)
			this.m_globalSpawnProbability = valuesDictionary.GetValue<float>("DeathSpawnProbability", 1.0f);

			// Suscribirse al evento de despawn (similar a ComponentShapeshifter)
			ComponentSpawn componentSpawn = this.m_componentSpawn;
			componentSpawn.Despawned = (Action<ComponentSpawn>)Delegate.Combine(componentSpawn.Despawned, new Action<ComponentSpawn>(this.OnDespawned));
		}

		// Token: 0x06000C4A RID: 3146 RVA: 0x0004B874 File Offset: 0x00049A74
		public virtual void Update(float dt)
		{
			// Verificar si la entidad ha muerto
			if (this.m_componentHealth.Health <= 0f && !this.m_hasCheckedDeath)
			{
				this.m_hasCheckedDeath = true;

				// Verificar probabilidad global de spawn al morir
				if (this.m_spawnEntries.Count > 0 && ComponentDeathSpawn.s_random.Float(0f, 1f) < this.m_globalSpawnProbability)
				{
					// Seleccionar un NPC basado en probabilidades individuales
					SpawnEntry selectedEntry = SelectRandomNPC();

					if (selectedEntry != null)
					{
						this.m_shouldSpawnOnDespawn = true;
						this.m_selectedSpawnEntry = selectedEntry;

						// Crear sistema de partículas SOLO cuando va a morir
						if (this.m_particleSystem == null)
						{
							this.m_particleSystem = new NewShapeshiftParticleSystem();
							this.m_subsystemParticles.AddParticleSystem(this.m_particleSystem, false);
						}

						// Configurar despawn con animación (como en ComponentShapeshifter)
						this.m_componentSpawn.DespawnDuration = 3f; // Duración de la animación
						this.m_componentSpawn.Despawn();

						// Reproducir sonido de spawn
						if (this.m_subsystemAudio != null)
						{
							this.m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, this.m_componentBody.Position, 3f, true);
						}
					}
				}
			}

			// Actualizar sistema de partículas si existe (solo durante la animación de muerte)
			if (this.m_particleSystem != null && this.m_shouldSpawnOnDespawn)
			{
				this.m_particleSystem.BoundingBox = this.m_componentBody.BoundingBox;
			}
		}

		// Token: 0x06000C4B RID: 3147 RVA: 0x0004B8E4 File Offset: 0x00049AE4
		public virtual void OnDespawned(ComponentSpawn componentSpawn)
		{
			// Spawnear después de la animación de partículas (como ComponentShapeshifter)
			if (this.m_shouldSpawnOnDespawn && this.m_selectedSpawnEntry != null)
			{
				// Crear la nueva entidad
				Entity entity = DatabaseManager.CreateEntity(base.Project, this.m_selectedSpawnEntry.TemplateName, true);
				ComponentBody componentBody = entity.FindComponent<ComponentBody>(true);

				// Posicionar en la misma ubicación que la entidad muerta
				componentBody.Position = this.m_componentBody.Position;
				componentBody.Rotation = this.m_componentBody.Rotation;
				componentBody.Velocity = this.m_componentBody.Velocity;

				// Configurar duración de spawn
				ComponentSpawn spawnComponent = entity.FindComponent<ComponentSpawn>(true);
				if (spawnComponent != null)
				{
					spawnComponent.SpawnDuration = 0.5f;
				}

				// Hook para mods (igual que en ComponentShapeshifter)
				ModsManager.HookAction("OnDespawned", delegate (ModLoader modLoader)
				{
					modLoader.OnDespawned(entity, componentSpawn);
					return false;
				});

				// Agregar la nueva entidad al proyecto
				base.Project.AddEntity(entity);

				this.m_shouldSpawnOnDespawn = false;
				this.m_selectedSpawnEntry = null;
			}

			// Detener partículas (como en ComponentShapeshifter)
			if (this.m_particleSystem != null)
			{
				this.m_particleSystem.Stopped = true;
				// Opcional: remover el sistema de partículas después de un tiempo
				this.m_particleSystem = null;
			}
		}

		// Token: 0x06000C4C RID: 3148 RVA: 0x0004BA00 File Offset: 0x00049C00
		private SpawnEntry SelectRandomNPC()
		{
			if (this.m_spawnEntries.Count == 0)
				return null;

			// Calcular la suma total de probabilidades
			float totalProbability = 0f;
			foreach (var entry in this.m_spawnEntries)
			{
				totalProbability += entry.Probability;
			}

			// Si la suma es 0, usar probabilidades iguales
			if (totalProbability <= 0f)
			{
				int index = ComponentDeathSpawn.s_random.Int(0, this.m_spawnEntries.Count - 1);
				return this.m_spawnEntries[index];
			}

			// Selección basada en probabilidades ponderadas
			float randomValue = ComponentDeathSpawn.s_random.Float(0f, totalProbability);
			float cumulative = 0f;

			foreach (var entry in this.m_spawnEntries)
			{
				cumulative += entry.Probability;
				if (randomValue <= cumulative)
				{
					return entry;
				}
			}

			// Fallback: último elemento
			return this.m_spawnEntries[this.m_spawnEntries.Count - 1];
		}

		// Token: 0x04000733 RID: 1843
		public SubsystemGameInfo m_subsystemGameInfo;

		// Token: 0x04000734 RID: 1844
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x04000735 RID: 1845
		public SubsystemParticles m_subsystemParticles;

		// Token: 0x04000736 RID: 1846
		public ComponentBody m_componentBody;

		// Token: 0x04000737 RID: 1847
		public ComponentHealth m_componentHealth;

		// Token: 0x04000738 RID: 1848
		public ComponentSpawn m_componentSpawn;

		// Token: 0x04000739 RID: 1849
		public List<SpawnEntry> m_spawnEntries = new List<SpawnEntry>();

		// Token: 0x0400073A RID: 1850
		public float m_globalSpawnProbability = 1.0f;

		// Token: 0x0400073B RID: 1851
		public bool m_shouldSpawnOnDespawn;

		// Token: 0x0400073C RID: 1852
		public bool m_hasCheckedDeath;

		// Token: 0x0400073D RID: 1853
		public SpawnEntry m_selectedSpawnEntry;

		// Token: 0x0400073E RID: 1854
		public NewShapeshiftParticleSystem m_particleSystem;

		// Token: 0x0400073F RID: 1855
		public static Random s_random = new Random();

		// Token: 0x0200055D RID: 1373
		public class SpawnEntry
		{
			// Token: 0x04001CA7 RID: 7335
			public string TemplateName;

			// Token: 0x04001CA8 RID: 7336
			public float Probability;
		}
	}
}
