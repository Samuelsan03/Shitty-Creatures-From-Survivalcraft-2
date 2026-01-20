using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAntidoteBucketBlockBehavior : SubsystemBlockBehavior
	{
		public SubsystemAudio m_subsystemAudio;
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemPlayers m_subsystemPlayers;

		public override int[] HandledBlocks
		{
			get { return new int[] { AntidoteBucketBlock.Index }; } // Usar la constante Index de AntidoteBucketBlock
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			// Solo funciona para jugadores en modos de supervivencia
			if (m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
				!m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
			{
				return false;
			}

			ComponentPlayer componentPlayer = componentMiner?.ComponentPlayer;
			if (componentPlayer == null)
			{
				return false;
			}

			// Reproducir sonido de bebida
			m_subsystemAudio.PlaySound("Audio/UI/drinking", 1f, 0f, 0f, 0f);

			// Curar efectos negativos (sin gripa/flu)
			bool curedPoison = CurePoison(componentPlayer);
			bool curedSickness = CureSickness(componentPlayer);
			bool restoredHealth = RestoreHealth(componentPlayer);

			// Mostrar mensajes separados con colores específicos usando LanguageControl
			if (curedPoison)
			{
				string poisonMessage = LanguageControl.Get("Messages", "PoisonCured");
				componentPlayer.ComponentGui.DisplaySmallMessage(poisonMessage, new Color(0, 255, 0), true, false); // Verde
			}

			if (curedSickness)
			{
				string sicknessMessage = LanguageControl.Get("Messages", "SicknessCured");
				componentPlayer.ComponentGui.DisplaySmallMessage(sicknessMessage, new Color(0, 255, 0), true, false); // Verde
			}

			// Restaurar salud al 100% (sin mensaje)

			// Reemplazar el AntidoteBucketBlock con un EmptyBucketBlock
			IInventory inventory = componentMiner.Inventory;
			if (inventory != null)
			{
				int activeSlotIndex = inventory.ActiveSlotIndex;
				inventory.RemoveSlotItems(activeSlotIndex, 1);
				inventory.AddSlotItems(activeSlotIndex, EmptyBucketBlock.Index, 1);
			}

			return true;
		}

		private bool CurePoison(ComponentPlayer player)
		{
			if (player == null) return false;

			ComponentPoisonInfected poison = player.Entity.FindComponent<ComponentPoisonInfected>();
			if (poison != null && poison.IsInfected)
			{
				// Usar métodos alternativos para curar el veneno
				System.Reflection.MethodInfo method = typeof(ComponentPoisonInfected).GetMethod("StartInfect",
					System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

				if (method != null)
				{
					// Si existe el método StartInfect, pasarle 0 para curar
					method.Invoke(poison, new object[] { 0f });
				}
				else
				{
					// Usar reflexión para campos privados como fallback
					System.Reflection.FieldInfo infectField = typeof(ComponentPoisonInfected).GetField("m_InfectDuration",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

					if (infectField != null)
					{
						infectField.SetValue(poison, 0f);
					}
				}

				// Aumentar resistencia
				poison.PoisonResistance = MathUtils.Max(poison.PoisonResistance, 50f);

				return true;
			}
			return false;
		}

		private bool CureSickness(ComponentPlayer player)
		{
			if (player == null) return false;

			ComponentSickness sickness = player.Entity.FindComponent<ComponentSickness>();
			if (sickness != null && sickness.IsSick)
			{
				sickness.m_sicknessDuration = 0f;
				sickness.m_greenoutDuration = 0f;
				sickness.m_greenoutFactor = 0f;

				// Limpiar efecto verde en pantalla
				player.ComponentScreenOverlays.GreenoutFactor = 0f;
				return true;
			}
			return false;
		}

		private bool RestoreHealth(ComponentPlayer player)
		{
			if (player == null) return false;

			ComponentHealth health = player.Entity.FindComponent<ComponentHealth>();
			if (health != null && health.Health < 1f)
			{
				// Restaurar salud al 100%
				float missingHealth = 1f - health.Health;
				health.Heal(missingHealth);

				return true;
			}
			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);

			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
		}

		public override void Save(ValuesDictionary valuesDictionary)
		{
			base.Save(valuesDictionary);
		}
	}
}
