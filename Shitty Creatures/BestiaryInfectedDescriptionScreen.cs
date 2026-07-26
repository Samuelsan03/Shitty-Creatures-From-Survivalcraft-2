using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class BestiaryInfectedDescriptionScreen : Screen
	{
		public ModelWidget m_modelWidget;
		public LabelWidget m_nameWidget;
		public ButtonWidget m_leftButtonWidget;
		public ButtonWidget m_rightButtonWidget;
		public LabelWidget m_descriptionWidget;
		public LabelWidget m_propertyNames1Widget;
		public LabelWidget m_propertyValues1Widget;
		public LabelWidget m_propertyNames2Widget;
		public LabelWidget m_propertyValues2Widget;
		public ContainerWidget m_dropsPanel;
		public int m_index;
		public IList<BestiaryCreatureInfo> m_infoList;
		public const string fName = "BestiaryInfectedDescriptionScreen";

		public BestiaryInfectedDescriptionScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/BestiaryInfectedDescriptionScreen");
			this.LoadContents(this, node);
			this.m_modelWidget = this.Children.Find<ModelWidget>("Model", true);
			this.m_nameWidget = this.Children.Find<LabelWidget>("Name", true);
			this.m_leftButtonWidget = this.Children.Find<ButtonWidget>("Left", true);
			this.m_rightButtonWidget = this.Children.Find<ButtonWidget>("Right", true);
			this.m_descriptionWidget = this.Children.Find<LabelWidget>("Description", true);
			this.m_propertyNames1Widget = this.Children.Find<LabelWidget>("PropertyNames1", true);
			this.m_propertyValues1Widget = this.Children.Find<LabelWidget>("PropertyValues1", true);
			this.m_propertyNames2Widget = this.Children.Find<LabelWidget>("PropertyNames2", true);
			this.m_propertyValues2Widget = this.Children.Find<LabelWidget>("PropertyValues2", true);
			this.m_dropsPanel = this.Children.Find<ContainerWidget>("Drops", true);
		}

		public override void Enter(object[] parameters)
		{
			BestiaryCreatureInfo item = (BestiaryCreatureInfo)parameters[0];
			this.m_infoList = (IList<BestiaryCreatureInfo>)parameters[1];
			this.m_index = this.m_infoList.IndexOf(item);
			this.UpdateCreatureProperties();
		}

		public override void Update()
		{
			this.m_leftButtonWidget.IsEnabled = (this.m_index > 0);
			this.m_rightButtonWidget.IsEnabled = (this.m_index < this.m_infoList.Count - 1);
			if (this.m_leftButtonWidget.IsClicked || base.Input.Left)
			{
				this.m_index = Math.Max(this.m_index - 1, 0);
				this.UpdateCreatureProperties();
			}
			if (this.m_rightButtonWidget.IsClicked || base.Input.Right)
			{
				this.m_index = Math.Min(this.m_index + 1, this.m_infoList.Count - 1);
				this.UpdateCreatureProperties();
			}
			if (base.Input.Back || base.Input.Cancel || this.Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
			{
				ScreensManager.SwitchScreen(ScreensManager.PreviousScreen, Array.Empty<object>());
			}
		}

		public virtual void UpdateCreatureProperties()
		{
			if (this.m_index >= 0 && this.m_index < this.m_infoList.Count)
			{
				BestiaryCreatureInfo bestiaryCreatureInfo = this.m_infoList[this.m_index];
				this.m_modelWidget.AutoRotationVector = new Vector3(0f, 1f, 0f);
				BestiaryScreen.SetupBestiaryModelWidget(bestiaryCreatureInfo, this.m_modelWidget, new Vector3(-1f, 0f, -1f), true, true);
				this.m_nameWidget.Text = bestiaryCreatureInfo.DisplayName;
				this.m_descriptionWidget.Text = bestiaryCreatureInfo.Description;
				this.m_propertyNames1Widget.Text = string.Empty;
				this.m_propertyValues1Widget.Text = string.Empty;
				LabelWidget propertyNames1Widget = this.m_propertyNames1Widget;
				propertyNames1Widget.Text += LanguageControl.Get("BestiaryDescriptionScreen", "resilience");
				LabelWidget propertyValues1Widget = this.m_propertyValues1Widget;
				propertyValues1Widget.Text = propertyValues1Widget.Text + bestiaryCreatureInfo.AttackResilience.ToString("0.0") + "\n";
				LabelWidget propertyNames1Widget2 = this.m_propertyNames1Widget;
				propertyNames1Widget2.Text += LanguageControl.Get("BestiaryDescriptionScreen", "attack");
				LabelWidget propertyValues1Widget2 = this.m_propertyValues1Widget;
				propertyValues1Widget2.Text = propertyValues1Widget2.Text + ((bestiaryCreatureInfo.AttackPower > 0f) ? bestiaryCreatureInfo.AttackPower.ToString("0.0") : LanguageControl.None) + "\n";
				LabelWidget propertyNames1Widget3 = this.m_propertyNames1Widget;
				propertyNames1Widget3.Text += LanguageControl.Get("BestiaryDescriptionScreen", "herding");
				LabelWidget propertyValues1Widget3 = this.m_propertyValues1Widget;
				propertyValues1Widget3.Text = propertyValues1Widget3.Text + (bestiaryCreatureInfo.IsHerding ? LanguageControl.Yes : LanguageControl.No) + "\n";
				LabelWidget propertyNames1Widget4 = this.m_propertyNames1Widget;
				propertyNames1Widget4.Text += LanguageControl.Get("BestiaryDescriptionScreen", 1);
				LabelWidget propertyValues1Widget4 = this.m_propertyValues1Widget;
				propertyValues1Widget4.Text = propertyValues1Widget4.Text + (bestiaryCreatureInfo.CanBeRidden ? LanguageControl.Yes : LanguageControl.No) + "\n";
				this.m_propertyNames1Widget.Text = this.m_propertyNames1Widget.Text.TrimEnd();
				this.m_propertyValues1Widget.Text = this.m_propertyValues1Widget.Text.TrimEnd();
				this.m_propertyNames2Widget.Text = string.Empty;
				this.m_propertyValues2Widget.Text = string.Empty;
				LabelWidget propertyNames2Widget = this.m_propertyNames2Widget;
				propertyNames2Widget.Text += LanguageControl.Get("BestiaryDescriptionScreen", "speed");
				LabelWidget propertyValues2Widget = this.m_propertyValues2Widget;
				propertyValues2Widget.Text = propertyValues2Widget.Text + ((double)bestiaryCreatureInfo.MovementSpeed * 3.6).ToString("0") + LanguageControl.Get("BestiaryDescriptionScreen", "speed unit");
				LabelWidget propertyNames2Widget2 = this.m_propertyNames2Widget;
				propertyNames2Widget2.Text += LanguageControl.Get("BestiaryDescriptionScreen", "jump height");
				LabelWidget propertyValues2Widget2 = this.m_propertyValues2Widget;
				propertyValues2Widget2.Text = propertyValues2Widget2.Text + bestiaryCreatureInfo.JumpHeight.ToString("0.0") + LanguageControl.Get("BestiaryDescriptionScreen", "length unit");
				LabelWidget propertyNames2Widget3 = this.m_propertyNames2Widget;
				propertyNames2Widget3.Text += LanguageControl.Get("BestiaryDescriptionScreen", "weight");
				LabelWidget propertyValues2Widget3 = this.m_propertyValues2Widget;
				propertyValues2Widget3.Text = propertyValues2Widget3.Text + bestiaryCreatureInfo.Mass.ToString() + LanguageControl.Get("BestiaryDescriptionScreen", "weight unit");
				LabelWidget propertyNames2Widget4 = this.m_propertyNames2Widget;
				propertyNames2Widget4.Text = propertyNames2Widget4.Text + LanguageControl.Get("BlocksManager", "Spawner Eggs") + ":";
				LabelWidget propertyValues2Widget4 = this.m_propertyValues2Widget;
				propertyValues2Widget4.Text = propertyValues2Widget4.Text + (bestiaryCreatureInfo.HasSpawnerEgg ? LanguageControl.Exists : LanguageControl.None) + "\n";
				this.m_propertyNames2Widget.Text = this.m_propertyNames2Widget.Text.TrimEnd();
				this.m_propertyValues2Widget.Text = this.m_propertyValues2Widget.Text.TrimEnd();
				this.m_dropsPanel.Children.Clear();
				ValuesDictionary valuesDictionary = DatabaseManager.FindValuesDictionaryForComponent(bestiaryCreatureInfo.EntityValuesDictionary, typeof(ComponentLoot));
				if (valuesDictionary != null)
				{
					bestiaryCreatureInfo.Loot = ComponentLoot.ParseLootList(valuesDictionary.GetValue<ValuesDictionary>("Loot"));
				}
				if (bestiaryCreatureInfo.Loot.Count > 0)
				{
					foreach (ComponentLoot.Loot loot in bestiaryCreatureInfo.Loot)
					{
						if (loot.MaxCount != 0 && loot.Probability != 0f)
						{
							string text;
							if (loot.MinCount < loot.MaxCount)
							{
								text = string.Format(LanguageControl.Get("BestiaryDescriptionScreen", "range"), loot.MinCount, loot.MaxCount);
							}
							else
							{
								text = loot.MinCount.ToString();
							}
							string text2 = text;
							if (loot.Probability < 1f)
							{
								string str = text2;
								string format = LanguageControl.Get("BestiaryDescriptionScreen", 2);
								text2 = str + string.Format(format, (loot.Probability * 100f).ToString("0"));
							}
							this.m_dropsPanel.Children.Add(new StackPanelWidget
							{
								Margin = new Vector2(20f, 0f),
								Children =
								{
									new BlockIconWidget
									{
										Size = new Vector2(32f),
										Scale = 1.2f,
										VerticalAlignment = WidgetAlignment.Center,
										Value = loot.Value
									},
									new CanvasWidget
									{
										Size = new Vector2(10f, 0f)
									},
									new LabelWidget
									{
										VerticalAlignment = WidgetAlignment.Center,
										Text = text2
									}
								}
							});
						}
					}
				}
				else
				{
					this.m_dropsPanel.Children.Add(new LabelWidget
					{
						Margin = new Vector2(20f, 0f),
						Text = LanguageControl.Nothing
					});
				}
			}
		}
	}
}
