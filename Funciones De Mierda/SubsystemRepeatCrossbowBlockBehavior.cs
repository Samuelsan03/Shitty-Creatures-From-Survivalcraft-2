using System;
using System.Collections.Generic;
using Engine;
using Game;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x020000ED RID: 237
	public class SubsystemRepeatCrossbowBlockBehavior : SubsystemBlockBehavior
	{
		// Token: 0x170000B3 RID: 179
		// (get) Token: 0x060006D9 RID: 1753 RVA: 0x0002DE50 File Offset: 0x0002C050
		public override int[] HandledBlocks
		{
			get
			{
				return Array.Empty<int>();
			}
		}

		// Token: 0x060006DA RID: 1754 RVA: 0x0002DE57 File Offset: 0x0002C057
		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			componentPlayer.ComponentGui.ModalPanelWidget = ((componentPlayer.ComponentGui.ModalPanelWidget == null) ? new RepeatCrossbowWidget(inventory, slotIndex) : null);
			return true;
		}

		// Token: 0x060006DB RID: 1755 RVA: 0x0002DE7C File Offset: 0x0002C07C
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
			if (!(BlocksManager.Blocks[num] is RepeatCrossbowBlock) || slotCount <= 0)
			{
				return false;
			}
			int draw = RepeatCrossbowBlock.GetDraw(data);
			double gameTime;
			if (!this.m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
			{
				gameTime = this.m_subsystemTime.GameTime;
				this.m_aimStartTimes[componentMiner] = gameTime;
			}
			float num2 = (float)(this.m_subsystemTime.GameTime - gameTime);
			float num3 = componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f;
			float num4 = MathUtils.Saturate((num2 - 2.5f) / 6f);
			float s = num3 + 0.15f * num4;
			float num5 = (float)MathUtils.Remainder(this.m_subsystemTime.GameTime, 1000.0);
			float x = SimplexNoise.OctavedNoise(num5, 2f, 3, 2f, 0.5f, false);
			float y = SimplexNoise.OctavedNoise(num5 + 100f, 2f, 3, 2f, 0.5f, false);
			float z = SimplexNoise.OctavedNoise(num5 + 200f, 2f, 3, 2f, 0.5f, false);
			Vector3 v = new Vector3(x, y, z) * s;
			aim.Direction = Vector3.Normalize(aim.Direction + v);
			switch (state)
			{
				case AimState.InProgress:
					{
						if ((double)num2 >= 10.0)
						{
							componentMiner.ComponentCreature.ComponentCreatureSounds.PlayMoanSound();
							return true;
						}
						ComponentFirstPersonModel componentFirstPersonModel = componentMiner.Entity.FindComponent<ComponentFirstPersonModel>();
						if (componentFirstPersonModel != null)
						{
							ComponentPlayer componentPlayer = componentMiner.ComponentPlayer;
							if (componentPlayer != null)
							{
								componentPlayer.ComponentAimingSights.ShowAimingSights(aim.Position, aim.Direction);
							}
							componentFirstPersonModel.ItemOffsetOrder = new Vector3(-0.22f, 0.15f, 0.1f);
							componentFirstPersonModel.ItemRotationOrder = new Vector3(-0.7f, 0f, 0f);
						}
						componentMiner.ComponentCreature.ComponentCreatureModel.AimHandAngleOrder = 1.3f;
						componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemOffsetOrder = new Vector3(-0.08f, -0.1f, 0.07f);
						componentMiner.ComponentCreature.ComponentCreatureModel.InHandItemRotationOrder = new Vector3(-1.55f, 0f, 0f);
						if (this.m_subsystemTime.PeriodicGameTimeEvent(0.10000000149011612, 0.0) && componentMiner.ComponentPlayer == null)
						{
							RepeatArrowBlock.ArrowType? arrowType = RepeatCrossbowBlock.GetArrowType(data);
							if (draw != 15)
							{
								data = RepeatCrossbowBlock.SetDraw(data, 15);
							}
							else if (arrowType == null)
							{
								float num6 = this.m_random.Float(0f, 1f);
								arrowType = new RepeatArrowBlock.ArrowType?(((double)num6 < 0.1000000149011612) ? RepeatArrowBlock.ArrowType.IronArrow : RepeatArrowBlock.ArrowType.CopperArrow);
								data = RepeatCrossbowBlock.SetArrowType(data, arrowType);
							}
							inventory.RemoveSlotItems(inventory.ActiveSlotIndex, 1);
							inventory.AddSlotItems(inventory.ActiveSlotIndex, Terrain.MakeBlockValue(num, 0, data), 1);
						}
						break;
					}
				case AimState.Cancelled:
					this.m_aimStartTimes.Remove(componentMiner);
					break;
				case AimState.Completed:
					{
						int loadCount = RepeatCrossbowBlock.GetLoadCount(slotValue);
						RepeatArrowBlock.ArrowType? arrowType2 = RepeatCrossbowBlock.GetArrowType(data);
						if (draw != 15)
						{
							ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
							if (componentPlayer2 != null)
							{
								componentPlayer2.ComponentGui.DisplaySmallMessage("先放拉开弓弩！", Color.Orange, true, false);
							}
						}
						else if (arrowType2 == null)
						{
							ComponentPlayer componentPlayer3 = componentMiner.ComponentPlayer;
							if (componentPlayer3 != null)
							{
								componentPlayer3.ComponentGui.DisplaySmallMessage("先装载箭头！", Color.Orange, true, false);
							}
						}
						else
						{
							Vector3 eyePosition = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
							Matrix matrix = componentMiner.ComponentCreature.ComponentBody.Matrix;
							Vector3 v2 = matrix.Right * 0.3f;
							Vector3 v3 = eyePosition + v2;
							Vector3 v4 = matrix.Up * -0.2f;
							Vector3 vector = v3 + v4;
							Vector3 v5 = Vector3.Normalize(vector + aim.Direction * 10f - vector);
							int value = Terrain.MakeBlockValue(RepeatArrowBlock.Index, 0, RepeatArrowBlock.SetArrowType(0, arrowType2.Value));
							Projectile projectile = this.m_subsystemProjectiles.FireProjectile(value, vector, v5 * 40f, Vector3.Zero, componentMiner.ComponentCreature);
							if (projectile != null)
							{
								if (RepeatCrossbowBlock.GetLoadCount(slotValue) == 0)
								{
									data = RepeatCrossbowBlock.SetArrowType(data, null);
								}
								else
								{
									RepeatCrossbowBlock.SetLoadCount(slotValue, RepeatCrossbowBlock.GetLoadCount(slotValue) - 1);
								}
								this.m_subsystemAudio.PlaySound("Audio/Bow", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0.05f);
								if (componentMiner.ComponentPlayer == null)
								{
									projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
								}
							}
						}
						inventory.RemoveSlotItems(activeSlotIndex, 1);
						int value2 = (loadCount > 1) ? Terrain.MakeBlockValue(num, loadCount - 1, RepeatCrossbowBlock.SetDraw(data, 15)) : Terrain.MakeBlockValue(num, 0, RepeatCrossbowBlock.SetDraw(RepeatCrossbowBlock.SetArrowType(data, null), 0));
						inventory.AddSlotItems(activeSlotIndex, value2, 1);
						if (draw > 0)
						{
							componentMiner.DamageActiveTool(1);
							this.m_subsystemAudio.PlaySound("Audio/CrossbowBoing", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0f);
						}
						this.m_aimStartTimes.Remove(componentMiner);
						break;
					}
				default:
					throw new ArgumentOutOfRangeException("state", state, null);
			}
			return false;
		}

		// Token: 0x060006DC RID: 1756 RVA: 0x0002E438 File Offset: 0x0002C638
		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int num = Terrain.ExtractContents(value);
			if (!(BlocksManager.Blocks[num] is RepeatArrowBlock))
			{
				return 0;
			}
			RepeatArrowBlock.ArrowType arrowType = RepeatArrowBlock.GetArrowType(Terrain.ExtractData(value));
			int slotValue = inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			if (RepeatCrossbowBlock.GetDraw(data) != 15)
			{
				return 0;
			}
			int loadCount = RepeatCrossbowBlock.GetLoadCount(slotValue);
			RepeatArrowBlock.ArrowType? arrowType2 = RepeatCrossbowBlock.GetArrowType(data);
			if (arrowType2 == null)
			{
				return 8;
			}
			if (loadCount >= 8)
			{
				return 0;
			}
			if (arrowType2.Value != arrowType)
			{
				return 0;
			}
			return 8 - loadCount;
		}

		// Token: 0x060006DD RID: 1757 RVA: 0x0002E4B8 File Offset: 0x0002C6B8
		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			RepeatArrowBlock.ArrowType arrowType = RepeatArrowBlock.GetArrowType(Terrain.ExtractData(value));
			int slotValue = inventory.GetSlotValue(slotIndex);
			int data = Terrain.ExtractData(slotValue);
			int loadCount = RepeatCrossbowBlock.GetLoadCount(slotValue);
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
			int value2 = Terrain.MakeBlockValue(RepeatCrossbowBlock.Index, processCount, RepeatCrossbowBlock.SetArrowType(data, new RepeatArrowBlock.ArrowType?(arrowType)));
			inventory.AddSlotItems(slotIndex, value2, 1);
		}

		// Token: 0x060006DE RID: 1758 RVA: 0x0002E52E File Offset: 0x0002C72E
		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			base.Load(valuesDictionary);
		}

		// Token: 0x040003DD RID: 989
		public SubsystemTime m_subsystemTime;

		// Token: 0x040003DE RID: 990
		public SubsystemProjectiles m_subsystemProjectiles;

		// Token: 0x040003DF RID: 991
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x040003E0 RID: 992
		public Game.Random m_random = new Game.Random();

		// Token: 0x040003E1 RID: 993
		public Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();

		// Token: 0x040003E2 RID: 994
		public const int MCount = 8;
	}
}
