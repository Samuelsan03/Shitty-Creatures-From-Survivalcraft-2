using System;
using System.Reflection;
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
		private Random m_random = new Random();
		private static Subtexture s_waterSubtexture; // cache de la textura de agua

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			m_componentGui = m_componentPlayer?.ComponentGui;

			Thirst = valuesDictionary.GetValue<float>("Thirst", 1f);
			m_lastThirst = Thirst;

			// Eliminar todas las barras de sed existentes (incluyendo huérfanas de muertes anteriores)
			RemoveAllThirstBarWidgets();
			CreateThirstBarWidget();
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			valuesDictionary.SetValue("Thirst", Thirst);
		}

		public override void OnEntityRemoved()
		{
			base.OnEntityRemoved();
			RemoveThirstBarWidget(); // limpieza al destruir el componente
		}

		private void RemoveThirstBarWidget()
		{
			if (m_thirstBarWidget != null && m_thirstBarWidget.ParentWidget is ContainerWidget parent)
			{
				parent.Children.Remove(m_thirstBarWidget);
				m_thirstBarWidget = null;
			}
		}

		private void RemoveAllThirstBarWidgets()
		{
			// Obtener la subtextura de agua (la misma que se usará en CreateThirstBarWidget)
			if (s_waterSubtexture == null)
				s_waterSubtexture = ContentManager.Get<Subtexture>("Textures/Gui/agua");

			if (m_componentGui?.FoodBarWidget?.ParentWidget is ContainerWidget parent)
			{
				// Recorrer los hijos en orden inverso para evitar problemas al eliminar
				for (int i = parent.Children.Count - 1; i >= 0; i--)
				{
					if (parent.Children[i] is ValueBarWidget bar && bar.BarSubtexture == s_waterSubtexture)
					{
						parent.Children.RemoveAt(i);
					}
				}
			}
			// Anular referencia local por si acaso
			m_thirstBarWidget = null;
		}

		private void CreateThirstBarWidget()
		{
			if (m_componentGui?.FoodBarWidget == null) return;

			var foodBar = m_componentGui.FoodBarWidget;
			// Cargar textura de agua (si no se ha cargado aún)
			if (s_waterSubtexture == null)
				s_waterSubtexture = ContentManager.Get<Subtexture>("Textures/Gui/agua");

			m_thirstBarWidget = new ValueBarWidget
			{
				BarSize = foodBar.BarSize,
				BarsCount = 10,               // Valor fijo, evita corrupción (la barra de comida a veces da 99993)
				Spacing = foodBar.Spacing,
				LitBarColor = new Color(64, 164, 255),
				UnlitBarColor = new Color(48, 48, 48),
				BarSubtexture = s_waterSubtexture,
				TextureLinearFilter = true,
				HalfBars = true,
				BarBlending = false,
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

		// ========== MÉTODOS ORIGINALES SIN CAMBIOS ==========
		private bool IsFruit(Block block)
		{
			return block is AppleBlock ||
				   block is OrangeBlock ||
				   block is PearBlock ||
				   block is CherryBlock ||
				   block is BlueberryBlock ||
				   block is BananaBlock ||
				   block is SliceOfWatermelonBlock;
		}

		private float GetFruitThirstRestore(Block block)
		{
			if (block is AppleBlock) return 0.10f;
			if (block is OrangeBlock) return 0.12f;
			if (block is PearBlock) return 0.10f;
			if (block is CherryBlock) return 0.08f;
			if (block is BlueberryBlock) return 0.06f;
			if (block is BananaBlock) return 0.12f;
			if (block is SliceOfWatermelonBlock) return 0.15f;
			return 0f;
		}

		public override bool Eat(int value)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];

			if (block is BucketBlock && !(block is EmptyBucketBlock))
			{
				return Drink(value);
			}

			if (IsFruit(block))
			{
				bool ateSuccess = base.Eat(value);
				if (!ateSuccess)
					return false;

				if (!ShittyCreaturesSettingsManager.ThirstEnabled)
					return true;

				float thirstRestore = GetFruitThirstRestore(block);
				if (thirstRestore > 0f)
				{
					Thirst = Math.Min(Thirst + thirstRestore, 1f);
					m_lastThirst = Thirst;

					m_componentGui?.DisplaySmallMessage(
						LanguageControl.Get("ComponentNewVitalStats", "AteFruit"),
						new Color(80, 80, 255), true, false);
				}
				return true;
			}

			return base.Eat(value);
		}

		public virtual bool Drink(int value)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			float thirstRestore = 0f;
			bool causesSickness = false;
			bool isRotten = false;
			bool isAntidote = false;
			bool isAntiflu = false;
			string messageKey = null;

			// Guardar referencia al inventario
			var playerInventory = m_componentPlayer?.ComponentMiner?.Inventory;
			int activeSlot = playerInventory != null ? playerInventory.ActiveSlotIndex : -1;

			// Detectar si el ítem viene de un arrastre
			bool fromDrag = false;
			if (m_componentGui != null)
			{
				var gameWidget = m_componentGui.GameWidget;
				if (gameWidget != null)
				{
					var dragHost = gameWidget.Children.Find<DragHostWidget>(false);
					if (dragHost != null)
					{
						var dragDataField = dragHost.GetType().GetField("m_dragData", BindingFlags.NonPublic | BindingFlags.Instance);
						if (dragDataField != null)
						{
							var dragData = dragDataField.GetValue(dragHost) as InventoryDragData;
							if (dragData != null && dragData.Inventory != null && dragData.SlotIndex >= 0)
							{
								int dragValue = dragData.Inventory.GetSlotValue(dragData.SlotIndex);
								if (dragValue == value)
								{
									fromDrag = true;
								}
							}
						}
					}
				}
			}

			if (block is BoiledWaterBucketBlock)
			{
				thirstRestore = 1f;
				messageKey = "DrankBoiledWater";
			}
			else if (block is WaterBucketBlock)
			{
				thirstRestore = 0.5f;
				messageKey = "DrankDrinkableWater";
				causesSickness = true;
			}
			else if (block is MilkBucketBlock)
			{
				thirstRestore = 0.4f;
				messageKey = "DrankDrinkableWater";
			}
			else if (block is PumpkinSoupBucketBlock)
			{
				thirstRestore = 0.6f;
				messageKey = "DrankDrinkableWater";
			}
			else if (block is RottenMilkBucketBlock || block is RottenPumpkinSoupBucketBlock)
			{
				thirstRestore = 0.1f;
				messageKey = "DrankRotten";
				isRotten = true;
				causesSickness = true;
			}
			else if (block is AntidoteBucketBlock)
			{
				thirstRestore = 0f;
				messageKey = "DrankAntidote";
				isAntidote = true;
			}
			else if (block is TeaAntifluBucketBlock)
			{
				thirstRestore = 0f;
				messageKey = "DrankAntiflu";
				isAntiflu = true;
			}
			else
			{
				return false;
			}

			// Comprobaciones para antídoto y antigripal
			if (isAntidote)
			{
				bool hasPoison = false;
				bool hasSickness = false;
				var poison = m_componentPlayer?.Entity.FindComponent<ComponentPoisonInfected>();
				if (poison != null && poison.IsInfected) hasPoison = true;
				var sickness = m_componentPlayer?.ComponentSickness;
				if (sickness != null && sickness.IsSick) hasSickness = true;

				if (!hasPoison && !hasSickness)
				{
					return false;
				}
			}

			if (isAntiflu)
			{
				bool hasFlu = false;
				var flu = m_componentPlayer?.ComponentFlu;
				if (flu != null && flu.HasFlu) hasFlu = true;

				if (!hasFlu)
				{
					return false;
				}
			}

			// --- REALIZAR EL REEMPLAZO DE LA CUBETA ---
			if (playerInventory != null)
			{
				int emptyBucketIndex = BlocksManager.GetBlockIndex<EmptyBucketBlock>(throwIfNotFound: true);
				int emptyBucketValue = Terrain.MakeBlockValue(emptyBucketIndex, 0, 0);

				if (fromDrag)
				{
					// Modo arrastre: la cubeta original será eliminada por el sistema de arrastre,
					// solo añadimos la vacía al inventario del jugador.
					playerInventory.AddSlotItems(activeSlot, emptyBucketValue, 1);
				}
				else
				{
					// Modo normal: la cubeta está en el slot activo
					int slotCount = playerInventory.GetSlotCount(activeSlot);
					if (slotCount > 0 && playerInventory.GetSlotValue(activeSlot) == value)
					{
						// Eliminar una unidad de la cubeta líquida
						playerInventory.RemoveSlotItems(activeSlot, 1);
						// Añadir una cubeta vacía
						playerInventory.AddSlotItems(activeSlot, emptyBucketValue, 1);
					}
				}
			}

			if (!ShittyCreaturesSettingsManager.ThirstEnabled)
			{
				m_subsystemAudio?.PlaySound("Audio/UI/drinking", 1f, 0f, 0f, 0f);

				if (isAntidote)
				{
					var poison = m_componentPlayer?.Entity.FindComponent<ComponentPoisonInfected>();
					if (poison != null && poison.IsInfected)
					{
						poison.m_InfectDuration = 0f;
						poison.PoisonResistance = MathUtils.Max(poison.PoisonResistance, 50f);
						m_componentGui?.DisplaySmallMessage(
							LanguageControl.Get("ComponentNewVitalStats", "DrankAntidote"),
							Color.White, true, false);
					}
					var sickness = m_componentPlayer?.ComponentSickness;
					if (sickness != null && sickness.IsSick)
					{
						sickness.m_sicknessDuration = 0f;
						sickness.m_greenoutDuration = 0f;
						sickness.m_greenoutFactor = 0f;
						m_componentGui?.DisplaySmallMessage(
							LanguageControl.Get("ComponentNewVitalStats", "DrankAntidote"),
							Color.LightGreen, true, false);
					}
				}
				else if (isAntiflu)
				{
					var flu = m_componentPlayer?.ComponentFlu;
					if (flu != null && flu.HasFlu)
					{
						flu.m_fluDuration = 0f;
						flu.m_fluOnset = 0f;
						flu.m_coughDuration = 0f;
						flu.m_sneezeDuration = 0f;
						flu.m_blackoutDuration = 0f;
						flu.m_blackoutFactor = 0f;
						m_componentPlayer.ComponentScreenOverlays.BlackoutFactor = 0f;
						m_componentGui?.DisplaySmallMessage(
							LanguageControl.Get("ComponentNewVitalStats", "DrankAntiflu"),
							Color.LightGreen, true, false);
					}
				}

				return true;
			}

			// Modo con sed activada
			if (thirstRestore > 0f && Thirst >= 0.99f)
			{
				m_componentGui?.DisplaySmallMessage(
					LanguageControl.Get("ComponentNewVitalStats", "AlreadyNotThirsty"),
					Color.White, true, true);
				return false;
			}

			if (thirstRestore > 0f)
			{
				Thirst = Math.Min(Thirst + thirstRestore, 1f);
				m_lastThirst = Thirst;
			}

			m_subsystemAudio?.PlaySound("Audio/UI/drinking", 1f, 0f, 0f, 0f);

			Color messageColor;
			if (messageKey == "DrankBoiledWater" || messageKey == "DrankDrinkableWater")
			{
				messageColor = new Color(80, 80, 255);
			}
			else if (messageKey == "DrankAntidote" || messageKey == "DrankAntiflu")
			{
				messageColor = Color.LightGreen;
			}
			else
			{
				messageColor = Color.White;
			}

			m_componentGui?.DisplaySmallMessage(
				LanguageControl.Get("ComponentNewVitalStats", messageKey),
				messageColor, true, false);

			if (causesSickness)
			{
				float sicknessProb = isRotten ? 0.9f : 0.3f;
				if (m_random.Float(0f, 1f) < sicknessProb)
				{
					m_componentPlayer?.ComponentSickness?.StartSickness();
				}
			}

			if (isAntidote)
			{
				var poison = m_componentPlayer?.Entity.FindComponent<ComponentPoisonInfected>();
				if (poison != null && poison.IsInfected)
				{
					poison.m_InfectDuration = 0f;
					poison.PoisonResistance = MathUtils.Max(poison.PoisonResistance, 50f);
				}
				var sickness = m_componentPlayer?.ComponentSickness;
				if (sickness != null && sickness.IsSick)
				{
					sickness.m_sicknessDuration = 0f;
					sickness.m_greenoutDuration = 0f;
					sickness.m_greenoutFactor = 0f;
				}
			}

			if (isAntiflu)
			{
				var flu = m_componentPlayer?.ComponentFlu;
				if (flu != null && flu.HasFlu)
				{
					flu.m_fluDuration = 0f;
					flu.m_fluOnset = 0f;
					flu.m_coughDuration = 0f;
					flu.m_sneezeDuration = 0f;
					flu.m_blackoutDuration = 0f;
					flu.m_blackoutFactor = 0f;
					m_componentPlayer.ComponentScreenOverlays.BlackoutFactor = 0f;
				}
			}

			return true;
		}

		public override void Update(float dt)
		{
			base.Update(dt);

			bool thirstEnabled = ShittyCreaturesSettingsManager.ThirstEnabled;
			bool isCreative = m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative;
			bool survivalMechanics = m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled;

			if (m_thirstBarWidget != null)
			{
				m_thirstBarWidget.IsVisible = thirstEnabled && !isCreative && survivalMechanics;
				m_thirstBarWidget.Value = Thirst;
			}

			if (!thirstEnabled)
			{
				Thirst = 1f;
				m_lastThirst = 1f;
				return;
			}

			if (!isCreative && survivalMechanics)
			{
				float delta = m_subsystemTime.GameTimeDelta;
				float hungerFactor = m_componentPlayer?.ComponentLevel?.HungerFactor ?? 1f;

				Thirst -= hungerFactor * delta / 2600.0f;
				Vector2? walk = m_componentPlayer?.ComponentLocomotion?.LastWalkOrder;
				float move = walk.HasValue ? walk.Value.Length() : 0f;
				Thirst -= hungerFactor * delta * move / 2600.0f;
				Thirst = Math.Clamp(Thirst, 0f, 1f);

				bool periodic = m_subsystemTime.PeriodicGameTimeEvent(240.0, 9.0);
				if (Thirst <= 0f)
				{
					if (m_subsystemTime.PeriodicGameTimeEvent(30.0, 0.0))
					{
						string cause = LanguageControl.Get("Injury", "Dehydration");
						m_componentPlayer?.ComponentHealth.Injure(0.1f, null, false, cause);
						m_componentGui?.DisplaySmallMessage(
							LanguageControl.Get("ComponentNewVitalStats", "Dehydrating"),
							Color.Red, true, false);
						m_thirstBarWidget?.Flash(10);
					}
				}
				else if (Thirst < 0.1f && (m_lastThirst >= 0.1f || periodic))
				{
					m_componentGui?.DisplaySmallMessage(
						LanguageControl.Get("ComponentNewVitalStats", "Dehydrating"),
						Color.White, true, true);
				}
				else if (Thirst < 0.25f && (m_lastThirst >= 0.25f || periodic))
				{
					m_componentGui?.DisplaySmallMessage(
						LanguageControl.Get("ComponentNewVitalStats", "HalfThirsty"),
						Color.White, true, false);
				}

				m_lastThirst = Thirst;
			}
			else
			{
				Thirst = 1f;
			}
		}
	}
}
