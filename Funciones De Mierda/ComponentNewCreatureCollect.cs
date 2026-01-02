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
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int count = 1;
			float num4 = this.m_random.Float(0f, 1f);
			string name = base.Entity.ValuesDictionary.DatabaseObject.Name;

			if (name == "InfectedNormal1")
			{
				// Probabilidad equitativa para todas las armas (aproximadamente 5.26% cada una)
				// 19 opciones en total (sin ItemsLauncher, sin spears como secundarias)
				if (num4 < 0.0526f) // 1/19 - StoneClubBlock
				{
					num = Terrain.MakeBlockValue(StoneClubBlock.Index);
				}
				else if (num4 < 0.1052f) // 2/19 - WoodenClubBlock
				{
					num = Terrain.MakeBlockValue(WoodenClubBlock.Index);
				}
				else if (num4 < 0.1578f) // 3/19 - DiamondAxeBlock
				{
					num = Terrain.MakeBlockValue(DiamondAxeBlock.Index);
				}
				else if (num4 < 0.2104f) // 4/19 - IronMacheteBlock
				{
					num = Terrain.MakeBlockValue(IronMacheteBlock.Index);
				}
				else if (num4 < 0.263f) // 5/19 - DiamondMacheteBlock
				{
					num = Terrain.MakeBlockValue(DiamondMacheteBlock.Index);
				}
				else if (num4 < 0.3156f) // 6/19 - IronAxeBlock
				{
					num = Terrain.MakeBlockValue(IronAxeBlock.Index);
				}
				else if (num4 < 0.3682f) // 7/19 - CopperAxeBlock
				{
					num = Terrain.MakeBlockValue(CopperAxeBlock.Index);
				}
				else if (num4 < 0.4208f) // 8/19 - StoneSpearBlock
				{
					num = Terrain.MakeBlockValue(StoneSpearBlock.Index);
				}
				else if (num4 < 0.4734f) // 9/19 - WoodenSpearBlock
				{
					num = Terrain.MakeBlockValue(WoodenSpearBlock.Index);
				}
				else if (num4 < 0.526f) // 10/19 - IronSpearBlock
				{
					num = Terrain.MakeBlockValue(IronSpearBlock.Index);
				}
				else if (num4 < 0.5786f) // 11/19 - DiamondSpearBlock
				{
					num = Terrain.MakeBlockValue(DiamondSpearBlock.Index);
				}
				else if (num4 < 0.6312f) // 12/19 - CopperSpearBlock
				{
					num = Terrain.MakeBlockValue(CopperSpearBlock.Index);
				}
				else if (num4 < 0.6838f) // 13/19 - WoodenAxeBlock
				{
					num = Terrain.MakeBlockValue(WoodAxeBlock.Index);
				}
				else if (num4 < 0.7364f) // 14/19 - WoodenMacheteBlock
				{
					num = Terrain.MakeBlockValue(WoodMacheteBlock.Index);
				}
				else if (num4 < 0.789f) // 15/19 - StoneAxeOriginalBlock
				{
					num = Terrain.MakeBlockValue(StoneAxeOriginalBlock.Index);
				}
				else if (num4 < 0.8416f) // 16/19 - StoneMacheteBlock
				{
					num = Terrain.MakeBlockValue(StoneMacheteBlock.Index);
				}
				else if (num4 < 0.8942f) // 17/19 - LavaAxeBlock
				{
					num = Terrain.MakeBlockValue(LavaAxeBlock.Index);
				}
				else if (num4 < 0.9468f) // 18/19 - LavaMacheteBlock
				{
					num = Terrain.MakeBlockValue(LavaMacheteBlock.Index);
				}
				else // 19/19 - LavaSpearBlock (último 5.26%)
				{
					num = Terrain.MakeBlockValue(LavaSpearBlock.Index);
				}

				// SELECCIÓN DE ARMA SECUNDARIA (solo axes y machetes, sin spears)
				float secondaryWeaponRoll = this.m_random.Float(0f, 1f);

				// Lista de armas secundarias permitidas (axes y machetes)
				if (secondaryWeaponRoll < 0.1429f) // 1/7 - DiamondAxeBlock
				{
					num2 = Terrain.MakeBlockValue(DiamondAxeBlock.Index);
				}
				else if (secondaryWeaponRoll < 0.2858f) // 2/7 - IronMacheteBlock
				{
					num2 = Terrain.MakeBlockValue(IronMacheteBlock.Index);
				}
				else if (secondaryWeaponRoll < 0.4287f) // 3/7 - DiamondMacheteBlock
				{
					num2 = Terrain.MakeBlockValue(DiamondMacheteBlock.Index);
				}
				else if (secondaryWeaponRoll < 0.5716f) // 4/7 - IronAxeBlock
				{
					num2 = Terrain.MakeBlockValue(IronAxeBlock.Index);
				}
				else if (secondaryWeaponRoll < 0.7145f) // 5/7 - CopperAxeBlock
				{
					num2 = Terrain.MakeBlockValue(CopperAxeBlock.Index);
				}
				else if (secondaryWeaponRoll < 0.8574f) // 6/7 - WoodenAxeBlock
				{
					num2 = Terrain.MakeBlockValue(WoodAxeBlock.Index);
				}
				else // 7/7 - LavaAxeBlock
				{
					num2 = Terrain.MakeBlockValue(LavaAxeBlock.Index);
				}

				// Verificar que el arma secundaria no sea la misma que la primaria
				if (num2 == num)
				{
					// Si son iguales, usar LavaMacheteBlock como alternativa
					num2 = Terrain.MakeBlockValue(LavaMacheteBlock.Index);
				}
			}

			if (num != 0)
			{
				inventory.AddSlotItems(0, num, count);
			}
			if (num2 != 0 && inventory.GetSlotCount(1) == 0)
			{
				inventory.AddSlotItems(1, num2, 1);
			}
			if (num3 != 0 && inventory.GetSlotCount(2) == 0)
			{
				inventory.AddSlotItems(2, num3, 1);
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
