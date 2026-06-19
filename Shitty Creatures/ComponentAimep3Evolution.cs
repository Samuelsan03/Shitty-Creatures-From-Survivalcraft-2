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

		// ★ CAMBIADO: De const a propiedad con valor por defecto
		public int MaxEvolutionLevel { get; private set; } = 10;

		public bool HasEvolved => EvolutionLevel > 0;
		public float EvolutionMultiplier { get; set; } = 1.5f;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// ===== CAMPOS PRIVADOS =====
		private SubsystemParticles m_subsystemParticles;
		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;
		private ComponentCreature m_componentCreature;
		private ComponentHealth m_componentHealth;
		private ComponentMiner m_componentMiner;

		// Valores BASE (sin multiplicar) para poder recalcular al cargar
		private float m_baseAttackResilienceFactor;
		private float m_baseAttackPower;

		private ShapeshiftParticleSystem m_particleSystem;
		private double m_evolutionParticleStartTime;
		private bool m_isEvolving;
		private bool m_pendingEvolution;
		private const float EvolutionParticleDuration = 2.5f;

		// Para restauración diferida al cargar
		private bool m_pendingStatRestoration = false;
		private bool m_hasSavedBaseValues = false;

		// ===== MÉTODOS PÚBLICOS =====
		public bool TryEvolve()
		{
			// ★ Ahora usa la propiedad que se carga del XML
			if (EvolutionLevel >= MaxEvolutionLevel || m_isEvolving)
				return false;

			if (m_componentHealth == null || m_componentHealth.Health <= 0f)
				return false;

			// Capturar valores base si no se han capturado aún
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
			// ★ Usa la propiedad en lugar de la constante
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

			SubsystemAudio audio = Project.FindSubsystem<SubsystemAudio>(true);
			if (audio != null && m_componentCreature.ComponentBody != null)
			{
				audio.PlaySound("Audio/Shapeshift", 1f, 0f,
					m_componentCreature.ComponentBody.Position, 5f, false);
			}
		}

		private void ApplyEvolution()
		{
			if (!m_pendingEvolution) return;

			// ★ Verificar nuevamente el límite por seguridad
			if (EvolutionLevel >= MaxEvolutionLevel)
			{
				m_pendingEvolution = false;
				m_isEvolving = false;
				return;
			}

			// Aplicar multiplicador para el NUEVO nivel
			m_componentHealth.AttackResilienceFactor = m_baseAttackResilienceFactor * (float)Math.Pow(EvolutionMultiplier, EvolutionLevel + 1);
			m_componentMiner.AttackPower = m_baseAttackPower * (float)Math.Pow(EvolutionMultiplier, EvolutionLevel + 1);

			// Curar un poco
			float currentHealth = m_componentHealth.Health;
			m_componentHealth.Health = MathUtils.Saturate(currentHealth + 0.2f);

			EvolutionLevel++;
			NotifyPlayers();
			m_pendingEvolution = false;
		}

		/// <summary>
		/// Recalcula y aplica los multiplicadores según el nivel actual
		/// </summary>
		private void RecalculateStats()
		{
			if (m_componentHealth == null || m_componentMiner == null)
				return;

			// Aplicar multiplicador según nivel actual
			float multiplier = (float)Math.Pow(EvolutionMultiplier, EvolutionLevel);
			m_componentHealth.AttackResilienceFactor = m_baseAttackResilienceFactor * multiplier;
			m_componentMiner.AttackPower = m_baseAttackPower * multiplier;
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
			// Restaurar estadísticas diferido
			if (m_pendingStatRestoration)
			{
				m_pendingStatRestoration = false;

				// Primero restaurar valores BASE
				if (m_componentMiner != null)
					m_componentMiner.AttackPower = m_baseAttackPower;
				if (m_componentHealth != null)
					m_componentHealth.AttackResilienceFactor = m_baseAttackResilienceFactor;

				// Luego aplicar multiplicadores de evolución
				RecalculateStats();
			}

			// Lógica de partículas de evolución
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
		}

		// ===== LOAD =====
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentMiner = Entity.FindComponent<ComponentMiner>(true);

			// Cargar el nivel de evolución guardado
			EvolutionLevel = valuesDictionary.GetValue<int>("EvolutionLevel", 0);

			// ★ NUEVO: Cargar MaxEvolutionLevel desde el XML (usa 10 como valor por defecto)
			MaxEvolutionLevel = valuesDictionary.GetValue<int>("MaxEvolutionLevel", 10);

			EvolutionMultiplier = valuesDictionary.GetValue<float>("EvolutionMultiplier", 1.5f);

			// Verificar si hay valores base guardados
			m_hasSavedBaseValues = valuesDictionary.GetValue<bool>("HasSavedBaseValues", false);

			if (m_hasSavedBaseValues)
			{
				// Cargar valores base guardados
				m_baseAttackResilienceFactor = valuesDictionary.GetValue<float>("BaseAttackResilienceFactor");
				m_baseAttackPower = valuesDictionary.GetValue<float>("BaseAttackPower");

				// Diferir restauración al primer Update()
				if (EvolutionLevel > 0)
				{
					m_pendingStatRestoration = true;
				}
			}
			else
			{
				// Primera vez: capturar valores actuales como base
				m_baseAttackResilienceFactor = m_componentHealth.AttackResilienceFactor;
				m_baseAttackPower = m_componentMiner.AttackPower;
			}

			// ★ NUEVO: Seguridad: si por algún motivo el nivel actual supera el máximo, ajustarlo
			if (EvolutionLevel > MaxEvolutionLevel)
			{
				EvolutionLevel = MaxEvolutionLevel;
			}
		}

		// ===== SAVE =====
		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			// Guardar nivel de evolución
			valuesDictionary.SetValue("EvolutionLevel", EvolutionLevel);
			valuesDictionary.SetValue("EvolutionMultiplier", EvolutionMultiplier);

			// Guardar valores base para prevenir doble multiplicación al cargar
			valuesDictionary.SetValue("HasSavedBaseValues", true);
			valuesDictionary.SetValue("BaseAttackResilienceFactor", m_baseAttackResilienceFactor);
			valuesDictionary.SetValue("BaseAttackPower", m_baseAttackPower);

			// ★ NOTA: No guardamos MaxEvolutionLevel porque es un valor del TEMPLATE (XML),
			// no del estado de la entidad. Siempre se lee del XML al cargar.
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
