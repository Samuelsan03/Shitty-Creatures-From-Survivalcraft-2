using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAK48Behavior : SubsystemBlockBehavior
	{
		private const int MaxBullets = 60; // Capacidad mejorada

		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex(typeof(AK48Block), true, false)
				};
			}
		}

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemNoise m_subsystemNoise;
		private SubsystemGameInfo m_subsystemGameInfo;
		private Game.Random m_random = new Game.Random();
		private Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
		}

		public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
		{
			Vector3 direction = aim.Direction;
			IInventory inventory = componentMiner.Inventory;
			if (inventory != null)
			{
				int activeSlotIndex = inventory.ActiveSlotIndex;
				if (activeSlotIndex >= 0)
				{
					int slotValue = inventory.GetSlotValue(activeSlotIndex);
					int slotCount = inventory.GetSlotCount(activeSlotIndex);
					int contents = Terrain.ExtractContents(slotValue);
					int newValue = slotValue;

					if (contents == BlocksManager.GetBlockIndex(typeof(AK48Block), true, false) && slotCount > 0)
					{
						double aimStartTime;
						if (!m_aimStartTimes.TryGetValue(componentMiner, out aimStartTime))
						{
							aimStartTime = m_subsystemTime.GameTime;
							m_aimStartTimes[componentMiner] = aimStartTime;
						}

						float aimDuration = (float)(m_subsystemTime.GameTime - aimStartTime);
						float noiseTime = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);

						Vector3 spread = (float)((componentMiner.ComponentCreature.ComponentBody.IsSneaking ? 0.01 : 0.03) + 0.2 * MathUtils.Saturate((aimDuration - 6.5f) / 40f)) * new Vector3
						{
							X = SimplexNoise.OctavedNoise(noiseTime, 2f, 3, 2f, 0.5f, false),
							Y = SimplexNoise.OctavedNoise(noiseTime + 100f, 2f, 3, 2f, 0.5f, false),
							Z = SimplexNoise.OctavedNoise(noiseTime + 200f, 2f, 3, 2f, 0.5f, false)
						};

						if (aimDuration > 1f)
						{
							if (0.2f * aimDuration < 1.2f)
								direction.Y += 0.1f * (aimDuration - 1f);
							else
								direction.Y += 0.3f;
						}

						direction = Vector3.Normalize(direction + spread * 2f);

						switch (state)
						{
							case AimState.InProgress:
								{
									ComponentFirstPersonModel firstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									ComponentPlayer player = componentMiner.ComponentPlayer;

									if (firstPersonModel != null)
									{
										if (player != null)
											player.ComponentAimingSights.ShowAimingSights(aim.Position, direction);

										firstPersonModel.ItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
										firstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
									}

									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);

									int bulletNum = AK48Block.GetBulletNum(Terrain.ExtractData(slotValue));
									// Cadencia de disparo: 0.17s (igual que el AK original)
									if (m_subsystemTime.PeriodicGameTimeEvent(0.17, 0.0) && bulletNum > 0)
									{
										Vector3 eyePos = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
										Vector3 right = componentMiner.ComponentCreature.ComponentBody.Matrix.Right;
										Vector3 up = componentMiner.ComponentCreature.ComponentBody.Matrix.Up;
										Vector3 firePos = eyePos + right * 0.3f - up * 0.2f;
										Vector3 dirNorm = Vector3.Normalize(firePos + direction * 10f - firePos);
										Vector3 rightVec = Vector3.Normalize(Vector3.Cross(dirNorm, Vector3.UnitY));
										Vector3 upVec = Vector3.Normalize(Vector3.Cross(dirNorm, rightVec));

										int projectileCount = 2;
										Vector3 spreadRange = new Vector3(0.01f, 0.01f, 0.05f);
										for (int i = 0; i < projectileCount; i++)
										{
											// Proyectil exclusivo: NuevaBala6
											int projectileValue = Terrain.MakeBlockValue(
												BlocksManager.GetBlockIndex(typeof(NuevaBala6), true, false), 0, 2);
											Vector3 randomSpread = m_random.Float(-spreadRange.X, spreadRange.X) * rightVec +
																   m_random.Float(-spreadRange.Y, spreadRange.Y) * upVec +
																   m_random.Float(-spreadRange.Z, spreadRange.Z) * dirNorm;
											m_subsystemProjectiles.FireProjectile(projectileValue, firePos,
												280f * (dirNorm + randomSpread), Vector3.Zero, componentMiner.ComponentCreature);
										}

										// Sonido de disparo personalizado
										m_subsystemAudio.PlaySound("Audio/Armas/AK48 fire", 1f,
											m_random.Float(-0.0001f, 0.00001f),
											componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 10f, true);

										Vector3 viewDir = Vector3.Normalize(componentMiner.ComponentPlayer.GameWidget.ActiveCamera.ViewDirection);
										m_subsystemParticles.AddParticleSystem(
											new GunFireParticleSystem(m_subsystemTerrain, firePos + 1.3f * viewDir, viewDir), false);
										m_subsystemNoise.MakeNoise(firePos, 1f, 40f);

										int newBulletNum = bulletNum - 1;
										newValue = Terrain.MakeBlockValue(
											BlocksManager.GetBlockIndex(typeof(AK48Block), true, false), 0,
											AK48Block.SetBulletNum(newBulletNum));

										if (player != null)
											player.ComponentGui.DisplaySmallMessage(newBulletNum.ToString(), Color.White, true, false);

										if (firstPersonModel != null)
											firstPersonModel.ItemRotationOrder = new Vector3(-0.9f, 0f, 0f);
									}
									else if (bulletNum <= 0 && m_subsystemTime.PeriodicGameTimeEvent(0.5, 0.0))
									{
										m_subsystemAudio.PlaySound("Audio/Armas/Empty fire", 1f,
											m_random.Float(-0.1f, 0.1f),
											componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, true);

										ComponentPlayer playerComp = componentMiner.ComponentPlayer;
										if (playerComp != null)
										{
											string bulletName = LanguageControl.Get("Blocks", "AK48BulletBlock:0", "DisplayName");
											playerComp.ComponentGui.DisplaySmallMessage(
												LanguageControl.Get("Messages", "NeedAmmo").Replace("{0}", bulletName),
												Color.White, true, false);
										}
									}
									break;
								}

							case AimState.Cancelled:
							case AimState.Completed:
								{
									m_aimStartTimes.Remove(componentMiner);
									if (newValue != slotValue)
									{
										inventory.RemoveSlotItems(activeSlotIndex, 1);
										inventory.AddSlotItems(activeSlotIndex, newValue, 1);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;

									ComponentFirstPersonModel fpModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									if (fpModel != null)
									{
										fpModel.ItemOffsetOrder = Vector3.Zero;
										fpModel.ItemRotationOrder = Vector3.Zero;
									}
									break;
								}
						}

						if (state == AimState.InProgress && newValue != slotValue)
						{
							inventory.RemoveSlotItems(activeSlotIndex, 1);
							inventory.AddSlotItems(activeSlotIndex, newValue, 1);
						}
					}
				}
			}
			return false;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			if (value == BlocksManager.GetBlockIndex(typeof(AK48BulletBlock), true, false))
			{
				int currentBullets = AK48Block.GetBulletNum(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));
				if (currentBullets < MaxBullets)
					return 1;
			}
			return 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;

			int currentBullets = AK48Block.GetBulletNum(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));
			if (currentBullets < MaxBullets)
			{
				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex,
					Terrain.MakeBlockValue(
						BlocksManager.GetBlockIndex(typeof(AK48Block), true, false), 0,
						AK48Block.SetBulletNum(MaxBullets)),
					1);

				// Reproducir sonido de recarga
				var subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
				if (subsystemPlayers != null && m_subsystemAudio != null)
				{
					foreach (var player in subsystemPlayers.ComponentPlayers)
					{
						if (player != null && player.ComponentMiner != null && player.ComponentMiner.Inventory == inventory)
						{
							m_subsystemAudio.PlaySound("Audio/Armas/reload", 1f,
								m_random.Float(-0.1f, 0.1f),
								player.ComponentCreatureModel.EyePosition, 5f, true);
							break;
						}
					}
				}
			}
		}
	}
}
