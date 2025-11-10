using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000380 RID: 896
	public class SubsystemMusketBlockBehavior : SubsystemBlockBehavior
	{
		// Token: 0x170003F6 RID: 1014
		// (get) Token: 0x06001C31 RID: 7217 RVA: 0x000DF30E File Offset: 0x000DD50E
		public override int[] HandledBlocks
		{
			get
			{
				return new int[0];
			}
		}

		// Token: 0x06001C32 RID: 7218 RVA: 0x000DF316 File Offset: 0x000DD516
		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			componentPlayer.ComponentGui.ModalPanelWidget = ((componentPlayer.ComponentGui.ModalPanelWidget == null) ? new MusketWidget(inventory, slotIndex) : null);
			return true;
		}

		// Token: 0x06001C33 RID: 7219 RVA: 0x000DF33C File Offset: 0x000DD53C
		public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
		{
			IInventory inventory = componentMiner.Inventory;
			if (inventory != null)
			{
				int activeSlotIndex = inventory.ActiveSlotIndex;
				if (activeSlotIndex >= 0)
				{
					int slotValue = inventory.GetSlotValue(activeSlotIndex);
					int slotCount = inventory.GetSlotCount(activeSlotIndex);
					int num = Terrain.ExtractContents(slotValue);
					int data = Terrain.ExtractData(slotValue);
					int num2 = slotValue;
					int num3 = 0;
					if (num == this.m_MusketBlockIndex && slotCount > 0)
					{
						double gameTime;
						if (!this.m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
						{
							gameTime = this.m_subsystemTime.GameTime;
							this.m_aimStartTimes[componentMiner] = gameTime;
						}
						float num4 = (float)(this.m_subsystemTime.GameTime - gameTime);
						float num5 = (float)MathUtils.Remainder(this.m_subsystemTime.GameTime, 1000.0);
						float num6 = (componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.2f * MathUtils.Saturate((num4 - 2.5f) / 6f);
						Vector3 vector = default(Vector3);
						vector.X = SimplexNoise.OctavedNoise(num5, 2f, 3, 2f, 0.5f, false);
						vector.Y = SimplexNoise.OctavedNoise(num5 + 100f, 2f, 3, 2f, 0.5f, false);
						vector.Z = SimplexNoise.OctavedNoise(num5 + 200f, 2f, 3, 2f, 0.5f, false);
						Vector3 vector2 = num6 * vector;
						aim.Direction = Vector3.Normalize(aim.Direction + vector2);
						switch (state)
						{
						case AimState.InProgress:
						{
							if (num4 >= 10f)
							{
								componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
								return true;
							}
							if (num4 > 0.5f && !MusketBlock.GetHammerState(Terrain.ExtractData(num2)))
							{
								num2 = Terrain.MakeBlockValue(num, 0, MusketBlock.SetHammerState(Terrain.ExtractData(num2), true));
								this.m_subsystemAudio.PlaySound("Audio/HammerCock", 1f, this.m_random.Float(-0.1f, 0.1f), 0f, 0f);
							}
							ComponentFirstPersonModel componentFirstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
							if (componentFirstPersonModel != null)
							{
								ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
								if (componentPlayer != null)
								{
									componentPlayer.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);
								}
								componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
								componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
							}
							componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
							componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
							componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
							break;
						}
						case AimState.Cancelled:
							if (MusketBlock.GetHammerState(Terrain.ExtractData(num2)))
							{
								num2 = Terrain.MakeBlockValue(num, 0, MusketBlock.SetHammerState(Terrain.ExtractData(num2), false));
								this.m_subsystemAudio.PlaySound("Audio/HammerUncock", 1f, this.m_random.Float(-0.1f, 0.1f), 0f, 0f);
							}
							this.m_aimStartTimes.Remove(componentMiner);
							break;
						case AimState.Completed:
						{
							bool flag = false;
							int value = 0;
							int num7 = 0;
							float num8 = 0f;
							Vector3 zero = Vector3.Zero;
							MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
							BulletBlock.BulletType? bulletType = MusketBlock.GetBulletType(data);
							if (MusketBlock.GetHammerState(Terrain.ExtractData(num2)))
							{
								switch (loadState)
								{
								case MusketBlock.LoadState.Empty:
								{
									ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
									if (componentPlayer2 != null)
									{
										componentPlayer2.ComponentGui.DisplaySmallMessage(LanguageControl.Get(SubsystemMusketBlockBehavior.fName, 0), Color.White, true, false, 1f);
									}
									break;
								}
								case MusketBlock.LoadState.Gunpowder:
								case MusketBlock.LoadState.Wad:
								{
									flag = true;
									ComponentPlayer componentPlayer3 = componentMiner.ComponentPlayer;
									if (componentPlayer3 != null)
									{
										componentPlayer3.ComponentGui.DisplaySmallMessage(LanguageControl.Get(SubsystemMusketBlockBehavior.fName, 1), Color.White, true, false, 1f);
									}
									break;
								}
								case MusketBlock.LoadState.Loaded:
									flag = true;
									if (bulletType.GetValueOrDefault() == BulletBlock.BulletType.Buckshot)
									{
										value = Terrain.MakeBlockValue(this.m_BulletBlockIndex, 0, BulletBlock.SetBulletType(0, BulletBlock.BulletType.BuckshotBall));
										num7 = 8;
										zero..ctor(0.04f, 0.04f, 0.25f);
										num8 = 80f;
									}
									else if (bulletType.GetValueOrDefault() == BulletBlock.BulletType.BuckshotBall)
									{
										value = Terrain.MakeBlockValue(this.m_BulletBlockIndex, 0, BulletBlock.SetBulletType(0, BulletBlock.BulletType.BuckshotBall));
										num7 = 1;
										zero..ctor(0.06f, 0.06f, 0f);
										num8 = 60f;
									}
									else if (bulletType != null)
									{
										value = Terrain.MakeBlockValue(this.m_BulletBlockIndex, 0, BulletBlock.SetBulletType(0, bulletType.Value));
										num7 = 1;
										num8 = 120f;
									}
									break;
								}
							}
							if (flag)
							{
								if (componentMiner.ComponentCreature.ComponentBody.ImmersionFactor > 0.4f)
								{
									this.m_subsystemAudio.PlaySound("Audio/MusketMisfire", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, true);
								}
								else
								{
									Vector3 vector3 = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
									Vector3 vector4 = Vector3.Normalize(vector3 + aim.Direction * 10f - vector3);
									Vector3 vector5 = Vector3.Normalize(Vector3.Cross(vector4, Vector3.UnitY));
									Vector3 vector6 = Vector3.Normalize(Vector3.Cross(vector4, vector5));
									for (int i = 0; i < num7; i++)
									{
										Vector3 vector7 = this.m_random.Float(0f - zero.X, zero.X) * vector5 + this.m_random.Float(0f - zero.Y, zero.Y) * vector6 + this.m_random.Float(0f - zero.Z, zero.Z) * vector4;
										Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + num8 * (vector4 + vector7);
										Projectile projectile = this.m_subsystemProjectiles.FireProjectile(value, vector3, velocity, Vector3.Zero, componentMiner.ComponentCreature);
										if (projectile != null)
										{
											projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
										}
									}
									this.m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 10f, true);
									this.m_subsystemParticles.AddParticleSystem(new GunSmokeParticleSystem(this.m_subsystemTerrain, vector3 + 0.3f * vector4, vector4), false);
									this.m_subsystemNoise.MakeNoise(vector3, 1f, 40f);
									componentMiner.ComponentCreature.ComponentBody.ApplyImpulse(-4f * vector4);
								}
								num2 = Terrain.MakeBlockValue(Terrain.ExtractContents(num2), 0, MusketBlock.SetLoadState(Terrain.ExtractData(num2), MusketBlock.LoadState.Empty));
								num3 = 1;
							}
							if (MusketBlock.GetHammerState(Terrain.ExtractData(num2)))
							{
								num2 = Terrain.MakeBlockValue(Terrain.ExtractContents(num2), 0, MusketBlock.SetHammerState(Terrain.ExtractData(num2), false));
								this.m_subsystemAudio.PlaySound("Audio/HammerRelease", 1f, this.m_random.Float(-0.1f, 0.1f), 0f, 0f);
							}
							this.m_aimStartTimes.Remove(componentMiner);
							break;
						}
						}
					}
					if (num2 != slotValue)
					{
						inventory.RemoveSlotItems(activeSlotIndex, 1);
						inventory.AddSlotItems(activeSlotIndex, num2, 1);
					}
					if (num3 > 0)
					{
						componentMiner.DamageActiveTool(num3);
					}
				}
			}
			return false;
		}

		// Token: 0x06001C34 RID: 7220 RVA: 0x000DFB44 File Offset: 0x000DDD44
		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int num = Terrain.ExtractContents(value);
			MusketBlock.LoadState loadState = MusketBlock.GetLoadState(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));
			if (loadState == MusketBlock.LoadState.Empty && num == 109)
			{
				return 1;
			}
			if (loadState == MusketBlock.LoadState.Gunpowder && num == 205)
			{
				return 1;
			}
			if (loadState == MusketBlock.LoadState.Wad && num == this.m_BulletBlockIndex)
			{
				return 1;
			}
			return 0;
		}

		// Token: 0x06001C35 RID: 7221 RVA: 0x000DFB94 File Offset: 0x000DDD94
		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;
			if (processCount == 1)
			{
				int data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
				MusketBlock.LoadState loadState = MusketBlock.GetLoadState(data);
				BulletBlock.BulletType? bulletType = MusketBlock.GetBulletType(data);
				switch (loadState)
				{
				case MusketBlock.LoadState.Empty:
					loadState = MusketBlock.LoadState.Gunpowder;
					bulletType = null;
					break;
				case MusketBlock.LoadState.Gunpowder:
					loadState = MusketBlock.LoadState.Wad;
					bulletType = null;
					break;
				case MusketBlock.LoadState.Wad:
				{
					loadState = MusketBlock.LoadState.Loaded;
					int data2 = Terrain.ExtractData(value);
					bulletType = new BulletBlock.BulletType?(BulletBlock.GetBulletType(data2));
					break;
				}
				}
				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(this.m_MusketBlockIndex, 0, MusketBlock.SetBulletType(MusketBlock.SetLoadState(data, loadState), bulletType)), 1);
			}
		}

		// Token: 0x06001C36 RID: 7222 RVA: 0x000DFC44 File Offset: 0x000DDE44
		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			this.m_BulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>(false, false);
			this.m_MusketBlockIndex = BlocksManager.GetBlockIndex<MusketBlock>(false, false);
			base.Load(valuesDictionary);
		}

		// Token: 0x04001343 RID: 4931
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x04001344 RID: 4932
		public SubsystemTime m_subsystemTime;

		// Token: 0x04001345 RID: 4933
		public SubsystemProjectiles m_subsystemProjectiles;

		// Token: 0x04001346 RID: 4934
		public SubsystemParticles m_subsystemParticles;

		// Token: 0x04001347 RID: 4935
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x04001348 RID: 4936
		public SubsystemNoise m_subsystemNoise;

		// Token: 0x04001349 RID: 4937
		public static string fName = "SubsystemMusketBlockBehavior";

		// Token: 0x0400134A RID: 4938
		public Random m_random = new Random();

		// Token: 0x0400134B RID: 4939
		public Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();

		// Token: 0x0400134C RID: 4940
		public int m_BulletBlockIndex;

		// Token: 0x0400134D RID: 4941
		public int m_MusketBlockIndex;
	}
}
