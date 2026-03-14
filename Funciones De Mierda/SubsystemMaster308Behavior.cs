using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemMaster308Behavior : SubsystemBlockBehavior
	{
		private const int MaxCapacity = 8; // Cargador de 8 cartuchos

		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex(typeof(Master308Block), true, false)
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

					if (contents == BlocksManager.GetBlockIndex(typeof(Master308Block), true, false) && slotCount > 0)
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

									int bulletNum = Master308Block.GetBulletNum(Terrain.ExtractData(slotValue));
									// Cadencia semiautomática: 0.4 segundos
									if (m_subsystemTime.PeriodicGameTimeEvent(0.48, 0.0) && bulletNum > 0)
									{
										Vector3 eyePos = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
										Vector3 right = componentMiner.ComponentCreature.ComponentBody.Matrix.Right;
										Vector3 up = componentMiner.ComponentCreature.ComponentBody.Matrix.Up;
										Vector3 firePos = eyePos + right * 0.3f - up * 0.2f;
										Vector3 dirNorm = Vector3.Normalize(firePos + direction * 10f - firePos);

										// Un solo proyectil, sin dispersión
										int projectileValue = Terrain.MakeBlockValue(
											BlocksManager.GetBlockIndex(typeof(NuevaBala4), true, false), 0, 0);
										m_subsystemProjectiles.FireProjectile(projectileValue, firePos,
											300f * dirNorm, Vector3.Zero, componentMiner.ComponentCreature);

										// Sonido personalizado
										m_subsystemAudio.PlaySound("Audio/Armas/308 Master fire", 1f,
											m_random.Float(-0.1f, 0.1f),
											componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 15f, true);

										Vector3 viewDir = Vector3.Normalize(componentMiner.ComponentPlayer.GameWidget.ActiveCamera.ViewDirection);
										m_subsystemParticles.AddParticleSystem(
											new GunFireParticleSystem(m_subsystemTerrain, firePos + 1.3f * viewDir, viewDir), false);
										m_subsystemNoise.MakeNoise(firePos, 1.5f, 50f);

										int newBulletNum = bulletNum - 1;
										newValue = Terrain.MakeBlockValue(
											BlocksManager.GetBlockIndex(typeof(Master308Block), true, false), 0,
											Master308Block.SetBulletNum(newBulletNum));

										if (player != null)
											player.ComponentGui.DisplaySmallMessage(newBulletNum.ToString(), Color.White, true, false);

										if (firstPersonModel != null)
											firstPersonModel.ItemRotationOrder = new Vector3(-0.85f, 0f, 0f);
									}
									else if (bulletNum <= 0 && m_subsystemTime.PeriodicGameTimeEvent(0.5, 0.0))
									{
										m_subsystemAudio.PlaySound("Audio/Armas/Empty fire", 1f,
											m_random.Float(-0.1f, 0.1f),
											componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, true);

										ComponentPlayer playerComp = componentMiner.ComponentPlayer;
										if (playerComp != null)
										{
											string bulletName = LanguageControl.Get("Blocks", "Master308Bullet:0", "DisplayName");
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
			int master308BlockIndex = BlocksManager.GetBlockIndex(typeof(Master308Block), true, false);
			int master308BulletIndex = BlocksManager.GetBlockIndex(typeof(Master308BulletBlock), true, false);

			if (value == master308BulletIndex)
			{
				int slotValue = inventory.GetSlotValue(slotIndex);
				int slotContents = Terrain.ExtractContents(slotValue);

				if (slotContents == master308BlockIndex)
				{
					int data = Terrain.ExtractData(slotValue);
					int bulletNum = Master308Block.GetBulletNum(data);
					return (bulletNum < MaxCapacity) ? 1 : 0;
				}
			}
			return 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;

			int master308BlockIndex = BlocksManager.GetBlockIndex(typeof(Master308Block), true, false);
			int master308BulletIndex = BlocksManager.GetBlockIndex(typeof(Master308BulletBlock), true, false);

			if (value == master308BulletIndex)
			{
				int slotValue = inventory.GetSlotValue(slotIndex);
				int slotContents = Terrain.ExtractContents(slotValue);

				if (slotContents == master308BlockIndex)
				{
					int data = Terrain.ExtractData(slotValue);
					int bulletNum = Master308Block.GetBulletNum(data);

					if (bulletNum < MaxCapacity)
					{
						processedValue = 0;
						processedCount = 0;
						inventory.RemoveSlotItems(slotIndex, 1);
						int newData = Master308Block.SetBulletNum(MaxCapacity);
						int newValue = Terrain.MakeBlockValue(master308BlockIndex, 0, newData);
						inventory.AddSlotItems(slotIndex, newValue, 1);

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
	}
}
