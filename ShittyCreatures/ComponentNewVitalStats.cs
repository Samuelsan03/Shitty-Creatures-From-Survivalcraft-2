using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000052 RID: 82
	public class ComponentNewVitalStats : ComponentVitalStats
	{
		// Token: 0x1700006D RID: 109
		// (get) Token: 0x060003D2 RID: 978 RVA: 0x0002F73A File Offset: 0x0002D93A
		// (set) Token: 0x060003D3 RID: 979 RVA: 0x0002F742 File Offset: 0x0002D942
		public float Thirst { get; set; } = 1f;

		// Token: 0x060003D4 RID: 980 RVA: 0x0002F74C File Offset: 0x0002D94C
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			ComponentPlayer componentPlayer = this.m_componentPlayer;
			this.m_componentGui = ((componentPlayer != null) ? componentPlayer.ComponentGui : null);
			this.Thirst = valuesDictionary.GetValue<float>("Thirst", 1f);
			this.m_lastThirst = this.Thirst;
			this.RemoveAllThirstBarWidgets();
			this.CreateThirstBarWidget();
		}

		// Token: 0x060003D5 RID: 981 RVA: 0x0002F7AC File Offset: 0x0002D9AC
		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);
			valuesDictionary.SetValue<float>("Thirst", this.Thirst);
		}

		// Token: 0x060003D6 RID: 982 RVA: 0x0002F7CA File Offset: 0x0002D9CA
		public override void OnEntityRemoved()
		{
			base.OnEntityRemoved();
			this.RemoveThirstBarWidget();
		}

		// Token: 0x060003D7 RID: 983 RVA: 0x0002F7DC File Offset: 0x0002D9DC
		private void RemoveThirstBarWidget()
		{
			if (this.m_thirstBarWidget != null)
			{
				ContainerWidget parentWidget = this.m_thirstBarWidget.ParentWidget;
				if (parentWidget != null)
				{
					parentWidget.Children.Remove(this.m_thirstBarWidget);
				}
				this.m_thirstBarWidget = null;
			}
		}

		// Token: 0x060003D8 RID: 984 RVA: 0x0002F824 File Offset: 0x0002DA24
		private void RemoveAllThirstBarWidgets()
		{
			bool flag = ComponentNewVitalStats.s_waterSubtexture == null;
			if (flag)
			{
				ComponentNewVitalStats.s_waterSubtexture = ContentManager.Get<Subtexture>("Textures/Gui/agua");
			}
			ComponentGui componentGui = this.m_componentGui;
			ContainerWidget containerWidget;
			if (componentGui == null)
			{
				containerWidget = null;
			}
			else
			{
				ValueBarWidget foodBarWidget = componentGui.FoodBarWidget;
				containerWidget = ((foodBarWidget != null) ? foodBarWidget.ParentWidget : null);
			}
			ContainerWidget containerWidget2 = containerWidget;
			bool flag2 = containerWidget2 != null;
			if (flag2)
			{
				for (int i = containerWidget2.Children.Count - 1; i >= 0; i--)
				{
					ValueBarWidget valueBarWidget = containerWidget2.Children[i] as ValueBarWidget;
					bool flag3 = valueBarWidget != null && valueBarWidget.BarSubtexture == ComponentNewVitalStats.s_waterSubtexture;
					if (flag3)
					{
						containerWidget2.Children.RemoveAt(i);
					}
				}
			}
			this.m_thirstBarWidget = null;
		}

		// Token: 0x060003D9 RID: 985 RVA: 0x0002F8E0 File Offset: 0x0002DAE0
		private void CreateThirstBarWidget()
		{
			ComponentGui componentGui = this.m_componentGui;
			bool flag = ((componentGui != null) ? componentGui.FoodBarWidget : null) == null;
			if (!flag)
			{
				ValueBarWidget foodBarWidget = this.m_componentGui.FoodBarWidget;
				bool flag2 = ComponentNewVitalStats.s_waterSubtexture == null;
				if (flag2)
				{
					ComponentNewVitalStats.s_waterSubtexture = ContentManager.Get<Subtexture>("Textures/Gui/agua");
				}
				this.m_thirstBarWidget = new ValueBarWidget
				{
					BarSize = foodBarWidget.BarSize,
					BarsCount = 10,
					Spacing = foodBarWidget.Spacing,
					LitBarColor = new Color(64, 164, 255),
					UnlitBarColor = new Color(48, 48, 48),
					BarSubtexture = ComponentNewVitalStats.s_waterSubtexture,
					TextureLinearFilter = true,
					HalfBars = true,
					BarBlending = false,
					LayoutDirection = LayoutDirection.Horizontal,
					Value = this.Thirst
				};
				ContainerWidget parentWidget = foodBarWidget.ParentWidget;
				bool flag3 = parentWidget != null;
				if (flag3)
				{
					int num = parentWidget.Children.IndexOf(foodBarWidget);
					parentWidget.Children.Insert(num + 1, this.m_thirstBarWidget);
				}
			}
		}

		// Token: 0x060003DA RID: 986 RVA: 0x0002F9FC File Offset: 0x0002DBFC
		private bool IsFruit(Block block)
		{
			return block is AppleBlock || block is OrangeBlock || block is PearBlock || block is CherryBlock || block is BlueberryBlock || block is BananaBlock || block is SliceOfWatermelonBlock;
		}

		// Token: 0x060003DB RID: 987 RVA: 0x0002FA4C File Offset: 0x0002DC4C
		private float GetFruitThirstRestore(Block block)
		{
			bool flag = block is AppleBlock;
			float result;
			if (flag)
			{
				result = 0.1f;
			}
			else
			{
				bool flag2 = block is OrangeBlock;
				if (flag2)
				{
					result = 0.12f;
				}
				else
				{
					bool flag3 = block is PearBlock;
					if (flag3)
					{
						result = 0.1f;
					}
					else
					{
						bool flag4 = block is CherryBlock;
						if (flag4)
						{
							result = 0.08f;
						}
						else
						{
							bool flag5 = block is BlueberryBlock;
							if (flag5)
							{
								result = 0.06f;
							}
							else
							{
								bool flag6 = block is BananaBlock;
								if (flag6)
								{
									result = 0.12f;
								}
								else
								{
									bool flag7 = block is SliceOfWatermelonBlock;
									if (flag7)
									{
										result = 0.15f;
									}
									else
									{
										result = 0f;
									}
								}
							}
						}
					}
				}
			}
			return result;
		}

		// Token: 0x060003DC RID: 988 RVA: 0x0002FB04 File Offset: 0x0002DD04
		public override bool Eat(int value)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			bool flag = (block is BucketBlock && !(block is EmptyBucketBlock)) || block is WaterBowlBlock || block is BoiledWaterBowlBlock || block is CactusJuiceBowlBlock || block is AntidoteBowlBlock || block is TeaAntifluBowlBlock;
			bool result;
			if (flag)
			{
				result = this.Drink(value);
			}
			else
			{
				bool flag2 = this.IsFruit(block);
				if (flag2)
				{
					bool flag3 = base.Eat(value);
					bool flag4 = !flag3;
					if (flag4)
					{
						result = false;
					}
					else
					{
						bool flag5 = !ShittyCreaturesSettingsManager.ThirstEnabled;
						if (flag5)
						{
							result = true;
						}
						else
						{
							float fruitThirstRestore = this.GetFruitThirstRestore(block);
							bool flag6 = fruitThirstRestore > 0f;
							if (flag6)
							{
								this.Thirst = Math.Min(this.Thirst + fruitThirstRestore, 1f);
								this.m_lastThirst = this.Thirst;
								ComponentGui componentGui = this.m_componentGui;
								if (componentGui != null)
								{
									componentGui.DisplaySmallMessage(LanguageControl.Get(new string[]
									{
										"ComponentNewVitalStats",
										"AteFruit"
									}), new Color(80, 80, 255), true, false);
								}
							}
							result = true;
						}
					}
				}
				else
				{
					result = base.Eat(value);
				}
			}
			return result;
		}

		// Token: 0x060003DD RID: 989 RVA: 0x0002FC38 File Offset: 0x0002DE38
		public virtual bool Drink(int value)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			bool flag4 = false;
			ComponentPlayer componentPlayer = this.m_componentPlayer;
			IInventory inventory;
			if (componentPlayer == null)
			{
				inventory = null;
			}
			else
			{
				ComponentMiner componentMiner = componentPlayer.ComponentMiner;
				inventory = ((componentMiner != null) ? componentMiner.Inventory : null);
			}
			IInventory inventory2 = inventory;
			int slotIndex = (inventory2 != null) ? inventory2.ActiveSlotIndex : -1;
			int blockIndex = BlocksManager.GetBlockIndex<EmptyBowlBlock>(true, false);
			int num = Terrain.MakeBlockValue(blockIndex, 0, 0);
			bool flag5 = false;
			bool flag6 = this.m_componentGui != null;
			if (flag6)
			{
				GameWidget gameWidget = this.m_componentGui.GameWidget;
				bool flag7 = gameWidget != null;
				if (flag7)
				{
					DragHostWidget dragHostWidget = gameWidget.Children.Find<DragHostWidget>(false);
					bool flag8 = dragHostWidget != null;
					if (flag8)
					{
						FieldInfo field = dragHostWidget.GetType().GetField("m_dragData", BindingFlags.Instance | BindingFlags.NonPublic);
						bool flag9 = field != null;
						if (flag9)
						{
							InventoryDragData inventoryDragData = field.GetValue(dragHostWidget) as InventoryDragData;
							bool flag10 = inventoryDragData != null && inventoryDragData.Inventory != null && inventoryDragData.SlotIndex >= 0;
							if (flag10)
							{
								int slotValue = inventoryDragData.Inventory.GetSlotValue(inventoryDragData.SlotIndex);
								bool flag11 = slotValue == value;
								if (flag11)
								{
									flag5 = true;
								}
							}
						}
					}
				}
			}
			bool flag12 = block is BoiledWaterBucketBlock;
			float num2;
			string text;
			if (flag12)
			{
				num2 = 1f;
				text = "DrankBoiledWater";
			}
			else
			{
				bool flag13 = block is WaterBucketBlock;
				if (flag13)
				{
					num2 = 0.5f;
					text = "DrankDrinkableWater";
					flag = true;
				}
				else
				{
					bool flag14 = block is MilkBucketBlock;
					if (flag14)
					{
						num2 = 0.4f;
						text = "DrankDrinkableWater";
					}
					else
					{
						bool flag15 = block is PumpkinSoupBucketBlock;
						if (flag15)
						{
							num2 = 0.6f;
							text = "DrankDrinkableWater";
						}
						else
						{
							bool flag16 = block is RottenMilkBucketBlock || block is RottenPumpkinSoupBucketBlock;
							if (flag16)
							{
								num2 = 0.1f;
								text = "DrankRotten";
								flag2 = true;
								flag = true;
							}
							else
							{
								bool flag17 = block is AntidoteBucketBlock;
								if (flag17)
								{
									num2 = 0f;
									text = "DrankAntidote";
									flag3 = true;
								}
								else
								{
									bool flag18 = block is TeaAntifluBucketBlock;
									if (flag18)
									{
										num2 = 0f;
										text = "DrankAntiflu";
										flag4 = true;
									}
									else
									{
										bool flag19 = block is WaterBowlBlock;
										if (flag19)
										{
											num2 = 0.25f;
											text = "DrankDrinkableWater";
											flag = true;
										}
										else
										{
											bool flag20 = block is BoiledWaterBowlBlock;
											if (flag20)
											{
												num2 = 0.4f;
												text = "DrankBoiledWater";
											}
											else
											{
												bool flag21 = block is CactusJuiceBowlBlock;
												if (flag21)
												{
													num2 = 0.35f;
													text = "DrankDrinkableWater";
												}
												else
												{
													bool flag22 = block is AntidoteBowlBlock;
													if (flag22)
													{
														num2 = 0f;
														text = "DrankAntidote";
														flag3 = true;
													}
													else
													{
														bool flag23 = block is TeaAntifluBowlBlock;
														if (!flag23)
														{
															return false;
														}
														num2 = 0f;
														text = "DrankAntiflu";
														flag4 = true;
													}
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}
			bool flag24 = flag3;
			if (flag24)
			{
				bool flag25 = false;
				bool flag26 = false;
				ComponentPlayer componentPlayer2 = this.m_componentPlayer;
				ComponentPoisonInfected componentPoisonInfected = (componentPlayer2 != null) ? componentPlayer2.Entity.FindComponent<ComponentPoisonInfected>() : null;
				bool flag27 = componentPoisonInfected != null && componentPoisonInfected.IsInfected;
				if (flag27)
				{
					flag25 = true;
				}
				ComponentPlayer componentPlayer3 = this.m_componentPlayer;
				ComponentSickness componentSickness = (componentPlayer3 != null) ? componentPlayer3.ComponentSickness : null;
				bool flag28 = componentSickness != null && componentSickness.IsSick;
				if (flag28)
				{
					flag26 = true;
				}
				bool flag29 = !flag25 && !flag26;
				if (flag29)
				{
					return false;
				}
			}
			bool flag30 = flag4;
			if (flag30)
			{
				bool flag31 = false;
				ComponentPlayer componentPlayer4 = this.m_componentPlayer;
				ComponentFlu componentFlu = (componentPlayer4 != null) ? componentPlayer4.ComponentFlu : null;
				bool flag32 = componentFlu != null && componentFlu.HasFlu;
				if (flag32)
				{
					flag31 = true;
				}
				bool flag33 = !flag31;
				if (flag33)
				{
					return false;
				}
			}
			if (!ShittyCreaturesSettingsManager.ThirstEnabled && !flag3 && !flag4)
			{
				return false;
			}
			bool flag34 = inventory2 != null;
			if (flag34)
			{
				bool flag35 = block is BucketBlock;
				int value2;
				if (flag35)
				{
					int blockIndex2 = BlocksManager.GetBlockIndex<EmptyBucketBlock>(true, false);
					value2 = Terrain.MakeBlockValue(blockIndex2, 0, 0);
				}
				else
				{
					value2 = num;
				}
				bool flag36 = flag5;
				if (flag36)
				{
					int num3 = -1;
					ComponentGui componentGui = this.m_componentGui;
					DragHostWidget dragHostWidget2;
					if (componentGui == null)
					{
						dragHostWidget2 = null;
					}
					else
					{
						GameWidget gameWidget2 = componentGui.GameWidget;
						dragHostWidget2 = ((gameWidget2 != null) ? gameWidget2.Children.Find<DragHostWidget>(false) : null);
					}
					DragHostWidget dragHostWidget3 = dragHostWidget2;
					bool flag37 = dragHostWidget3 != null;
					if (flag37)
					{
						FieldInfo field2 = dragHostWidget3.GetType().GetField("m_dragData", BindingFlags.Instance | BindingFlags.NonPublic);
						bool flag38 = field2 != null;
						if (flag38)
						{
							InventoryDragData inventoryDragData2 = field2.GetValue(dragHostWidget3) as InventoryDragData;
							bool flag39 = inventoryDragData2 != null && inventoryDragData2.Inventory == inventory2;
							if (flag39)
							{
								num3 = inventoryDragData2.SlotIndex;
							}
						}
					}
					bool flag40 = num3 >= 0 && inventory2.GetSlotCapacity(num3, value2) > inventory2.GetSlotCount(num3);
					if (flag40)
					{
						inventory2.AddSlotItems(num3, value2, 1);
					}
					else
					{
						int num4 = ComponentInventoryBase.FindAcquireSlotForItem(inventory2, value2);
						bool flag41 = num4 >= 0;
						if (flag41)
						{
							inventory2.AddSlotItems(num4, value2, 1);
						}
						else
						{
							inventory2.AddSlotItems(slotIndex, value2, 1);
						}
					}
				}
				else
				{
					int slotCount = inventory2.GetSlotCount(slotIndex);
					bool flag42 = slotCount > 0 && inventory2.GetSlotValue(slotIndex) == value;
					if (flag42)
					{
						inventory2.RemoveSlotItems(slotIndex, 1);
						inventory2.AddSlotItems(slotIndex, value2, 1);
					}
				}
			}
			bool flag43 = !ShittyCreaturesSettingsManager.ThirstEnabled;
			bool result;
			if (flag43)
			{
				SubsystemAudio subsystemAudio = this.m_subsystemAudio;
				if (subsystemAudio != null)
				{
					subsystemAudio.PlaySound("Audio/UI/drinking", 1f, 0f, 0f, 0f);
				}
				bool flag44 = flag3;
				if (flag44)
				{
					ComponentPlayer componentPlayer5 = this.m_componentPlayer;
					ComponentPoisonInfected componentPoisonInfected2 = (componentPlayer5 != null) ? componentPlayer5.Entity.FindComponent<ComponentPoisonInfected>() : null;
					bool flag45 = componentPoisonInfected2 != null && componentPoisonInfected2.IsInfected;
					if (flag45)
					{
						componentPoisonInfected2.m_InfectDuration = 0f;
						componentPoisonInfected2.PoisonResistance = MathUtils.Max(componentPoisonInfected2.PoisonResistance, 50f);
						ComponentGui componentGui2 = this.m_componentGui;
						if (componentGui2 != null)
						{
							componentGui2.DisplaySmallMessage(LanguageControl.Get(new string[]
							{
						"ComponentNewVitalStats",
						"DrankAntidote"
							}), Color.White, true, false);
						}
					}
					ComponentPlayer componentPlayer6 = this.m_componentPlayer;
					ComponentSickness componentSickness2 = (componentPlayer6 != null) ? componentPlayer6.ComponentSickness : null;
					bool flag46 = componentSickness2 != null && componentSickness2.IsSick;
					if (flag46)
					{
						componentSickness2.m_sicknessDuration = 0f;
						componentSickness2.m_greenoutDuration = 0f;
						componentSickness2.m_greenoutFactor = 0f;
						ComponentGui componentGui3 = this.m_componentGui;
						if (componentGui3 != null)
						{
							componentGui3.DisplaySmallMessage(LanguageControl.Get(new string[]
							{
						"ComponentNewVitalStats",
						"DrankAntidote"
							}), Color.LightGreen, true, false);
						}
					}
				}
				else
				{
					bool flag47 = flag4;
					if (flag47)
					{
						ComponentPlayer componentPlayer7 = this.m_componentPlayer;
						ComponentFlu componentFlu2 = (componentPlayer7 != null) ? componentPlayer7.ComponentFlu : null;
						bool flag48 = componentFlu2 != null && componentFlu2.HasFlu;
						if (flag48)
						{
							componentFlu2.m_fluDuration = 0f;
							componentFlu2.m_fluOnset = 0f;
							componentFlu2.m_coughDuration = 0f;
							componentFlu2.m_sneezeDuration = 0f;
							componentFlu2.m_blackoutDuration = 0f;
							componentFlu2.m_blackoutFactor = 0f;
							this.m_componentPlayer.ComponentScreenOverlays.BlackoutFactor = 0f;
							ComponentGui componentGui4 = this.m_componentGui;
							if (componentGui4 != null)
							{
								componentGui4.DisplaySmallMessage(LanguageControl.Get(new string[]
								{
							"ComponentNewVitalStats",
							"DrankAntiflu"
								}), Color.LightGreen, true, false);
							}
						}
					}
				}
				result = true;
			}
			else
			{
				bool flag49 = num2 > 0f && this.Thirst >= 0.99f;
				if (flag49)
				{
					ComponentGui componentGui5 = this.m_componentGui;
					if (componentGui5 != null)
					{
						componentGui5.DisplaySmallMessage(LanguageControl.Get(new string[]
						{
					"ComponentNewVitalStats",
					"AlreadyNotThirsty"
						}), Color.White, true, true);
					}
					result = false;
				}
				else
				{
					bool flag50 = num2 > 0f;
					if (flag50)
					{
						this.Thirst = Math.Min(this.Thirst + num2, 1f);
						this.m_lastThirst = this.Thirst;
					}
					SubsystemAudio subsystemAudio2 = this.m_subsystemAudio;
					if (subsystemAudio2 != null)
					{
						subsystemAudio2.PlaySound("Audio/UI/drinking", 1f, 0f, 0f, 0f);
					}
					bool flag51 = text == "DrankBoiledWater" || text == "DrankDrinkableWater";
					Color color;
					if (flag51)
					{
						color = new Color(80, 80, 255);
					}
					else
					{
						bool flag52 = text == "DrankAntidote" || text == "DrankAntiflu";
						if (flag52)
						{
							color = Color.LightGreen;
						}
						else
						{
							color = Color.White;
						}
					}
					ComponentGui componentGui6 = this.m_componentGui;
					if (componentGui6 != null)
					{
						componentGui6.DisplaySmallMessage(LanguageControl.Get(new string[]
						{
					"ComponentNewVitalStats",
					text
						}), color, true, false);
					}
					bool flag53 = flag;
					if (flag53)
					{
						float num5 = flag2 ? 0.9f : 0.3f;
						bool flag54 = this.m_random.Float(0f, 1f) < num5;
						if (flag54)
						{
							ComponentPlayer componentPlayer8 = this.m_componentPlayer;
							if (componentPlayer8 != null)
							{
								ComponentSickness componentSickness3 = componentPlayer8.ComponentSickness;
								if (componentSickness3 != null)
								{
									componentSickness3.StartSickness();
								}
							}
						}
					}
					bool flag55 = flag3;
					if (flag55)
					{
						ComponentPlayer componentPlayer9 = this.m_componentPlayer;
						ComponentPoisonInfected componentPoisonInfected3 = (componentPlayer9 != null) ? componentPlayer9.Entity.FindComponent<ComponentPoisonInfected>() : null;
						bool flag56 = componentPoisonInfected3 != null && componentPoisonInfected3.IsInfected;
						if (flag56)
						{
							componentPoisonInfected3.m_InfectDuration = 0f;
							componentPoisonInfected3.PoisonResistance = MathUtils.Max(componentPoisonInfected3.PoisonResistance, 50f);
						}
						ComponentPlayer componentPlayer10 = this.m_componentPlayer;
						ComponentSickness componentSickness4 = (componentPlayer10 != null) ? componentPlayer10.ComponentSickness : null;
						bool flag57 = componentSickness4 != null && componentSickness4.IsSick;
						if (flag57)
						{
							componentSickness4.m_sicknessDuration = 0f;
							componentSickness4.m_greenoutDuration = 0f;
							componentSickness4.m_greenoutFactor = 0f;
						}
					}
					bool flag58 = flag4;
					if (flag58)
					{
						ComponentPlayer componentPlayer11 = this.m_componentPlayer;
						ComponentFlu componentFlu3 = (componentPlayer11 != null) ? componentPlayer11.ComponentFlu : null;
						bool flag59 = componentFlu3 != null && componentFlu3.HasFlu;
						if (flag59)
						{
							componentFlu3.m_fluDuration = 0f;
							componentFlu3.m_fluOnset = 0f;
							componentFlu3.m_coughDuration = 0f;
							componentFlu3.m_sneezeDuration = 0f;
							componentFlu3.m_blackoutDuration = 0f;
							componentFlu3.m_blackoutFactor = 0f;
							this.m_componentPlayer.ComponentScreenOverlays.BlackoutFactor = 0f;
						}
					}
					result = true;
				}
			}
			return result;
		}

		// Token: 0x060003DE RID: 990 RVA: 0x000306C4 File Offset: 0x0002E8C4
		public override void Update(float dt)
		{
			base.Update(dt);
			bool thirstEnabled = ShittyCreaturesSettingsManager.ThirstEnabled;
			bool flag = this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative;
			bool areAdventureSurvivalMechanicsEnabled = this.m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled;
			bool flag2 = this.m_thirstBarWidget != null;
			if (flag2)
			{
				this.m_thirstBarWidget.IsVisible = (thirstEnabled && !flag && areAdventureSurvivalMechanicsEnabled);
				this.m_thirstBarWidget.Value = this.Thirst;
			}
			bool flag3 = !thirstEnabled;
			if (flag3)
			{
				this.Thirst = 1f;
				this.m_lastThirst = 1f;
			}
			else
			{
				bool flag4 = !flag && areAdventureSurvivalMechanicsEnabled;
				if (flag4)
				{
					float gameTimeDelta = this.m_subsystemTime.GameTimeDelta;
					ComponentPlayer componentPlayer = this.m_componentPlayer;
					float? num;
					if (componentPlayer == null)
					{
						num = null;
					}
					else
					{
						ComponentLevel componentLevel = componentPlayer.ComponentLevel;
						num = ((componentLevel != null) ? new float?(componentLevel.HungerFactor) : null);
					}
					float? num2 = num;
					float valueOrDefault = num2.GetValueOrDefault(1f);
					this.Thirst -= valueOrDefault * gameTimeDelta / 2600f;
					ComponentPlayer componentPlayer2 = this.m_componentPlayer;
					Vector2? vector;
					if (componentPlayer2 == null)
					{
						vector = null;
					}
					else
					{
						ComponentLocomotion componentLocomotion = componentPlayer2.ComponentLocomotion;
						vector = ((componentLocomotion != null) ? componentLocomotion.LastWalkOrder : null);
					}
					Vector2? vector2 = vector;
					float num3 = (vector2 != null) ? vector2.Value.Length() : 0f;
					this.Thirst -= valueOrDefault * gameTimeDelta * num3 / 2600f;
					this.Thirst = Math.Clamp(this.Thirst, 0f, 1f);
					bool flag5 = this.m_subsystemTime.PeriodicGameTimeEvent(240.0, 9.0);
					bool flag6 = this.Thirst <= 0f;
					if (flag6)
					{
						bool flag7 = this.m_subsystemTime.PeriodicGameTimeEvent(30.0, 0.0);
						if (flag7)
						{
							string cause = LanguageControl.Get(new string[]
							{
								"Injury",
								"Dehydration"
							});
							ComponentPlayer componentPlayer3 = this.m_componentPlayer;
							if (componentPlayer3 != null)
							{
								componentPlayer3.ComponentHealth.Injure(0.1f, null, false, cause);
							}
							ComponentGui componentGui = this.m_componentGui;
							if (componentGui != null)
							{
								componentGui.DisplaySmallMessage(LanguageControl.Get(new string[]
								{
									"ComponentNewVitalStats",
									"Dehydrating"
								}), Color.Red, true, false);
							}
							ValueBarWidget thirstBarWidget = this.m_thirstBarWidget;
							if (thirstBarWidget != null)
							{
								thirstBarWidget.Flash(10);
							}
						}
					}
					else
					{
						bool flag8 = this.Thirst < 0.1f && (this.m_lastThirst >= 0.1f || flag5);
						if (flag8)
						{
							ComponentGui componentGui2 = this.m_componentGui;
							if (componentGui2 != null)
							{
								componentGui2.DisplaySmallMessage(LanguageControl.Get(new string[]
								{
									"ComponentNewVitalStats",
									"Dehydrating"
								}), Color.White, true, true);
							}
						}
						else
						{
							bool flag9 = this.Thirst < 0.25f && (this.m_lastThirst >= 0.25f || flag5);
							if (flag9)
							{
								ComponentGui componentGui3 = this.m_componentGui;
								if (componentGui3 != null)
								{
									componentGui3.DisplaySmallMessage(LanguageControl.Get(new string[]
									{
										"ComponentNewVitalStats",
										"HalfThirsty"
									}), Color.White, true, false);
								}
							}
						}
					}
					this.m_lastThirst = this.Thirst;
				}
				else
				{
					this.Thirst = 1f;
				}
			}
		}

		// Token: 0x040003A9 RID: 937
		private float m_lastThirst = 1f;

		// Token: 0x040003AA RID: 938
		private ValueBarWidget m_thirstBarWidget;

		// Token: 0x040003AB RID: 939
		private ComponentGui m_componentGui;

		// Token: 0x040003AC RID: 940
		private new Random m_random = new Random();

		// Token: 0x040003AD RID: 941
		private static Subtexture s_waterSubtexture;
	}
}
