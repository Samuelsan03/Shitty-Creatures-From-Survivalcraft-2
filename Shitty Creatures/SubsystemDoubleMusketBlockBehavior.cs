using System;
using System.Collections.Generic;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemDoubleMusketBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks => new[] { DoubleMusketBlock.Index };

		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemParticles m_subsystemParticles;
		private SubsystemAudio m_subsystemAudio;
		private SubsystemNoise m_subsystemNoise;
		private Random m_random = new Random();
		private Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();

		private int m_BulletBlockIndex;
		private int m_AntiTanksBulletBlockIndex;
		private int m_MusketBlockIndex;

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_BulletBlockIndex = BlocksManager.GetBlockIndex<BulletBlock>();
			m_AntiTanksBulletBlockIndex = AntiTanksBulletBlock.Index;
			m_MusketBlockIndex = DoubleMusketBlock.Index;
		}

		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			componentPlayer.ComponentGui.ModalPanelWidget = (componentPlayer.ComponentGui.ModalPanelWidget == null)
				? new DoubleMusketWidget(inventory, slotIndex)
				: null;
			return true;
		}

		public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
		{
			IInventory inventory = componentMiner.Inventory;
			if (inventory == null) return false;

			int activeSlotIndex = inventory.ActiveSlotIndex;
			if (activeSlotIndex < 0) return false;

			int slotValue = inventory.GetSlotValue(activeSlotIndex);
			int slotCount = inventory.GetSlotCount(activeSlotIndex);
			int contents = Terrain.ExtractContents(slotValue);
			int data = Terrain.ExtractData(slotValue);

			if (contents != m_MusketBlockIndex || slotCount <= 0) return false;

			int newValue = slotValue;
			int durabilityCost = 0;

			// Aim timer
			if (!m_aimStartTimes.TryGetValue(componentMiner, out double startTime))
			{
				startTime = m_subsystemTime.GameTime;
				m_aimStartTimes[componentMiner] = startTime;
			}
			float aimDuration = (float)(m_subsystemTime.GameTime - startTime);

			// Spread calculation
			float noiseTime = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
			float baseSway = componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f;
			float aimFactor = 0.2f * MathUtils.Saturate((aimDuration - 2.5f) / 6f);
			Vector3 noise = new Vector3(
				SimplexNoise.OctavedNoise(noiseTime, 2f, 3, 2f, 0.5f, false),
				SimplexNoise.OctavedNoise(noiseTime + 100f, 2f, 3, 2f, 0.5f, false),
				SimplexNoise.OctavedNoise(noiseTime + 200f, 2f, 3, 2f, 0.5f, false)
			);
			Vector3 sway = new Vector3(baseSway) + aimFactor * noise;
			aim.Direction = Vector3.Normalize(aim.Direction + sway);

			switch (state)
			{
				case AimState.InProgress:
					if (aimDuration >= 10f)
					{
						componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
						return true;
					}
					if (aimDuration > 0.5f && !DoubleMusketBlock.GetHammerState(data))
					{
						newValue = Terrain.MakeBlockValue(contents, 0, DoubleMusketBlock.SetHammerState(data, true));
						m_subsystemAudio.PlaySound("Audio/Items/Hammer Cock Remake", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
					}

					if (componentMiner.Entity.FindComponent<ComponentFirstPersonModel>() is ComponentFirstPersonModel fpModel)
					{
						componentMiner.ComponentPlayer?.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);
						fpModel.ItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
						fpModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
					}
					componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
					componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
					componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
					break;

				case AimState.Cancelled:
					if (DoubleMusketBlock.GetHammerState(data))
					{
						newValue = Terrain.MakeBlockValue(contents, 0, DoubleMusketBlock.SetHammerState(data, false));
						m_subsystemAudio.PlaySound("Audio/Items/Hammer Uncock Remake", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
					}
					m_aimStartTimes.Remove(componentMiner);
					break;

				case AimState.Completed:
					bool fired = false;
					int projectileValue = 0;
					int projectileCount = 0;
					float speed = 0f;
					Vector3 spread = Vector3.Zero;

					bool isLoaded = DoubleMusketBlock.IsLoaded(data);
					int shotsRemaining = DoubleMusketBlock.GetShotsRemaining(data);
					bool isAntiTanks = DoubleMusketBlock.IsAntiTanksBullet(data);
					BulletBlock.BulletType? bulletType = DoubleMusketBlock.GetBulletType(data);

					if (DoubleMusketBlock.GetHammerState(data) && isLoaded && shotsRemaining > 0)
					{
						fired = true;
						// Configurar proyectil según tipo
						if (isAntiTanks)
						{
							projectileValue = Terrain.MakeBlockValue(m_AntiTanksBulletBlockIndex, 0, 0);
							projectileCount = 1;
							spread = new Vector3(0.04f, 0.04f, 0f);
							speed = 180f;
						}
						else
						{
							if (bulletType == BulletBlock.BulletType.Buckshot)
							{
								projectileValue = Terrain.MakeBlockValue(m_BulletBlockIndex, 0, BulletBlock.SetBulletType(0, BulletBlock.BulletType.BuckshotBall));
								projectileCount = 8;
								spread = new Vector3(0.04f, 0.04f, 0.25f);
								speed = 80f;
							}
							else if (bulletType == BulletBlock.BulletType.BuckshotBall)
							{
								projectileValue = Terrain.MakeBlockValue(m_BulletBlockIndex, 0, BulletBlock.SetBulletType(0, BulletBlock.BulletType.BuckshotBall));
								projectileCount = 1;
								spread = new Vector3(0.06f, 0.06f, 0f);
								speed = 60f;
							}
							else if (bulletType != null)
							{
								projectileValue = Terrain.MakeBlockValue(m_BulletBlockIndex, 0, BulletBlock.SetBulletType(0, bulletType.Value));
								projectileCount = 1;
								speed = 120f;
							}
						}

						// Disparo SIN RESTRICCIÓN DE AGUA
						Vector3 muzzlePos = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition +
											componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f -
											componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
						Vector3 dirNorm = Vector3.Normalize(muzzlePos + aim.Direction * 10f - muzzlePos);
						Vector3 right = Vector3.Normalize(Vector3.Cross(dirNorm, Vector3.UnitY));
						Vector3 up = Vector3.Normalize(Vector3.Cross(dirNorm, right));

						for (int i = 0; i < projectileCount; i++)
						{
							Vector3 offset = m_random.Float(-spread.X, spread.X) * right +
											 m_random.Float(-spread.Y, spread.Y) * up +
											 m_random.Float(-spread.Z, spread.Z) * dirNorm;
							Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + speed * (dirNorm + offset);
							Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, muzzlePos, velocity, Vector3.Zero, componentMiner.ComponentCreature);
							if (projectile != null)
								projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
						}

						m_subsystemAudio.PlaySound("Audio/Items/GunShot Musket Remake", 1f, m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 10f, true);
						m_subsystemParticles.AddParticleSystem(new GunSmokeParticleSystem(m_subsystemTerrain, muzzlePos + 0.3f * dirNorm, dirNorm), false);
						m_subsystemNoise.MakeNoise(muzzlePos, 1f, 40f);
						componentMiner.ComponentCreature.ComponentBody.ApplyImpulse(-4f * dirNorm);

						// Reducir contador de disparos
						shotsRemaining--;
						if (shotsRemaining <= 0)
						{
							data = DoubleMusketBlock.SetLoaded(data, false);
							data = DoubleMusketBlock.SetShotsRemaining(data, 0);
							data = DoubleMusketBlock.SetAntiTanksBullet(data, false);
						}
						else
						{
							data = DoubleMusketBlock.SetShotsRemaining(data, shotsRemaining);
						}
						newValue = Terrain.MakeBlockValue(contents, 0, data);
						durabilityCost = 1;
					}
					else
					{
						if (DoubleMusketBlock.GetHammerState(data) && (!isLoaded || shotsRemaining == 0))
							componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(LanguageControl.Get("SubsystemDoubleMusketBlockBehavior", 0), Color.White, true, false);
					}

					// Soltar martillo
					if (DoubleMusketBlock.GetHammerState(Terrain.ExtractData(newValue)))
					{
						newValue = Terrain.MakeBlockValue(Terrain.ExtractContents(newValue), 0, DoubleMusketBlock.SetHammerState(Terrain.ExtractData(newValue), false));
						m_subsystemAudio.PlaySound("Audio/Items/Hammer Release Remake", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
					}

					m_aimStartTimes.Remove(componentMiner);
					break;
			}

			if (newValue != slotValue)
			{
				inventory.RemoveSlotItems(activeSlotIndex, 1);
				inventory.AddSlotItems(activeSlotIndex, newValue, 1);
			}
			if (durabilityCost > 0)
				componentMiner.DamageActiveTool(durabilityCost);

			return false;
		}

		// Capacidad de proceso de ítems: SOLO BALAS (hasta 2)
		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int contents = Terrain.ExtractContents(value);
			int slotValue = inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			int shotsRemaining = DoubleMusketBlock.GetShotsRemaining(data);

			// Solo acepta balas si hay menos de 2 cargadas
			if (shotsRemaining < 2 && (contents == m_BulletBlockIndex || contents == m_AntiTanksBulletBlockIndex))
				return 1;

			return 0;
		}

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;
			if (processCount != 1) return;

			int slotValue = inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			int shotsRemaining = DoubleMusketBlock.GetShotsRemaining(data);
			BulletBlock.BulletType? bulletType = DoubleMusketBlock.GetBulletType(data);
			bool isAntiTanks = DoubleMusketBlock.IsAntiTanksBullet(data);
			int ammoContents = Terrain.ExtractContents(value);

			if (shotsRemaining < 2 && (ammoContents == m_BulletBlockIndex || ammoContents == m_AntiTanksBulletBlockIndex))
			{
				shotsRemaining++;
				if (ammoContents == m_BulletBlockIndex)
				{
					bulletType = BulletBlock.GetBulletType(Terrain.ExtractData(value));
					isAntiTanks = false;
				}
				else if (ammoContents == m_AntiTanksBulletBlockIndex)
				{
					bulletType = BulletBlock.BulletType.MusketBall;
					isAntiTanks = true;
				}
			}

			int newData = DoubleMusketBlock.SetLoaded(data, shotsRemaining > 0);
			newData = DoubleMusketBlock.SetShotsRemaining(newData, shotsRemaining);
			newData = DoubleMusketBlock.SetBulletType(newData, bulletType);
			newData = DoubleMusketBlock.SetAntiTanksBullet(newData, isAntiTanks);

			processedValue = 0;
			processedCount = 0;
			inventory.RemoveSlotItems(slotIndex, 1);
			inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(m_MusketBlockIndex, 0, newData), 1);
		}
	}
}
