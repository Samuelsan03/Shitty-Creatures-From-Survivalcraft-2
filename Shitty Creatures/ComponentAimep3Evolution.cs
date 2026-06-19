using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentAimep3Evolution : Component, IUpdateable
	{
		// ===== PROPIEDADES PÚBLICAS =====
		public int EvolutionLevel { get; private set; } = 0;
		public int MaxEvolutionLevel { get; private set; } = 10;
		public bool HasEvolved => EvolutionLevel > 0;
		public float EvolutionMultiplier { get; set; } = 1.5f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// ===== CONFIGURACIÓN DE FUEGO (una sola área, un solo radio, una sola probabilidad) =====
		/// <summary>Duración BASE del fuego al nivel 1. Escala con la evolución.</summary>
		public float FireDuration { get; set; } = 12f;

		/// <summary>Radio único del área de fuego. Si la criatura está fuera, no hace nada.</summary>
		public float BurnRadius { get; set; } = 50f;

		/// <summary>Probabilidad de quemar a cada criatura dentro del radio.</summary>
		public float BurnProbability { get; set; } = 0.5f;

		/// <summary>Cooldown en segundos entre cada ráfaga de fuego.</summary>
		public float FireCooldown { get; set; } = 30f;

		// ===== PROPIEDADES CALCULADAS =====
		/// <summary>Duración efectiva del fuego: escala con el nivel. Nivel 1=12s, Nivel 10=~34s</summary>
		private float EffectiveFireDuration => FireDuration * (1f + (EvolutionLevel - 1) * 0.2f);

		private bool HasFireAbility => EvolutionLevel >= 1;
		private bool IsMaxLevel => EvolutionLevel >= MaxEvolutionLevel;

		// ===== MÉTODOS ESTÁTICOS DE NUEVABALA =====
		public static bool IsNuevaBalaBlock(int blockValue)
		{
			int contents = Terrain.ExtractContents(blockValue);
			if (contents <= 0) return false;

			string typeName = BlocksManager.Blocks[contents].GetType().Name;

			return typeName == "NuevaBala" ||
				   typeName == "NuevaBala2" ||
				   typeName == "NuevaBala3" ||
				   typeName == "NuevaBala4" ||
				   typeName == "NuevaBala5" ||
				   typeName == "NuevaBala6";
		}

		public float GetNuevaBalaDamageReduction()
		{
			if (IsMaxLevel)
				return 0.7f;
			return 0f;
		}

		// ===== CAMPOS PRIVADOS =====
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		private SubsystemAudio m_subsystemAudio;
		private ComponentCreature m_componentCreature;
		private ComponentHealth m_componentHealth;
		private ComponentMiner m_componentMiner;
		private ComponentNewHerdBehavior m_componentHerd;

		private float m_baseAttackResilienceFactor;
		private float m_baseAttackPower;

		private ShapeshiftParticleSystem m_particleSystem;
		private double m_evolutionParticleStartTime;
		private bool m_isEvolving;
		private bool m_pendingEvolution;
		private const float EvolutionParticleDuration = 2.5f;

		private bool m_pendingStatRestoration = false;
		private bool m_hasSavedBaseValues = false;

		private double m_lastFireBurstTime;
		private Random m_random = new Random();

		// ===== MÉTODOS PÚBLICOS =====
		public bool TryEvolve()
		{
			if (EvolutionLevel >= MaxEvolutionLevel || m_isEvolving)
				return false;

			if (m_componentHealth == null || m_componentHealth.Health <= 0f)
				return false;

			if (!m_hasSavedBaseValues)
			{
				m_baseAttackResilienceFactor = m_componentHealth.AttackResilienceFactor;
				m_baseAttackPower = m_componentMiner.AttackPower;
				m_hasSavedBaseValues = true;
			}

			m_isEvolving = true;
			m_pendingEvolution = true;
			StartEvolutionParticles();
			return true;
		}

		public float GetEvolutionProgress()
		{
			return (float)EvolutionLevel / MaxEvolutionLevel;
		}

		// ===== MÉTODOS PRIVADOS =====
		private void StartEvolutionParticles()
		{
			if (m_subsystemParticles == null || m_componentCreature == null)
				return;

			if (m_particleSystem != null)
			{
				m_particleSystem.Stopped = true;
				m_particleSystem = null;
			}

			m_particleSystem = new ShapeshiftParticleSystem();
			m_particleSystem.Position = m_componentCreature.ComponentBody.Position;
			m_particleSystem.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;
			m_particleSystem.Stopped = false;

			m_subsystemParticles.AddParticleSystem(m_particleSystem, false);
			m_evolutionParticleStartTime = m_subsystemTime.GameTime;

			if (m_subsystemAudio != null && m_componentCreature.ComponentBody != null)
			{
				m_subsystemAudio.PlaySound("Audio/Shapeshift", 1f, 0f,
					m_componentCreature.ComponentBody.Position, 5f, false);
			}
		}

		private void ApplyEvolution()
		{
			if (!m_pendingEvolution) return;

			if (EvolutionLevel >= MaxEvolutionLevel)
			{
				m_pendingEvolution = false;
				m_isEvolving = false;
				return;
			}

			m_componentHealth.AttackResilienceFactor = m_baseAttackResilienceFactor * (float)Math.Pow(EvolutionMultiplier, EvolutionLevel + 1);
			m_componentMiner.AttackPower = m_baseAttackPower * (float)Math.Pow(EvolutionMultiplier, EvolutionLevel + 1);

			float currentHealth = m_componentHealth.Health;
			m_componentHealth.Health = MathUtils.Saturate(currentHealth + 0.2f);

			EvolutionLevel++;
			NotifyPlayers();
			m_pendingEvolution = false;
		}

		private void RecalculateStats()
		{
			if (m_componentHealth == null || m_componentMiner == null)
				return;

			float multiplier = (float)Math.Pow(EvolutionMultiplier, EvolutionLevel);
			m_componentHealth.AttackResilienceFactor = m_baseAttackResilienceFactor * multiplier;
			m_componentMiner.AttackPower = m_baseAttackPower * multiplier;
		}

		/// <summary>
		/// Ráfaga de fuego: quema criaturas dentro del radio único.
		/// Si está fuera del área, no hace nada.
		/// Usa cooldown de 30s. Suena el audio del fósforo si quemó al menos una.
		/// La duración del fuego escala con el nivel de evolución.
		/// </summary>
		private void TryIgniteNearbyCreatures()
		{
			if (!HasFireAbility)
				return;

			if (m_subsystemCreatureSpawn == null || m_componentCreature?.ComponentBody == null)
				return;

			if (m_componentHealth.Health <= 0f)
				return;

			// Cooldown: no hacer nada si no pasó el tiempo
			if (m_subsystemTime.GameTime - m_lastFireBurstTime < FireCooldown)
				return;

			Vector3 position = m_componentCreature.ComponentBody.Position;
			float radiusSquared = BurnRadius * BurnRadius;
			float duration = EffectiveFireDuration;
			bool burnedAny = false;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				// No quemarse a sí mismo
				if (creature == m_componentCreature)
					continue;

				// Ignorar muertos
				if (creature.ComponentHealth == null || creature.ComponentHealth.Health <= 0f)
					continue;

				// Fuera del área: no hacer nada
				float distSq = Vector3.DistanceSquared(position, creature.ComponentBody.Position);
				if (distSq > radiusSquared)
					continue;

				// No quemar a la propia manada
				if (m_componentHerd != null && m_componentHerd.IsSameHerdOrGuardian(creature))
					continue;

				// No quemar a jugadores si somos guardian o aliado
				if (creature.Entity.FindComponent<ComponentPlayer>() != null && m_componentHerd != null)
				{
					string herdName = m_componentHerd.HerdName;
					if (!string.IsNullOrEmpty(herdName) &&
						(herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
						 herdName.ToLower().Contains("guardian")))
					{
						continue;
					}
				}

				// Probabilidad
				if (m_random.Float(0f, 1f) > BurnProbability)
					continue;

				// Prender fuego con duración que escala con evolución
				ComponentOnFire componentOnFire = creature.Entity.FindComponent<ComponentOnFire>();
				if (componentOnFire != null)
				{
					componentOnFire.SetOnFire(m_componentCreature, duration);
					burnedAny = true;
				}
			}

			// Sonido de fósforo original si quemó al menos una criatura
			if (burnedAny && m_subsystemAudio != null)
			{
				m_subsystemAudio.PlaySound("Audio/Match", 1f, m_random.Float(-0.1f, 0.1f), position, 1f, true);
			}

			// Registrar el tiempo de esta ráfaga (inicia el cooldown)
			m_lastFireBurstTime = m_subsystemTime.GameTime;
		}

		private void NotifyPlayers()
		{
			if (m_subsystemPlayers == null) return;

			string message;
			Color color = new Color(255, 215, 0);

			if (EvolutionLevel >= MaxEvolutionLevel)
			{
				message = string.Format(LanguageControl.Get(new string[] { "ComponentAimep3Evolution", "1" }), m_componentCreature.DisplayName, EvolutionLevel);
				color = new Color(255, 100, 100);
			}
			else
			{
				message = string.Format(LanguageControl.Get(new string[] { "ComponentAimep3Evolution", "0" }), m_componentCreature.DisplayName, EvolutionLevel);
			}

			foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
			{
				if (player?.ComponentGui != null)
				{
					player.ComponentGui.DisplaySmallMessage(message, color, false, true);
				}
			}
		}

		// ===== UPDATE =====
		public void Update(float dt)
		{
			if (m_pendingStatRestoration)
			{
				m_pendingStatRestoration = false;

				if (m_componentMiner != null)
					m_componentMiner.AttackPower = m_baseAttackPower;
				if (m_componentHealth != null)
					m_componentHealth.AttackResilienceFactor = m_baseAttackResilienceFactor;

				RecalculateStats();
			}

			if (m_isEvolving && m_particleSystem != null)
			{
				double elapsed = m_subsystemTime.GameTime - m_evolutionParticleStartTime;
				if (elapsed >= EvolutionParticleDuration)
				{
					m_particleSystem.Stopped = true;
					m_particleSystem = null;
					m_isEvolving = false;

					if (m_pendingEvolution)
					{
						ApplyEvolution();
					}
				}
				else
				{
					if (m_componentCreature?.ComponentBody != null)
					{
						m_particleSystem.Position = m_componentCreature.ComponentBody.Position;
						m_particleSystem.BoundingBox = m_componentCreature.ComponentBody.BoundingBox;
					}
				}
			}

			// Ráfaga de fuego con cooldown
			if (HasFireAbility && m_subsystemTime != null)
			{
				TryIgniteNearbyCreatures();
			}
		}

		// ===== LOAD =====
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
			m_subsystemCreatureSpawn = Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
			m_componentHerd = Entity.FindComponent<ComponentNewHerdBehavior>();

			EvolutionLevel = valuesDictionary.GetValue<int>("EvolutionLevel", 0);
			MaxEvolutionLevel = valuesDictionary.GetValue<int>("MaxEvolutionLevel", 10);
			EvolutionMultiplier = valuesDictionary.GetValue<float>("EvolutionMultiplier", 1.5f);

			FireDuration = valuesDictionary.GetValue<float>("FireDuration", 12f);
			BurnRadius = valuesDictionary.GetValue<float>("BurnRadius", 50f);
			BurnProbability = valuesDictionary.GetValue<float>("BurnProbability", 0.5f);
			FireCooldown = valuesDictionary.GetValue<float>("FireCooldown", 30f);

			m_hasSavedBaseValues = valuesDictionary.GetValue<bool>("HasSavedBaseValues", false);

			if (m_hasSavedBaseValues)
			{
				m_baseAttackResilienceFactor = valuesDictionary.GetValue<float>("BaseAttackResilienceFactor");
				m_baseAttackPower = valuesDictionary.GetValue<float>("BaseAttackPower");

				if (EvolutionLevel > 0)
				{
					m_pendingStatRestoration = true;
				}
			}
			else
			{
				m_baseAttackResilienceFactor = m_componentHealth.AttackResilienceFactor;
				m_baseAttackPower = m_componentMiner.AttackPower;
			}

			if (EvolutionLevel > MaxEvolutionLevel)
			{
				EvolutionLevel = MaxEvolutionLevel;
			}

			m_lastFireBurstTime = m_subsystemTime.GameTime;
		}

		// ===== SAVE =====
		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue("EvolutionLevel", EvolutionLevel);

			valuesDictionary.SetValue("HasSavedBaseValues", true);
			valuesDictionary.SetValue("BaseAttackResilienceFactor", m_baseAttackResilienceFactor);
			valuesDictionary.SetValue("BaseAttackPower", m_baseAttackPower);
		}

		// ===== DISPOSE =====
		public override void Dispose()
		{
			if (m_particleSystem != null)
			{
				m_particleSystem.Stopped = true;
				m_particleSystem = null;
			}
			base.Dispose();
		}
	}
}
