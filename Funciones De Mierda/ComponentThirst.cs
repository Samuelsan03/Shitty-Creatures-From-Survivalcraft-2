using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentThirst : Component, IUpdateable
	{
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemTime m_subsystemTime;
		public ComponentPlayer m_componentPlayer;
		private ValueBarWidget m_waterBar;
		public float Water { get; set; } = 1f;
		public float LastWater { get; set; }
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentPlayer = Entity.FindComponent<ComponentPlayer>(true);
			Water = valuesDictionary.GetValue<float>("Water");
			LastWater = Water;
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<float>("Water", Water);
		}

		public override void OnEntityAdded()
		{
			if (m_componentPlayer?.ComponentGui != null)
			{
				CreateWaterBar();
			}
		}

		public override void OnEntityRemoved()
		{
			if (m_waterBar != null && m_componentPlayer?.ComponentGui != null)
			{
				var bottomBars = FindBottomBarsContainer();
				if (bottomBars != null && bottomBars.Children.Contains(m_waterBar))
				{
					bottomBars.Children.Remove(m_waterBar);
				}
				m_waterBar = null;
			}
		}

		private StackPanelWidget FindBottomBarsContainer()
		{
			if (m_componentPlayer?.ComponentGui?.ControlsContainerWidget == null) return null;
			return FindWidgetByName(m_componentPlayer.ComponentGui.ControlsContainerWidget, "BottomBarsContainer") as StackPanelWidget;
		}

		private Widget FindWidgetByName(ContainerWidget container, string name)
		{
			if (container == null) return null;
			foreach (var child in container.Children)
			{
				if (child.Name == name) return child;
			}
			foreach (var child in container.Children)
			{
				if (child is ContainerWidget childContainer)
				{
					var found = FindWidgetByName(childContainer, name);
					if (found != null) return found;
				}
			}
			return null;
		}

		private void CreateWaterBar()
		{
			try
			{
				var bottomBars = FindBottomBarsContainer();
				if (bottomBars == null)
				{
					Log.Error("No se encontró BottomBarsContainer");
					return;
				}
				if (bottomBars.Children.Find("WaterBar", false) != null) return;
				m_waterBar = new ValueBarWidget
				{
					Name = "WaterBar",
					LayoutDirection = LayoutDirection.Horizontal,
					Margin = new Vector2(8f, 0f),
					VerticalAlignment = WidgetAlignment.Center,
					BarsCount = 10,
					BarBlending = false,
					HalfBars = true,
					LitBarColor = new Color(0, 100, 255),
					UnlitBarColor = new Color(64, 64, 64),
					BarSize = new Vector2(15f, 16f),
					Spacing = -1.5f,
					BarSubtexture = ContentManager.Get<Subtexture>("Textures/Gui/agua"),
					TextureLinearFilter = true,
					Value = this.Water
				};
				bool shouldShow = !(m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative || !ShittyCreaturesSettingsManager.ThirstEnabled);
				m_waterBar.IsVisible = shouldShow;
				var healthBar = bottomBars.Children.Find<ValueBarWidget>("HealthBar", false);
				int insertIndex = healthBar != null ? bottomBars.Children.IndexOf(healthBar) + 1 : 0;
				bottomBars.Children.Insert(insertIndex, m_waterBar);
			}
			catch (Exception ex)
			{
				Log.Error($"Error al crear barra de agua: {ex.Message}");
			}
		}

		public void Update(float dt)
		{
			if (m_componentPlayer.ComponentHealth.Health <= 0f) return;

			bool shouldShow = !(m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative || !ShittyCreaturesSettingsManager.ThirstEnabled);
			if (!shouldShow)
			{
				if (m_waterBar != null && m_waterBar.IsVisible)
					m_waterBar.IsVisible = false;
				return;
			}
			else
			{
				if (m_waterBar != null && !m_waterBar.IsVisible)
					m_waterBar.IsVisible = true;
			}

			// Misma tasa base que el hambre: 1/2880 por segundo
			float drainRate = 1f / 2880f;
			float hungerFactor = m_componentPlayer.ComponentLevel?.HungerFactor ?? 1f;

			// Reducción base (siempre presente)
			float decrease = drainRate * hungerFactor * dt;

			// Extra por caminar (igual que el hambre)
			var loco = m_componentPlayer.ComponentLocomotion;
			if (loco.LastWalkOrder != null)
			{
				float walkSpeed = loco.LastWalkOrder.Value.Length();
				decrease += drainRate * hungerFactor * walkSpeed * dt;
			}

			// Extra por saltar (igual que el hambre: 1/1200 por salto)
			if (loco.LastJumpOrder > 0f)
			{
				decrease += hungerFactor * loco.LastJumpOrder / 1200f;
			}

			Water -= decrease;
			Water = Math.Clamp(Water, 0f, 1f);

			// Daño por deshidratación (cada 50 segundos como el hambre)
			if (Water <= 0f && !m_componentPlayer.ComponentSleep.IsSleeping && m_subsystemTime.PeriodicGameTimeEvent(50.0, 0.0))
			{
				m_componentPlayer.ComponentHealth.Injure(0.1f, null, false, "Dehydration");
				string message = LanguageControl.Get("ComponentThirst", "Dehydrating");
				m_componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.Red, true, false);
			}

			LastWater = Water;

			if (m_waterBar != null)
				m_waterBar.Value = Water;
			else
			{
				var bottomBars = FindBottomBarsContainer();
				if (bottomBars != null)
					m_waterBar = bottomBars.Children.Find<ValueBarWidget>("WaterBar", false);
			}
		}

		public bool DrinkWater(float amount)
		{
			if (Math.Round(Water, 2) >= 1.0)
			{
				string message = LanguageControl.Get("ComponentThirst", "AlreadyNotThirsty");
				if (!string.IsNullOrEmpty(message))
				{
					m_componentPlayer?.ComponentGui?.DisplaySmallMessage(message, Color.White, true, true);
				}
				return false;
			}
			Water = Math.Clamp(Water + amount, 0f, 1f);
			return true;
		}

		public bool Drink(float amount)
		{
			if (Math.Round(Water, 2) >= 1.0)
			{
				string message = LanguageControl.Get("ComponentThirst", "AlreadyNotThirsty");
				m_componentPlayer?.ComponentGui?.DisplaySmallMessage(message, Color.White, true, true);
				return false;
			}
			Water = Math.Clamp(Water + amount, 0f, 1f);
			return true;
		}
	}
}
