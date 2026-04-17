using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemM249Behavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex(typeof(M249Block), true, false)
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
					bool flag3 = num == BlocksManager.GetBlockIndex(typeof(M249Block), true, false) && slotCount > 0;
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
						Vector3 v = (float)((componentMiner.ComponentCreature.ComponentBody.IsSneaking ? 0.01 : 0.04) + 0.25 * (double)MathUtils.Saturate((num3 - 4f) / 30f)) * new Vector3
						{
							X = SimplexNoise.OctavedNoise(num4, 2f, 3, 1.5f, 0.4f, false),
							Y = SimplexNoise.OctavedNoise(num4 + 100f, 2f, 3, 1.5f, 0.4f, false),
							Z = SimplexNoise.OctavedNoise(num4 + 200f, 2f, 3, 1.5f, 0.4f, false)
						};
						bool flag5 = num3 > 1f;
						if (flag5)
						{
							bool flag6 = num3 < 4f;
							if (flag6)
							{
								vector.Y += 0.015f * (num3 - 1f);
							}
							else
							{
								vector.Y += 0.045f;
							}
						}
						vector = Vector3.Normalize(vector + v * 1.5f);
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
										componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.18f, 0.12f, 0.06f);
										componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.5f, 0f, 0f);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.2f;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.06f, -0.06f, 0.05f);
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.4f, 0f, 0f);
									int bulletNum = M249Block.GetBulletNum(Terrain.ExtractData(slotValue));
									bool flag9 = bulletNum > 0;
									// M249 tiene cadencia de 750-1000 RPM (0.08s por disparo)
									bool flag10 = this.m_subsystemTime.PeriodicGameTimeEvent(0.08, 0.0) && flag9;
									if (flag10)
									{
										Vector3 vector2 = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.25f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.15f;
										Vector3 vector3 = Vector3.Normalize(vector2 + vector * 10f - vector2);
										Vector3 vector4 = Vector3.Normalize(Vector3.Cross(vector3, Vector3.UnitY));
										Vector3 v2 = Vector3.Normalize(Vector3.Cross(vector3, vector4));

										// M249 dispara balas individuales rápidas
										int value = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(NuevaBala5), true, false), 0, 2);
										Vector3 v3 = this.m_random.Float(-0.01f, 0.01f) * vector4 + this.m_random.Float(-0.01f, 0.01f) * v2;
										this.m_subsystemProjectiles.FireProjectile(value, vector2, 400f * (vector3 + v3), Vector3.Zero, componentMiner.ComponentCreature);

										this.m_subsystemAudio.PlaySound("Audio/Armas/M249 fuego", 1.5f, this.m_random.Float(-0.05f, 0.05f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 15f, true);
										Vector3 vector6 = Vector3.Normalize(componentMiner.ComponentPlayer.GameWidget.ActiveCamera.ViewDirection);
										this.m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(this.m_subsystemTerrain, vector2 + 1.2f * vector6, vector6), false);
										this.m_subsystemNoise.MakeNoise(vector2, 1.5f, 50f);
										int bulletNum2 = bulletNum - 1;
										num2 = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(M249Block), true, false), 0, M249Block.SetBulletNum(Terrain.ExtractData(slotValue), bulletNum2));
										ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
										bool flag11 = componentPlayer2 != null;
										if (flag11)
										{
											componentPlayer2.ComponentGui.DisplaySmallMessage(bulletNum2.ToString(), Color.White, true, false);
										}
										bool flag12 = componentFirstPersonModel != null;
										if (flag12)
										{
											componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
										}
									}
									else
									{
										bool flag13 = !flag9 && this.m_subsystemTime.PeriodicGameTimeEvent(0.5, 0.0);
										if (flag13)
										{
											// Sonido de disparo sin balas
											this.m_subsystemAudio.PlaySound("Audio/Armas/Empty fire", 1f,
												this.m_random.Float(-0.1f, 0.1f),
												componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, true);

											// Mostrar mensaje de que necesita munición
											ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
											if (componentPlayer2 != null)
											{
												string bulletName = LanguageControl.Get("Blocks", "M249BulletBlock:0", "DisplayName");
												componentPlayer2.ComponentGui.DisplaySmallMessage(
													LanguageControl.Get("Messages", "NeedAmmo").Replace("{0}", bulletName),
													Color.White, true, false);
											}
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
			int slotData = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
			int bulletNum = M249Block.GetBulletNum(slotData);

			if (value == BlocksManager.GetBlockIndex(typeof(M249BulletBlock), true, false) && bulletNum < 100)
			{
				return 1;
			}

			return 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;

			int slotData = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
			int bulletNum = M249Block.GetBulletNum(slotData);

			if (bulletNum < 100)
			{
				processedValue = 0;
				processedCount = 0;
				int newData = M249Block.SetBulletNum(slotData, 100);
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex,
					Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(M249Block), true, false), 0, newData), 1);

				// Reproducir sonido de recarga
				var subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
				if (subsystemPlayers != null && this.m_subsystemAudio != null)
				{
					// Buscar entre todos los jugadores cuál tiene este inventario
					for (int i = 0; i < subsystemPlayers.ComponentPlayers.Count; i++)
					{
						var componentPlayer = subsystemPlayers.ComponentPlayers[i];
						if (componentPlayer != null && componentPlayer.ComponentMiner != null &&
							componentPlayer.ComponentMiner.Inventory == inventory)
						{
							Vector3 position = componentPlayer.ComponentCreatureModel.EyePosition;
							this.m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f,
								this.m_random.Float(-0.1f, 0.1f), position, 5f, true);
							break;
						}
					}
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
