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

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>();

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

			// Contar monedas nucleares en el inventario del jugador
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

			// Descontar las monedas
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

			// Marcar como contratado
			IsHired = true;

			// Mensaje de éxito y sonido
			string successMessage = LanguageControl.GetContentWidgets("HireWidget", "SuccessMessage");
			buyer.ComponentGui.DisplaySmallMessage(successMessage, Color.Green, false, false);
			m_subsystemAudio.PlaySound("Audio/UI/money", 1f, 0f, 0f, 0f);

			return true;
		}

		public void Update(float dt)
		{
			// Sin actualizaciones periódicas necesarias
		}
	}
}
