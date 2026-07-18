using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Engine;
using Engine.Graphics;
using TemplatesDatabase;

namespace Game
{
	public class BestiaryInfectedScreen : Screen
	{
		private ListPanelWidget m_creaturesList;

		public BestiaryInfectedScreen()
		{
			XElement node = ContentManager.Get<XElement>("Screens/BestiaryInfectedScreen");
			LoadContents(this, node);

			m_creaturesList = Children.Find<ListPanelWidget>("CreaturesList", true);
			m_creaturesList.ItemWidgetFactory = (object item) =>
			{
				BestiaryCreatureInfo info = (BestiaryCreatureInfo)item;
				XElement itemNode = ContentManager.Get<XElement>("Widgets/BestiaryItem");
				ContainerWidget container = (ContainerWidget)Widget.LoadWidget(this, itemNode, null);
				ModelWidget model = container.Children.Find<ModelWidget>("BestiaryItem.Model", true);
				BestiaryScreen.SetupBestiaryModelWidget(
					info,
					model,
					(m_creaturesList.Items.IndexOf(item) % 2 == 0) ? new Vector3(-1f, 0f, -1f) : new Vector3(1f, 0f, -1f),
					false,
					false
				);
				container.Children.Find<LabelWidget>("BestiaryItem.Text", true).Text = info.DisplayName;
				container.Children.Find<LabelWidget>("BestiaryItem.Details", true).Text = info.Description;
				return container;
			};
			m_creaturesList.ItemClicked += (object item) =>
			{
				ScreensManager.SwitchScreen("BestiaryInfectedDescription", new object[]
				{
					item,
					m_creaturesList.Items.Cast<BestiaryCreatureInfo>().ToList()
				});
			};

			// Construir la lista de infectados usando los templates del huevo
			List<BestiaryCreatureInfo> list = new List<BestiaryCreatureInfo>();

			// Obtener todos los templates de todas las categorías del huevo
			HashSet<string> infectedTemplates = new HashSet<string>();
			foreach (var kvp in SubsystemInfectedSpawnEggBlockBehavior.s_spawnTemplates)
			{
				foreach (string template in kvp.Value)
				{
					infectedTemplates.Add(template);
				}
			}

			foreach (ValuesDictionary dict in DatabaseManager.EntitiesValuesDictionaries)
			{
				ValuesDictionary creatureDict = DatabaseManager.FindValuesDictionaryForComponent(dict, typeof(ComponentCreature));
				if (creatureDict == null)
					continue;

				string templateName = dict.DatabaseObject.Name;

				// Filtrar: solo si está en la lista de templates de infectados
				if (!infectedTemplates.Contains(templateName))
					continue;

				// No mostrar al jugador
				if (dict.GetValue<ValuesDictionary>("Player", null) != null)
					continue;

				// Obtener datos necesarios
				ValuesDictionary modelDict = DatabaseManager.FindValuesDictionaryForComponent(dict, typeof(ComponentCreatureModel));
				ValuesDictionary bodyDict = DatabaseManager.FindValuesDictionaryForComponent(dict, typeof(ComponentBody));
				ValuesDictionary healthDict = DatabaseManager.FindValuesDictionaryForComponent(dict, typeof(ComponentHealth));
				ValuesDictionary minerDict = DatabaseManager.FindValuesDictionaryForComponent(dict, typeof(ComponentMiner));
				ValuesDictionary locoDict = DatabaseManager.FindValuesDictionaryForComponent(dict, typeof(ComponentLocomotion));
				ValuesDictionary herdDict = DatabaseManager.FindValuesDictionaryForComponent(dict, typeof(ComponentHerdBehavior));
				ValuesDictionary mountDict = DatabaseManager.FindValuesDictionaryForComponent(dict, typeof(ComponentMount));
				ValuesDictionary lootDict = DatabaseManager.FindValuesDictionaryForComponent(dict, typeof(ComponentLoot));
				ValuesDictionary eggDict = dict.GetValue<ValuesDictionary>("CreatureEggData", null);

				string displayName = creatureDict.GetValue<string>("DisplayName");
				if (displayName.StartsWith('[') && displayName.EndsWith(']'))
				{
					string[] parts = displayName.Substring(1, displayName.Length - 2).Split(':', StringSplitOptions.RemoveEmptyEntries);
					displayName = LanguageControl.GetDatabase("DisplayName", parts[1]);
				}

				string description = creatureDict.GetValue<string>("Description");
				if (description.StartsWith('[') && description.EndsWith(']'))
				{
					string[] parts = description.Substring(1, description.Length - 2).Split(':', StringSplitOptions.RemoveEmptyEntries);
					description = LanguageControl.GetDatabase("Description", parts[1]);
				}

				BestiaryCreatureInfo info = new BestiaryCreatureInfo
				{
					EntityValuesDictionary = dict,
					Order = list.Count,
					DisplayName = displayName,
					Description = description,
					ModelName = modelDict.GetValue<string>("ModelName"),
					TextureOverride = modelDict.GetValue<string>("TextureOverride"),
					Mass = bodyDict.GetValue<float>("Mass"),
					AttackResilience = healthDict.GetValue<float>("AttackResilience"),
					AttackPower = (minerDict != null) ? minerDict.GetValue<float>("AttackPower") : 0f,
					MovementSpeed = MathUtils.Max(
						locoDict.GetValue<float>("WalkSpeed"),
						locoDict.GetValue<float>("FlySpeed"),
						locoDict.GetValue<float>("SwimSpeed")
					),
					JumpHeight = MathUtils.Sqr(locoDict.GetValue<float>("JumpSpeed")) / 20f,
					IsHerding = (herdDict != null),
					CanBeRidden = (mountDict != null),
					HasSpawnerEgg = (eggDict != null && eggDict.GetValue<bool>("ShowEgg")),
					Loot = (lootDict != null) ? ComponentLoot.ParseLootList(lootDict.GetValue<ValuesDictionary>("Loot")) : new List<ComponentLoot.Loot>()
				};

				list.Add(info);
			}

			// Ordenar por nombre
			foreach (BestiaryCreatureInfo item in list.OrderBy(ci => ci.DisplayName))
			{
				m_creaturesList.AddItem(item);
			}

			if (ScreensManager.FindScreen<BestiaryInfectedDescriptionScreen>("BestiaryInfectedDescription") == null)
			{
				ScreensManager.AddScreen("BestiaryInfectedDescription", new BestiaryInfectedDescriptionScreen());
			}
		}

		public override void Enter(object[] parameters)
		{
			m_creaturesList.SelectedItem = null;
		}

		public override void Update()
		{
			if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back", true).IsClicked)
			{
				ScreensManager.GoBack(Array.Empty<object>());
			}
		}
	}
}
