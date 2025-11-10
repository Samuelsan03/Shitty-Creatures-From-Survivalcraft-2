using System;
using System.Globalization;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000171 RID: 369
	public class ComponentMiner : Component, IUpdateable
	{
		// Token: 0x17000138 RID: 312
		// (get) Token: 0x06000AC8 RID: 2760 RVA: 0x0004365F File Offset: 0x0004185F
		// (set) Token: 0x06000AC9 RID: 2761 RVA: 0x00043667 File Offset: 0x00041867
		public virtual double HitInterval { get; set; }

		// Token: 0x17000139 RID: 313
		// (get) Token: 0x06000ACA RID: 2762 RVA: 0x00043670 File Offset: 0x00041870
		// (set) Token: 0x06000ACB RID: 2763 RVA: 0x00043678 File Offset: 0x00041878
		public ComponentCreature ComponentCreature { get; set; }

		// Token: 0x1700013A RID: 314
		// (get) Token: 0x06000ACC RID: 2764 RVA: 0x00043681 File Offset: 0x00041881
		// (set) Token: 0x06000ACD RID: 2765 RVA: 0x00043689 File Offset: 0x00041889
		public ComponentPlayer ComponentPlayer { get; set; }

		// Token: 0x1700013B RID: 315
		// (get) Token: 0x06000ACE RID: 2766 RVA: 0x00043692 File Offset: 0x00041892
		// (set) Token: 0x06000ACF RID: 2767 RVA: 0x0004369A File Offset: 0x0004189A
		public ComponentFactors ComponentFactors { get; set; }

		// Token: 0x1700013C RID: 316
		// (get) Token: 0x06000AD0 RID: 2768 RVA: 0x000436A3 File Offset: 0x000418A3
		// (set) Token: 0x06000AD1 RID: 2769 RVA: 0x000436AB File Offset: 0x000418AB
		public IInventory Inventory { get; set; }

		// Token: 0x1700013D RID: 317
		// (get) Token: 0x06000AD2 RID: 2770 RVA: 0x000436B4 File Offset: 0x000418B4
		public int ActiveBlockValue
		{
			get
			{
				if (this.Inventory == null)
				{
					return 0;
				}
				return this.Inventory.GetSlotValue(this.Inventory.ActiveSlotIndex);
			}
		}

		// Token: 0x1700013E RID: 318
		// (get) Token: 0x06000AD3 RID: 2771 RVA: 0x000436D6 File Offset: 0x000418D6
		// (set) Token: 0x06000AD4 RID: 2772 RVA: 0x000436DE File Offset: 0x000418DE
		public float AttackPower { get; set; }

		// Token: 0x1700013F RID: 319
		// (get) Token: 0x06000AD5 RID: 2773 RVA: 0x000436E7 File Offset: 0x000418E7
		// (set) Token: 0x06000AD6 RID: 2774 RVA: 0x000436EF File Offset: 0x000418EF
		public float AutoInteractRate { get; set; }

		// Token: 0x17000140 RID: 320
		// (get) Token: 0x06000AD7 RID: 2775 RVA: 0x000436F8 File Offset: 0x000418F8
		public float StrengthFactor
		{
			get
			{
				ComponentFactors componentFactors = this.ComponentFactors;
				if (componentFactors == null)
				{
					return 1f;
				}
				return componentFactors.StrengthFactor;
			}
		}

		// Token: 0x17000141 RID: 321
		// (get) Token: 0x06000AD8 RID: 2776 RVA: 0x0004370F File Offset: 0x0004190F
		// (set) Token: 0x06000AD9 RID: 2777 RVA: 0x00043717 File Offset: 0x00041917
		public float PokingPhase { get; set; }

		// Token: 0x17000142 RID: 322
		// (get) Token: 0x06000ADA RID: 2778 RVA: 0x00043720 File Offset: 0x00041920
		// (set) Token: 0x06000ADB RID: 2779 RVA: 0x00043728 File Offset: 0x00041928
		public CellFace? DigCellFace { get; set; }

		// Token: 0x17000143 RID: 323
		// (get) Token: 0x06000ADC RID: 2780 RVA: 0x00043734 File Offset: 0x00041934
		public float DigTime
		{
			get
			{
				if (this.DigCellFace == null)
				{
					return 0f;
				}
				return (float)(this.m_subsystemTime.GameTime - this.m_digStartTime);
			}
		}

		// Token: 0x17000144 RID: 324
		// (get) Token: 0x06000ADD RID: 2781 RVA: 0x0004376C File Offset: 0x0004196C
		public float DigProgress
		{
			get
			{
				if (this.DigCellFace == null)
				{
					return 0f;
				}
				return this.m_digProgress;
			}
		}

		// Token: 0x17000145 RID: 325
		// (get) Token: 0x06000ADE RID: 2782 RVA: 0x00043795 File Offset: 0x00041995
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x06000ADF RID: 2783 RVA: 0x00043798 File Offset: 0x00041998
		public virtual void Poke(bool forceRestart)
		{
			this.PokingPhase = (forceRestart ? 0.0001f : MathUtils.Max(0.0001f, this.PokingPhase));
		}

		// Token: 0x06000AE0 RID: 2784 RVA: 0x000437BC File Offset: 0x000419BC
		public bool Dig(TerrainRaycastResult raycastResult)
		{
			bool result = false;
			this.m_lastDigFrameIndex = Time.FrameIndex;
			CellFace cellFace = raycastResult.CellFace;
			int cellValue = this.m_subsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
			int num = Terrain.ExtractContents(cellValue);
			Block block = BlocksManager.Blocks[num];
			int activeBlockValue = this.ActiveBlockValue;
			int num2 = Terrain.ExtractContents(activeBlockValue);
			Block block2 = BlocksManager.Blocks[num2];
			if (this.DigCellFace == null || this.DigCellFace.Value.X != cellFace.X || this.DigCellFace.Value.Y != cellFace.Y || this.DigCellFace.Value.Z != cellFace.Z)
			{
				this.m_digStartTime = this.m_subsystemTime.GameTime;
				this.DigCellFace = new CellFace?(cellFace);
			}
			float num3 = this.CalculateDigTime(cellValue, activeBlockValue);
			this.m_digProgress = ((num3 > 0f) ? MathUtils.Saturate((float)(this.m_subsystemTime.GameTime - this.m_digStartTime) / num3) : 1f);
			if (!this.IsLevelSufficientForTool(activeBlockValue))
			{
				this.m_digProgress = 0f;
				if (this.m_subsystemTime.PeriodicGameTimeEvent(5.0, this.m_digStartTime + 1.0))
				{
					ComponentPlayer componentPlayer = this.ComponentPlayer;
					if (componentPlayer != null)
					{
						componentPlayer.ComponentGui.DisplaySmallMessage(string.Format(LanguageControl.Get(ComponentMiner.fName, 1), block2.PlayerLevelRequired, block2.GetDisplayName(this.m_subsystemTerrain, activeBlockValue)), Color.White, true, true, 1f);
					}
				}
			}
			bool flag2 = this.ComponentPlayer != null && !this.ComponentPlayer.ComponentInput.IsControlledByTouch && this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative;
			ModsManager.HookAction("OnMinerDig", delegate(ModLoader modLoader)
			{
				bool flag2;
				modLoader.OnMinerDig(this, raycastResult, ref this.m_digProgress, out flag2);
				flag2 = (flag2 || flag2);
				return false;
			});
			if ((this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Survival || this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Harmless) && this.ComponentPlayer != null && num3 >= 3f && this.m_digProgress > 0.5f && (this.m_lastToolHintTime == 0.0 || Time.FrameStartTime - this.m_lastToolHintTime > 300.0))
			{
				bool flag = num3 == this.CalculateDigTime(cellValue, 0);
				int num4 = this.FindBestInventoryToolForDigging(cellValue);
				if (num4 == 0)
				{
					if (num2 != 23 && flag)
					{
						this.ComponentPlayer.ComponentGui.DisplaySmallMessage(LanguageControl.Get(new string[]
						{
							ComponentMiner.fName,
							"11"
						}), Color.White, true, true, 1f);
						this.m_lastToolHintTime = Time.FrameStartTime;
					}
				}
				else if (this.CalculateDigTime(cellValue, num4) < 0.5f * num3 || flag)
				{
					string displayName = BlocksManager.Blocks[Terrain.ExtractContents(num4)].GetDisplayName(this.m_subsystemTerrain, num4);
					this.ComponentPlayer.ComponentGui.DisplaySmallMessage(string.Format(LanguageControl.Get(new string[]
					{
						ComponentMiner.fName,
						"12"
					}), displayName), Color.White, true, true, 1f);
					this.m_lastToolHintTime = Time.FrameStartTime;
				}
			}
			if (flag2 || (this.m_lastPokingPhase <= 0.5f && this.PokingPhase > 0.5f))
			{
				if (this.m_digProgress >= 1f)
				{
					this.DigCellFace = null;
					if (flag2)
					{
						this.Poke(true);
					}
					BlockPlacementData digValue = block.GetDigValue(this.m_subsystemTerrain, this, cellValue, activeBlockValue, raycastResult);
					this.m_subsystemTerrain.DestroyCell(block2.ToolLevel, digValue.CellFace.X, digValue.CellFace.Y, digValue.CellFace.Z, digValue.Value, false, false, null);
					int durabilityReduction = 1;
					int playerDataAdd = 1;
					bool mute_ = false;
					ModsManager.HookAction("OnBlockDug", delegate(ModLoader modLoader)
					{
						bool flag2 = false;
						modLoader.OnBlockDug(this, digValue, cellValue, ref durabilityReduction, ref flag2, ref playerDataAdd);
						mute_ = (mute_ || flag2);
						return false;
					});
					if (!mute_)
					{
						this.m_subsystemSoundMaterials.PlayImpactSound(cellValue, new Vector3((float)cellFace.X, (float)cellFace.Y, (float)cellFace.Z), 2f);
					}
					this.DamageActiveTool(durabilityReduction);
					if (this.ComponentCreature.PlayerStats != null)
					{
						this.ComponentCreature.PlayerStats.BlocksDug += (long)playerDataAdd;
					}
					result = true;
				}
				else
				{
					this.m_subsystemSoundMaterials.PlayImpactSound(cellValue, new Vector3((float)cellFace.X, (float)cellFace.Y, (float)cellFace.Z), 1f);
					BlockDebrisParticleSystem particleSystem = block.CreateDebrisParticleSystem(this.m_subsystemTerrain, raycastResult.HitPoint(0.1f), cellValue, 0.35f);
					base.Project.FindSubsystem<SubsystemParticles>(true).AddParticleSystem(particleSystem, false);
				}
			}
			return result;
		}

		// Token: 0x06000AE1 RID: 2785 RVA: 0x00043D68 File Offset: 0x00041F68
		public bool Place(TerrainRaycastResult raycastResult)
		{
			if (this.Place(raycastResult, this.ActiveBlockValue))
			{
				if (this.Inventory != null)
				{
					this.Inventory.RemoveSlotItems(this.Inventory.ActiveSlotIndex, 1);
				}
				return true;
			}
			return false;
		}

		// Token: 0x06000AE2 RID: 2786 RVA: 0x00043D9C File Offset: 0x00041F9C
		public bool Place(TerrainRaycastResult raycastResult, int value)
		{
			int num = Terrain.ExtractContents(value);
			if (BlocksManager.Blocks[num].IsPlaceable_(value))
			{
				Block block = BlocksManager.Blocks[num];
				BlockPlacementData placementValue = block.GetPlacementValue(this.m_subsystemTerrain, this, value, raycastResult);
				if (placementValue.Value != 0)
				{
					Point3 point = CellFace.FaceToPoint3(placementValue.CellFace.Face);
					int num2 = placementValue.CellFace.X + point.X;
					int num3 = placementValue.CellFace.Y + point.Y;
					int num4 = placementValue.CellFace.Z + point.Z;
					bool placed = false;
					ModsManager.HookAction("OnMinerPlace", delegate(ModLoader modLoader)
					{
						bool flag2;
						modLoader.OnMinerPlace(this, raycastResult, num2, num3, num4, value, out flag2);
						placed = (placed || flag2);
						return false;
					});
					if (placed)
					{
						return true;
					}
					if (!this.m_canSqueezeBlock && this.m_subsystemTerrain.Terrain.GetCellContents(num2, num3, num4) != 0)
					{
						return false;
					}
					if (num3 > 0 && num3 < 255 && (this.m_canJumpToPlace || ComponentMiner.IsBlockPlacingAllowed(this.ComponentCreature.ComponentBody) || this.m_subsystemGameInfo.WorldSettings.GameMode <= GameMode.Survival))
					{
						bool flag = false;
						if (block.IsCollidable_(value))
						{
							BoundingBox boundingBox = this.ComponentCreature.ComponentBody.BoundingBox;
							boundingBox.Min += new Vector3(0.2f);
							boundingBox.Max -= new Vector3(0.2f);
							foreach (BoundingBox boundingBox2 in block.GetCustomCollisionBoxes(this.m_subsystemTerrain, placementValue.Value))
							{
								boundingBox2.Min += new Vector3((float)num2, (float)num3, (float)num4);
								boundingBox2.Max += new Vector3((float)num2, (float)num3, (float)num4);
								if (boundingBox.Intersection(boundingBox2))
								{
									flag = true;
									break;
								}
							}
						}
						if (!flag)
						{
							SubsystemBlockBehavior[] blockBehaviors = this.m_subsystemBlockBehaviors.GetBlockBehaviors(Terrain.ExtractContents(placementValue.Value));
							for (int j = 0; j < blockBehaviors.Length; j++)
							{
								blockBehaviors[j].OnItemPlaced(num2, num3, num4, ref placementValue, value);
							}
							this.m_subsystemTerrain.DestroyCell(0, num2, num3, num4, placementValue.Value, false, false, null);
							this.m_subsystemAudio.PlaySound("Audio/BlockPlaced", 1f, 0f, new Vector3((float)placementValue.CellFace.X, (float)placementValue.CellFace.Y, (float)placementValue.CellFace.Z), 5f, false);
							this.Poke(false);
							if (this.ComponentCreature.PlayerStats != null)
							{
								this.ComponentCreature.PlayerStats.BlocksPlaced += 1L;
							}
							return true;
						}
					}
				}
			}
			return false;
		}

		// Token: 0x06000AE3 RID: 2787 RVA: 0x0004411C File Offset: 0x0004231C
		public bool Use(Ray3 ray)
		{
			int num = Terrain.ExtractContents(this.ActiveBlockValue);
			Block block = BlocksManager.Blocks[num];
			if (!this.IsLevelSufficientForTool(this.ActiveBlockValue))
			{
				ComponentPlayer componentPlayer = this.ComponentPlayer;
				if (componentPlayer != null)
				{
					componentPlayer.ComponentGui.DisplaySmallMessage(string.Format(LanguageControl.Get(ComponentMiner.fName, 1), block.PlayerLevelRequired, block.GetDisplayName(this.m_subsystemTerrain, this.ActiveBlockValue)), Color.White, true, true, 1f);
				}
				this.Poke(false);
				return false;
			}
			SubsystemBlockBehavior[] blockBehaviors = this.m_subsystemBlockBehaviors.GetBlockBehaviors(Terrain.ExtractContents(this.ActiveBlockValue));
			for (int i = 0; i < blockBehaviors.Length; i++)
			{
				if (blockBehaviors[i].OnUse(ray, this))
				{
					this.Poke(false);
					return true;
				}
			}
			return false;
		}

		// Token: 0x06000AE4 RID: 2788 RVA: 0x000441E0 File Offset: 0x000423E0
		public bool Interact(TerrainRaycastResult raycastResult)
		{
			SubsystemBlockBehavior[] blockBehaviors = this.m_subsystemBlockBehaviors.GetBlockBehaviors(Terrain.ExtractContents(raycastResult.Value));
			for (int i = 0; i < blockBehaviors.Length; i++)
			{
				if (blockBehaviors[i].OnInteract(raycastResult, this))
				{
					if (this.ComponentCreature.PlayerStats != null)
					{
						this.ComponentCreature.PlayerStats.BlocksInteracted += 1L;
					}
					this.Poke(false);
					return true;
				}
			}
			return false;
		}

		// Token: 0x06000AE5 RID: 2789 RVA: 0x00044250 File Offset: 0x00042450
		public bool Interact(MovingBlocksRaycastResult raycastResult)
		{
			if (raycastResult.MovingBlock == null)
			{
				return false;
			}
			SubsystemBlockBehavior[] blockBehaviors = this.m_subsystemBlockBehaviors.GetBlockBehaviors(Terrain.ExtractContents(raycastResult.MovingBlock.Value));
			for (int i = 0; i < blockBehaviors.Length; i++)
			{
				if (blockBehaviors[i].OnInteract(raycastResult, this))
				{
					if (this.ComponentCreature.PlayerStats != null)
					{
						this.ComponentCreature.PlayerStats.BlocksInteracted += 1L;
					}
					this.Poke(false);
					return true;
				}
			}
			return false;
		}

		// Token: 0x06000AE6 RID: 2790 RVA: 0x000442D0 File Offset: 0x000424D0
		public void Hit(ComponentBody componentBody, Vector3 hitPoint, Vector3 hitDirection)
		{
			if (this.m_subsystemTime.GameTime - this.m_lastHitTime <= this.HitInterval)
			{
				return;
			}
			this.m_lastHitTime = this.m_subsystemTime.GameTime;
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(this.ActiveBlockValue)];
			if (!this.IsLevelSufficientForTool(this.ActiveBlockValue))
			{
				ComponentPlayer componentPlayer = this.ComponentPlayer;
				if (componentPlayer != null)
				{
					componentPlayer.ComponentGui.DisplaySmallMessage(string.Format(LanguageControl.Get(ComponentMiner.fName, 1), block.PlayerLevelRequired, block.GetDisplayName(this.m_subsystemTerrain, this.ActiveBlockValue)), Color.White, true, true, 1f);
				}
				this.Poke(false);
				return;
			}
			float num = 0f;
			float num2 = 1f;
			float num3 = 1f;
			if (this.ActiveBlockValue != 0)
			{
				num = block.GetMeleePower(this.ActiveBlockValue) * this.AttackPower * this.m_random.Float(0.8f, 1.2f);
				num2 = block.GetMeleeHitProbability(this.ActiveBlockValue);
			}
			else
			{
				num = this.AttackPower * this.m_random.Float(0.8f, 1.2f);
				num2 = 0.66f;
			}
			num2 *= ((componentBody.Velocity.Length() < 0.05f) ? 2f : 1f);
			ModsManager.HookAction("OnMinerHit", delegate(ModLoader modLoader)
			{
				bool result;
				modLoader.OnMinerHit(this, componentBody, hitPoint, hitDirection, ref num, ref num2, ref num3, out result);
				return result;
			});
			bool flag;
			if (this.ComponentPlayer != null)
			{
				this.m_subsystemAudio.PlaySound("Audio/Swoosh", 1f, this.m_random.Float(-0.2f, 0.2f), componentBody.Position, 3f, false);
				flag = this.m_random.Bool(num2);
			}
			else
			{
				flag = this.m_random.Bool(num3);
			}
			num *= this.StrengthFactor;
			if (flag)
			{
				int durabilityReduction = 1;
				Attackment attackment = new MeleeAttackment(componentBody, base.Entity, hitPoint, hitDirection, num);
				ModsManager.HookAction("OnMinerHit2", delegate(ModLoader loader)
				{
					loader.OnMinerHit2(this, componentBody, hitPoint, hitDirection, ref durabilityReduction, ref attackment);
					return false;
				});
				ComponentMiner.AttackBody(attackment);
				this.DamageActiveTool(durabilityReduction);
			}
			else if (this.ComponentCreature is ComponentPlayer)
			{
				HitValueParticleSystem particleSystem = new HitValueParticleSystem(hitPoint + 0.75f * hitDirection, 1f * hitDirection + this.ComponentCreature.ComponentBody.Velocity, Color.White, LanguageControl.Get(ComponentMiner.fName, 2));
				ModsManager.HookAction("SetHitValueParticleSystem", delegate(ModLoader modLoader)
				{
					modLoader.SetHitValueParticleSystem(particleSystem, null);
					return false;
				});
				base.Project.FindSubsystem<SubsystemParticles>(true).AddParticleSystem(particleSystem, false);
			}
			if (this.ComponentCreature.PlayerStats != null)
			{
				this.ComponentCreature.PlayerStats.MeleeAttacks += 1L;
				if (flag)
				{
					this.ComponentCreature.PlayerStats.MeleeHits += 1L;
				}
			}
			this.Poke(false);
		}

		// Token: 0x06000AE7 RID: 2791 RVA: 0x00044680 File Offset: 0x00042880
		public bool Aim(Ray3 aim, AimState state)
		{
			int num = Terrain.ExtractContents(this.ActiveBlockValue);
			Block block = BlocksManager.Blocks[num];
			if (block.IsAimable_(this.ActiveBlockValue))
			{
				if (!this.IsLevelSufficientForTool(this.ActiveBlockValue))
				{
					ComponentPlayer componentPlayer = this.ComponentPlayer;
					if (componentPlayer != null)
					{
						componentPlayer.ComponentGui.DisplaySmallMessage(string.Format(LanguageControl.Get(ComponentMiner.fName, 1), block.GetPlayerLevelRequired(this.ActiveBlockValue), block.GetDisplayName(this.m_subsystemTerrain, this.ActiveBlockValue)), Color.White, true, true, 1f);
					}
					this.Poke(false);
					return true;
				}
				SubsystemBlockBehavior[] blockBehaviors = this.m_subsystemBlockBehaviors.GetBlockBehaviors(Terrain.ExtractContents(this.ActiveBlockValue));
				for (int i = 0; i < blockBehaviors.Length; i++)
				{
					if (blockBehaviors[i].OnAim(aim, this, state))
					{
						return true;
					}
				}
			}
			return false;
		}

		// Token: 0x06000AE8 RID: 2792 RVA: 0x00044754 File Offset: 0x00042954
		public virtual object Raycast(Ray3 ray, RaycastMode mode, bool raycastTerrain = true, bool raycastBodies = true, bool raycastMovingBlocks = true, float? Reach = null)
		{
			float reach = (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) ? SettingsManager.CreativeReach : 5f;
			if (Reach != null)
			{
				reach = Reach.Value;
			}
			Vector3 creaturePosition = this.ComponentCreature.ComponentCreatureModel.EyePosition;
			Vector3 start = ray.Position;
			Vector3 direction = Vector3.Normalize(ray.Direction);
			Vector3 end = ray.Position + direction * 15f;
			Point3 startCell = Terrain.ToCell(start);
			BodyRaycastResult? bodyRaycastResult = null;
			if (raycastBodies)
			{
				bodyRaycastResult = this.m_subsystemBodies.Raycast(start, end, 0.35f, (ComponentBody body, float distance) => Vector3.DistanceSquared(start + distance * direction, creaturePosition) <= reach * reach && body.Entity != this.Entity && !body.IsChildOfBody(this.ComponentCreature.ComponentBody) && !this.ComponentCreature.ComponentBody.IsChildOfBody(body) && Vector3.Dot(Vector3.Normalize(body.BoundingBox.Center() - start), direction) > 0.7f);
			}
			MovingBlocksRaycastResult? movingBlocksRaycastResult = null;
			if (raycastMovingBlocks)
			{
				movingBlocksRaycastResult = this.m_subsystemMovingBlocks.Raycast(start, end, true, null);
			}
			TerrainRaycastResult? terrainRaycastResult = null;
			if (raycastTerrain)
			{
				terrainRaycastResult = this.m_subsystemTerrain.Raycast(start, end, true, true, delegate(int value, float distance)
				{
					if (Vector3.DistanceSquared(start + distance * direction, creaturePosition) <= reach * reach)
					{
						Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
						if (distance == 0f && block is CrossBlock && Vector3.Dot(direction, new Vector3(startCell) + new Vector3(0.5f) - start) < 0f)
						{
							return false;
						}
						if (mode == RaycastMode.Digging)
						{
							return !block.IsDiggingTransparent;
						}
						if (mode == RaycastMode.Interaction)
						{
							return !block.IsPlacementTransparent_(value) || block.IsInteractive(this.m_subsystemTerrain, value);
						}
						if (mode == RaycastMode.Gathering)
						{
							return block.IsGatherable_(value);
						}
					}
					return false;
				});
			}
			if (!raycastBodies)
			{
				bodyRaycastResult = null;
			}
			if (!raycastTerrain)
			{
				terrainRaycastResult = null;
			}
			if (!raycastMovingBlocks)
			{
				movingBlocksRaycastResult = null;
			}
			float num = (bodyRaycastResult != null) ? bodyRaycastResult.Value.Distance : float.PositiveInfinity;
			float num2 = (movingBlocksRaycastResult != null) ? movingBlocksRaycastResult.Value.Distance : float.PositiveInfinity;
			float num3 = (terrainRaycastResult != null) ? terrainRaycastResult.Value.Distance : float.PositiveInfinity;
			if (num < num2 && num < num3)
			{
				return bodyRaycastResult.Value;
			}
			if (num2 < num && num2 < num3)
			{
				return movingBlocksRaycastResult.Value;
			}
			if (num3 < num && num3 < num2)
			{
				return terrainRaycastResult.Value;
			}
			return new Ray3(start, direction);
		}

		// Token: 0x06000AE9 RID: 2793 RVA: 0x0004496C File Offset: 0x00042B6C
		public T? Raycast<T>(Ray3 ray, RaycastMode mode, bool raycastTerrain = true, bool raycastBodies = true, bool raycastMovingBlocks = true, float? reach = null) where T : struct
		{
			object obj = this.Raycast(ray, mode, raycastTerrain, raycastBodies, raycastMovingBlocks, reach);
			if (!(obj is T))
			{
				return null;
			}
			return new T?((T)((object)obj));
		}

		// Token: 0x06000AEA RID: 2794 RVA: 0x000449A6 File Offset: 0x00042BA6
		public virtual void RemoveActiveTool(int removeCount)
		{
			if (this.Inventory != null)
			{
				this.Inventory.RemoveSlotItems(this.Inventory.ActiveSlotIndex, removeCount);
			}
		}

		// Token: 0x06000AEB RID: 2795 RVA: 0x000449C8 File Offset: 0x00042BC8
		public virtual void DamageActiveTool(int damageCount)
		{
			if (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative || this.Inventory == null)
			{
				return;
			}
			int num = BlocksManager.DamageItem(this.ActiveBlockValue, damageCount, base.Entity);
			if (num != 0)
			{
				int slotCount = this.Inventory.GetSlotCount(this.Inventory.ActiveSlotIndex);
				this.Inventory.RemoveSlotItems(this.Inventory.ActiveSlotIndex, slotCount);
				if (this.Inventory.GetSlotCount(this.Inventory.ActiveSlotIndex) == 0)
				{
					this.Inventory.AddSlotItems(this.Inventory.ActiveSlotIndex, num, slotCount);
					return;
				}
			}
			else
			{
				this.Inventory.RemoveSlotItems(this.Inventory.ActiveSlotIndex, 1);
			}
		}

		// Token: 0x06000AEC RID: 2796 RVA: 0x00044A80 File Offset: 0x00042C80
		public static void AttackBody(Attackment attackment)
		{
			try
			{
				attackment.ProcessAttackment();
			}
			catch (Exception ex)
			{
				string str = "Attack execute error: ";
				Exception ex2 = ex;
				Log.Error(str + ((ex2 != null) ? ex2.ToString() : null));
			}
		}

		// Token: 0x06000AED RID: 2797 RVA: 0x00044AC4 File Offset: 0x00042CC4
		public static void AttackBody(ComponentBody target, ComponentCreature attacker, Vector3 hitPoint, Vector3 hitDirection, float attackPower, bool isMeleeAttack)
		{
			if (isMeleeAttack)
			{
				ComponentMiner.AttackBody(new MeleeAttackment((target != null) ? target.Entity : null, (attacker != null) ? attacker.Entity : null, hitPoint, hitDirection, attackPower));
				return;
			}
			ComponentMiner.AttackBody(new ProjectileAttackment((target != null) ? target.Entity : null, (attacker != null) ? attacker.Entity : null, hitPoint, hitDirection, attackPower, null));
		}

		// Token: 0x06000AEE RID: 2798 RVA: 0x00044B24 File Offset: 0x00042D24
		[Obsolete("Use AddHitValueParticleSystem() in Attackment instead.", true)]
		public static void AddHitValueParticleSystem(float damage, Entity attacker, Entity attacked, Vector3 hitPoint, Vector3 hitDirection)
		{
			ComponentBody componentBody = (attacker != null) ? attacker.FindComponent<ComponentBody>() : null;
			bool flag = ((attacker != null) ? attacker.FindComponent<ComponentPlayer>() : null) != null;
			ComponentHealth componentHealth = (attacked != null) ? attacked.FindComponent<ComponentHealth>() : null;
			string text = (0f - damage).ToString("0", CultureInfo.InvariantCulture);
			Vector3 vector = Vector3.Zero;
			if (componentBody != null)
			{
				vector = componentBody.Velocity;
			}
			Color color = (flag && damage > 0f && componentHealth != null) ? Color.White : Color.Transparent;
			HitValueParticleSystem particleSystem = new HitValueParticleSystem(hitPoint + 0.75f * hitDirection, 1f * hitDirection + vector, color, text);
			ModsManager.HookAction("SetHitValueParticleSystem", delegate(ModLoader modLoader)
			{
				modLoader.SetHitValueParticleSystem(particleSystem, null);
				return false;
			});
			if (attacked != null)
			{
				attacked.Project.FindSubsystem<SubsystemParticles>(true).AddParticleSystem(particleSystem, false);
			}
		}

		// Token: 0x06000AEF RID: 2799 RVA: 0x00044C0C File Offset: 0x00042E0C
		public void Update(float dt)
		{
			float num = (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) ? (1f / SettingsManager.CreativeDigTime) : 4f;
			this.m_lastPokingPhase = this.PokingPhase;
			if (this.DigCellFace != null || this.PokingPhase > 0f)
			{
				this.PokingPhase += num * this.m_subsystemTime.GameTimeDelta;
				if (this.PokingPhase > 1f)
				{
					this.PokingPhase = ((this.DigCellFace != null) ? MathUtils.Remainder(this.PokingPhase, 1f) : 0f);
				}
			}
			if (this.DigCellFace != null && Time.FrameIndex - this.m_lastDigFrameIndex > 1)
			{
				this.DigCellFace = null;
			}
			if ((this.m_componentHealth != null && this.m_componentHealth.Health <= 0f) || this.AutoInteractRate <= 0f || !this.m_random.Bool(this.AutoInteractRate) || !this.m_subsystemTime.PeriodicGameTimeEvent(1.0, (double)((float)(this.GetHashCode() % 100) / 100f)))
			{
				return;
			}
			ComponentCreatureModel componentCreatureModel = this.ComponentCreature.ComponentCreatureModel;
			Vector3 eyePosition = componentCreatureModel.EyePosition;
			Vector3 forwardVector = componentCreatureModel.EyeRotation.GetForwardVector();
			for (int i = 0; i < 10; i++)
			{
				TerrainRaycastResult? terrainRaycastResult = this.Raycast<TerrainRaycastResult>(new Ray3(eyePosition, forwardVector + this.m_random.Vector3(0.75f)), RaycastMode.Interaction, true, true, true, null);
				if (terrainRaycastResult != null && terrainRaycastResult.Value.Distance < 1.5f && Terrain.ExtractContents(terrainRaycastResult.Value.Value) != 57 && this.Interact(terrainRaycastResult.Value))
				{
					break;
				}
			}
		}

		// Token: 0x06000AF0 RID: 2800 RVA: 0x00044DF0 File Offset: 0x00042FF0
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			this.m_subsystemMovingBlocks = base.Project.FindSubsystem<SubsystemMovingBlocks>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemSoundMaterials = base.Project.FindSubsystem<SubsystemSoundMaterials>(true);
			this.m_subsystemBlockBehaviors = base.Project.FindSubsystem<SubsystemBlockBehaviors>(true);
			this.ComponentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.ComponentPlayer = base.Entity.FindComponent<ComponentPlayer>();
			this.m_componentHealth = base.Entity.FindComponent<ComponentHealth>();
			this.ComponentFactors = base.Entity.FindComponent<ComponentFactors>(true);
			IInventory inventory2;
			if (this.m_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative || this.ComponentPlayer == null)
			{
				IInventory inventory = base.Entity.FindComponent<ComponentInventory>();
				inventory2 = inventory;
			}
			else
			{
				IInventory inventory = base.Entity.FindComponent<ComponentCreativeInventory>();
				inventory2 = inventory;
			}
			this.Inventory = inventory2;
			this.AttackPower = valuesDictionary.GetValue<float>("AttackPower");
			this.HitInterval = (double)valuesDictionary.GetValue<float>("HitInterval");
			this.AutoInteractRate = valuesDictionary.GetValue<float>("AutoInteractRate");
			if (string.CompareOrdinal(this.m_subsystemGameInfo.WorldSettings.OriginalSerializationVersion, "2.4") < 0 || this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Harmless || this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Survival)
			{
				this.AutoInteractRate = 0f;
			}
		}

		// Token: 0x06000AF1 RID: 2801 RVA: 0x00044F91 File Offset: 0x00043191
		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			valuesDictionary.SetValue<float>("AttackPower", this.AttackPower);
		}

		// Token: 0x06000AF2 RID: 2802 RVA: 0x00044FA4 File Offset: 0x000431A4
		public static bool IsBlockPlacingAllowed(ComponentBody componentBody)
		{
			if (componentBody.StandingOnBody != null || componentBody.StandingOnValue != null)
			{
				return true;
			}
			if (componentBody.ImmersionFactor > 0.01f)
			{
				return true;
			}
			if (componentBody.ParentBody != null && ComponentMiner.IsBlockPlacingAllowed(componentBody.ParentBody))
			{
				return true;
			}
			ComponentLocomotion componentLocomotion = componentBody.Entity.FindComponent<ComponentLocomotion>();
			return componentLocomotion != null && componentLocomotion.LadderValue != null;
		}

		// Token: 0x06000AF3 RID: 2803 RVA: 0x00045014 File Offset: 0x00043214
		public virtual float CalculateDigTime(int digValue, int toolValue)
		{
			Block block = BlocksManager.Blocks[Terrain.ExtractContents(toolValue)];
			Block block2 = BlocksManager.Blocks[Terrain.ExtractContents(digValue)];
			float digResilience = block2.GetDigResilience(digValue);
			BlockDigMethod blockDigMethod = block2.GetBlockDigMethod(digValue);
			float shovelPower = block.GetShovelPower(toolValue);
			float quarryPower = block.GetQuarryPower(toolValue);
			float hackPower = block.GetHackPower(toolValue);
			if (this.ComponentPlayer != null && this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative)
			{
				if (digResilience < float.PositiveInfinity)
				{
					return 0f;
				}
				return float.PositiveInfinity;
			}
			else if (this.ComponentPlayer != null && this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Adventure)
			{
				float num = 0f;
				if (blockDigMethod == BlockDigMethod.Shovel && shovelPower >= 2f)
				{
					num = shovelPower;
				}
				else if (blockDigMethod == BlockDigMethod.Quarry && quarryPower >= 2f)
				{
					num = quarryPower;
				}
				else if (blockDigMethod == BlockDigMethod.Hack && hackPower >= 2f)
				{
					num = hackPower;
				}
				num *= this.StrengthFactor;
				if (num <= 0f)
				{
					return float.PositiveInfinity;
				}
				return MathUtils.Max(digResilience / num, 0f);
			}
			else
			{
				float num2 = 1f;
				if (blockDigMethod == BlockDigMethod.Shovel)
				{
					num2 = shovelPower;
				}
				else if (blockDigMethod == BlockDigMethod.Quarry)
				{
					num2 = quarryPower;
				}
				else if (blockDigMethod == BlockDigMethod.Hack)
				{
					num2 = hackPower;
				}
				num2 *= this.StrengthFactor;
				if (num2 <= 0f)
				{
					return float.PositiveInfinity;
				}
				return MathUtils.Max(digResilience / num2, 0f);
			}
		}

		// Token: 0x06000AF4 RID: 2804 RVA: 0x00045158 File Offset: 0x00043358
		public virtual bool IsLevelSufficientForTool(int toolValue)
		{
			if (this.m_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative && this.m_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
			{
				Block block = BlocksManager.Blocks[Terrain.ExtractContents(toolValue)];
				if (this.ComponentPlayer != null && this.ComponentPlayer.PlayerData.Level < (float)block.GetPlayerLevelRequired(toolValue))
				{
					return false;
				}
			}
			return true;
		}

		// Token: 0x06000AF5 RID: 2805 RVA: 0x000451BC File Offset: 0x000433BC
		public virtual int FindBestInventoryToolForDigging(int digValue)
		{
			int result = 0;
			float num = this.CalculateDigTime(digValue, 0);
			foreach (IInventory inventory in base.Entity.FindComponents<IInventory>())
			{
				if (!(inventory is ComponentCreativeInventory))
				{
					for (int i = 0; i < inventory.SlotsCount; i++)
					{
						int slotValue = inventory.GetSlotValue(i);
						if (this.IsLevelSufficientForTool(slotValue))
						{
							float num2 = this.CalculateDigTime(digValue, slotValue);
							if (num2 < num)
							{
								num = num2;
								result = slotValue;
							}
						}
					}
				}
			}
			return result;
		}

		// Token: 0x04000632 RID: 1586
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x04000633 RID: 1587
		public SubsystemBodies m_subsystemBodies;

		// Token: 0x04000634 RID: 1588
		public SubsystemMovingBlocks m_subsystemMovingBlocks;

		// Token: 0x04000635 RID: 1589
		public SubsystemGameInfo m_subsystemGameInfo;

		// Token: 0x04000636 RID: 1590
		public SubsystemTime m_subsystemTime;

		// Token: 0x04000637 RID: 1591
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x04000638 RID: 1592
		public SubsystemSoundMaterials m_subsystemSoundMaterials;

		// Token: 0x04000639 RID: 1593
		public SubsystemBlockBehaviors m_subsystemBlockBehaviors;

		// Token: 0x0400063A RID: 1594
		private ComponentHealth m_componentHealth;

		// Token: 0x0400063B RID: 1595
		public Random m_random = new Random();

		// Token: 0x0400063C RID: 1596
		public static Random s_random = new Random();

		// Token: 0x0400063D RID: 1597
		public double m_digStartTime;

		// Token: 0x0400063E RID: 1598
		public float m_digProgress;

		// Token: 0x0400063F RID: 1599
		public double m_lastHitTime;

		// Token: 0x04000640 RID: 1600
		public static string fName = "ComponentMiner";

		// Token: 0x04000641 RID: 1601
		public int m_lastDigFrameIndex;

		// Token: 0x04000642 RID: 1602
		public float m_lastPokingPhase;

		// Token: 0x04000643 RID: 1603
		private double m_lastToolHintTime;

		// Token: 0x0400064D RID: 1613
		public bool m_canSqueezeBlock = true;

		// Token: 0x0400064E RID: 1614
		public bool m_canJumpToPlace;
	}
}
