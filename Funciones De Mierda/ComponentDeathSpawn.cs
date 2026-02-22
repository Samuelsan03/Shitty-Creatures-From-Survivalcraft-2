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

			string spawnTemplatesValue = valuesDictionary.GetValue<string>("DeathSpawnTemplates", "");
			if (!string.IsNullOrEmpty(spawnTemplatesValue))
			{
				string[] entries = spawnTemplatesValue.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

				foreach (string entry in entries)
				{
					string trimmedEntry = entry.Trim();
					if (string.IsNullOrEmpty(trimmedEntry))
						continue;

					string[] parts = trimmedEntry.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length == 0)
						continue;

					string npcName = parts[0].Trim();
					float probability = 1.0f;

					if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]))
					{
						string probStr = parts[1].Trim();
						float.TryParse(probStr, NumberStyles.Float, CultureInfo.InvariantCulture, out probability);
					}

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
					}
				}
			}

			m_globalSpawnProbability = valuesDictionary.GetValue<float>("DeathSpawnProbability", 1.0f);

			m_componentSpawn.Despawned += OnDespawned;
		}

		public virtual void Update(float dt)
		{
			if (m_componentHealth.Health <= 0f && !m_hasCheckedDeath)
			{
				m_hasCheckedDeath = true;

				if (m_spawnEntries.Count > 0)
				{
					// CORRECCIÓN: Primero seleccionar el NPC basado en sus probabilidades relativas
					SpawnEntry selectedEntry = SelectRandomNPC();

					if (selectedEntry != null)
					{
						// CORRECCIÓN: Luego aplicar la probabilidad global multiplicada por la individual
						float finalProbability = m_globalSpawnProbability * selectedEntry.Probability;

						if (s_random.Float(0f, 1f) < finalProbability)
						{
							m_shouldSpawnOnDespawn = true;
							m_selectedSpawnEntry = selectedEntry;

							if (m_particleSystem == null)
							{
								m_particleSystem = new NewShapeshiftParticleSystem();
								m_subsystemParticles.AddParticleSystem(m_particleSystem, false);
							}

							m_componentSpawn.DespawnDuration = 3f;
							m_componentSpawn.Despawn();

							if (m_subsystemAudio != null)
							{
								m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f, m_componentBody.Position, 3f, true);
							}
						}
					}
				}
			}

			if (m_particleSystem != null && m_shouldSpawnOnDespawn)
			{
				m_particleSystem.BoundingBox = m_componentBody.BoundingBox;
			}
		}

		public virtual void OnDespawned(ComponentSpawn componentSpawn)
		{
			if (m_shouldSpawnOnDespawn && m_selectedSpawnEntry != null)
			{
				Entity entity = DatabaseManager.CreateEntity(Project, m_selectedSpawnEntry.TemplateName, true);
				ComponentBody componentBody = entity.FindComponent<ComponentBody>(true);

				componentBody.Position = m_componentBody.Position;
				componentBody.Rotation = m_componentBody.Rotation;
				componentBody.Velocity = m_componentBody.Velocity;

				ComponentSpawn spawnComponent = entity.FindComponent<ComponentSpawn>(true);
				if (spawnComponent != null)
				{
					spawnComponent.SpawnDuration = 0.5f;
				}

				ModsManager.HookAction("OnDespawned", delegate (ModLoader modLoader)
				{
					modLoader.OnDespawned(entity, componentSpawn);
					return false;
				});

				Project.AddEntity(entity);

				m_shouldSpawnOnDespawn = false;
				m_selectedSpawnEntry = null;
			}

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

			// Si solo hay un NPC, devolverlo directamente
			if (m_spawnEntries.Count == 1)
				return m_spawnEntries[0];

			float totalProbability = 0f;
			foreach (var entry in m_spawnEntries)
			{
				totalProbability += entry.Probability;
			}

			if (totalProbability <= 0f)
			{
				int index = s_random.Int(0, m_spawnEntries.Count - 1);
				return m_spawnEntries[index];
			}

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
