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

			// Cargar la lista de plantillas de entidades para spawnear al morir
			string spawnTemplatesValue = valuesDictionary.GetValue<string>("DeathSpawnTemplates", "");
			if (!string.IsNullOrEmpty(spawnTemplatesValue))
			{
				string[] templates = spawnTemplatesValue.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string template in templates)
				{
					string trimmedTemplate = template.Trim();
					if (!string.IsNullOrEmpty(trimmedTemplate))
					{
						// Verificar que la plantilla existe
						try
						{
							DatabaseManager.FindEntityValuesDictionary(trimmedTemplate, true);
							this.m_spawnTemplates.Add(trimmedTemplate);
						}
						catch
						{
							Log.Warning($"Plantilla de entidad no encontrada: {trimmedTemplate}");
						}
					}
				}
			}

			// Cargar la probabilidad (0-1)
			this.m_spawnProbability = valuesDictionary.GetValue<float>("DeathSpawnProbability", 1.0f);

			// Suscribirse al evento de despawn (similar a ComponentShapeshifter)
			ComponentSpawn componentSpawn = this.m_componentSpawn;
			componentSpawn.Despawned = (Action<ComponentSpawn>)Delegate.Combine(componentSpawn.Despawned, new Action<ComponentSpawn>(this.OnDespawned));

			// NO crear partículas aquí, solo cuando realmente vaya a morir
		}

		// Token: 0x06000C4A RID: 3146 RVA: 0x0004B874 File Offset: 0x00049A74
		public virtual void Update(float dt)
		{
			// Verificar si la entidad ha muerto
			if (this.m_componentHealth.Health <= 0f && !this.m_hasCheckedDeath)
			{
				this.m_hasCheckedDeath = true;

				// Verificar probabilidad de spawn al morir
				if (this.m_spawnTemplates.Count > 0 && ComponentDeathSpawn.s_random.Float(0f, 1f) < this.m_spawnProbability)
				{
					this.m_shouldSpawnOnDespawn = true;
					this.m_spawnEntityTemplateName = this.m_spawnTemplates[ComponentDeathSpawn.s_random.Int(0, this.m_spawnTemplates.Count - 1)];

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
			if (this.m_shouldSpawnOnDespawn && !string.IsNullOrEmpty(this.m_spawnEntityTemplateName))
			{
				// Crear la nueva entidad
				Entity entity = DatabaseManager.CreateEntity(base.Project, this.m_spawnEntityTemplateName, true);
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
				this.m_spawnEntityTemplateName = null;
			}

			// Detener partículas (como en ComponentShapeshifter)
			if (this.m_particleSystem != null)
			{
				this.m_particleSystem.Stopped = true;
				// Opcional: remover el sistema de partículas después de un tiempo
				this.m_particleSystem = null;
			}
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
		public List<string> m_spawnTemplates = new List<string>();

		// Token: 0x0400073A RID: 1850
		public float m_spawnProbability = 1.0f;

		// Token: 0x0400073B RID: 1851
		public bool m_shouldSpawnOnDespawn;

		// Token: 0x0400073C RID: 1852
		public bool m_hasCheckedDeath;

		// Token: 0x0400073D RID: 1853
		public string m_spawnEntityTemplateName;

		// Token: 0x0400073E RID: 1854
		public NewShapeshiftParticleSystem m_particleSystem;

		// Token: 0x04000740 RID: 1856
		public static Random s_random = new Random();
	}
}
