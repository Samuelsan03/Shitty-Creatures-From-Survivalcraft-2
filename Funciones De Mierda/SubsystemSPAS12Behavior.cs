using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Engine;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemSPAS12Behavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false)
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
			string name = "Audio/Armas/SPAS 12 fuego";
			float num = 0.09f;
			int slotValue = componentMiner.Inventory.GetSlotValue(componentMiner.Inventory.ActiveSlotIndex);
			bool flag = Terrain.ExtractContents(slotValue) == BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false);
			bool flag2 = flag;
			if (flag2)
			{
				double gameTime;
				bool flag3 = !this.m_aimStartTimes.TryGetValue(componentMiner, out gameTime);
				bool flag4 = flag3;
				if (flag4)
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
				bool flag5 = num2 > 1f;
				bool flag6 = flag5;
				if (flag6)
				{
					bool flag7 = num2 < 6f;
					bool flag8 = flag7;
					if (flag8)
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
							bool flag9 = componentFirstPersonModel != null;
							if (flag9)
							{
								ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
								bool flag10 = componentPlayer != null;
								if (flag10)
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
							bool flag11 = componentFirstPersonModel2 != null;
							if (flag11)
							{
								componentFirstPersonModel2.ItemOffsetOrder = Vector3.Zero;
								componentFirstPersonModel2.ItemRotationOrder = Vector3.Zero;
							}
							ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
							bool flag12 = componentPlayer2 != null;
							if (flag12)
							{
								componentPlayer2.ComponentGui.DisplaySmallMessage("Recargue municion", Color.White, false, false);
							}
							break;
						}
					case AimState.Completed:
						{
							this.m_aimStartTimes.Remove(componentMiner);
							int bulletNum = SPAS12Block.GetBulletNum(Terrain.ExtractData(slotValue));
							this.fire = (bulletNum > 0);
							bool flag13 = this.fire;
							bool flag14 = flag13;
							if (flag14)
							{
								int value = Terrain.MakeBlockValue(214, 0, 2);
								Vector3 vector = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
								Vector3 vector2 = Vector3.Normalize(vector + aim.Direction * 10f - vector);
								float num4 = this.m_random.Float(-1f * num, num);
								new Vector3(0f, num4, num4);
								for (int i = 0; i < 8; i++)
								{
									this.m_subsystemProjectiles.FireProjectile(value, vector, 280f * (vector2 + new Vector3(0f, this.m_random.Float(-1f * num, num), this.m_random.Float(-1f * num, num))), Vector3.Zero, componentMiner.ComponentCreature);
								}
								ComponentPlayer componentPlayer3 = componentMiner.ComponentPlayer;
								bool flag15 = componentPlayer3 != null;
								if (flag15)
								{
									componentPlayer3.ComponentGui.DisplaySmallMessage((bulletNum - 1).ToString(CultureInfo.InvariantCulture), Color.White, false, false);
								}
								componentMiner.Inventory.RemoveSlotItems(componentMiner.Inventory.ActiveSlotIndex, 1);
								componentMiner.Inventory.AddSlotItems(componentMiner.Inventory.ActiveSlotIndex, Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false), 0, SPAS12Block.SetBulletNum(SPAS12Block.GetBulletNum(Terrain.ExtractData(slotValue)) - 1)), 1);
								Vector3.Normalize(componentMiner.ComponentPlayer.GameWidget.ActiveCamera.ViewDirection);
								componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.3f, 0f, 0f);
								this.m_subsystemAudio.PlaySound(name, 1.5f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 10f, true);
								this.m_subsystemParticles.AddParticleSystem(new GunFireParticleSystem(this.m_subsystemTerrain, vector + 0.3f * vector2, vector2), false);
								this.m_subsystemNoise.MakeNoise(vector, 1f, 40f);
								// La línea de retroceso ha sido eliminada
							}
							componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = Vector3.Zero;
							componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = Vector3.Zero;
							componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 0f;
							ComponentFirstPersonModel componentFirstPersonModel3 = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
							bool flag16 = componentFirstPersonModel3 != null;
							if (flag16)
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
			bool flag = value == BlocksManager.GetBlockIndex(typeof(SPAS12BulletBlock), true, false) && SPAS12Block.GetBulletNum(Terrain.ExtractData(inventory.GetSlotValue(slotIndex))) < 8;
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
			int bulletNum = SPAS12Block.GetBulletNum(Terrain.ExtractData(inventory.GetSlotValue(slotIndex)));
			bool flag = bulletNum < 8;
			bool flag2 = flag;
			if (flag2)
			{
				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(typeof(SPAS12Block), true, false), 0, SPAS12Block.SetBulletNum(8)), 1);
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