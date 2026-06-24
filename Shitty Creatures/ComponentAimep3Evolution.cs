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

		// ===== CONFIGURACIÓN DE FUEGO =====
		public float FireDuration { get; set; } = 12f;
		public float BurnRadius { get; set; } = 50f;
		public float BurnProbability { get; set; } = 0.5f;
		public float FireCooldown { get; set; } = 30f;

		// ===== PROPIEDADES CALCULADAS =====
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

		// ===== MÉTODO AUXILIAR: Validar y capturar valores base =====
		/// <summary>
		/// Asegura que los valores base sean válidos (> 0). Si no lo son, los captura de las estadísticas actuales.
		/// </summary>
		private void EnsureValidBaseValues()
		{
			if (m_componentHealth == null || m_componentMiner == null)
				return;

			bool needsCapture = false;

			// Si los valores base son inválidos (0 o negativos), necesitamos recapturarlos
			if (m_baseAttackResilienceFactor <= 0f)
			{
				m_baseAttackResilienceFactor = m_componentHealth.AttackResilienceFactor;
				needsCapture = true;
			}

			if (m_baseAttackPower <= 0f)
			{
				m_baseAttackPower = m_componentMiner.AttackPower;
				needsCapture = true;
			}

			if (needsCapture)
			{
				m_hasSavedBaseValues = true;
			}
		}

		// ===== MÉTODOS PÚBLICOS =====
		public bool TryEvolve()
		{
			if (EvolutionLevel >= MaxEvolutionLevel || m_isEvolving)
				return false;

			if (m_componentHealth == null || m_componentHealth.Health <= 0f)
				return false;

			// ===== FIX PRINCIPAL: Siempre validar valores base antes de evolucionar =====
			if (!m_hasSavedBaseValues || m_baseAttackResilienceFactor <= 0f || m_baseAttackPower <= 0f)
			{
				m_baseAttackResilienceFactor = m_componentHealth.AttackResilienceFactor;
				m_baseAttackPower = m_componentMiner.AttackPower;
				m_hasSavedBaseValues = true;

				// Log de debug (puedes comentarlo después)
				Log.Information($"[Aimep3Evolution] Valores base capturados - Resilience: {m_baseAttackResilienceFactor}, Attack: {m_baseAttackPower}");
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

			// ===== FIX: Validar valores base antes de aplicar =====
			EnsureValidBaseValues();

			float multiplier = (float)Math.Pow(EvolutionMultiplier, EvolutionLevel + 1);

			m_componentHealth.AttackResilienceFactor = m_baseAttackResilienceFactor * multiplier;
			m_componentMiner.AttackPower = m_baseAttackPower * multiplier;

			float currentHealth = m_componentHealth.Health;
			m_componentHealth.Health = MathUtils.Saturate(currentHealth + 0.2f);

			EvolutionLevel++;

			// 🔁 Notificar logros
			AchievementsManager.OnAimep3Evolution(Project, EvolutionLevel, MaxEvolutionLevel);

			NotifyPlayers();
			m_pendingEvolution = false;
		}

		private void RecalculateStats()
		{
			if (m_componentHealth == null || m_componentMiner == null)
				return;

			// ===== FIX: Validar valores base antes de recalcular =====
			EnsureValidBaseValues();

			float multiplier = (float)Math.Pow(EvolutionMultiplier, EvolutionLevel);
			m_componentHealth.AttackResilienceFactor = m_baseAttackResilienceFactor * multiplier;
			m_componentMiner.AttackPower = m_baseAttackPower * multiplier;

			Log.Information($"[Aimep3Evolution] Stats recalculados - Nivel: {EvolutionLevel}, Multiplicador: {multiplier:F2}");
		}

		private void TryIgniteNearbyCreatures()
		{
			if (!HasFireAbility)
				return;

			if (m_componentCreature?.ComponentBody == null)
				return;

			if (m_componentHealth.Health <= 0f)
				return;

			if (m_subsystemTime.GameTime - m_lastFireBurstTime < FireCooldown)
				return;

			// Solo quemar si estamos persiguiendo a alguien
			ComponentNewChaseBehavior chaseBehavior = Entity.FindComponent<ComponentNewChaseBehavior>();
			if (chaseBehavior == null || chaseBehavior.Target == null)
				return;

			ComponentCreature target = chaseBehavior.Target;

			// Verificar que el objetivo sigue vivo
			if (target.ComponentHealth == null || target.ComponentHealth.Health <= 0f)
				return;

			// Verificar distancia
			Vector3 position = m_componentCreature.ComponentBody.Position;
			float distSq = Vector3.DistanceSquared(position, target.ComponentBody.Position);
			if (distSq > BurnRadius * BurnRadius)
				return;

			// Verificar que no es aliado
			if (m_componentHerd != null && m_componentHerd.IsSameHerdOrGuardian(target))
				return;

			// Probabilidad de quemar
			if (m_random.Float(0f, 1f) > BurnProbability)
				return;

			ComponentOnFire componentOnFire = target.Entity.FindComponent<ComponentOnFire>();
			if (componentOnFire != null)
			{
				componentOnFire.SetOnFire(m_componentCreature, EffectiveFireDuration);

				if (m_subsystemAudio != null)
				{
					m_subsystemAudio.PlaySound("Audio/Match", 1f, m_random.Float(-0.1f, 0.1f), position, 1f, true);
				}
			}

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

				// ===== FIX: Validar valores base antes de restaurar =====
				EnsureValidBaseValues();

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

			// ===== FIX PRINCIPAL EN LOAD =====
			m_hasSavedBaseValues = valuesDictionary.GetValue<bool>("HasSavedBaseValues", false);

			if (m_hasSavedBaseValues)
			{
				m_baseAttackResilienceFactor = valuesDictionary.GetValue<float>("BaseAttackResilienceFactor", 0f);
				m_baseAttackPower = valuesDictionary.GetValue<float>("BaseAttackPower", 0f);

				// ===== FIX: Validar que los valores cargados sean válidos =====
				// Si son 0 o negativos, indica un save corrupto o de una versión anterior
				if (m_baseAttackResilienceFactor <= 0f || m_baseAttackPower <= 0f)
				{
					Log.Warning($"[Aimep3Evolution] Valores base inválidos detectados al cargar (Resilience: {m_baseAttackResilienceFactor}, Attack: {m_baseAttackPower}). Recapturando...");

					// Recapturar valores base reales de las estadísticas actuales
					m_baseAttackResilienceFactor = m_componentHealth.AttackResilienceFactor;
					m_baseAttackPower = m_componentMiner.AttackPower;
					m_hasSavedBaseValues = true;
				}

				if (EvolutionLevel > 0)
				{
					m_pendingStatRestoration = true;
				}
			}
			else
			{
				// No hay valores base guardados, capturarlos ahora
				m_baseAttackResilienceFactor = m_componentHealth.AttackResilienceFactor;
				m_baseAttackPower = m_componentMiner.AttackPower;
				m_hasSavedBaseValues = true;
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

			// ===== FIX PRINCIPAL EN SAVE: Solo guardar si realmente tenemos valores válidos =====
			if (m_hasSavedBaseValues && m_baseAttackResilienceFactor > 0f && m_baseAttackPower > 0f)
			{
				valuesDictionary.SetValue("HasSavedBaseValues", true);
				valuesDictionary.SetValue("BaseAttackResilienceFactor", m_baseAttackResilienceFactor);
				valuesDictionary.SetValue("BaseAttackPower", m_baseAttackPower);
			}
			else
			{
				// No guardar valores base inválidos
				valuesDictionary.SetValue("HasSavedBaseValues", false);
			}
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
