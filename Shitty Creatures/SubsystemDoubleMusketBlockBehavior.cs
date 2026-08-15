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

		private int m_AntiTanksBulletBlockIndex;
		private int m_DoubleMusketBlockIndex;

		public static string fName = "SubsystemDoubleMusketBlockBehavior";

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true);
			m_AntiTanksBulletBlockIndex = AntiTanksBulletBlock.Index;
			m_DoubleMusketBlockIndex = DoubleMusketBlock.Index;
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

			if (contents != m_DoubleMusketBlockIndex || slotCount <= 0) return false;

			int newValue = slotValue;
			int durabilityCost = 0;

			double gameTime;
			if (!m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
			{
				gameTime = m_subsystemTime.GameTime;
				m_aimStartTimes[componentMiner] = gameTime;
			}
			float aimDuration = (float)(m_subsystemTime.GameTime - gameTime);

			float noiseTime = (float)MathUtils.Remainder(m_subsystemTime.GameTime, 1000.0);
			Vector3 v = ((componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.2f * MathUtils.Saturate((aimDuration - 2.5f) / 6f)) * new Vector3
			{
				X = SimplexNoise.OctavedNoise(noiseTime, 2f, 3, 2f, 0.5f, false),
				Y = SimplexNoise.OctavedNoise(noiseTime + 100f, 2f, 3, 2f, 0.5f, false),
				Z = SimplexNoise.OctavedNoise(noiseTime + 200f, 2f, 3, 2f, 0.5f, false)
			};
			aim.Direction = Vector3.Normalize(aim.Direction + v);

			switch (state)
			{
				case AimState.InProgress:
					{
						if (aimDuration >= 10f)
						{
							componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
							return true;
						}
						if (aimDuration > 0.5f && !DoubleMusketBlock.GetHammerState(Terrain.ExtractData(newValue)))
						{
							newValue = Terrain.MakeBlockValue(contents, 0, DoubleMusketBlock.SetHammerState(Terrain.ExtractData(newValue), true));
							m_subsystemAudio.PlaySound("Audio/Items/Hammer Cock Remake", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
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
					if (DoubleMusketBlock.GetHammerState(Terrain.ExtractData(newValue)))
					{
						newValue = Terrain.MakeBlockValue(contents, 0, DoubleMusketBlock.SetHammerState(Terrain.ExtractData(newValue), false));
						m_subsystemAudio.PlaySound("Audio/Items/Hammer Uncock Remake", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
					}
					m_aimStartTimes.Remove(componentMiner);
					break;
				case AimState.Completed:
					{
						bool fired = false;
						int projectileValue = 0;
						int projectileCount = 0;
						float projectileSpeed = 0f;
						Vector3 projectileSpread = Vector3.Zero;

						bool isLoaded = DoubleMusketBlock.IsLoaded(data);
						int shotsRemaining = DoubleMusketBlock.GetShotsRemaining(data);

						if (DoubleMusketBlock.GetHammerState(Terrain.ExtractData(newValue)))
						{
							if (!isLoaded || shotsRemaining <= 0)
							{
								ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
								if (componentPlayer2 != null)
								{
									componentPlayer2.ComponentGui.DisplaySmallMessage(LanguageControl.Get(fName, 0), Color.White, true, false);
								}
							}
							else
							{
								fired = true;
								projectileValue = Terrain.MakeBlockValue(m_AntiTanksBulletBlockIndex, 0, 0);
								projectileCount = 1;
								projectileSpeed = 180f;
								projectileSpread = new Vector3(0.04f, 0.04f, 0f);
							}
						}

						if (fired)
						{
							Vector3 muzzlePos = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition +
												componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f -
												componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
							Vector3 dirNorm = Vector3.Normalize(aim.Direction);
							Vector3 right = Vector3.Normalize(Vector3.Cross(dirNorm, Vector3.UnitY));
							Vector3 up = Vector3.Normalize(Vector3.Cross(dirNorm, right));

							for (int i = 0; i < projectileCount; i++)
							{
								Vector3 offset = m_random.Float(-projectileSpread.X, projectileSpread.X) * right +
												 m_random.Float(-projectileSpread.Y, projectileSpread.Y) * up +
												 m_random.Float(-projectileSpread.Z, projectileSpread.Z) * dirNorm;
								Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + projectileSpeed * (dirNorm + offset);
								Projectile projectile = m_subsystemProjectiles.FireProjectile(projectileValue, muzzlePos, velocity, Vector3.Zero, componentMiner.ComponentCreature);
								if (projectile != null)
								{
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
								}
							}
							m_subsystemAudio.PlaySound("Audio/Items/GunShot Musket Remake", 1f, m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 10f, true);
							m_subsystemParticles.AddParticleSystem(new GunSmokeParticleSystem(m_subsystemTerrain, muzzlePos + 0.3f * dirNorm, dirNorm), false);
							m_subsystemNoise.MakeNoise(muzzlePos, 1f, 40f);

							shotsRemaining--;
							int newData = data;
							if (shotsRemaining <= 0)
							{
								newData = DoubleMusketBlock.SetLoaded(newData, false);
								newData = DoubleMusketBlock.SetShotsRemaining(newData, 0);
								newData = DoubleMusketBlock.SetAntiTanksBullet(newData, false);
							}
							else
							{
								newData = DoubleMusketBlock.SetShotsRemaining(newData, shotsRemaining);
							}
							newValue = Terrain.MakeBlockValue(Terrain.ExtractContents(newValue), 0, newData);
							durabilityCost = 1;
						}

						if (DoubleMusketBlock.GetHammerState(Terrain.ExtractData(newValue)))
						{
							newValue = Terrain.MakeBlockValue(Terrain.ExtractContents(newValue), 0, DoubleMusketBlock.SetHammerState(Terrain.ExtractData(newValue), false));
							m_subsystemAudio.PlaySound("Audio/Items/Hammer Release Remake", 1f, m_random.Float(-0.1f, 0.1f), 0f, 0f);
						}
						m_aimStartTimes.Remove(componentMiner);
						break;
					}
			}

			if (newValue != slotValue)
			{
				inventory.RemoveSlotItems(activeSlotIndex, 1);
				inventory.AddSlotItems(activeSlotIndex, newValue, 1);
			}
			if (durabilityCost > 0)
			{
				componentMiner.DamageActiveTool(durabilityCost);
			}
			return false;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int contents = Terrain.ExtractContents(value);
			int slotValue = inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			int shotsRemaining = DoubleMusketBlock.GetShotsRemaining(data);

			// Solo permite cargar cuando está completamente vacío con 1 bala anti-tanque
			if (shotsRemaining == 0 && contents == m_AntiTanksBulletBlockIndex)
			{
				return 1;
			}
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
			int ammoContents = Terrain.ExtractContents(value);

			// Al colocar 1 bala anti-tanque, carga 2 disparos automáticamente
			if (shotsRemaining == 0 && ammoContents == m_AntiTanksBulletBlockIndex)
			{
				int newData = DoubleMusketBlock.SetLoaded(data, true);
				newData = DoubleMusketBlock.SetShotsRemaining(newData, 2);
				newData = DoubleMusketBlock.SetAntiTanksBullet(newData, true);

				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(m_DoubleMusketBlockIndex, 0, newData), 1);
			}
		}
	}
}
