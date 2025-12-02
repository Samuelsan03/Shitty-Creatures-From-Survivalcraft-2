using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x0200002F RID: 47
	public class ComponentPathBreaker : Component, IUpdateable
	{
		// Token: 0x1700002F RID: 47
		// (get) Token: 0x06000158 RID: 344 RVA: 0x0000FC84 File Offset: 0x0000DE84
		// (set) Token: 0x06000159 RID: 345 RVA: 0x0000FC8C File Offset: 0x0000DE8C
		public float BreakProbability { get; set; }

		// Token: 0x17000030 RID: 48
		// (get) Token: 0x0600015A RID: 346 RVA: 0x0000FC95 File Offset: 0x0000DE95
		// (set) Token: 0x0600015B RID: 347 RVA: 0x0000FC9D File Offset: 0x0000DE9D
		public bool CanBreakBlocks { get; set; }

		// Token: 0x17000031 RID: 49
		// (get) Token: 0x0600015C RID: 348 RVA: 0x0000FCA6 File Offset: 0x0000DEA6
		// (set) Token: 0x0600015D RID: 349 RVA: 0x0000FCAE File Offset: 0x0000DEAE
		public ComponentPathBreaker.AnimationType CreatureAnimationType { get; set; }

		// Token: 0x17000032 RID: 50
		// (get) Token: 0x0600015E RID: 350 RVA: 0x0000FCB7 File Offset: 0x0000DEB7
		// (set) Token: 0x0600015F RID: 351 RVA: 0x0000FCBF File Offset: 0x0000DEBF
		public bool CanPlaceBlocks { get; set; }

		// Token: 0x17000033 RID: 51
		// (get) Token: 0x06000160 RID: 352 RVA: 0x0000FCC8 File Offset: 0x0000DEC8
		// (set) Token: 0x06000161 RID: 353 RVA: 0x0000FCD0 File Offset: 0x0000DED0
		public string SpecificBlocks { get; set; }

		// Token: 0x17000034 RID: 52
		// (get) Token: 0x06000162 RID: 354 RVA: 0x0000FCD9 File Offset: 0x0000DED9
		// (set) Token: 0x06000163 RID: 355 RVA: 0x0000FCE1 File Offset: 0x0000DEE1
		public bool UseFromInventory { get; set; }

		// Token: 0x17000035 RID: 53
		// (get) Token: 0x06000164 RID: 356 RVA: 0x0000FCEA File Offset: 0x0000DEEA
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x06000165 RID: 357 RVA: 0x0000FCF0 File Offset: 0x0000DEF0
		public override void Load(ValuesDictionary values, IdToEntityMap map)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
			this.m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			this.m_componentInventory = base.Entity.FindComponent<ComponentInventory>();
			this.m_componentLocomotion = base.Entity.FindComponent<ComponentLocomotion>(true);
			this.m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			this.SpecificBlocks = values.GetValue<string>("SpecificBlocks");
			this.UseFromInventory = values.GetValue<bool>("UseFromInventory");
			this.m_customSound = base.ValuesDictionary.GetValue<string>("CustomSound");
			this.CanBreakBlocks = values.GetValue<bool>("CanBreakBlocks");
			this.CanPlaceBlocks = values.GetValue<bool>("CanPlaceBlocks");
			this.CreatureAnimationType = this.DetectAnimationType();
			this.BreakProbability = values.GetValue<float>("BreakProbability");
			this.m_specificBlockIds = new List<int>();
			bool flag = !string.IsNullOrEmpty(this.SpecificBlocks);
			if (flag)
			{
				try
				{
					Dictionary<string, int> dictionary = new Dictionary<string, int>();
					for (int i = 0; i < BlocksManager.Blocks.Length; i++)
					{
						Block block = BlocksManager.Blocks[i];
						bool flag2 = block != null && !(block is AirBlock);
						if (flag2)
						{
							string name = block.GetType().Name;
							bool flag3 = !dictionary.ContainsKey(name);
							if (flag3)
							{
								dictionary.Add(name, i);
							}
						}
					}
					foreach (string text in this.SpecificBlocks.Split(new char[]
					{
						','
					}, StringSplitOptions.RemoveEmptyEntries))
					{
						string text2 = text.Trim();
						int item;
						bool flag4 = dictionary.TryGetValue(text2, out item);
						if (flag4)
						{
							this.m_specificBlockIds.Add(item);
						}
						else
						{
							Log.Warning("Block '" + text2 + "' not found in ComponentPathBreaker");
						}
					}
				}
				catch (Exception ex)
				{
					Log.Warning("Error parsing SpecificBlocks in ComponentPathBreaker: " + ex.Message);
				}
			}
		}

		// Token: 0x06000166 RID: 358 RVA: 0x0000FF58 File Offset: 0x0000E158
		public void Update(float dt)
		{
			bool flag = (!this.CanBreakBlocks && !this.CanPlaceBlocks) || this.m_componentChaseBehavior.Target == null;
			if (flag)
			{
				this.m_blockToBreak = null;
			}
			else
			{
				bool flag2 = this.m_blockToBreak != null;
				if (flag2)
				{
					this.TriggerAttackAnimation();
					bool isAttackHitMoment = this.m_componentCreatureModel.IsAttackHitMoment;
					if (isAttackHitMoment)
					{
						bool flag3 = this.m_random.Float(0f, 1f) <= this.BreakProbability;
						if (flag3)
						{
							this.DestroyBlock(this.m_blockToBreak.Value);
						}
						this.m_blockToBreak = null;
						this.m_lastActionTime = this.m_subsystemTime.GameTime;
					}
				}
				else
				{
					bool flag4 = this.m_subsystemTime.GameTime < this.m_lastActionTime + 0.5;
					if (!flag4)
					{
						Point3 p = Terrain.ToCell(this.m_componentBody.Position);
						Point3 point = this.GetMainFacingDirection();
						bool flag5 = point == Point3.Zero && !this.m_componentPathfinding.IsStuck;
						if (!flag5)
						{
							bool flag6 = point == Point3.Zero && this.m_componentPathfinding.IsStuck;
							if (flag6)
							{
								Vector3 vector = Vector3.Normalize(this.m_componentChaseBehavior.Target.ComponentBody.Position - this.m_componentBody.Position);
								point = this.GetDirectionFromVector(vector);
							}
							Point3 point2 = p + point;
							Point3 point3 = point2 + new Point3(0, 1, 0);
							int cellValue = this.m_subsystemTerrain.Terrain.GetCellValue(point3.X, point3.Y, point3.Z);
							bool flag7 = this.IsBlockBreakable(cellValue);
							if (flag7)
							{
								this.m_blockToBreak = new Point3?(point3);
							}
							else
							{
								int cellValue2 = this.m_subsystemTerrain.Terrain.GetCellValue(point2.X, point2.Y, point2.Z);
								bool flag8 = this.IsBlockBreakable(cellValue2);
								if (flag8)
								{
									this.m_blockToBreak = new Point3?(point2);
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x06000167 RID: 359 RVA: 0x00010190 File Offset: 0x0000E390
		private void PlaceBlock(Point3 point)
		{
			int blockToPlaceFromInventory = this.GetBlockToPlaceFromInventory();
			bool flag = blockToPlaceFromInventory != 0;
			if (flag)
			{
				this.m_subsystemTerrain.ChangeCell(point.X, point.Y, point.Z, blockToPlaceFromInventory, true, null);
				this.m_lastActionTime = this.m_subsystemTime.GameTime;
				this.m_componentLocomotion.JumpOrder = 1f;
			}
		}

		// Token: 0x06000168 RID: 360 RVA: 0x000101F4 File Offset: 0x0000E3F4
		private int GetBlockToPlaceFromInventory()
		{
			bool useFromInventory = this.UseFromInventory;
			int result;
			if (useFromInventory)
			{
				bool flag = this.m_componentInventory != null;
				if (flag)
				{
					IEnumerable<int> enumerable;
					if (this.m_specificBlockIds.Count <= 0)
					{
						enumerable = from s in this.m_componentInventory.m_slots
									 select Terrain.ExtractContents(s.Value);
					}
					else
					{
						IEnumerable<int> specificBlockIds = this.m_specificBlockIds;
						enumerable = specificBlockIds;
					}
					IEnumerable<int> source = enumerable;
					foreach (int num in source.Distinct<int>())
					{
						bool flag2 = num == 0;
						if (!flag2)
						{
							int num2 = this.FindSlotWithBlock(num);
							bool flag3 = num2 != -1;
							if (flag3)
							{
								int slotValue = this.m_componentInventory.GetSlotValue(num2);
								this.m_componentInventory.RemoveSlotItems(num2, 1);
								return slotValue;
							}
						}
					}
				}
				result = 0;
			}
			else
			{
				result = Terrain.MakeBlockValue((this.m_specificBlockIds.Count > 0) ? this.m_specificBlockIds[0] : 3);
			}
			return result;
		}

		// Token: 0x06000169 RID: 361 RVA: 0x00010328 File Offset: 0x0000E528
		private int FindSlotWithBlock(int blockContents)
		{
			bool flag = this.m_componentInventory == null;
			int result;
			if (flag)
			{
				result = -1;
			}
			else
			{
				for (int i = 0; i < this.m_componentInventory.SlotsCount; i++)
				{
					bool flag2 = Terrain.ExtractContents(this.m_componentInventory.GetSlotValue(i)) == blockContents;
					if (flag2)
					{
						return i;
					}
				}
				result = -1;
			}
			return result;
		}

		// Token: 0x0600016A RID: 362 RVA: 0x00010388 File Offset: 0x0000E588
		private void DestroyBlock(Point3 point)
		{
			int cellValue = this.m_subsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
			int num = Terrain.ExtractContents(cellValue);
			this.m_subsystemTerrain.DestroyCell(0, point.X, point.Y, point.Z, 0, true, false, null);
			bool flag = !string.IsNullOrEmpty(this.m_customSound);
			if (flag)
			{
				this.m_subsystemAudio.PlaySound(this.m_customSound, 1f, 0f, new Vector3(point), 16f, true);
			}
			else
			{
				SubsystemSoundMaterials subsystemSoundMaterials = base.Project.FindSubsystem<SubsystemSoundMaterials>();
				bool flag2 = subsystemSoundMaterials != null;
				if (flag2)
				{
					subsystemSoundMaterials.PlayImpactSound(cellValue, new Vector3(point), 1f);
				}
			}
		}

		// Token: 0x0600016B RID: 363 RVA: 0x00010450 File Offset: 0x0000E650
		private ComponentPathBreaker.AnimationType DetectAnimationType()
		{
			bool flag = base.Entity.FindComponent<ComponentFourLeggedModel>() != null;
			ComponentPathBreaker.AnimationType result;
			if (flag)
			{
				result = ComponentPathBreaker.AnimationType.FourLegged;
			}
			else
			{
				result = ComponentPathBreaker.AnimationType.None;
			}
			return result;
		}

		// Token: 0x0600016C RID: 364 RVA: 0x0001047C File Offset: 0x0000E67C
		private void TriggerAttackAnimation()
		{
			bool flag = this.m_componentCreatureModel != null;
			if (flag)
			{
				this.m_componentCreatureModel.AttackOrder = true;
			}
		}

		// Token: 0x0600016D RID: 365 RVA: 0x000104A8 File Offset: 0x0000E6A8
		private bool IsBlockBreakable(int value)
		{
			int num = Terrain.ExtractContents(value);
			bool flag = num == 0;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				Block block = BlocksManager.Blocks[num];
				bool flag2 = this.m_specificBlockIds.Count == 0 || this.m_specificBlockIds.Contains(num);
				result = (block.IsCollidable_(value) && block.DigResilience >= 0f && flag2);
			}
			return result;
		}

		// Token: 0x0600016E RID: 366 RVA: 0x00010514 File Offset: 0x0000E714
		private Point3 GetMainFacingDirection()
		{
			return this.GetDirectionFromVector(this.m_componentBody.Matrix.Forward);
		}

		// Token: 0x0600016F RID: 367 RVA: 0x00010540 File Offset: 0x0000E740
		private Point3 GetDirectionFromVector(Vector3 vector)
		{
			bool flag = Math.Abs(vector.X) > Math.Abs(vector.Z);
			Point3 result;
			if (flag)
			{
				result = new Point3(Math.Sign(vector.X), 0, 0);
			}
			else
			{
				bool flag2 = Math.Abs(vector.Z) > 0f;
				if (flag2)
				{
					result = new Point3(0, 0, Math.Sign(vector.Z));
				}
				else
				{
					result = Point3.Zero;
				}
			}
			return result;
		}

		// Token: 0x0400017F RID: 383
		private SubsystemTime m_subsystemTime;

		// Token: 0x04000180 RID: 384
		private SubsystemTerrain m_subsystemTerrain;

		// Token: 0x04000181 RID: 385
		private SubsystemAudio m_subsystemAudio;

		// Token: 0x04000182 RID: 386
		private ComponentChaseBehavior m_componentChaseBehavior;

		// Token: 0x04000183 RID: 387
		private ComponentPathfinding m_componentPathfinding;

		// Token: 0x04000184 RID: 388
		private ComponentBody m_componentBody;

		// Token: 0x04000185 RID: 389
		private ComponentInventory m_componentInventory;

		// Token: 0x04000186 RID: 390
		private ComponentLocomotion m_componentLocomotion;

		// Token: 0x04000187 RID: 391
		private ComponentCreatureModel m_componentCreatureModel;

		// Token: 0x04000188 RID: 392
		private readonly bool m_isBreakingBlock;

		// Token: 0x04000189 RID: 393
		public string m_customSound;

		// Token: 0x0400018A RID: 394
		private List<int> m_specificBlockIds;

		// Token: 0x0400018C RID: 396
		private Random m_random = new Random();

		// Token: 0x04000192 RID: 402
		private double m_lastActionTime;

		// Token: 0x04000193 RID: 403
		private Point3? m_blockToBreak;

		// Token: 0x04000194 RID: 404
		private const float ActionCooldown = 0.5f;

		// Token: 0x0200005C RID: 92
		public enum AnimationType
		{
			// Token: 0x04000362 RID: 866
			None,
			// Token: 0x04000363 RID: 867
			Humanoid,
			// Token: 0x04000364 RID: 868
			FourLegged,
			// Token: 0x04000365 RID: 869
			ComboModel
		}
	}
}
