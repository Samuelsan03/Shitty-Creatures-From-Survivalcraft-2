using System;
using System.Collections.Generic;
using Engine;
using Game;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemFlameThrowerBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex<FlameThrowerBlock>(false, false)
				};
			}
		}

		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			if (componentPlayer.ComponentGui.ModalPanelWidget == null)
			{
				componentPlayer.ComponentGui.ModalPanelWidget = new FlameThrowerWidget(inventory, slotIndex);
			}
			return true;
		}

		public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
		{
			IInventory inventory = componentMiner.Inventory;
			if (inventory == null)
			{
				return false;
			}
			int activeSlotIndex = inventory.ActiveSlotIndex;
			if (activeSlotIndex < 0)
			{
				return false;
			}
			int slotValue = inventory.GetSlotValue(activeSlotIndex);
			int slotCount = inventory.GetSlotCount(activeSlotIndex);
			int num = Terrain.ExtractContents(slotValue);
			int data = Terrain.ExtractData(slotValue);
			int num2 = slotValue;
			int num3 = 0;
			if (num == this.m_FlameThrowerBlockIndex && slotCount > 0)
			{
				double gameTime;
				if (!this.m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
				{
					gameTime = this.m_subsystemTime.GameTime;
					this.m_aimStartTimes[componentMiner] = gameTime;
				}
				float num4 = (float)(this.m_subsystemTime.GameTime - gameTime);
				float num5 = (float)MathUtils.Remainder(this.m_subsystemTime.GameTime, 1000.0);
				Vector3 v = ((componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.2f * MathUtils.Saturate((num4 - 2.5f) / 6f)) * new Vector3
				{
					X = SimplexNoise.OctavedNoise(num5, 2f, 3, 2f, 0.5f, false),
					Y = SimplexNoise.OctavedNoise(num5 + 100f, 2f, 3, 2f, 0.5f, false),
					Z = SimplexNoise.OctavedNoise(num5 + 200f, 2f, 3, 2f, 0.5f, false)
				};
				aim.Direction = Vector3.Normalize(aim.Direction + v);
				switch (state)
				{
					case AimState.InProgress:
						{
							if (num4 >= 5f)
							{
								int loadCount = FlameThrowerBlock.GetLoadCount(num2);
								if (loadCount > 1)
								{
									num2 = FlameThrowerBlock.SetLoadCount(Terrain.MakeBlockValue(num, 0, FlameThrowerBlock.SetSwitchState(Terrain.ExtractData(num2), true)), loadCount - 1);
									num3 = 1;
								}
								else
								{
									num2 = FlameThrowerBlock.SetLoadCount(Terrain.MakeBlockValue(num, 0, FlameThrowerBlock.SetLoadState(FlameThrowerBlock.SetSwitchState(FlameThrowerBlock.SetBulletType(Terrain.ExtractData(num2), null), false), FlameThrowerBlock.LoadState.Empty)), 0);
								}
								componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
								if (num2 != slotValue)
								{
									inventory.RemoveSlotItems(activeSlotIndex, 1);
									inventory.AddSlotItems(activeSlotIndex, num2, 1);
								}
								if (num3 > 0)
								{
									componentMiner.DamageActiveTool(1);
								}
								return true;
							}
							if (num4 > 0.5f && !FlameThrowerBlock.GetSwitchState(Terrain.ExtractData(num2)))
							{
								num2 = FlameThrowerBlock.SetLoadCount(Terrain.MakeBlockValue(num, 0, FlameThrowerBlock.SetSwitchState(Terrain.ExtractData(num2), true)), FlameThrowerBlock.GetLoadCount(num2));
								this.m_subsystemAudio.PlaySound("Audio/Items/Hammer Cock Remake", 1f, this.m_random.Float(-0.1f, 0.1f), 0f, 0f);
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
							if (num4 >= this.nextTime)
							{
								bool flag = false;
								int value = 0;
								int num6 = 0;
								float s = 0f;
								Vector3 zero = Vector3.Zero;
								FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
								FlameBulletBlock.FlameBulletType? bulletType = FlameThrowerBlock.GetBulletType(data);
								if (FlameThrowerBlock.GetSwitchState(Terrain.ExtractData(num2)))
								{
									if (loadState == FlameThrowerBlock.LoadState.Empty)
									{
										ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
										if (componentPlayer2 != null)
										{
											componentPlayer2.ComponentGui.DisplaySmallMessage(LanguageControl.GetContentWidgets("FlameThrowerWidget", "LoadAmmunition"), Color.Orange, true, false);
										}
										return true;
									}
									if (loadState == FlameThrowerBlock.LoadState.Loaded)
									{
										flag = true;
										value = Terrain.MakeBlockValue(this.m_BulletBlockIndex, 0, FlameBulletBlock.SetBulletType(0, bulletType.GetValueOrDefault()));
										num6 = 1;
										s = 40f;
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
										Vector3 vector = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
										Vector3 vector2 = Vector3.Normalize(vector + aim.Direction * 10f - vector);
										Vector3 vector3 = Vector3.Normalize(Vector3.Cross(vector2, Vector3.UnitY));
										Vector3 v2 = Vector3.Normalize(Vector3.Cross(vector2, vector3));
										for (int i = 0; i < num6; i++)
										{
											Vector3 v3 = this.m_random.Float(0f - zero.X, zero.X) * vector3 + this.m_random.Float(0f - zero.Y, zero.Y) * v2 + this.m_random.Float(0f - zero.Z, zero.Z) * vector2;
											Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + s * (vector2 + v3);
											Projectile projectile = this.m_subsystemProjectiles.FireProjectile(value, vector, velocity, Vector3.Zero, componentMiner.ComponentCreature);
											if (projectile != null)
											{
												projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
											}
										}

										// Reproducir sonido y partículas según el tipo de bala
										if (bulletType == FlameBulletBlock.FlameBulletType.Flame)
										{
											this.m_subsystemAudio.PlaySound("Audio/Flamethrower/Flamethrower Fire", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 10f, true);
											this.m_subsystemParticles.AddParticleSystem(new FlameSmokeParticleSystem(this.m_subsystemTerrain, vector + 0.3f * vector2, vector2), false);
										}
										else // Poison
										{
											this.m_subsystemAudio.PlaySound("Audio/Flamethrower/PoisonSmoke", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 8f, true);
											this.m_subsystemParticles.AddParticleSystem(new PoisonSmokeParticleSystem(this.m_subsystemTerrain, vector + 0.3f * vector2, vector2), false);
										}
									}
									this.nextTime = num4 + 0.3f;
								}
							}
							break;
						}
					case AimState.Cancelled:
						if (FlameThrowerBlock.GetSwitchState(Terrain.ExtractData(num2)))
						{
							num2 = FlameThrowerBlock.SetLoadCount(Terrain.MakeBlockValue(num, 0, FlameThrowerBlock.SetSwitchState(Terrain.ExtractData(num2), false)), FlameThrowerBlock.GetLoadCount(num2));
							this.m_subsystemAudio.PlaySound("Audio/Items/Hammer Uncock Remake", 1f, this.m_random.Float(-0.1f, 0.1f), 0f, 0f);
						}
						this.nextTime = 0f;
						this.m_aimStartTimes.Remove(componentMiner);
						break;
					case AimState.Completed:
						if (FlameThrowerBlock.GetSwitchState(Terrain.ExtractData(num2)))
						{
							int loadCount2 = FlameThrowerBlock.GetLoadCount(num2);
							if (loadCount2 > 1)
							{
								num2 = Terrain.MakeBlockValue(num, loadCount2 - 1, data);
								num3 = 1;
							}
							else
							{
								num2 = Terrain.MakeBlockValue(num, 0, FlameThrowerBlock.SetLoadState(FlameThrowerBlock.SetBulletType(Terrain.ExtractData(num2), null), FlameThrowerBlock.LoadState.Empty));
							}
							num2 = FlameThrowerBlock.SetLoadCount(Terrain.MakeBlockValue(num, 0, FlameThrowerBlock.SetSwitchState(Terrain.ExtractData(num2), false)), FlameThrowerBlock.GetLoadCount(num2));
							this.m_subsystemAudio.PlaySound("Audio/Items/Hammer Cock Remake", 1f, this.m_random.Float(-0.1f, 0.1f), 0f, 0f);
						}
						this.nextTime = 0f;
						this.m_aimStartTimes.Remove(componentMiner);
						break;
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
			return false;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int num = Terrain.ExtractContents(value);
			if (!(BlocksManager.Blocks[num] is FlameBulletBlock))
			{
				return 0;
			}
			FlameBulletBlock.FlameBulletType bulletType = FlameBulletBlock.GetBulletType(Terrain.ExtractData(value));
			int slotValue = inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			int loadCount = FlameThrowerBlock.GetLoadCount(slotValue);
			FlameBulletBlock.FlameBulletType? bulletType2 = FlameThrowerBlock.GetBulletType(data);
			if (bulletType2 == null)
			{
				return 15;
			}
			if (loadCount >= 15)
			{
				return 0;
			}
			if (bulletType2.Value != bulletType)
			{
				return 0;
			}
			return 15 - loadCount;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			FlameBulletBlock.FlameBulletType bulletType = FlameBulletBlock.GetBulletType(Terrain.ExtractData(value));
			int slotValue = inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			int loadCount = FlameThrowerBlock.GetLoadCount(slotValue);
			FlameThrowerBlock.LoadState loadState = FlameThrowerBlock.GetLoadState(data);
			if (loadState == FlameThrowerBlock.LoadState.Empty)
			{
				loadState = FlameThrowerBlock.LoadState.Loaded;
			}
			processedCount = count - processCount;
			if (loadCount != 0)
			{
				processCount += loadCount;
			}
			processedValue = value;
			if (processedCount == 0)
			{
				processedValue = 0;
			}
			inventory.RemoveSlotItems(slotIndex, 1);
			int value2 = Terrain.MakeBlockValue(this.m_FlameThrowerBlockIndex, processCount, FlameThrowerBlock.SetLoadState(FlameThrowerBlock.SetBulletType(data, new FlameBulletBlock.FlameBulletType?(bulletType)), loadState));
			inventory.AddSlotItems(slotIndex, value2, 1);
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_BulletBlockIndex = BlocksManager.GetBlockIndex<FlameBulletBlock>(false, false);
			this.m_FlameThrowerBlockIndex = BlocksManager.GetBlockIndex<FlameThrowerBlock>(false, false);
			base.Load(valuesDictionary);
		}

		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemTime m_subsystemTime;
		public SubsystemProjectiles m_subsystemProjectiles;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemAudio m_subsystemAudio;
		public Game.Random m_random = new Game.Random();
		public Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();
		public int m_BulletBlockIndex;
		public int m_FlameThrowerBlockIndex;
		public float nextTime;
		public const int MaxCount = 15;
	}
}
