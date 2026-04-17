using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x020000F5 RID: 245
	public class SubsystemBK43Behavior : SubsystemBlockBehavior
	{
		// Token: 0x170000E0 RID: 224
		// (get) Token: 0x06000901 RID: 2305 RVA: 0x00062A4C File Offset: 0x00060C4C
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex(typeof(BK43Block), true, false)
				};
			}
		}

		// Token: 0x06000902 RID: 2306 RVA: 0x00062A78 File Offset: 0x00060C78
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

		// Token: 0x06000903 RID: 2307 RVA: 0x00062B14 File Offset: 0x00060D14
		public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
		{
			Vector3 vector = aim.Direction;
			IInventory inventory = componentMiner.Inventory;
			bool flag = inventory != null;
			bool flag2 = flag;
			if (flag2)
			{
				int activeSlotIndex = inventory.ActiveSlotIndex;
				bool flag3 = activeSlotIndex >= 0;
				bool flag4 = flag3;
				if (flag4)
				{
					int slotValue = inventory.GetSlotValue(activeSlotIndex);
					int slotCount = inventory.GetSlotCount(activeSlotIndex);
					int num = Terrain.ExtractContents(slotValue);
					int num2 = slotValue;
					bool flag5 = num == BlocksManager.GetBlockIndex(typeof(BK43Block), true, false) && slotCount > 0;
					bool flag6 = flag5;
					if (flag6)
					{
						double gameTime;
						bool flag7 = !this.m_aimStartTimes.TryGetValue(componentMiner, out gameTime);
						bool flag8 = flag7;
						if (flag8)
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
						bool flag9 = num3 > 1f;
						bool flag10 = flag9;
						if (flag10)
						{
							bool flag11 = 0.2f * num3 < 1.2f;
							bool flag12 = flag11;
							if (flag12)
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
									bool flag13 = componentFirstPersonModel != null;
									bool flag14 = flag13;
									if (flag14)
									{
										ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
										bool flag15 = componentPlayer != null;
										bool flag16 = flag15;
										if (flag16)
										{
											componentPlayer.ComponentAimingSights.ShowAimingSights(aim.Position, vector);
										}
										componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
										componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
									int bulletNum = BK43Block.GetBulletNum(Terrain.ExtractData(slotValue));
									bool flag17 = bulletNum > 0;
									bool flag18 = this.m_subsystemTime.PeriodicGameTimeEvent(1.5, 0.0) && flag17;
									bool flag19 = flag18;
									if (flag19)
									{
										Vector3 vector2 = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
										Vector3 v2 = Vector3.Normalize(vector2 + vector * 10f - vector2);
										int value = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(NuevaBala3), true, false), 0, 0);
										this.m_subsystemProjectiles.FireProjectile(value, vector2, 300f * v2, Vector3.Zero, componentMiner.ComponentCreature);
										this.m_subsystemAudio.PlaySound("Audio/Armas/bk 43", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 15f, true);
										Vector3 vector3 = Vector3.Normalize(componentMiner.ComponentPlayer.GameWidget.ActiveCamera.ViewDirection);
										this.m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(this.m_subsystemTerrain, vector2 + 1.3f * vector3, vector3), false);
										this.m_subsystemNoise.MakeNoise(vector2, 1.5f, 50f);
										int bulletNum2 = bulletNum - 1;
										num2 = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(BK43Block), true, false), 0, BK43Block.SetBulletNum(bulletNum2));
										ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
										bool flag20 = componentPlayer2 != null;
										bool flag21 = flag20;
										if (flag21)
										{
											componentPlayer2.ComponentGui.DisplaySmallMessage(bulletNum2.ToString(), Color.White, true, false);
										}
										bool flag22 = componentFirstPersonModel != null;
										bool flag23 = flag22;
										if (flag23)
										{
											componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.85f, 0f, 0f);
										}
									}
									else
									{
										bool flag24 = !flag17 && this.m_subsystemTime.PeriodicGameTimeEvent(0.5, 0.0);
										bool flag25 = flag24;
										if (flag25)
										{
											this.m_subsystemAudio.PlaySound("Audio/Armas/Empty fire", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, true);
											ComponentPlayer componentPlayer3 = componentMiner.ComponentPlayer;
											bool flag26 = componentPlayer3 != null;
											if (flag26)
											{
												string newValue = LanguageControl.Get(new string[]
												{
											"Blocks",
											"BK43BulletBlock:0",
											"DisplayName"
												});
												componentPlayer3.ComponentGui.DisplaySmallMessage(LanguageControl.Get(new string[]
												{
											"Messages",
											"NeedAmmo"
												}).Replace("{0}", newValue), Color.White, true, false);
											}
										}
									}
									break;
								}
							case AimState.Cancelled:
								{
									this.m_aimStartTimes.Remove(componentMiner);
									bool flag27 = num2 != slotValue;
									bool flag28 = flag27;
									if (flag28)
									{
										inventory.RemoveSlotItems(activeSlotIndex, 1);
										inventory.AddSlotItems(activeSlotIndex, num2, 1);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
									ComponentFirstPersonModel componentFirstPersonModel2 = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									bool flag29 = componentFirstPersonModel2 != null;
									bool flag30 = flag29;
									if (flag30)
									{
										componentFirstPersonModel2.ItemOffsetOrder = Vector3.Zero;
										componentFirstPersonModel2.ItemRotationOrder = Vector3.Zero;
									}
									break;
								}
							case AimState.Completed:
								{
									this.m_aimStartTimes.Remove(componentMiner);
									bool flag31 = num2 != slotValue;
									bool flag32 = flag31;
									if (flag32)
									{
										inventory.RemoveSlotItems(activeSlotIndex, 1);
										inventory.AddSlotItems(activeSlotIndex, num2, 1);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
									ComponentFirstPersonModel componentFirstPersonModel3 = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									bool flag33 = componentFirstPersonModel3 != null;
									bool flag34 = flag33;
									if (flag34)
									{
										componentFirstPersonModel3.ItemOffsetOrder = Vector3.Zero;
										componentFirstPersonModel3.ItemRotationOrder = Vector3.Zero;
									}
									break;
								}
						}
						bool flag35 = state == AimState.InProgress && num2 != slotValue;
						bool flag36 = flag35;
						if (flag36)
						{
							inventory.RemoveSlotItems(activeSlotIndex, 1);
							inventory.AddSlotItems(activeSlotIndex, num2, 1);
						}
					}
				}
			}
			return false;
		}

		// Token: 0x06000904 RID: 2308 RVA: 0x00063314 File Offset: 0x00061514
		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int blockIndex = BlocksManager.GetBlockIndex(typeof(BK43Block), true, false);
			int blockIndex2 = BlocksManager.GetBlockIndex(typeof(BK43BulletBlock), true, false);
			bool flag = value == blockIndex2;
			if (flag)
			{
				int slotValue = inventory.GetSlotValue(slotIndex);
				int num = Terrain.ExtractContents(slotValue);
				bool flag2 = num == blockIndex;
				if (flag2)
				{
					int data = Terrain.ExtractData(slotValue);
					int bulletNum = BK43Block.GetBulletNum(data);
					return (bulletNum < 2) ? 1 : 0;
				}
			}
			return 0;
		}

		// Token: 0x06000905 RID: 2309 RVA: 0x00063394 File Offset: 0x00061594
		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;
			int blockIndex = BlocksManager.GetBlockIndex(typeof(BK43Block), true, false);
			int blockIndex2 = BlocksManager.GetBlockIndex(typeof(BK43BulletBlock), true, false);
			bool flag = value == blockIndex2;
			if (flag)
			{
				int slotValue = inventory.GetSlotValue(slotIndex);
				int num = Terrain.ExtractContents(slotValue);
				bool flag2 = num == blockIndex;
				if (flag2)
				{
					int data = Terrain.ExtractData(slotValue);
					int bulletNum = BK43Block.GetBulletNum(data);
					bool flag3 = bulletNum < 2;
					if (flag3)
					{
						processedValue = 0;
						processedCount = 0;
						inventory.RemoveSlotItems(slotIndex, 1);
						int data2 = BK43Block.SetBulletNum(2);
						int value2 = Terrain.MakeBlockValue(blockIndex, 0, data2);
						inventory.AddSlotItems(slotIndex, value2, 1);
						SubsystemPlayers subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
						bool flag4 = subsystemPlayers != null && this.m_subsystemAudio != null;
						if (flag4)
						{
							for (int i = 0; i < subsystemPlayers.ComponentPlayers.Count; i++)
							{
								ComponentPlayer componentPlayer = subsystemPlayers.ComponentPlayers[i];
								bool flag5 = componentPlayer != null && componentPlayer.ComponentMiner != null && componentPlayer.ComponentMiner.Inventory == inventory;
								if (flag5)
								{
									Vector3 eyePosition = componentPlayer.ComponentCreatureModel.EyePosition;
									this.m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 5f, true);
									break;
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x0400078B RID: 1931
		public SubsystemTerrain m_subsystemTerrain;

		// Token: 0x0400078C RID: 1932
		public SubsystemTime m_subsystemTime;

		// Token: 0x0400078D RID: 1933
		public SubsystemProjectiles m_subsystemProjectiles;

		// Token: 0x0400078E RID: 1934
		public SubsystemParticles m_subsystemParticles;

		// Token: 0x0400078F RID: 1935
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x04000790 RID: 1936
		private SubsystemGameInfo m_subsystemGameInfo;

		// Token: 0x04000791 RID: 1937
		public SubsystemNoise m_subsystemNoise;

		// Token: 0x04000792 RID: 1938
		public Random m_random = new Random();

		// Token: 0x04000793 RID: 1939
		public Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();

		// Token: 0x04000794 RID: 1940
		public bool fire;
	}
}
