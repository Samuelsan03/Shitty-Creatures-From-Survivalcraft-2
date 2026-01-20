using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemTeaAntifluBucketBlockBehavior : SubsystemBlockBehavior
	{
		public SubsystemAudio m_subsystemAudio;
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemPlayers m_subsystemPlayers;

		public override int[] HandledBlocks
		{
			get { return new int[] { TeaAntifluBucketBlock.Index }; }
		}

		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
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

			m_subsystemAudio.PlaySound("Audio/UI/drinking", 1f, 0f, 0f, 0f);

			bool curedFlu = CureFlu(componentPlayer);
			bool restoredHealth = RestoreHealth(componentPlayer);

			if (curedFlu)
			{
				// Intentar obtener el mensaje del sistema de idiomas
				string fluMessage;
				try
				{
					fluMessage = LanguageControl.Get("Messages", "FluCured");
				}
				catch
				{
					// Si falla, usar mensaje por defecto en inglés
					fluMessage = "Flu has been cured!";
				}
				componentPlayer.ComponentGui.DisplaySmallMessage(fluMessage, new Color(102, 178, 255), true, false);
			}

			IInventory inventory = componentMiner.Inventory;
			if (inventory != null)
			{
				int activeSlotIndex = inventory.ActiveSlotIndex;
				inventory.RemoveSlotItems(activeSlotIndex, 1);
				inventory.AddSlotItems(activeSlotIndex, EmptyBucketBlock.Index, 1);
			}

			return true;
		}

		private bool CureFlu(ComponentPlayer player)
		{
			if (player == null) return false;

			ComponentFlu flu = player.Entity.FindComponent<ComponentFlu>();
			if (flu != null && flu.HasFlu)
			{
				// Resetear todos los parámetros de la gripa
				flu.m_fluDuration = 0f;
				flu.m_fluOnset = 0f;
				flu.m_coughDuration = 0f;
				flu.m_sneezeDuration = 0f;
				flu.m_blackoutDuration = 0f;
				flu.m_blackoutFactor = 0f;

				// Limpiar blackout de la pantalla
				player.ComponentScreenOverlays.BlackoutFactor = MathUtils.Max(0f, player.ComponentScreenOverlays.BlackoutFactor);
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
