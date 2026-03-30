using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewVitalStats : ComponentVitalStats
	{
		public float Thirst { get; set; } = 1f;
		private float m_lastThirst = 1f;
		private ValueBarWidget m_thirstBarWidget;
		private ComponentGui m_componentGui;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_componentGui = m_componentPlayer?.ComponentGui;

			Thirst = valuesDictionary.GetValue<float>("Thirst", 1f);
			m_lastThirst = Thirst;

			CreateThirstBarWidget();
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			valuesDictionary.SetValue("Thirst", Thirst);
		}

		private void CreateThirstBarWidget()
		{
			if (m_componentGui?.FoodBarWidget == null) return;

			var foodBar = m_componentGui.FoodBarWidget;
			m_thirstBarWidget = new ValueBarWidget
			{
				BarSize = foodBar.BarSize,
				BarsCount = foodBar.BarsCount,
				Spacing = foodBar.Spacing,
				LitBarColor = new Color(64, 164, 255),
				UnlitBarColor = new Color(48, 48, 48),
				BarSubtexture = ContentManager.Get<Subtexture>("Textures/Gui/agua"),
				TextureLinearFilter = true,
				HalfBars = false,
				LayoutDirection = LayoutDirection.Horizontal,
				Value = Thirst
			};

			var parent = foodBar.ParentWidget as ContainerWidget;
			if (parent != null)
			{
				int index = parent.Children.IndexOf(foodBar);
				parent.Children.Insert(index + 1, m_thirstBarWidget);
			}
		}

		public virtual bool Drink(int value)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			if (!(block is BoiledWaterBlock))
				return false;

			if (Thirst >= 0.99f)
			{
				m_componentGui?.DisplaySmallMessage(
					LanguageControl.Get("ComponentThirst", "AlreadyNotThirsty"),
					Color.White, true, true);
				return false;
			}

			Thirst = 1f;
			m_lastThirst = Thirst;

			m_subsystemAudio?.PlaySound("Audio/UI/drinking", 1f, 0f, 0f, 0f);

			m_componentGui?.DisplaySmallMessage(
				LanguageControl.Get("ComponentThirst", "DrankBoiledWater"),
				Color.White, true, false);

			ReplaceHeldItemWithEmptyBucket();

			return true;
		}

		private void ReplaceHeldItemWithEmptyBucket()
		{
			var inventory = m_componentPlayer?.ComponentMiner?.Inventory;
			if (inventory == null) return;

			int slot = inventory.ActiveSlotIndex;
			int count = inventory.GetSlotCount(slot);
			if (count > 0)
			{
				int emptyBucketIndex = BlocksManager.GetBlockIndex<EmptyBucketBlock>(throwIfNotFound: true);
				int emptyBucketValue = Terrain.MakeBlockValue(emptyBucketIndex, 0, 0);
				inventory.RemoveSlotItems(slot, count);
				inventory.AddSlotItems(slot, emptyBucketValue, 1);
			}
		}

		public override bool Eat(int value)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			if (block is BoiledWaterBlock)
			{
				return Drink(value);
			}
			return base.Eat(value);
		}

		public override void Update(float dt)
		{
			base.Update(dt);

			if (m_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative &&
				m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
			{
				float delta = m_subsystemTime.GameTimeDelta;
				float hungerFactor = m_componentPlayer?.ComponentLevel?.HungerFactor ?? 1f;

				Thirst -= hungerFactor * delta / 2880f;

				Vector2? walk = m_componentPlayer?.ComponentLocomotion?.LastWalkOrder;
				float move = walk.HasValue ? walk.Value.Length() : 0f;
				Thirst -= hungerFactor * delta * move / 2880f;

				Thirst = Math.Clamp(Thirst, 0f, 1f);

				bool periodic = m_subsystemTime.PeriodicGameTimeEvent(240.0, 9.0);
				if (Thirst <= 0f)
				{
					if (m_subsystemTime.PeriodicGameTimeEvent(50.0, 0.0))
					{
						m_componentPlayer?.ComponentHealth.Injure(0.05f, null, false,
							LanguageControl.Get("ComponentThirst", "Dehydrating"));
						m_componentGui?.DisplaySmallMessage(
							LanguageControl.Get("ComponentThirst", "Dehydrating"),
							Color.White, true, false);
						m_thirstBarWidget?.Flash(10);
					}
				}
				else if (Thirst < 0.1f && (m_lastThirst >= 0.1f || periodic))
				{
					m_componentGui?.DisplaySmallMessage(
						LanguageControl.Get("ComponentThirst", "Dehydrating"),
						Color.White, true, true);
				}
				else if (Thirst < 0.25f && (m_lastThirst >= 0.25f || periodic))
				{
					m_componentGui?.DisplaySmallMessage(
						LanguageControl.Get("ComponentThirst", "HalfThirsty"),
						Color.White, true, false);
				}

				m_lastThirst = Thirst;
			}
			else
			{
				Thirst = 1f;
			}

			if (m_thirstBarWidget != null)
				m_thirstBarWidget.Value = Thirst;
		}
	}
}
