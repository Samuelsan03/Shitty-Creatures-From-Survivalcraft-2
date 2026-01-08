using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Engine;
using Game;
using TemplatesDatabase;

namespace Armas
{
	public class SubsystemMinigunBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false)
				};
			}
		}

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
					bool flag3 = num == BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false) && slotCount > 0;
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
						Vector3 v = (float)((componentMiner.ComponentCreature.ComponentBody.IsSneaking ? 0.015 : 0.04) + 0.300000002980232 * (double)MathUtils.Saturate((num3 - 6.5f) / 40f)) * new Vector3
						{
							X = SimplexNoise.OctavedNoise(num4, 2f, 3, 2f, 0.6f, false),
							Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 2f, 0.6f, false),
							Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 2f, 0.6f, false)
						};
						bool flag5 = num3 > 1f;
						if (flag5)
						{
							bool flag6 = 0.2f * num3 < 1.2f;
							if (flag6)
							{
								vector.Y += 0.15f * (num3 - 1f);
							}
							else
							{
								vector.Y += 0.4f;
							}
						}
						vector = Vector3.Normalize(vector + v * 2.5f);
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
										componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.25f, 0.1f, 0.06f);
										componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.8f, 0f, 0f);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.3f;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.1f, 0.05f);
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.8f, 0f, 0f);
									int bulletNum = MinigunBlock.GetBulletNum(Terrain.ExtractData(slotValue));
									bool flag9 = bulletNum > 0;
									bool flag10 = this.m_subsystemTime.PeriodicGameTimeEvent(0.08, 0.0) && flag9;
									if (flag10)
									{
										Vector3 vector2 = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.35f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.25f;
										Vector3 vector3 = Vector3.Normalize(vector2 + vector * 10f - vector2);
										Vector3 vector4 = Vector3.Normalize(Vector3.Cross(vector3, Vector3.UnitY));
										Vector3 v2 = Vector3.Normalize(Vector3.Cross(vector3, vector4));
										int num5 = 1;
										Vector3 vector5 = new Vector3(0.02f, 0.02f, 0.08f);
										for (int i = 0; i < num5; i++)
										{
											// USAR MinigunBullet para disparar
											int value = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(NuevaBala6), true, false), 0, 0);
											Vector3 v3 = this.m_random.Float(-vector5.X, vector5.X) * vector4 + this.m_random.Float(-vector5.Y, vector5.Y) * v2 + this.m_random.Float(-vector5.Z, vector5.Z) * vector3;
											this.m_subsystemProjectiles.FireProjectile(value, vector2, 260f * (vector3 + v3), Vector3.Zero, componentMiner.ComponentCreature);
										}
										this.m_subsystemAudio.PlaySound("Audio/Armas/Chaingun fuego", 1.3f, this.m_random.Float(-0.15f, 0.15f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 12f, true);
										Vector3 vector6 = Vector3.Normalize(componentMiner.ComponentPlayer.GameWidget.ActiveCamera.ViewDirection);
										this.m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(this.m_subsystemTerrain, vector2 + 1.5f * vector6, vector6), false);
										this.m_subsystemNoise.MakeNoise(vector2, 1.3f, 50f);
										int bulletNum2 = bulletNum - 1;
										num2 = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false), 0, MinigunBlock.SetBulletNum(bulletNum2));
										ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
										bool flag11 = componentPlayer2 != null;
										if (flag11)
										{
											componentPlayer2.ComponentGui.DisplaySmallMessage(bulletNum2.ToString(), Color.White, true, false);
										}
										componentMiner.ComponentCreature.ComponentBody.ApplyImpulse(-0.15f * vector3);
										bool flag12 = componentFirstPersonModel != null;
										if (flag12)
										{
											componentFirstPersonModel.ItemRotationOrder = new Vector3(-1.1f, this.m_random.Float(-0.1f, 0.1f), 0f);
										}
									}
									else
									{
										bool flag13 = !flag9 && this.m_subsystemTime.PeriodicGameTimeEvent(0.5, 0.0);
										if (flag13)
										{
											this.m_subsystemAudio.PlaySound("Audio/WeaponDryFire", 0.8f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 4f, true);
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

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			// VERIFICAR SI ES MinigunBullet para recargar
			bool flag = value == BlocksManager.GetBlockIndex(typeof(MinigunBullet), true, false) &&
					   MinigunBlock.GetBulletNum(Terrain.ExtractData(inventory.GetSlotValue(slotIndex))) < 100;

			return flag ? 1 : 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;

			int slotValue = inventory.GetSlotValue(slotIndex);
			int blockIndex = Terrain.ExtractContents(slotValue);

			if (blockIndex == BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false))
			{
				int bulletNum = MinigunBlock.GetBulletNum(Terrain.ExtractData(slotValue));
				bool flag = bulletNum < 100;

				if (flag)
				{
					processedValue = 0;
					processedCount = 0;
					inventory.RemoveSlotItems(slotIndex, 1);
					inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(MinigunBlock), true, false), 0, MinigunBlock.SetBulletNum(100)), 1);
				}
			}
		}

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemTime m_subsystemTime;
		public SubsystemProjectiles m_subsystemProjectiles;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemAudio m_subsystemAudio;
		private SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemNoise m_subsystemNoise;
		public Game.Random m_random = new Game.Random();
		public Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();
		public bool fire;
	}
}