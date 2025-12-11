using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentPlayerPoisonImmunity : Component, IUpdateable
	{
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public void Update(float dt)
		{
			if (this.m_componentPlayer == null)
				return;

			// Verificar si está en modo creativo o supervivencia desactivada
			bool isCreativeOrDisabled =
				this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
				!this.m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled;

			if (isCreativeOrDisabled)
			{
				// Limpiar efectos existentes
				ClearExistingEffects();
			}
		}

		private void ClearExistingEffects()
		{
			// Limpiar enfermedad del jugador (Sickness)
			if (this.m_componentSickness != null && this.m_componentSickness.IsSick)
			{
				this.m_componentSickness.m_sicknessDuration = 0f;
			}

			// Limpiar infección por veneno (PoisonInfected)
			if (this.m_componentPoisonInfected != null && this.m_componentPoisonInfected.IsInfected)
			{
				this.m_componentPoisonInfected.m_InfectDuration = 0f;
			}
		}

		// Método que puede ser llamado por ComponentPoisonInfectedBehavior ANTES de aplicar veneno
		public bool CanReceivePoison(float poisonIntensity)
		{
			if (this.m_componentPlayer == null)
				return true;

			// Si está en modo creativo, NO puede recibir veneno
			if (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
				!this.m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
			{
				return false;
			}

			return true;
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_componentPlayer = base.Entity.FindComponent<ComponentPlayer>(true);
			this.m_componentSickness = base.Entity.FindComponent<ComponentSickness>();
			this.m_componentPoisonInfected = base.Entity.FindComponent<ComponentPoisonInfected>();
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			// No es necesario guardar estado
		}

		private SubsystemGameInfo m_subsystemGameInfo;
		private ComponentPlayer m_componentPlayer;
		private ComponentSickness m_componentSickness;
		private ComponentPoisonInfected m_componentPoisonInfected;
	}
}
