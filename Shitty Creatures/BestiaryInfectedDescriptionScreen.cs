using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class BestiaryInfectedDescriptionScreen : Screen
	{
		private ModelWidget m_modelWidget;
		private LabelWidget m_nameWidget;
		private ButtonWidget m_leftButtonWidget;
		private ButtonWidget m_rightButtonWidget;
		private LabelWidget m_descriptionWidget;
		private LabelWidget m_propertyNames1Widget;
		private LabelWidget m_propertyValues1Widget;
		private LabelWidget m_propertyNames2Widget;
		private LabelWidget m_propertyValues2Widget;
		private ContainerWidget m_dropsPanel;
		private int m_index;
		private IList<BestiaryCreatureInfo> m_infoList;

		public BestiaryInfectedDescriptionScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/BestiaryInfectedDescriptionScreen");
			LoadContents(this, node);
			m_modelWidget = Children.Find<ModelWidget>("Model", true);
			m_nameWidget = Children.Find<LabelWidget>("Name", true);
			m_leftButtonWidget = Children.Find<ButtonWidget>("Left", true);
			m_rightButtonWidget = Children.Find<ButtonWidget>("Right", true);
			m_descriptionWidget = Children.Find<LabelWidget>("Description", true);
			m_propertyNames1Widget = Children.Find<LabelWidget>("PropertyNames1", true);
			m_propertyValues1Widget = Children.Find<LabelWidget>("PropertyValues1", true);
			m_propertyNames2Widget = Children.Find<LabelWidget>("PropertyNames2", true);
			m_propertyValues2Widget = Children.Find<LabelWidget>("PropertyValues2", true);
			m_dropsPanel = Children.Find<ContainerWidget>("Drops", true);
		}

		public override void Enter(object[] parameters)
		{
			BestiaryCreatureInfo item = (BestiaryCreatureInfo)parameters[0];
			m_infoList = (IList<BestiaryCreatureInfo>)parameters[1];
			m_index = m_infoList.IndexOf(item);
			UpdateCreatureProperties();
		}

		public override void Update()
		{
			m_leftButtonWidget.IsEnabled = (m_index > 0);
			m_rightButtonWidget.IsEnabled = (m_index < m_infoList.Count - 1);
			if (m_leftButtonWidget.IsClicked || Input.Left)
			{
				m_index = Math.Max(m_index - 1, 0);
				UpdateCreatureProperties();
			}
			if (m_rightButtonWidget.IsClicked || Input.Right)
			{
				m_index = Math.Min(m_index + 1, m_infoList.Count - 1);
				UpdateCreatureProperties();
			}
			if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
			{
				ScreensManager.SwitchScreen(ScreensManager.PreviousScreen, Array.Empty<object>());
			}
		}

		public virtual void UpdateCreatureProperties()
		{
			if (m_index < 0 || m_index >= m_infoList.Count)
				return;

			BestiaryCreatureInfo info = m_infoList[m_index];
			m_modelWidget.AutoRotationVector = new Vector3(0f, 1f, 0f);
			BestiaryScreen.SetupBestiaryModelWidget(info, m_modelWidget, new Vector3(-1f, 0f, -1f), true, true);
			m_nameWidget.Text = info.DisplayName;
			m_descriptionWidget.Text = info.Description;

			// Propiedades 1
			m_propertyNames1Widget.Text = string.Empty;
			m_propertyValues1Widget.Text = string.Empty;
			AddProperty("resilience", info.AttackResilience.ToString("0.0"));
			AddProperty("attack", info.AttackPower > 0f ? info.AttackPower.ToString("0.0") : LanguageControl.None);
			AddProperty("herding", info.IsHerding ? LanguageControl.Yes : LanguageControl.No);
			AddProperty("rideable", info.CanBeRidden ? LanguageControl.Yes : LanguageControl.No);
			m_propertyNames1Widget.Text = m_propertyNames1Widget.Text.TrimEnd();
			m_propertyValues1Widget.Text = m_propertyValues1Widget.Text.TrimEnd();

			// Propiedades 2
			m_propertyNames2Widget.Text = string.Empty;
			m_propertyValues2Widget.Text = string.Empty;
			AddProperty2("speed", (info.MovementSpeed * 3.6).ToString("0") + LanguageControl.Get("BestiaryDescriptionScreen", "speed unit"));
			AddProperty2("jump height", info.JumpHeight.ToString("0.0") + LanguageControl.Get("BestiaryDescriptionScreen", "length unit"));
			AddProperty2("weight", info.Mass.ToString() + LanguageControl.Get("BestiaryDescriptionScreen", "weight unit"));
			AddProperty2("egg", info.HasSpawnerEgg ? LanguageControl.Exists : LanguageControl.None);
			m_propertyNames2Widget.Text = m_propertyNames2Widget.Text.TrimEnd();
			m_propertyValues2Widget.Text = m_propertyValues2Widget.Text.TrimEnd();

			// Loot
			m_dropsPanel.Children.Clear();
			if (info.Loot.Count > 0)
			{
				foreach (var loot in info.Loot)
				{
					if (loot.MaxCount == 0 || loot.Probability == 0f)
						continue;
					string countText;
					if (loot.MinCount < loot.MaxCount)
						countText = string.Format(LanguageControl.Get("BestiaryDescriptionScreen", "range"), loot.MinCount, loot.MaxCount);
					else
						countText = loot.MinCount.ToString();
					if (loot.Probability < 1f)
						countText += string.Format(LanguageControl.Get("BestiaryDescriptionScreen", 2), (loot.Probability * 100f).ToString("0"));

					m_dropsPanel.Children.Add(new StackPanelWidget
					{
						Margin = new Vector2(20f, 0f),
						Children =
						{
							new BlockIconWidget { Size = new Vector2(32f), Scale = 1.2f, VerticalAlignment = WidgetAlignment.Center, Value = loot.Value },
							new CanvasWidget { Size = new Vector2(10f, 0f) },
							new LabelWidget { VerticalAlignment = WidgetAlignment.Center, Text = countText }
						}
					});
				}
			}
			else
			{
				m_dropsPanel.Children.Add(new LabelWidget { Margin = new Vector2(20f, 0f), Text = LanguageControl.Nothing });
			}
		}

		private void AddProperty(string key, string value)
		{
			m_propertyNames1Widget.Text += LanguageControl.Get("BestiaryDescriptionScreen", key) + "\n";
			m_propertyValues1Widget.Text += value + "\n";
		}

		private void AddProperty2(string key, string value)
		{
			m_propertyNames2Widget.Text += LanguageControl.Get("BestiaryDescriptionScreen", key) + "\n";
			m_propertyValues2Widget.Text += value + "\n";
		}
	}
}
