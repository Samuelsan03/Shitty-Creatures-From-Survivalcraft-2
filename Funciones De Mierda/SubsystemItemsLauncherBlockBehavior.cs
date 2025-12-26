using System;
using System.Collections.Generic;
using Engine;
using Game;
using TemplatesDatabase;

namespace Game
{

	public class SubsystemItemsLauncherBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
				ItemsLauncherBlock.Index
				};
			}
		}

		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			componentPlayer.ComponentGui.ModalPanelWidget = new AutoCannonWidget(componentPlayer, slotIndex);
			return true;
		}

		public override bool OnAim(Ray3 aim, ComponentMiner miner, AimState state)
		{
			bool flag = miner.Inventory == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				int slotValue = miner.Inventory.GetSlotValue(miner.Inventory.ActiveSlotIndex);
				bool flag2 = Terrain.ExtractContents(slotValue) != ItemsLauncherBlock.Index;
				if (flag2)
				{
					result = false;
				}
				else
				{
					int data = Terrain.ExtractData(slotValue);
					int num = ItemsLauncherBlock.GetRateLevel(data);
					bool flag3 = num == 0;
					if (flag3)
					{
						num = 2;
					}
					switch (state)
					{
						case 0:
							{
								ComponentFirstPersonModel componentFirstPersonModel = miner.Entity.FindComponent<ComponentFirstPersonModel>();
								bool flag4 = componentFirstPersonModel != null;
								if (flag4)
								{
									ComponentPlayer componentPlayer = miner.ComponentPlayer;
									if (componentPlayer != null)
									{
										componentPlayer.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);
									}
									componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.21f, 0.15f, 0.08f);
									componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
								}
								miner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.4f;
								miner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.08f, 0.07f);
								miner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.7f, 0f, 0f);
								bool flag5 = num > 1;
								if (flag5)
								{
									float num2 = SubsystemItemsLauncherBlockBehavior.m_rateValues[num - 1];
									double gameTime = this.m_subsystemTime.GameTime;
									double num3;
									bool flag6 = !this.m_nextFireTimes.TryGetValue(miner, out num3);
									if (flag6)
									{
										num3 = gameTime + 0.2;
										this.m_nextFireTimes[miner] = num3;
									}
									bool flag7 = gameTime >= num3;
									if (flag7)
									{
										this.Fire(miner, aim);
										this.m_nextFireTimes[miner] = gameTime + 1.0 / (double)num2;
									}
								}
								break;
							}
						case (AimState)1:
							this.m_nextFireTimes.Remove(miner);
							break;
						case (AimState)2:
							{
								bool flag8 = num == 1;
								if (flag8)
								{
									this.Fire(miner, aim);
								}
								this.m_nextFireTimes.Remove(miner);
								break;
							}
					}
					result = false;
				}
			}
			return result;
		}

		private void Fire(ComponentMiner miner, Ray3 aim)
		{
			IInventory inventory = miner.Inventory;
			int activeSlotIndex = miner.Inventory.ActiveSlotIndex;
			int slotValue = inventory.GetSlotValue(activeSlotIndex);
			int data = Terrain.ExtractData(slotValue);
			GameMode gameMode = this.m_subsystemGameInfo.WorldSettings.GameMode;

			// Verificar si hay fuel disponible (pero no impedir el disparo)
			bool hasFuel = false;
			if (gameMode > 0)
			{
				int fuel = ItemsLauncherBlock.GetFuel(data);
				hasFuel = fuel > 0;

				// Consumir fuel si está disponible
				if (hasFuel)
				{
					int num3 = ItemsLauncherBlock.SetFuel(data, fuel - 1);
					int num4 = Terrain.ReplaceData(slotValue, num3);
					inventory.RemoveSlotItems(activeSlotIndex, 1);
					inventory.AddSlotItems(activeSlotIndex, num4, 1);
				}
			}

			int num = 0;
			int num2 = -1;
			for (int i = 0; i < 10; i++)
			{
				bool flag3 = i != activeSlotIndex;
				if (flag3)
				{
					bool flag4 = inventory.GetSlotCount(i) > 0;
					if (flag4)
					{
						num = inventory.GetSlotValue(i);
						num2 = i;
						break;
					}
				}
			}

			bool flag5 = num2 != -1;
			if (flag5)
			{
				// Configurar parámetros de disparo
				int num5 = ItemsLauncherBlock.GetSpeedLevel(data);
				int num6 = ItemsLauncherBlock.GetSpreadLevel(data);
				bool flag7 = num5 == 0;
				if (flag7)
				{
					num5 = 2;
				}
				bool flag8 = num6 == 0;
				if (flag8)
				{
					num6 = 2;
				}

				float num7 = SubsystemItemsLauncherBlockBehavior.m_speedValues[num5 - 1];
				float num8 = SubsystemItemsLauncherBlockBehavior.m_spreadValues[num6 - 1];
				Vector3 eyePosition = miner.ComponentCreature.ComponentCreatureModel.EyePosition;
				Vector3 vector = Vector3.Normalize(aim.Direction + num8 * new Vector3(this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f), this.m_random.Float(-1f, 1f)));

				SubsystemProjectiles subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
				SubsystemAudio subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
				SubsystemParticles subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
				SubsystemTerrain subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);

				// Disparar el proyectil
				subsystemProjectiles.FireProjectile(num, eyePosition, vector * num7, Vector3.Zero, miner.ComponentCreature);
				subsystemAudio.PlaySound("Audio/Items/ItemLauncher/Item Cannon Fire", 0.5f, this.m_random.Float(-0.1f, 0.1f), eyePosition, 10f, true);
				subsystemParticles.AddParticleSystem(new GunSmokeParticleSystem(subsystemTerrain, eyePosition + 0.5f * vector, vector), false);

				// Aplicar efectos solo si hay fuel
				if (gameMode > 0 && hasFuel)
				{
					miner.ComponentCreature.ComponentBody.ApplyImpulse(-4f * vector);
					this.m_subsystemNoise.MakeNoise(eyePosition, 1f, 15f);
				}

				// Remover el proyectil del inventario
				inventory.RemoveSlotItems(num2, 1);
			}
			else
			{
				// Mostrar mensaje cuando no hay munición - SIN SONIDO DE NOTIFICACIÓN
				ComponentPlayer componentPlayer = miner.ComponentPlayer;
				if (componentPlayer != null)
				{
					// El último parámetro 'false' evita que se reproduzca el sonido de notificación
					string message = LanguageControl.Get("YouNeedAmmunition", "You need ammunition to fire the item launcher");
					componentPlayer.ComponentGui.DisplaySmallMessage(message, Color.Orange, true, false);
				}

				// MANTENER el sonido del martillo del lanzador de ítems
				base.Project.FindSubsystem<SubsystemAudio>(true).PlaySound("Audio/Items/ItemLauncher/Item Launcher Hammer Release", 1f, this.m_random.Float(-0.1f, 0.1f), miner.ComponentCreature.ComponentCreatureModel.EyePosition, 2f, false);
			}
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
			this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
		}

		public SubsystemTime m_subsystemTime;
		public SubsystemGameInfo m_subsystemGameInfo;
		public Game.Random m_random = new Game.Random();
		public SubsystemNoise m_subsystemNoise;

		private static readonly float[] m_speedValues = new float[]
		{
		10f,
		35f,
		60f
		};

		private static readonly float[] m_rateValues = new float[]
		{
		1f,
		2f,
		3f,
		4f,
		5f,
		6f,
		7f,
		8f,
		9f,
		10f,
		11f,
		12f,
		13f,
		14f,
		15f
		};

		private static readonly float[] m_spreadValues = new float[]
		{
		0.01f,
		0.1f,
		0.5f
		};

		private Dictionary<ComponentMiner, double> m_nextFireTimes = new Dictionary<ComponentMiner, double>();
	}
}
