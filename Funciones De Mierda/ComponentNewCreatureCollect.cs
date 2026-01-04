using System;
using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewCreatureCollect : ComponentBehavior, IUpdateable
	{
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		public void Update(float dt)
		{
			if ((double)this.m_satiation > 0.0)
			{
				this.m_satiation = MathUtils.Max(this.m_satiation - 0.01f * this.m_subsystemTime.GameTimeDelta, 0f);
			}
			this.m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemPickables = base.Project.FindSubsystem<SubsystemPickables>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.m_pickFactors = new Dictionary<string, float>();

			// CAMBIO AQUÍ: Ahora PickFactors es un string en lugar de ValuesDictionary
			string pickFactorsString = valuesDictionary.GetValue<string>("PickFactors", "");
			if (!string.IsNullOrEmpty(pickFactorsString))
			{
				string[] pairs = pickFactorsString.Split(',');
				foreach (string pair in pairs)
				{
					string[] parts = pair.Split(';');
					if (parts.Length == 2)
					{
						string category = parts[0].Trim();
						if (float.TryParse(parts[1].Trim(), out float factor))
						{
							this.m_pickFactors.Add(category, factor);
						}
					}
				}
			}

			SubsystemPickables subsystemPickables = this.m_subsystemPickables;
			subsystemPickables.PickableAdded = (Action<Pickable>)Delegate.Combine(subsystemPickables.PickableAdded, new Action<Pickable>(delegate (Pickable pickable)
			{
				if (!this.TryAddPickable(pickable) || this.m_pickable != null)
				{
					return;
				}
				this.m_pickable = pickable;
			}));
			SubsystemPickables subsystemPickables2 = this.m_subsystemPickables;
			subsystemPickables2.PickableRemoved = (Action<Pickable>)Delegate.Combine(subsystemPickables2.PickableRemoved, new Action<Pickable>(delegate (Pickable pickable)
			{
				this.m_pickables.Remove(pickable);
				if (this.m_pickable != pickable)
				{
					return;
				}
				this.m_pickable = null;
			}));
			this.m_stateMachine.AddState("Inactive", new Action(this.Inactive_Enter), new Action(this.Inactive_Update), null);
			this.m_stateMachine.AddState("Move", new Action(this.Move_Enter), new Action(this.Move_Update), null);
			this.m_stateMachine.AddState("PickableMoved", null, new Action(this.PickableMoved_Update), null);
			this.m_stateMachine.AddState("Pick", new Action(this.Pick_Enter), new Action(this.Pick_Update), null);
			this.m_stateMachine.TransitionTo("Inactive");
		}

		public Pickable FindPickable(Vector3 position)
		{
			if (this.m_subsystemTime.GameTime > this.m_nextPickablesUpdateTime)
			{
				this.m_nextPickablesUpdateTime = this.m_subsystemTime.GameTime + (double)this.m_random.Float(2f, 4f);
				this.m_pickables.Clear();
				foreach (Pickable pickable in this.m_subsystemPickables.Pickables)
				{
					this.TryAddPickable(pickable);
				}
				if (this.m_pickable != null && !this.m_pickables.ContainsKey(this.m_pickable))
				{
					this.m_pickable = null;
				}
			}
			foreach (Pickable pickable2 in this.m_pickables.Keys)
			{
				float num = Vector3.DistanceSquared(position, pickable2.Position);
				if ((double)this.m_random.Float(0f, 1f) > (double)num / 256.0)
				{
					return pickable2;
				}
			}
			return null;
		}

		public bool TryAddPickable(Pickable pickable)
		{
			string category = BlocksManager.Blocks[Terrain.ExtractContents(pickable.Value)].GetCategory(pickable.Value);
			if (!this.m_pickFactors.ContainsKey(category) || (double)this.m_pickFactors[category] <= 0.0 || (double)Vector3.DistanceSquared(pickable.Position, this.m_componentCreature.ComponentBody.Position) >= 256.0)
			{
				return false;
			}
			this.m_pickables.Add(pickable, true);
			return true;
		}

		public void Inactive_Enter()
		{
			this.m_importanceLevel = 0f;
			this.m_pickable = null;
		}

		public void Inactive_Update()
		{
			if ((double)this.m_satiation < 1.0)
			{
				if (this.m_pickable == null)
				{
					if (this.m_subsystemTime.GameTime > this.m_nextFindPickableTime)
					{
						this.m_nextFindPickableTime = this.m_subsystemTime.GameTime + (double)this.m_random.Float(2f, 4f);
						this.m_pickable = this.FindPickable(this.m_componentCreature.ComponentBody.Position);
					}
				}
				else
				{
					this.m_importanceLevel = this.m_random.Float(5f, 10f);
				}
			}
			if (!this.IsActive)
			{
				return;
			}
			this.m_stateMachine.TransitionTo("Move");
			this.m_blockedCount = 0;
		}

		public void Move_Enter()
		{
			if (this.m_pickable == null)
			{
				return;
			}
			this.m_componentPathfinding.SetDestination(new Vector3?(this.m_pickable.Position), ((double)this.m_satiation == 0.0) ? this.m_random.Float(0.5f, 0.7f) : 0.5f, 2f, ((double)this.m_satiation == 0.0) ? 1000 : 500, true, false, true, null);
			if ((double)this.m_random.Float(0f, 1f) >= 0.66)
			{
				return;
			}
			this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
		}

		public void Move_Update()
		{
			if (!this.IsActive)
			{
				this.m_stateMachine.TransitionTo("Inactive");
			}
			else if (this.m_pickable == null)
			{
				this.m_importanceLevel = 0f;
			}
			else if (this.m_componentPathfinding.IsStuck)
			{
				this.m_importanceLevel = 0f;
			}
			else if (this.m_componentPathfinding.Destination == null)
			{
				this.m_stateMachine.TransitionTo("Pick");
			}
			else if ((double)Vector3.DistanceSquared(this.m_componentPathfinding.Destination.Value, this.m_pickable.Position) > 0.0625)
			{
				this.m_stateMachine.TransitionTo("PickableMoved");
			}
			if ((double)this.m_random.Float(0f, 1f) < 0.1 * (double)this.m_subsystemTime.GameTimeDelta)
			{
				this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
			}
			if (this.m_pickable != null)
			{
				this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_pickable.Position);
				return;
			}
			this.m_componentCreature.ComponentCreatureModel.LookRandomOrder = true;
		}

		public void PickableMoved_Update()
		{
			if (this.m_pickable != null)
			{
				this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_pickable.Position);
			}
			if (!this.m_subsystemTime.PeriodicGameTimeEvent(0.25, (double)(this.GetHashCode() % 100) * 0.01))
			{
				return;
			}
			this.m_stateMachine.TransitionTo("Move");
		}

		public void Pick_Enter()
		{
			this.m_pickTime = (double)this.m_random.Float(0.2f, 0.5f);
			this.m_blockedTime = 0f;
		}

		public void Pick_Update()
		{
			if (!this.IsActive)
			{
				this.m_stateMachine.TransitionTo("Inactive");
			}
			if (this.m_pickable == null)
			{
				this.m_importanceLevel = 0f;
			}
			if (this.m_pickable != null)
			{
				if ((double)Vector3.DistanceSquared(new Vector3(this.m_componentCreature.ComponentCreatureModel.EyePosition.X, this.m_componentCreature.ComponentBody.Position.Y, this.m_componentCreature.ComponentCreatureModel.EyePosition.Z), this.m_pickable.Position) < 0.5625)
				{
					this.m_pickTime -= (double)this.m_subsystemTime.GameTimeDelta;
					this.m_blockedTime = 0f;
					if (this.m_pickTime <= 0.0)
					{
						int count = this.m_pickable.Count;
						this.m_pickable.Count = this.Pick(this.m_pickable);
						if (count == this.m_pickable.Count)
						{
							this.m_satiation += 1f;
						}
						if (this.m_pickable.Count == 0)
						{
							this.m_pickable.ToRemove = true;
							this.m_importanceLevel = 0f;
							this.m_subsystemAudio.PlaySound("Audio/PickableCollected", 0.7f, -0.4f, this.m_pickable.Position, 2f, false);
						}
						else if ((double)this.m_random.Float(0f, 1f) < 0.5)
						{
							this.m_importanceLevel = 0f;
						}
					}
				}
				else
				{
					this.m_componentPathfinding.SetDestination(new Vector3?(this.m_pickable.Position), 0.4f, 1.5f, 0, false, true, false, null);
					this.m_blockedTime += this.m_subsystemTime.GameTimeDelta;
				}
				if ((double)this.m_blockedTime > 3.0)
				{
					this.m_blockedCount++;
					if (this.m_blockedCount >= 3)
					{
						this.m_importanceLevel = 0f;
					}
					else
					{
						this.m_stateMachine.TransitionTo("Move");
					}
				}
			}
			this.m_componentCreature.ComponentCreatureModel.FeedOrder = true;
			if ((double)this.m_random.Float(0f, 1f) < 0.1 * (double)this.m_subsystemTime.GameTimeDelta)
			{
				this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
			}
			if ((double)this.m_random.Float(0f, 1f) >= 1.5 * (double)this.m_subsystemTime.GameTimeDelta)
			{
				return;
			}
			this.m_componentCreature.ComponentCreatureSounds.PlayFootstepSound(2f);
		}

		public int Pick(Pickable pickable)
		{
			IInventory inventory = this.m_componentMiner.Inventory;
			if (ComponentInventoryBase.FindAcquireSlotForItem(inventory, pickable.Value) < 0)
			{
				return pickable.Count;
			}
			this.m_componentCreature.Entity.FindComponent<ComponentMiner>(true).Poke(false);
			pickable.Count = ComponentInventoryBase.AcquireItems(inventory, pickable.Value, pickable.Count);
			return pickable.Count;
		}

		public override void OnEntityAdded()
		{
			IInventory inventory = this.m_componentMiner.Inventory;
			if (inventory == null || inventory.GetSlotCount(0) != 0)
			{
				return;
			}

			float randomChance = this.m_random.Float(0f, 1f);
			string name = base.Entity.ValuesDictionary.DatabaseObject.Name;

			if (name == "InfectedNormal1" || name == "InfectedNormal2" || name == "InfectedMuscle1" || name == "InfectedMuscle2" || name == "Werewolf" || name == "CapitanPirata" || name == "PirataNormal" || name == "PirataElite" || name == "PirataHostilComerciante" || name == "GhostNormal")
			{
				int weaponValue = 0;

				// Primero decidir el tipo de arma (40% probabilidad de armas principales)
				if (randomChance < 0.40f)
				{
					// 40% probabilidad de armas principales
					float mainWeaponChance = this.m_random.Float(0f, 1f);

					if (mainWeaponChance < 0.20f) // 20% de las armas principales: Mosquete
					{
						weaponValue = Terrain.MakeBlockValue(MusketBlock.Index);
					}
					else if (mainWeaponChance < 0.40f) // 20% de las armas principales: Arco
					{
						weaponValue = Terrain.MakeBlockValue(BowBlock.Index);
					}
					else if (mainWeaponChance < 0.60f) // 20% de las armas principales: Ballesta
					{
						weaponValue = Terrain.MakeBlockValue(CrossbowBlock.Index);
					}
					else if (mainWeaponChance < 0.80f) // 20% de las armas principales: Ballesta repetidora
					{
						weaponValue = Terrain.MakeBlockValue(RepeatCrossbowBlock.Index);
					}
					else // 20% de las armas principales: Lanzallamas
					{
						// Lanzallamas cargado con 8 disparos (50% fuego, 50% veneno)
						weaponValue = FlameThrowerBlock.SetLoadCount(
							Terrain.MakeBlockValue(FlameThrowerBlock.Index, 0,
								FlameThrowerBlock.SetBulletType(
									FlameThrowerBlock.SetLoadState(0, FlameThrowerBlock.LoadState.Loaded),
									new FlameBulletBlock.FlameBulletType?(this.m_random.Bool(0.5f) ?
										FlameBulletBlock.FlameBulletType.Flame :
										FlameBulletBlock.FlameBulletType.Poison)
								)
							),
							8
						);
					}
				}
				else
				{
					// 60% probabilidad de armas tradicionales (machetes, spears, axes)
					float weaponTypeChance = this.m_random.Float(0f, 1f);

					if (weaponTypeChance < 0.3333f) // 1/3 - Machetes
					{
						float macheteRoll = this.m_random.Float(0f, 1f);
						if (macheteRoll < 0.1667f) // 16.67% - WoodenMachete
						{
							weaponValue = Terrain.MakeBlockValue(WoodMacheteBlock.Index);
						}
						else if (macheteRoll < 0.3333f) // 16.67% - StoneMachete
						{
							weaponValue = Terrain.MakeBlockValue(StoneMacheteBlock.Index);
						}
						else if (macheteRoll < 0.50f) // 16.67% - CopperMachete
						{
							weaponValue = Terrain.MakeBlockValue(CopperMacheteBlock.Index);
						}
						else if (macheteRoll < 0.6667f) // 16.67% - IronMachete
						{
							weaponValue = Terrain.MakeBlockValue(IronMacheteBlock.Index);
						}
						else if (macheteRoll < 0.8333f) // 16.67% - DiamondMachete
						{
							weaponValue = Terrain.MakeBlockValue(DiamondMacheteBlock.Index);
						}
						else // 16.67% - LavaMachete
						{
							weaponValue = Terrain.MakeBlockValue(LavaMacheteBlock.Index);
						}
					}
					else if (weaponTypeChance < 0.6666f) // 2/3 - Spears
					{
						float spearRoll = this.m_random.Float(0f, 1f);
						if (spearRoll < 0.1667f) // 16.67% - WoodenSpear
						{
							weaponValue = Terrain.MakeBlockValue(WoodenSpearBlock.Index);
						}
						else if (spearRoll < 0.3333f) // 16.67% - StoneSpear
						{
							weaponValue = Terrain.MakeBlockValue(StoneSpearBlock.Index);
						}
						else if (spearRoll < 0.50f) // 16.67% - CopperSpear
						{
							weaponValue = Terrain.MakeBlockValue(CopperSpearBlock.Index);
						}
						else if (spearRoll < 0.6667f) // 16.67% - IronSpear
						{
							weaponValue = Terrain.MakeBlockValue(IronSpearBlock.Index);
						}
						else if (spearRoll < 0.8333f) // 16.67% - DiamondSpear
						{
							weaponValue = Terrain.MakeBlockValue(DiamondSpearBlock.Index);
						}
						else // 16.67% - LavaSpear
						{
							weaponValue = Terrain.MakeBlockValue(LavaSpearBlock.Index);
						}
					}
					else // 3/3 - Axes
					{
						float axeRoll = this.m_random.Float(0f, 1f);
						if (axeRoll < 0.1667f) // 16.67% - WoodenAxe
						{
							weaponValue = Terrain.MakeBlockValue(WoodAxeBlock.Index);
						}
						else if (axeRoll < 0.3333f) // 16.67% - StoneAxeOriginal
						{
							weaponValue = Terrain.MakeBlockValue(StoneAxeOriginalBlock.Index);
						}
						else if (axeRoll < 0.50f) // 16.67% - CopperAxe
						{
							weaponValue = Terrain.MakeBlockValue(CopperAxeBlock.Index);
						}
						else if (axeRoll < 0.6667f) // 16.67% - IronAxe
						{
							weaponValue = Terrain.MakeBlockValue(IronAxeBlock.Index);
						}
						else if (axeRoll < 0.8333f) // 16.67% - DiamondAxe
						{
							weaponValue = Terrain.MakeBlockValue(DiamondAxeBlock.Index);
						}
						else // 16.67% - LavaAxe
						{
							weaponValue = Terrain.MakeBlockValue(LavaAxeBlock.Index);
						}
					}
				}

				if (weaponValue != 0)
				{
					inventory.AddSlotItems(0, weaponValue, 1);
				}
			}
		}

		// Agrega un método público para copiar inventario
		public void CopyInventoryFrom(ComponentNewCreatureCollect source)
		{
			if (source == null || this.m_componentMiner == null || source.m_componentMiner == null)
				return;

			IInventory sourceInventory = source.m_componentMiner.Inventory;
			IInventory targetInventory = this.m_componentMiner.Inventory;

			if (sourceInventory == null || targetInventory == null)
				return;

			Console.WriteLine($"Copiando inventario de {source.Entity.ValuesDictionary.DatabaseObject.Name} a {this.Entity.ValuesDictionary.DatabaseObject.Name}");
			Console.WriteLine($"Source slots: {sourceInventory.SlotsCount}, Target slots: {targetInventory.SlotsCount}");

			// Copiar todos los slots del inventario
			for (int i = 0; i < sourceInventory.SlotsCount && i < targetInventory.SlotsCount; i++)
			{
				int slotValue = sourceInventory.GetSlotValue(i);
				int slotCount = sourceInventory.GetSlotCount(i);

				Console.WriteLine($"Slot {i}: Valor={slotValue}, Cantidad={slotCount}");

				if (slotValue != 0)
				{
					// Limpiar el slot actual si tiene algo
					int currentCount = targetInventory.GetSlotCount(i);
					if (currentCount > 0)
					{
						targetInventory.RemoveSlotItems(i, currentCount);
					}
					// Copiar los items del source
					targetInventory.AddSlotItems(i, slotValue, slotCount);
					Console.WriteLine($"  Copiado al slot {i}: {slotCount}x {slotValue}");
				}
			}
		}

		public SubsystemTime m_subsystemTime;
		public SubsystemPickables m_subsystemPickables;
		public Dictionary<Pickable, bool> m_pickables = new Dictionary<Pickable, bool>();
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public StateMachine m_stateMachine = new StateMachine();
		public Dictionary<string, float> m_pickFactors;
		public float m_importanceLevel;
		public double m_nextFindPickableTime;
		public double m_nextPickablesUpdateTime;
		public Pickable m_pickable;
		public double m_pickTime;
		public float m_satiation;
		public float m_blockedTime;
		public SubsystemAudio m_subsystemAudio;
		public int m_blockedCount;
		private ComponentMiner m_componentMiner;
		public Game.Random m_random = new Game.Random();
	}
}
