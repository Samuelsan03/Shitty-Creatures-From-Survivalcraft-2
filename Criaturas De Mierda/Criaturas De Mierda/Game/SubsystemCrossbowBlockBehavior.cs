using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000358 RID: 856
	public class SubsystemCrossbowBlockBehavior : SubsystemBlockBehavior
	{
		// Token: 0x170003B6 RID: 950
		// (get) Token: 0x06001A86 RID: 6790 RVA: 0x000CF906 File Offset: 0x000CDB06
		public override int[] HandledBlocks
		{
			get
			{
				return new int[0];
			}
		}

		// Token: 0x06001A87 RID: 6791 RVA: 0x000CF90E File Offset: 0x000CDB0E
		public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
		{
			componentPlayer.ComponentGui.ModalPanelWidget = ((componentPlayer.ComponentGui.ModalPanelWidget == null) ? new CrossbowWidget(inventory, slotIndex) : null);
			return true;
		}

		// Token: 0x06001A88 RID: 6792 RVA: 0x000CF934 File Offset: 0x000CDB34
		public override bool OnAim(Ray3 aim, ComponentMiner componentMiner, AimState state)
		{
			IInventory inventory = componentMiner.Inventory;
			if (inventory != null)
			{
				int activeSlotIndex = inventory.ActiveSlotIndex;
				if (activeSlotIndex >= 0)
				{
					int slotValue = inventory.GetSlotValue(activeSlotIndex);
					int slotCount = inventory.GetSlotCount(activeSlotIndex);
					int contents = Terrain.ExtractContents(slotValue);
					int data = Terrain.ExtractData(slotValue);
					if (slotCount > 0)
					{
						int draw = CrossbowBlock.GetDraw(data);
						double gameTime;
						if (!this.m_aimStartTimes.TryGetValue(componentMiner, out gameTime))
						{
							gameTime = this.m_subsystemTime.GameTime;
							this.m_aimStartTimes[componentMiner] = gameTime;
						}
						float num = (float)(this.m_subsystemTime.GameTime - gameTime);
						float num2 = (float)MathUtils.Remainder(this.m_subsystemTime.GameTime, 1000.0);
						float num3 = (componentMiner.ComponentCreature.ComponentBody.IsCrouching ? 0.01f : 0.03f) + 0.15f * MathUtils.Saturate((num - 2.5f) / 6f);
						Vector3 vector = default(Vector3);
						vector.X = SimplexNoise.OctavedNoise(num2, 2f, 3, 2f, 0.5f, false);
						vector.Y = SimplexNoise.OctavedNoise(num2 + 100f, 2f, 3, 2f, 0.5f, false);
						vector.Z = SimplexNoise.OctavedNoise(num2 + 200f, 2f, 3, 2f, 0.5f, false);
						Vector3 vector2 = num3 * vector;
						aim.Direction = Vector3.Normalize(aim.Direction + vector2);
						switch (state)
						{
						case AimState.InProgress:
						{
							if (num >= 10f)
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
							break;
						}
						case AimState.Cancelled:
							this.m_aimStartTimes.Remove(componentMiner);
							break;
						case AimState.Completed:
						{
							ArrowBlock.ArrowType? arrowType = CrossbowBlock.GetArrowType(data);
							if (draw != 15)
							{
								ComponentPlayer componentPlayer2 = componentMiner.ComponentPlayer;
								if (componentPlayer2 != null)
								{
									componentPlayer2.ComponentGui.DisplaySmallMessage(LanguageControl.Get(SubsystemCrossbowBlockBehavior.fName, 0), Color.White, true, false, 1f);
								}
							}
							else if (arrowType == null)
							{
								ComponentPlayer componentPlayer3 = componentMiner.ComponentPlayer;
								if (componentPlayer3 != null)
								{
									componentPlayer3.ComponentGui.DisplaySmallMessage(LanguageControl.Get(SubsystemCrossbowBlockBehavior.fName, 1), Color.White, true, false, 1f);
								}
							}
							else
							{
								Vector3 vector3 = componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition + componentMiner.ComponentCreature.ComponentBody.Matrix.Right * 0.3f - componentMiner.ComponentCreature.ComponentBody.Matrix.Up * 0.2f;
								Vector3 vector4 = Vector3.Normalize(vector3 + aim.Direction * 10f - vector3);
								int value = Terrain.MakeBlockValue(this.m_ArrowBlockIndex, 0, ArrowBlock.SetArrowType(0, arrowType.Value));
								float num4 = 38f;
								Vector3 velocity = componentMiner.ComponentCreature.ComponentBody.Velocity + num4 * vector4;
								if (this.m_subsystemProjectiles.FireProjectile(value, vector3, velocity, Vector3.Zero, componentMiner.ComponentCreature) != null)
								{
									data = CrossbowBlock.SetArrowType(data, null);
									this.m_subsystemAudio.PlaySound("Audio/Bow", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0.05f);
								}
							}
							inventory.RemoveSlotItems(activeSlotIndex, 1);
							int value2 = Terrain.MakeBlockValue(contents, 0, CrossbowBlock.SetDraw(data, 0));
							inventory.AddSlotItems(activeSlotIndex, value2, 1);
							if (draw > 0)
							{
								componentMiner.DamageActiveTool(1);
								this.m_subsystemAudio.PlaySound("Audio/CrossbowBoing", 1f, this.m_random.Float(-0.1f, 0.1f), componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition, 3f, 0f);
							}
							this.m_aimStartTimes.Remove(componentMiner);
							break;
						}
						}
					}
				}
			}
			return false;
		}

		// Token: 0x06001A89 RID: 6793 RVA: 0x000CFE00 File Offset: 0x000CE000
		public override int GetProcessInventoryItemCapacity(IInventory inventory, int slotIndex, int value)
		{
			int num = Terrain.ExtractContents(value);
			ArrowBlock.ArrowType arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(value));
			if (num != this.m_ArrowBlockIndex || !this.m_supportedArrowTypes.Contains(arrowType))
			{
				return 0;
			}
			int data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
			ArrowBlock.ArrowType? arrowType2 = CrossbowBlock.GetArrowType(data);
			int draw = CrossbowBlock.GetDraw(data);
			if (arrowType2 == null && draw == 15)
			{
				return 1;
			}
			return 0;
		}

		// Token: 0x06001A8A RID: 6794 RVA: 0x000CFE64 File Offset: 0x000CE064
		public override void ProcessInventoryItem(IInventory inventory, int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
		{
			if (processCount == 1)
			{
				ArrowBlock.ArrowType arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(value));
				int data = Terrain.ExtractData(inventory.GetSlotValue(slotIndex));
				processedValue = 0;
				processedCount = 0;
				inventory.RemoveSlotItems(slotIndex, 1);
				inventory.AddSlotItems(slotIndex, Terrain.MakeBlockValue(this.m_CrossbowBlockIndex, 0, CrossbowBlock.SetArrowType(data, new ArrowBlock.ArrowType?(arrowType))), 1);
				return;
			}
			processedValue = value;
			processedCount = count;
		}

		// Token: 0x06001A8B RID: 6795 RVA: 0x000CFECC File Offset: 0x000CE0CC
		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_CrossbowBlockIndex = BlocksManager.GetBlockIndex<CrossbowBlock>(false, false);
			this.m_ArrowBlockIndex = BlocksManager.GetBlockIndex<ArrowBlock>(false, false);
			base.Load(valuesDictionary);
		}

		// Token: 0x04001257 RID: 4695
		public SubsystemTime m_subsystemTime;

		// Token: 0x04001258 RID: 4696
		public SubsystemProjectiles m_subsystemProjectiles;

		// Token: 0x04001259 RID: 4697
		public SubsystemAudio m_subsystemAudio;

		// Token: 0x0400125A RID: 4698
		public Random m_random = new Random();

		// Token: 0x0400125B RID: 4699
		public static string fName = "SubsystemCrossbowBlockBehavior";

		// Token: 0x0400125C RID: 4700
		public Dictionary<ComponentMiner, double> m_aimStartTimes = new Dictionary<ComponentMiner, double>();

		// Token: 0x0400125D RID: 4701
		public ArrowBlock.ArrowType[] m_supportedArrowTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		// Token: 0x0400125E RID: 4702
		public int m_CrossbowBlockIndex;

		// Token: 0x0400125F RID: 4703
		public int m_ArrowBlockIndex;
	}
}
