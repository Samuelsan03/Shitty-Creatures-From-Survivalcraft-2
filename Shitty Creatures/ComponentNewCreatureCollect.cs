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

			DifficultyMode currentDifficulty = DifficultyMode.Normal;
			var greenNight = Project.FindSubsystem<SubsystemGreenNightSky>(true);
			if (greenNight != null)
			{
				currentDifficulty = greenNight.DifficultyMode;
			}
			bool isHardOrHigher = (currentDifficulty >= DifficultyMode.Hard);

			// MÉTODO LOCAL: Agrega un item de forma segura buscando el primer slot vacío válido
			int AddSafe(int value, int count = 1, int startSlot = 0)
			{
				if (value == 0 || Terrain.ExtractContents(value) <= 0 || Terrain.ExtractContents(value) >= 1024) return -1;
				for (int i = startSlot; i < inventory.SlotsCount; i++)
				{
					if (inventory.GetSlotCount(i) == 0 && inventory.GetSlotCapacity(i, value) >= count)
					{
						inventory.AddSlotItems(i, value, count);
						return i;
					}
				}
				return -1;
			}

			// MÉTODO LOCAL: Armas a distancia NORMALES (Sin armas de fuego, para Piratas/Werewolf)
			int GetNormalRanged()
			{
				float r = this.m_random.Float(0f, 1f);
				if (r < 0.20f) return Terrain.MakeBlockValue(MusketBlock.Index);
				else if (r < 0.40f) return Terrain.MakeBlockValue(BowBlock.Index);
				else if (r < 0.60f) return Terrain.MakeBlockValue(CrossbowBlock.Index);
				else if (r < 0.80f) return Terrain.MakeBlockValue(RepeatCrossbowBlock.Index);
				else return FlameThrowerBlock.SetLoadCount(Terrain.MakeBlockValue(FlameThrowerBlock.Index, 0, FlameThrowerBlock.SetBulletType(FlameThrowerBlock.SetLoadState(0, FlameThrowerBlock.LoadState.Loaded), new FlameBulletBlock.FlameBulletType?(this.m_random.Bool(0.5f) ? FlameBulletBlock.FlameBulletType.Flame : FlameBulletBlock.FlameBulletType.Poison))), 8);
			}

			// MÉTODO LOCAL: EXCLUSIVO PARA INFECTADOS. Arma a distancia o arma de fuego (5% prob) DE FORMA SEGURA
			int GetInfectedRangedOrFirearm()
			{
				if (this.m_random.Float(0f, 1f) < 0.05f)
				{
					string[] firearmNames = new string[] { "AKBlock", "SPAS12Block", "SWM500Block", "BK43Block", "M4Block", "AK48Block", "AUGBlock", "P90Block", "SCARBlock", "M249Block", "SniperBlock", "Izh43Block", "KABlock", "G3Block", "NewG3Block", "MendozaBlock", "GrozaBlock", "Master308Block", "AA12Block", "MinigunBlock", "Mac10Block", "UziBlock", "MP5SSDBlock", "FamasBlock", "RevolverBlock" };
					string chosenFirearm = firearmNames[this.m_random.Int(0, firearmNames.Length)];
					int firearmIndex = BlocksManager.GetBlockIndex(chosenFirearm);

					// SEGURIDAD: Evitar crasheo si el bloque del arma de fuego no existe
					if (firearmIndex > 0 && firearmIndex < 1024) return Terrain.MakeBlockValue(firearmIndex);
				}

				float r = this.m_random.Float(0f, 1f);
				if (r < 0.20f) return Terrain.MakeBlockValue(MusketBlock.Index);
				else if (r < 0.40f) return Terrain.MakeBlockValue(BowBlock.Index);
				else if (r < 0.60f) return Terrain.MakeBlockValue(CrossbowBlock.Index);
				else if (r < 0.80f) return Terrain.MakeBlockValue(RepeatCrossbowBlock.Index);
				else return FlameThrowerBlock.SetLoadCount(Terrain.MakeBlockValue(FlameThrowerBlock.Index, 0, FlameThrowerBlock.SetBulletType(FlameThrowerBlock.SetLoadState(0, FlameThrowerBlock.LoadState.Loaded), new FlameBulletBlock.FlameBulletType?(this.m_random.Bool(0.5f) ? FlameBulletBlock.FlameBulletType.Flame : FlameBulletBlock.FlameBulletType.Poison))), 8);
			}

			// MÉTODO LOCAL: Genera un arma cuerpo a cuerpo genérica
			int GetRandomMelee()
			{
				float weaponTypeChance = this.m_random.Float(0f, 1f);
				if (weaponTypeChance < 0.25f)
				{
					float r = this.m_random.Float(0f, 1f);
					if (r < 0.5f) return Terrain.MakeBlockValue(WoodenClubBlock.Index);
					else return Terrain.MakeBlockValue(StoneClubBlock.Index);
				}
				else if (weaponTypeChance < 0.50f)
				{
					float r = this.m_random.Float(0f, 1f);
					if (r < 0.1667f) return Terrain.MakeBlockValue(WoodMacheteBlock.Index);
					else if (r < 0.3333f) return Terrain.MakeBlockValue(StoneMacheteBlock.Index);
					else if (r < 0.5f) return Terrain.MakeBlockValue(CopperMacheteBlock.Index);
					else if (r < 0.6667f) return Terrain.MakeBlockValue(IronMacheteBlock.Index);
					else if (r < 0.8333f) return Terrain.MakeBlockValue(DiamondMacheteBlock.Index);
					else return Terrain.MakeBlockValue(LavaMacheteBlock.Index);
				}
				else if (weaponTypeChance < 0.75f)
				{
					float r = this.m_random.Float(0f, 1f);
					if (r < 0.1667f) return Terrain.MakeBlockValue(WoodenSpearBlock.Index);
					else if (r < 0.3333f) return Terrain.MakeBlockValue(StoneSpearBlock.Index);
					else if (r < 0.5f) return Terrain.MakeBlockValue(CopperSpearBlock.Index);
					else if (r < 0.6667f) return Terrain.MakeBlockValue(IronSpearBlock.Index);
					else if (r < 0.8333f) return Terrain.MakeBlockValue(DiamondSpearBlock.Index);
					else return Terrain.MakeBlockValue(LavaSpearBlock.Index);
				}
				else
				{
					float r = this.m_random.Float(0f, 1f);
					if (r < 0.1667f) return Terrain.MakeBlockValue(WoodAxeBlock.Index);
					else if (r < 0.3333f) return Terrain.MakeBlockValue(StoneAxeOriginalBlock.Index);
					else if (r < 0.5f) return Terrain.MakeBlockValue(CopperAxeBlock.Index);
					else if (r < 0.6667f) return Terrain.MakeBlockValue(IronAxeBlock.Index);
					else if (r < 0.8333f) return Terrain.MakeBlockValue(DiamondAxeBlock.Index);
					else return Terrain.MakeBlockValue(LavaAxeBlock.Index);
				}
			}

			// MÉTODO LOCAL: Agrega bombas de forma 100% segura
			void AddBombsToInventory(int startSlot)
			{
				float bombTypeChance = this.m_random.Float(0f, 1f);
				int bombValue = 0;
				if (bombTypeChance < 0.3333f) bombValue = Terrain.MakeBlockValue(BombBlock.Index);
				else if (bombTypeChance < 0.6666f) bombValue = Terrain.MakeBlockValue(IncendiaryBombBlock.Index);
				else bombValue = Terrain.MakeBlockValue(PoisonBombBlock.Index);

				if (bombValue != 0)
				{
					int bombCount = isHardOrHigher ? this.m_random.Int(8, 12) : this.m_random.Int(4, 8);
					int remainingBombs = bombCount;

					for (int i = startSlot; i < inventory.SlotsCount && remainingBombs > 0; i++)
					{
						if (inventory.GetSlotValue(i) == bombValue)
						{
							int currentCount = inventory.GetSlotCount(i);
							int spaceLeft = inventory.GetSlotCapacity(i, bombValue) - currentCount;
							if (spaceLeft > 0)
							{
								int addAmount = Math.Min(spaceLeft, remainingBombs);
								inventory.AddSlotItems(i, bombValue, addAmount);
								remainingBombs -= addAmount;
							}
						}
					}
					for (int i = startSlot; i < inventory.SlotsCount && remainingBombs > 0; i++)
					{
						if (inventory.GetSlotCount(i) == 0)
						{
							int addAmount = Math.Min(inventory.GetSlotCapacity(i, bombValue), remainingBombs);
							if (addAmount > 0)
							{
								inventory.AddSlotItems(i, bombValue, addAmount);
								remainingBombs -= addAmount;
							}
						}
					}
				}
			}


			// =====================================================================
			// LÓGICA DE INVENTARIOS POR CRIATURA
			// =====================================================================

			// ===== CAPITAN PIRATA =====
			if (name == "CapitanPirata")
			{
				string spawnCreatureName = m_random.Bool(0.5f) ? "PirataElite" : "PirataNormal";
				EggBlock eggBlock = BlocksManager.Blocks[EggBlock.Index] as EggBlock;
				EggBlock.EggType eggType = eggBlock?.GetEggTypeByCreatureTemplateName(spawnCreatureName) ?? eggBlock?.GetEggType(0);
				int eggData = EggBlock.SetEggType(0, eggType.EggTypeIndex);
				int eggValue = Terrain.MakeBlockValue(EggBlock.Index, 0, eggData);
				int eggCount = 10;
				int eggSlot = -1;
				for (int i = 0; i < inventory.SlotsCount; i++)
				{
					if (inventory.GetSlotCapacity(i, eggValue) >= eggCount) { eggSlot = i; break; }
				}
				if (eggSlot != -1) inventory.AddSlotItems(eggSlot, eggValue, eggCount);
				else
				{
					int rem = eggCount;
					for (int i = 0; i < inventory.SlotsCount && rem > 0; i++)
					{
						int cap = inventory.GetSlotCapacity(i, eggValue);
						if (cap > 0 && inventory.GetSlotCount(i) == 0) { int add = Math.Min(cap, rem); inventory.AddSlotItems(i, eggValue, add); rem -= add; }
					}
				}

				int rangedWeaponValue = m_random.Bool(0.5f) ? Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("FlameThrowerBlock")) : Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("RepeatCrossbowBlock"));
				if (rangedWeaponValue > 0) AddSafe(rangedWeaponValue, 1, eggSlot != -1 && eggSlot == 0 ? 1 : 0);

				int meleeWeaponValue = GetRandomMelee();
				if (meleeWeaponValue != 0) AddSafe(meleeWeaponValue, 1, eggSlot != -1 && eggSlot == 0 ? 1 : 0);
			}
			// ===== PIRATA HOSTIL COMERCIANTE =====
			else if (name == "PirataHostilComerciante")
			{
				string spawnCreatureName = m_random.Bool(0.5f) ? "PirataElite" : "PirataNormal";
				EggBlock eggBlock = BlocksManager.Blocks[EggBlock.Index] as EggBlock;
				EggBlock.EggType eggType = eggBlock?.GetEggTypeByCreatureTemplateName(spawnCreatureName) ?? eggBlock?.GetEggType(0);
				int eggData = EggBlock.SetEggType(0, eggType.EggTypeIndex);
				int eggValue = Terrain.MakeBlockValue(EggBlock.Index, 0, eggData);
				int eggCount = 10;
				int eggSlot = -1;
				for (int i = 0; i < inventory.SlotsCount; i++)
				{
					if (inventory.GetSlotCapacity(i, eggValue) >= eggCount) { eggSlot = i; break; }
				}
				if (eggSlot != -1) inventory.AddSlotItems(eggSlot, eggValue, eggCount);
				else
				{
					int rem = eggCount;
					for (int i = 0; i < inventory.SlotsCount && rem > 0; i++)
					{
						int cap = inventory.GetSlotCapacity(i, eggValue);
						if (cap > 0 && inventory.GetSlotCount(i) == 0) { int add = Math.Min(cap, rem); inventory.AddSlotItems(i, eggValue, add); rem -= add; }
					}
				}

				float r = m_random.Float(0f, 1f);
				int rangedWeaponValue = 0;
				if (r < 0.45f) rangedWeaponValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("FlameThrowerBlock"));
				else if (r < 0.9f) rangedWeaponValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("RepeatCrossbowBlock"));
				else rangedWeaponValue = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("MusketBlock"));

				if (rangedWeaponValue > 0) AddSafe(rangedWeaponValue, 1, eggSlot != -1 && eggSlot == 0 ? 1 : 0);

				int meleeWeaponValue = GetRandomMelee();
				if (meleeWeaponValue != 0) AddSafe(meleeWeaponValue, 1, eggSlot != -1 && eggSlot == 0 ? 1 : 0);
			}
			// ===== PIRATA ELITE =====
			else if (name == "PirataElite")
			{
				int weaponValue = GetNormalRanged(); // Usa el método normal
				if (weaponValue > 0) AddSafe(weaponValue);

				int meleeValue = GetRandomMelee();
				if (meleeValue != 0) AddSafe(meleeValue, 1, 1);

				if (m_random.Float(0f, 1f) < 0.10f)
				{
					int bombValue = m_random.Bool(0.5f) ? Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("BombBlock")) : Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("IncendiaryBombBlock"));
					if (bombValue > 0) AddSafe(bombValue, 5, 1);
				}
			}
			// ===== PIRATA NORMAL =====
			else if (name == "PirataNormal")
			{
				int weaponValue = GetNormalRanged(); // Usa el método normal
				if (weaponValue > 0) AddSafe(weaponValue);

				int meleeValue = GetRandomMelee();
				if (meleeValue != 0) AddSafe(meleeValue, 1, 1);

				if (m_random.Float(0f, 1f) < 0.20f)
				{
					int bombValue = m_random.Bool(0.5f) ? Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("BombBlock")) : Terrain.MakeBlockValue(BlocksManager.GetBlockIndex("IncendiaryBombBlock"));
					if (bombValue > 0) AddSafe(bombValue, 5, 1);
				}
			}
			// ===== WEREWOLF =====
			else if (name == "Werewolf")
			{
				int weaponValue = 0;
				if (randomChance < 0.40f) weaponValue = GetNormalRanged(); // Usa el método normal
				else weaponValue = GetRandomMelee();

				if (weaponValue > 0) AddSafe(weaponValue);

				if (randomChance < 0.20f) AddBombsToInventory(0);
			}
			// ===== INFECTADOS COMUNES (Esqueletos, Fantasmas, Boomers, etc) =====
			else if (name == "InfectedNormal1" || name == "InfectedNormal2" || name == "InfectedMuscle1" || name == "InfectedMuscle2" || name == "GhostNormal" || name == "GhostFast" || name == "Boomer1" || name == "Boomer2" || name == "Boomer3" || name == "GhostBoomer1" || name == "GhostBoomer2" || name == "GhostBoomer3" || name == "HumanoidSkeleton")
			{
				int firstSlotValue = 0;
				int secondSlotValue = 0;

				if (!isHardOrHigher)
				{
					if (m_random.Float(0f, 1f) < 0.7f) firstSlotValue = GetRandomMelee();
				}
				else
				{
					float mainChoice = this.m_random.Float(0f, 1f);

					if (mainChoice < 0.55f)
					{
						firstSlotValue = GetRandomMelee();
						secondSlotValue = GetInfectedRangedOrFirearm(); // MÉTODO EXCLUSIVO CON ARMAS DE FUEGO
					}
					else if (mainChoice < 0.90f)
					{
						firstSlotValue = GetInfectedRangedOrFirearm(); // MÉTODO EXCLUSIVO CON ARMAS DE FUEGO
						secondSlotValue = GetRandomMelee();
					}
					else
					{
						float throwChoice = this.m_random.Float(0f, 1f);
						if (throwChoice < 0.50f)
						{
							firstSlotValue = GetRandomMelee();
						}
						else
						{
							float bombTypeChance = this.m_random.Float(0f, 1f);
							if (bombTypeChance < 0.3333f) firstSlotValue = Terrain.MakeBlockValue(BombBlock.Index);
							else if (bombTypeChance < 0.6666f) firstSlotValue = Terrain.MakeBlockValue(IncendiaryBombBlock.Index);
							else firstSlotValue = Terrain.MakeBlockValue(PoisonBombBlock.Index);

							if (firstSlotValue != 0)
							{
								int bombCount = this.m_random.Int(8, 12);
								int slotCapacity = inventory.GetSlotCapacity(0, firstSlotValue);
								int addCount = Math.Min(bombCount, slotCapacity);
								if (addCount > 0) inventory.AddSlotItems(0, firstSlotValue, addCount);
								firstSlotValue = 0;
							}
						}

						if (firstSlotValue != 0)
						{
							float bombTypeChance2 = this.m_random.Float(0f, 1f);
							int bombValue = 0;
							if (bombTypeChance2 < 0.3333f) bombValue = Terrain.MakeBlockValue(BombBlock.Index);
							else if (bombTypeChance2 < 0.6666f) bombValue = Terrain.MakeBlockValue(IncendiaryBombBlock.Index);
							else bombValue = Terrain.MakeBlockValue(PoisonBombBlock.Index);

							if (bombValue != 0)
							{
								int bombCount = this.m_random.Int(8, 12);
								int slotCapacity = inventory.GetSlotCapacity(1, bombValue);
								int addCount = Math.Min(bombCount, slotCapacity);
								if (addCount > 0) inventory.AddSlotItems(1, bombValue, addCount);
							}
						}
					}
				}

				if (firstSlotValue > 0) AddSafe(firstSlotValue);
				if (secondSlotValue > 0) AddSafe(secondSlotValue, 1, 1);

				if (isHardOrHigher && this.m_random.Float(0f, 1f) < 0.25f) AddBombsToInventory(2);
			}
			// ===== CRIATURAS DE HIELO =====
			else if (name == "InfectedFreezer" || name == "FrozenGhostBoomer" || name == "BoomerFrozen" || name == "FrozenGhost")
			{
				int freezingSnowballIndex = BlocksManager.GetBlockIndex("FreezingSnowballBlock");
				int freezeBombIndex = BlocksManager.GetBlockIndex("FreezeBombBlock");

				float emptyChance = isHardOrHigher ? 0.2f : 0.3333f;
				float mainChoice = m_random.Float(0f, 1f);

				if (mainChoice >= emptyChance)
				{
					bool hasSnowball = isHardOrHigher ? m_random.Float(0f, 1f) < 0.8f : m_random.Float(0f, 1f) < 0.5f;
					bool hasFreezeBomb = isHardOrHigher ? m_random.Float(0f, 1f) < 0.01f : m_random.Float(0f, 1f) < 0.0005f;
					bool hasMeleeWeapon = isHardOrHigher ? m_random.Float(0f, 1f) < 0.8f : m_random.Float(0f, 1f) < 0.5f;
					bool hasRangedWeapon = isHardOrHigher ? m_random.Float(0f, 1f) < 0.15f : false;

					if (hasSnowball && freezingSnowballIndex > 0 && freezingSnowballIndex < 1024)
					{
						int snowballValue = Terrain.MakeBlockValue(freezingSnowballIndex);
						int snowballCount = isHardOrHigher ? Math.Min(m_random.Int(20, 40), inventory.GetSlotCapacity(0, snowballValue)) : Math.Min(m_random.Bool() ? 40 : 5, inventory.GetSlotCapacity(0, snowballValue));
						if (snowballCount > 0) inventory.AddSlotItems(0, snowballValue, snowballCount);
					}

					if (hasFreezeBomb && freezeBombIndex > 0 && freezeBombIndex < 1024)
					{
						int freezeBombValue = Terrain.MakeBlockValue(freezeBombIndex);
						int freezeBombCount = isHardOrHigher ? Math.Min(m_random.Int(10, 20), inventory.GetSlotCapacity(0, freezeBombValue)) : Math.Min(m_random.Bool() ? 40 : 5, inventory.GetSlotCapacity(0, freezeBombValue));
						if (freezeBombCount > 0)
						{
							if (hasSnowball && inventory.GetSlotCount(0) > 0) AddSafe(freezeBombValue, freezeBombCount, 1);
							else inventory.AddSlotItems(0, freezeBombValue, freezeBombCount);
						}
					}

					if (hasRangedWeapon)
					{
						int rangedValue = GetInfectedRangedOrFirearm(); // MÉTODO EXCLUSIVO CON ARMAS DE FUEGO
						if (rangedValue > 0) AddSafe(rangedValue, 1, 1);
					}

					if (hasMeleeWeapon)
					{
						int weaponValue = GetRandomMelee();
						if (weaponValue != 0) AddSafe(weaponValue, 1, 1);
					}
				}
			}
		}

		public void CopyInventoryFrom(ComponentNewCreatureCollect source)
		{
			if (source == null || this.m_componentMiner == null || source.m_componentMiner == null) return;

			IInventory sourceInventory = source.m_componentMiner.Inventory;
			IInventory targetInventory = this.m_componentMiner.Inventory;

			if (sourceInventory == null || targetInventory == null) return;

			for (int i = 0; i < sourceInventory.SlotsCount && i < targetInventory.SlotsCount; i++)
			{
				int slotValue = sourceInventory.GetSlotValue(i);
				int slotCount = sourceInventory.GetSlotCount(i);

				if (slotValue != 0)
				{
					int currentCount = targetInventory.GetSlotCount(i);
					if (currentCount > 0) targetInventory.RemoveSlotItems(i, currentCount);
					targetInventory.AddSlotItems(i, slotValue, slotCount);
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
