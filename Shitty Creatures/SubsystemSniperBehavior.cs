using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemSniperBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex(typeof(SniperBlock), true, false)
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
			Vector3 position = aim.Position;
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
					bool flag3 = num == BlocksManager.GetBlockIndex(typeof(SniperBlock), true, false) && slotCount > 0;
					if (flag3)
					{
						bool flag4 = this.m_subsystemTerrain.Raycast(position, position + vector * 1f, true, true, null) != null;
						if (flag4)
						{
							ComponentPlayer componentPlayer = componentMiner?.ComponentPlayer;
							bool flag5 = componentPlayer != null;
							if (flag5)
							{
								// Usar LanguageControl para obtener el mensaje traducido
								string message = LanguageControl.Get("Messages", "TooCloseToAim");
								componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.White, true, true);
							}
							return false;
						}
						IScalableCamera scalableCamera = componentMiner.ComponentPlayer.GameWidget.ActiveCamera as IScalableCamera;
						bool flag6 = scalableCamera != null;
						if (flag6)
						{
							vector = Vector3.Normalize(scalableCamera.GetDirection());
						}
						double gameTime;
						bool flag7 = !this.m_aimStartTimes.TryGetValue(componentMiner, out gameTime);
						if (flag7)
						{
							gameTime = this.m_subsystemTime.GameTime;
							this.m_aimStartTimes[componentMiner] = gameTime;
						}
						float num2 = (float)(this.m_subsystemTime.GameTime - gameTime);
						float num3 = (float)MathUtils.Remainder(this.m_subsystemTime.GameTime, 1000.0);
						Vector3 v = (float)((componentMiner.ComponentCreature.ComponentBody.IsSneaking ? 0.005 : 0.015) + 0.100000002980232 * (double)MathUtils.Saturate((num2 - 6.5f) / 40f)) * new Vector3
						{
							X = SimplexNoise.OctavedNoise(num3, 1.5f, 2, 1.5f, 0.3f, false),
							Y = SimplexNoise.OctavedNoise(num3 + 100f, 1.5f, 2, 1.5f, 0.3f, false),
							Z = SimplexNoise.OctavedNoise(num3 + 200f, 1.5f, 2, 1.5f, 0.3f, false)
						};
						vector = Vector3.Normalize(vector + v * 0.5f);
						switch (state)
						{
							case AimState.InProgress:
								{
									bool flag8 = num2 >= 60f;
									if (flag8)
									{
										componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
										return true;
									}
									GameWidget gameWidget = componentMiner.ComponentPlayer.GameWidget;
									bool flag9 = num2 > 0.2f && !(gameWidget.ActiveCamera is IScalableCamera);
									if (flag9)
									{
										gameWidget.ActiveCamera = new SniperScopeCamera(gameWidget);
									}
									ComponentFirstPersonModel componentFirstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									bool flag10 = componentFirstPersonModel != null;
									if (flag10)
									{
										ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
										bool flag11 = componentPlayer2 != null;
										if (flag11)
										{
											componentPlayer2.ComponentAimingSights.ShowAimingSights(aim.Position, vector);
										}
										componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.25f, 0.12f, 0.1f);
										componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.5f, 0f, 0f);
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.2f;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.1f, -0.06f, 0.08f);
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.5f, 0f, 0f);
									break;
								}
							case AimState.Cancelled:
								{
									GameWidget gameWidget2 = componentMiner.ComponentPlayer.GameWidget;
									bool flag12 = gameWidget2.ActiveCamera is IScalableCamera;
									if (flag12)
									{
										gameWidget2.ActiveCamera = gameWidget2.FindCamera<FppCamera>(true);
									}
									this.m_aimStartTimes.Remove(componentMiner);
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
									ComponentFirstPersonModel componentFirstPersonModel2 = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									bool flag13 = componentFirstPersonModel2 != null;
									if (flag13)
									{
										componentFirstPersonModel2.ItemOffsetOrder = Vector3.Zero;
										componentFirstPersonModel2.ItemRotationOrder = Vector3.Zero;
									}
									ComponentPlayer componentPlayer3 = componentMiner.ComponentPlayer;
									bool flag14 = componentPlayer3 != null;
									if (flag14)
									{
										componentPlayer3.ComponentGui.DisplaySmallMessage("Apuntado cancelado", Color.White, false, false);
									}
									break;
								}
							case AimState.Completed:
								{
									this.m_aimStartTimes.Remove(componentMiner);
									int bulletNum = SniperBlock.GetBulletNum(Terrain.ExtractData(slotValue));
									this.fire = (bulletNum > 0);
									bool flag15 = this.fire;
									if (flag15)
									{
										GameWidget gameWidget3 = componentMiner.ComponentPlayer.GameWidget;
										bool flag16 = gameWidget3.ActiveCamera is IScalableCamera;
										if (flag16)
										{
											gameWidget3.ActiveCamera = gameWidget3.FindCamera<FppCamera>(true);
										}
										int value = Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(NuevaBala6), true, false), 0, 180);
										Vector3 vector2 = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.35f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.15f;
										Vector3 v2 = vector;
										IScalableCamera scalableCamera2 = gameWidget3.ActiveCamera as IScalableCamera;
										bool flag17 = scalableCamera2 != null;
										if (flag17)
										{
											v2 = Vector3.Normalize(scalableCamera2.GetDirection());
										}
										Vector3 vector3 = Vector3.Normalize(vector2 + v2 * 10f - vector2);
										float num4 = 0.001f;
										Vector3 v3 = new Vector3(this.m_random.Float(-num4, num4), this.m_random.Float(-num4, num4), this.m_random.Float(-num4, num4));
										this.m_subsystemProjectiles.FireProjectile(value, vector2, 450f * (vector3 + v3), Vector3.Zero, componentMiner.ComponentCreature);
										ComponentPlayer componentPlayer4 = componentMiner.ComponentPlayer;
										bool flag18 = componentPlayer4 != null;
										if (flag18)
										{
											componentPlayer4.ComponentGui.DisplaySmallMessage((bulletNum - 1).ToString(CultureInfo.InvariantCulture), Color.White, false, false);
										}
										componentMiner.Inventory.RemoveSlotItems(componentMiner.Inventory.ActiveSlotIndex, 1);
										componentMiner.Inventory.AddSlotItems(componentMiner.Inventory.ActiveSlotIndex, Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(SniperBlock), true, false), 0, SniperBlock.SetBulletNum(SniperBlock.GetBulletNum(Terrain.ExtractData(slotValue)) - 1)), 1);
										this.m_subsystemAudio.PlaySound("Audio/Armas/Sniper fuego", 1.8f, this.m_random.Float(-0.05f, 0.05f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 15f, true);

										// Añadir partículas de fuego
										this.m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(this.m_subsystemTerrain, vector2 + 0.4f * vector3, vector3), false);

										this.m_subsystemNoise.MakeNoise(vector2, 1.5f, 80f);
										componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-2f, 0f, 0f);
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
											string bulletName = LanguageControl.Get("Blocks", "SniperBullet:0", "DisplayName");
											componentPlayer2.ComponentGui.DisplaySmallMessage(
												LanguageControl.Get("Messages", "NeedAmmo").Replace("{0}", bulletName),
												Color.White, true, false);
										}

										// CORRECCIÓN: Restaurar la cámara a FppCamera cuando no hay balas
										GameWidget gameWidget3 = componentMiner.ComponentPlayer.GameWidget;
										bool flag16 = gameWidget3.ActiveCamera is IScalableCamera;
										if (flag16)
										{
											gameWidget3.ActiveCamera = gameWidget3.FindCamera<FppCamera>(true);
										}
									}
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
									componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
									ComponentFirstPersonModel componentFirstPersonModel3 = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
									bool flag19 = componentFirstPersonModel3 != null;
									if (flag19)
									{
										componentFirstPersonModel3.ItemOffsetOrder = Vector3.Zero;
										componentFirstPersonModel3.ItemRotationOrder = Vector3.Zero;
									}
									break;
								}
						}
					}
				}
			}
			return false;
		}

		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			bool flag = value == BlocksManager.GetBlockIndex(typeof(SniperBullet), true, false) && SniperBlock.GetBulletNum(Terrain.ExtractData(inventory.GetSlotValue(slotIndex))) < 1;
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

		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			processedValue = value;
			processedCount = count;
			int bulletNum = SniperBlock.GetBulletNum(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));
			bool flag = bulletNum < 1;
			bool flag2 = flag;
			if (flag2)
			{
				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(SniperBlock), true, false), 0, SniperBlock.SetBulletNum(1)), 1);

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
