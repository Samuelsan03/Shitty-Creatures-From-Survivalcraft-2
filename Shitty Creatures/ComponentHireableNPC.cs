using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentHireableNPC : Component, IUpdateable
	{
		public int HirePrice { get; set; } = 100;
		public bool IsHired { get; private set; } = false;

		private SubsystemGameInfo m_subsystemGameInfo;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemPickables m_subsystemPickables;
		private ComponentHealth m_componentHealth;
		private ComponentCreatureSounds m_componentCreatureSounds; // NUEVO

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>();
			m_componentCreatureSounds = Entity.FindComponent<ComponentCreatureSounds>(); // NUEVO

			HirePrice = valuesDictionary.GetValue<int>("HirePrice", 100);
			IsHired = valuesDictionary.GetValue<bool>("IsHired", false);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			valuesDictionary.SetValue("IsHired", IsHired);
		}

		public bool TryHire(ComponentPlayer buyer)
		{
			if (m_componentHealth != null && m_componentHealth.Health <= 0f)
			{
				string message = LanguageControl.GetContentWidgets("HireWidget", "DeadMessage");
				buyer.ComponentGui.DisplaySmallMessage(message, Color.Red, true, false);
				m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
				return false;
			}

			int coinIndex = BlocksManager.GetBlockIndex(typeof(NuclearCoinBlock).Name, true);
			var inventory = buyer.ComponentMiner.Inventory;

			int totalCoins = 0;
			for (int i = 0; i < inventory.SlotsCount; i++)
			{
				int value = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(value) == coinIndex)
				{
					totalCoins += inventory.GetSlotCount(i);
				}
			}

			if (totalCoins < HirePrice)
			{
				string format = LanguageControl.GetContentWidgets("HireWidget", "NeedCoinsMessage");
				string message = string.Format(format, HirePrice);
				buyer.ComponentGui.DisplaySmallMessage(message, Color.Red, true, false);
				m_subsystemAudio.PlaySound("Audio/UI/warning", 1f, 0f, 0f, 0f);
				return false;
			}

			int remainingToRemove = HirePrice;
			for (int i = 0; i < inventory.SlotsCount && remainingToRemove > 0; i++)
			{
				int value = inventory.GetSlotValue(i);
				if (Terrain.ExtractContents(value) == coinIndex)
				{
					int count = inventory.GetSlotCount(i);
					int remove = MathUtils.Min(count, remainingToRemove);
					inventory.RemoveSlotItems(i, remove);
					remainingToRemove -= remove;
				}
			}

			IsHired = true;

			// Reproducir sonido idle (feliz) del NPC
			if (m_componentCreatureSounds != null)
			{
				m_componentCreatureSounds.PlayIdleSound(false); // false asegura que suene si ha pasado al menos 1s
			}

			string successMessage = LanguageControl.GetContentWidgets("HireWidget", "SuccessMessage");
			buyer.ComponentGui.DisplaySmallMessage(successMessage, Color.Green, false, false);
			m_subsystemAudio.PlaySound("Audio/UI/money", 1f, 0f, 0f, 0f);

			return true;
		}

		public void Update(float dt)
		{
		}
	}
}
