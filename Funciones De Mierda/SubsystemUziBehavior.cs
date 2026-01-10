using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x0200003A RID: 58
	public class SubsystemUziBehavior : SubsystemBlockBehavior
	{
		// Token: 0x1700001C RID: 28
		// (get) Token: 0x0600013A RID: 314 RVA: 0x0000F6BC File Offset: 0x0000D8BC
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex(typeof(UziBlock), true, false)
				};
			}
		}

		// Token: 0x0600013B RID: 315 RVA: 0x0000F6E8 File Offset: 0x0000D8E8
		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.fire = true;
		}

		// Token: 0x0600013C RID: 316 RVA: 0x0000F784 File Offset: 0x0000D984
		public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
		{
			Vector3 vector = aim.Direction;
			IInventory inventory = componentMiner.Inventory;
			bool flag = inventory != null;
			if (flag)
			{
				int activeSlotIndex = inventory.ActiveSlotIndex;
				bool flag2 = activeSlotIndex >= 0;
				if (flag2)
				{
					int slotValue = inventory.GetSlotValue(activeSlotIndex);
					int slotCount = inventory.GetSlotCount(activeSlotIndex);
					int num = Terrain.ExtractContents(slotValue);
					int num2 = slotValue;
					bool flag3 = num == BlocksManager.GetBlockIndex(typeof(UziBlock), true, false) && slotCount > 0;
					if (flag3)
					{
						double gameTime;
						bool flag4 = !this.m_aimStartTimes.TryGetValue(componentMiner, out gameTime);
						if (flag4)
						{
							gameTime = this.m_subsystemTime.GameTime;
							this.m_aimStartTimes[componentMiner] = gameTime;
						}
						float num3 = (float)(this.m_subsystemTime.GameTime - gameTime);
						float num4 = (float)MathUtils.Remainder(this.m_subsystemTime.GameTime, 1000.0);
						Vector3 v = (float)((componentMiner.ComponentCreature.ComponentBody.IsSneaking ? 0.00999999977648258 : 0.0299999993294477) + 0.200000002980232 * (double)MathUtils.Saturate((num3 - 6.5f) / 40f)) * new Vector3
						{
							X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.5f, false),
							Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.5f, false),
							Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.5f, false)
						};
						bool flag5 = num3 > 1f;
						if (flag5)
						{
							bool flag6 = 0.2f * num3 < 1.2f;
							if (flag6)
							{
								vector.Y += 0.1f * (num3 - 1f);
							}
							else
							{
								vector.Y += 0.3f;
							}
						}
						vector = Vector3.Normalize(vector + v * 2f);
						switch (state)
						{
							case AimState.InProgress:
								{
									ComponentFirstPersonModel componentFirstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									bool flag7 = componentFirstPersonModel != null;
									if (flag7)
									{
										ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
										bool flag8 = componentPlayer != null;
										if (flag8)
										{
											componentPlayer.ComponentAimingSights.ShowAimingSights(aim.Position, vector);
										}
										componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
										componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
									int bulletNum = UziBlock.GetBulletNum(Terrain.ExtractData(slotValue));
									bool flag9 = bulletNum > 0;
									bool flag10 = this.m_subsystemTime.PeriodicGameTimeEvent(0.08, 0.0) && flag9;
									if (flag10)
									{
										Vector3 vector2 = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
										Vector3 vector3 = Vector3.Normalize(vector2 + vector * 10f - vector2);
										Vector3 vector4 = Vector3.Normalize(Vector3.Cross(vector3, Vector3.UnitY));
										Vector3 v2 = Vector3.Normalize(Vector3.Cross(vector3, vector4));
										int num5 = 2;
										Vector3 vector5 = new Vector3(0.015f, 0.015f, 0.06f);
										for (int i = 0; i < num5; i++)
										{
											int value = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(NuevaBala2), true, false), 0, 2);
											Vector3 v3 = this.m_random.Float(-vector5.X, vector5.X) * vector4 + this.m_random.Float(-vector5.Y, vector5.Y) * v2 + this.m_random.Float(-vector5.Z, vector5.Z) * vector3;
											this.m_subsystemProjectiles.FireProjectile(value, vector2, 320f * (vector3 + v3), Vector3.Zero, componentMiner.ComponentCreature);
										}
										this.m_subsystemAudio.PlaySound("Audio/Armas/Uzi fuego", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 10f, true);
										Vector3 vector6 = Vector3.Normalize(componentMiner.ComponentPlayer.GameWidget.ActiveCamera.ViewDirection);
										this.m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(this.m_subsystemTerrain, vector2 + 1.3f * vector6, vector6), false);
										this.m_subsystemNoise.MakeNoise(vector2, 1f, 40f);
										int bulletNum2 = bulletNum - 1;
										num2 = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(UziBlock), true, false), 0, UziBlock.SetBulletNum(bulletNum2));
										ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
										bool flag11 = componentPlayer2 != null;
										if (flag11)
										{
											componentPlayer2.ComponentGui.DisplaySmallMessage(bulletNum2.ToString(), Color.White, true, false);
										}
										bool flag12 = componentFirstPersonModel != null;
										if (flag12)
										{
											componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.8f, 0f, 0f);
										}
									}
									else
									{
										bool flag13 = !flag9 && this.m_subsystemTime.PeriodicGameTimeEvent(0.5, 0.0);
										if (flag13)
										{
											this.m_subsystemAudio.PlaySound("Audio/WeaponDryFire", 0.7f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, true);
										}
									}
									break;
								}
							case AimState.Cancelled:
								{
									this.m_aimStartTimes.Remove(componentMiner);
									bool flag14 = num2 != slotValue;
									if (flag14)
									{
										inventory.RemoveSlotItems(activeSlotIndex, 1);
										inventory.AddSlotItems(activeSlotIndex, num2, 1);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
									ComponentFirstPersonModel componentFirstPersonModel2 = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									bool flag15 = componentFirstPersonModel2 != null;
									if (flag15)
									{
										componentFirstPersonModel2.ItemOffsetOrder = Vector3.Zero;
										componentFirstPersonModel2.ItemRotationOrder = Vector3.Zero;
									}
									break;
								}
							case AimState.Completed:
								{
									this.m_aimStartTimes.Remove(componentMiner);
									bool flag16 = num2 != slotValue;
									if (flag16)
									{
										inventory.RemoveSlotItems(activeSlotIndex, 1);
										inventory.AddSlotItems(activeSlotIndex, num2, 1);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
									ComponentFirstPersonModel componentFirstPersonModel3 = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									bool flag17 = componentFirstPersonModel3 != null;
									if (flag17)
									{
										componentFirstPersonModel3.ItemOffsetOrder = Vector3.Zero;
										componentFirstPersonModel3.ItemRotationOrder = Vector3.Zero;
									}
									break;
								}
						}
						bool flag18 = state == AimState.InProgress && num2 != slotValue;
						if (flag18)
						{
							inventory.RemoveSlotItems(activeSlotIndex, 1);
							inventory.AddSlotItems(activeSlotIndex, num2, 1);
						}
					}
				}
			}
			return false;
		}

		// Token: 0x0600013D RID: 317 RVA: 0x0000FFA8 File Offset: 0x0000E1A8
		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			bool flag = value == BlocksManager.GetBlockIndex(typeof(UziBullet), true, false) && UziBlock.GetBulletNum(Terrain.ExtractData(inventory.GetSlotValue(slotIndex))) < 30;
			bool flag2 = flag;
			int result;
			if (flag2)
			{
				result = 1;
			}
			else
			{
				result = 0;
			}
			return result;
		}

		// Token: 0x0600013E RID: 318 RVA: 0x0000FFF8 File Offset: 0x0000E1F8
		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;
			int bulletNum = UziBlock.GetBulletNum(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));
			bool flag = bulletNum < 30;
			bool flag2 = flag;
			if (flag2)
			{
				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(UziBlock), true, false), 0, UziBlock.SetBulletNum(30)), 1);
			}
		}

		// Token: 0x0400011B RID: 283
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x0400011C RID: 284
		public SubsystemTime m_subsystemTime;

		// Token: 0x0400011D RID: 285
		public SubsystemProjectiles m_subsystemProjectiles;

		// Token: 0x0400011E RID: 286
		public SubsystemParticles m_subsystemParticles;

		// Token: 0x0400011F RID: 287
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x04000120 RID: 288
		private SubsystemGameInfo m_subsystemGameInfo;

		// Token: 0x04000121 RID: 289
		public SubsystemNoise m_subsystemNoise;

		// Token: 0x04000122 RID: 290
		public Game.Random m_random = new Game.Random();

		// Token: 0x04000123 RID: 291
		public Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();

		// Token: 0x04000124 RID: 292
		public bool fire;
	}
}
