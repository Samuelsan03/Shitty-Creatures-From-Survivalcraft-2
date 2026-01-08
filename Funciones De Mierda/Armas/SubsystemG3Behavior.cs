using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Armas;
using Engine;
using Game;
using TemplatesDatabase;

namespace Armas
{
	// Token: 0x02000031 RID: 49
	public class SubsystemG3Behavior : SubsystemBlockBehavior
	{
		// Token: 0x17000013 RID: 19
		// (get) Token: 0x06000104 RID: 260 RVA: 0x0000A298 File Offset: 0x00008498
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex(typeof(G3Block), true, false)
				};
			}
		}

		// Token: 0x06000105 RID: 261 RVA: 0x0000A2C4 File Offset: 0x000084C4
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

		// Token: 0x06000106 RID: 262 RVA: 0x0000A360 File Offset: 0x00008560
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
					bool flag3 = num == BlocksManager.GetBlockIndex(typeof(G3Block), true, false) && slotCount > 0;
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
									int bulletNum = G3Block.GetBulletNum(Terrain.ExtractData(slotValue));
									bool flag9 = bulletNum > 0;
									bool flag10 = this.m_subsystemTime.PeriodicGameTimeEvent(0.12, 0.0) && flag9;
									if (flag10)
									{
										Vector3 vector2 = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
										Vector3 vector3 = Vector3.Normalize(vector2 + vector * 10f - vector2);
										Vector3 vector4 = Vector3.Normalize(Vector3.Cross(vector3, Vector3.UnitY));
										Vector3 v2 = Vector3.Normalize(Vector3.Cross(vector3, vector4));
										int num5 = 2;
										Vector3 vector5 = new Vector3(0.009f, 0.009f, 0.04f);
										for (int i = 0; i < num5; i++)
										{
											int value = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(NuevaBala), true, false), 0, 2);
											Vector3 v3 = this.m_random.Float(-vector5.X, vector5.X) * vector4 + this.m_random.Float(-vector5.Y, vector5.Y) * v2 + this.m_random.Float(-vector5.Z, vector5.Z) * vector3;
											this.m_subsystemProjectiles.FireProjectile(value, vector2, 290f * (vector3 + v3), Vector3.Zero, componentMiner.ComponentCreature);
										}
										this.m_subsystemAudio.PlaySound("Audio/Armas/FX05", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 10f, true);
										Vector3 vector6 = Vector3.Normalize(componentMiner.ComponentPlayer.GameWidget.ActiveCamera.ViewDirection);
										this.m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(this.m_subsystemTerrain, vector2 + 1.3f * vector6, vector6), false);
										this.m_subsystemNoise.MakeNoise(vector2, 1f, 40f);
										int bulletNum2 = bulletNum - 1;
										num2 = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(G3Block), true, false), 0, G3Block.SetBulletNum(bulletNum2));
										ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
										bool flag11 = componentPlayer2 != null;
										if (flag11)
										{
											componentPlayer2.ComponentGui.DisplaySmallMessage(bulletNum2.ToString(), Color.White, true, false);
										}
										componentMiner.ComponentCreature.ComponentBody.ApplyImpulse(-0.09f * vector3);
										bool flag12 = componentFirstPersonModel != null;
										if (flag12)
										{
											componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.85f, 0f, 0f);
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

		// Token: 0x06000107 RID: 263 RVA: 0x0000AB84 File Offset: 0x00008D84
		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			bool flag = value == BlocksManager.GetBlockIndex(typeof(G3Bullet), true, false) && G3Block.GetBulletNum(Terrain.ExtractData(inventory.GetSlotValue(slotIndex))) < 30;
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

		// Token: 0x06000108 RID: 264 RVA: 0x0000ABD4 File Offset: 0x00008DD4
		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;
			int bulletNum = G3Block.GetBulletNum(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));
			bool flag = bulletNum < 30;
			bool flag2 = flag;
			if (flag2)
			{
				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(G3Block), true, false), 0, G3Block.SetBulletNum(30)), 1);
			}
		}

		// Token: 0x040000C1 RID: 193
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x040000C2 RID: 194
		public SubsystemTime m_subsystemTime;

		// Token: 0x040000C3 RID: 195
		public SubsystemProjectiles m_subsystemProjectiles;

		// Token: 0x040000C4 RID: 196
		public SubsystemParticles m_subsystemParticles;

		// Token: 0x040000C5 RID: 197
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x040000C6 RID: 198
		private SubsystemGameInfo m_subsystemGameInfo;

		// Token: 0x040000C7 RID: 199
		public SubsystemNoise m_subsystemNoise;

		// Token: 0x040000C8 RID: 200
		public Game.Random m_random = new Game.Random();

		// Token: 0x040000C9 RID: 201
		public Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();

		// Token: 0x040000CA RID: 202
		public bool fire;
	}
}
