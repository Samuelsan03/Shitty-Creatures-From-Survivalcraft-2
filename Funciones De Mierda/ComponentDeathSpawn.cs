using System;
using System.Collections.Generic;
using System.Globalization;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentDeathSpawn : Component, IUpdateable
	{
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentSpawn = Entity.FindComponent<ComponentSpawn>(true);

			// Parsear la lista de plantillas con probabilidades individuales
			string spawnTemplatesValue = valuesDictionary.GetValue<string>("DeathSpawnTemplates", "");
			if (!string.IsNullOrEmpty(spawnTemplatesValue))
			{
				// Formato: "NPC1:0.5; NPC2:0.3; NPC3:0.8" (espacios opcionales)
				string[] entries = spawnTemplatesValue.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

				foreach (string entry in entries)
				{
					string trimmedEntry = entry.Trim();
					if (string.IsNullOrEmpty(trimmedEntry))
						continue;

					// Separar nombre y probabilidad
					string[] parts = trimmedEntry.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length == 0)
						continue;

					string npcName = parts[0].Trim();
					float probability = 1.0f; // Valor por defecto

					if (parts.Length >= 2)
					{
						string probStr = parts[1].Trim();
						// Usar CultureInfo.InvariantCulture para que el punto sea siempre el separador decimal
						if (!float.TryParse(probStr, NumberStyles.Float, CultureInfo.InvariantCulture, out probability))
						{
							probability = 1.0f;
							Log.Warning($"Probabilidad inválida para '{npcName}' (valor: '{probStr}'), usando 1.0 por defecto");
						}
					}

					// Verificar que la plantilla existe antes de añadirla
					try
					{
						DatabaseManager.FindEntityValuesDictionary(npcName, true);
						m_spawnEntries.Add(new SpawnEntry
						{
							TemplateName = npcName,
							Probability = probability
						});
					}
					catch
					{
						Log.Warning($"Plantilla de entidad no encontrada: '{npcName}'");
					}
				}
			}

			// Cargar la probabilidad global (0-1)
			m_globalSpawnProbability = valuesDictionary.GetValue<float>("DeathSpawnProbability", 1.0f);

			// Suscribirse al evento de despawn (similar a ComponentShapeshifter)
			m_componentSpawn.Despawned += OnDespawned;
		}

		public virtual void Update(float dt)
		{
			// Verificar si la entidad ha muerto
			if (m_componentHealth.Health <= 0f && !m_hasCheckedDeath)
			{
				m_hasCheckedDeath = true;

				// Comprobar la probabilidad global de spawn al morir
				if (m_spawnEntries.Count > 0 && s_random.Float(0f, 1f) < m_globalSpawnProbability)
				{
					// Seleccionar un NPC basado en probabilidades ponderadas
					SpawnEntry selectedEntry = SelectRandomNPC();

					if (selectedEntry != null)
					{
						m_shouldSpawnOnDespawn = true;
						m_selectedSpawnEntry = selectedEntry;

						// Crear sistema de partículas durante la transformación
						if (m_particleSystem == null)
						{
							m_particleSystem = new NewShapeshiftParticleSystem();
							m_subsystemParticles.AddParticleSystem(m_particleSystem, false);
						}

						// Configurar despawn con animación
						m_componentSpawn.DespawnDuration = 3f;
						m_componentSpawn.Despawn();

						// Reproducir sonido
						if (m_subsystemAudio != null)
						{
							m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, m_componentBody.Position, 3f, true);
						}
					}
				}
			}

			// Actualizar sistema de partículas durante la animación
			if (m_particleSystem != null && m_shouldSpawnOnDespawn)
			{
				m_particleSystem.BoundingBox = m_componentBody.BoundingBox;
			}
		}

		public virtual void OnDespawned(ComponentSpawn componentSpawn)
		{
			// Spawnear la nueva entidad después de la animación
			if (m_shouldSpawnOnDespawn && m_selectedSpawnEntry != null)
			{
				// Crear la entidad a partir de la plantilla seleccionada
				Entity entity = DatabaseManager.CreateEntity(Project, m_selectedSpawnEntry.TemplateName, true);
				ComponentBody componentBody = entity.FindComponent<ComponentBody>(true);

				// Posicionar en el mismo lugar que la entidad original
				componentBody.Position = m_componentBody.Position;
				componentBody.Rotation = m_componentBody.Rotation;
				componentBody.Velocity = m_componentBody.Velocity;

				// Configurar duración de spawn
				ComponentSpawn spawnComponent = entity.FindComponent<ComponentSpawn>(true);
				if (spawnComponent != null)
				{
					spawnComponent.SpawnDuration = 0.5f;
				}

				// Hook para mods
				ModsManager.HookAction("OnDespawned", delegate (ModLoader modLoader)
				{
					modLoader.OnDespawned(entity, componentSpawn);
					return false;
				});

				// Añadir la entidad al mundo
				Project.AddEntity(entity);

				m_shouldSpawnOnDespawn = false;
				m_selectedSpawnEntry = null;
			}

			// Detener el sistema de partículas
			if (m_particleSystem != null)
			{
				m_particleSystem.Stopped = true;
				m_particleSystem = null;
			}
		}

		private SpawnEntry SelectRandomNPC()
		{
			if (m_spawnEntries.Count == 0)
				return null;

			// Calcular la suma total de probabilidades
			float totalProbability = 0f;
			foreach (var entry in m_spawnEntries)
			{
				totalProbability += entry.Probability;
			}

			// Si la suma es 0 (todas las probabilidades son cero), elegir aleatoriamente
			if (totalProbability <= 0f)
			{
				int index = s_random.Int(0, m_spawnEntries.Count - 1);
				return m_spawnEntries[index];
			}

			// Selección ponderada
			float randomValue = s_random.Float(0f, totalProbability);
			float cumulative = 0f;

			foreach (var entry in m_spawnEntries)
			{
				cumulative += entry.Probability;
				if (randomValue <= cumulative)
				{
					return entry;
				}
			}

			// Fallback (nunca debería llegar aquí)
			return m_spawnEntries[m_spawnEntries.Count - 1];
		}

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemParticles m_subsystemParticles;
		private ComponentBody m_componentBody;
		private ComponentHealth m_componentHealth;
		private ComponentSpawn m_componentSpawn;
		private List<SpawnEntry> m_spawnEntries = new List<SpawnEntry>();
		private float m_globalSpawnProbability = 1.0f;
		private bool m_shouldSpawnOnDespawn;
		private bool m_hasCheckedDeath;
		private SpawnEntry m_selectedSpawnEntry;
		private NewShapeshiftParticleSystem m_particleSystem;
		private static Random s_random = new Random();

		public class SpawnEntry
		{
			public string TemplateName;
			public float Probability;
		}
	}
}
