using System;
using System.Collections.Generic;
using System.Globalization;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemRevolverBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex(typeof(RevolverBlock), true, false)
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
			string name = "Audio/Armas/Revolver fuego";
			float num = 0.08f; // Dispersión similar al SWM500
			int slotValue = componentMiner.Inventory.GetSlotValue(componentMiner.Inventory.ActiveSlotIndex);
			bool flag = Terrain.ExtractContents(slotValue) == BlocksManager.GetBlockIndex(typeof(RevolverBlock), true, false);

			if (flag)
			{
				double gameTime;
				bool flag3 = !this.m_aimStartTimes.TryGetValue(componentMiner, out gameTime);

				if (flag3)
				{
					gameTime = this.m_subsystemTime.GameTime;
					this.m_aimStartTimes[componentMiner] = gameTime;
				}

				float num2 = (float)(this.m_subsystemTime.GameTime - gameTime);
				float num3 = (float)MathUtils.Remainder(this.m_subsystemTime.GameTime, 1000.0);

				Vector3 v = (float)((componentMiner.ComponentCreature.ComponentBody.IsSneaking ? 0.00999999977648258 : 0.0299999993294477) + 0.200000002980232 * (double)MathUtils.Saturate((num2 - 6.5f) / 40f)) * new Vector3
				{
					X = SimplexNoise.OctavedNoise(num3, 2f, 3, 2f, 0.5f, false),
					Y = SimplexNoise.OctavedNoise(num3 + 100f, 2f, 3, 2f, 0.5f, false),
					Z = SimplexNoise.OctavedNoise(num3 + 200f, 2f, 3, 2f, 0.5f, false)
				};

				if (num2 > 1f)
				{
					if (num2 < 6f)
					{
						aim.Direction.Y = aim.Direction.Y + num * 0.6f * (num2 - 1f);
					}
					else
					{
						aim.Direction.Y = aim.Direction.Y + num * 2f;
					}
				}

				aim.Direction = Vector3.Normalize(aim.Direction + v * 2f);

				switch (state)
				{
					case AimState.InProgress:
						{
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
						{
							this.m_aimStartTimes.Remove(componentMiner);
							componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
							componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
							componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;

							ComponentFirstPersonModel componentFirstPersonModel2 = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
							if (componentFirstPersonModel2 != null)
							{
								componentFirstPersonModel2.ItemOffsetOrder = Vector3.Zero;
								componentFirstPersonModel2.ItemRotationOrder = Vector3.Zero;
							}

							ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
							if (componentPlayer2 != null)
							{
								componentPlayer2.ComponentGui.DisplaySmallMessage("Recargue munición", Color.White, false, false);
							}
							break;
						}

					case AimState.Completed:
						{
							this.m_aimStartTimes.Remove(componentMiner);
							int data = Terrain.ExtractData(slotValue);
							int bulletNum = RevolverBlock.GetBulletNum(data);
							this.fire = (bulletNum > 0);

							if (this.fire)
							{
								int value = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(NuevaBala4), true, false), 0, 2);
								Vector3 vector = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
								Vector3 vector2 = Vector3.Normalize(vector + aim.Direction * 10f - vector);

								for (int i = 0; i < 1; i++)
								{
									Vector3 v2 = new Vector3(0f, this.m_random.Float(-1f * num, num), this.m_random.Float(-1f * num, num));
									this.m_subsystemProjectiles.FireProjectile(value, vector, 320f * (vector2 + v2), Vector3.Zero, componentMiner.ComponentCreature);
								}

								ComponentPlayer componentPlayer3 = componentMiner.ComponentPlayer;
								if (componentPlayer3 != null)
								{
									componentPlayer3.ComponentGui.DisplaySmallMessage((bulletNum - 1).ToString(CultureInfo.InvariantCulture), Color.White, false, false);
								}

								// Reducir balas y actualizar el item
								int newBulletNum = bulletNum - 1;
								int newData = RevolverBlock.SetBulletNum(data, newBulletNum);

								componentMiner.Inventory.RemoveSlotItems(componentMiner.Inventory.ActiveSlotIndex, 1);
								componentMiner.Inventory.AddSlotItems(componentMiner.Inventory.ActiveSlotIndex,
									Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(RevolverBlock), true, false), 0, newData), 1);

								componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);

								this.m_subsystemAudio.PlaySound(name, 1.5f, this.m_random.Float(-0.1f, 0.1f),
									componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 10f, true);

								this.m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(vector + 0.3f * vector2, vector2, 10f), false);
								this.m_subsystemNoise.MakeNoise(vector, 1f, 40f);
							}
							else
							{
								// Sonido de disparo sin balas
								this.m_subsystemAudio.PlaySound("Audio/Armas/Empty fire", 1f,
									this.m_random.Float(-0.1f, 0.1f),
									componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, true);

								// Mostrar mensaje de que necesita munición
								ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
								if (componentPlayer2 != null)
								{
									string bulletName = LanguageControl.Get("Blocks", "RevolverBulletBlock:0", "DisplayName");
									componentPlayer2.ComponentGui.DisplaySmallMessage(
										LanguageControl.Get("Messages", "NeedAmmo").Replace("{0}", bulletName),
										Color.White, true, false);
								}
							}
							componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
							componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
							componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;

							ComponentFirstPersonModel componentFirstPersonModel3 = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
							if (componentFirstPersonModel3 != null)
							{
								componentFirstPersonModel3.ItemOffsetOrder = Vector3.Zero;
								componentFirstPersonModel3.ItemRotationOrder = Vector3.Zero;
							}
							break;
						}
				}
			}
			return false;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int slotData = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
			int bulletNum = RevolverBlock.GetBulletNum(slotData);

			if (value == BlocksManager.GetBlockIndex(typeof(RevolverBulletBlock), true, false) && bulletNum < 6)
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
			int bulletNum = RevolverBlock.GetBulletNum(slotData);

			if (bulletNum < 6)
			{
				processedValue = 0;
				processedCount = 0;
				int newData = RevolverBlock.SetBulletNum(slotData, 6);
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex,
					Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(RevolverBlock), true, false), 0, newData), 1);

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
