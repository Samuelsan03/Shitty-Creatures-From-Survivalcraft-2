using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentPathBreaker : Component, IUpdateable
	{
		public float BreakProbability { get; set; }
		public bool CanBreakBlocks { get; set; }
		public ComponentPathBreaker.AnimationType CreatureAnimationType { get; set; }
		public bool CanPlaceBlocks { get; set; }
		public string SpecificBlocks { get; set; }
		public bool UseFromInventory { get; set; }

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

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

			this.m_componentHealth = base.Entity.FindComponent<ComponentHealth>();

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

		public void Update(float dt)
		{
			if (this.m_componentHealth != null && this.m_componentHealth.Health <= 0f)
			{
				this.m_blockToBreak = null;
				return;
			}

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

		private void DestroyBlock(Point3 point)
		{
			int cellValue = this.m_subsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);

			// CAMBIAR noDebris A false PARA GENERAR PARTÍCULAS Y DROPS
			this.m_subsystemTerrain.DestroyCell(0, point.X, point.Y, point.Z, 0, false, false, null);

			// SONIDO PERSONALIZADO O SONIDO DE IMPACTO
			if (!string.IsNullOrEmpty(this.m_customSound))
			{
				this.m_subsystemAudio.PlaySound(this.m_customSound, 1f, 0f, new Vector3(point), 16f, true);
			}
			else
			{
				SubsystemSoundMaterials subsystemSoundMaterials = base.Project.FindSubsystem<SubsystemSoundMaterials>();
				if (subsystemSoundMaterials != null)
				{
					subsystemSoundMaterials.PlayImpactSound(cellValue, new Vector3(point), 1f);
				}
			}
		}

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

		private void TriggerAttackAnimation()
		{
			bool flag = this.m_componentCreatureModel != null;
			if (flag)
			{
				this.m_componentCreatureModel.AttackOrder = true;
			}
		}

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

		private Point3 GetMainFacingDirection()
		{
			return this.GetDirectionFromVector(this.m_componentBody.Matrix.Forward);
		}

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

		private SubsystemTime m_subsystemTime;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAudio m_subsystemAudio;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentBody m_componentBody;
		private ComponentInventory m_componentInventory;
		private ComponentLocomotion m_componentLocomotion;
		private ComponentCreatureModel m_componentCreatureModel;

		private ComponentHealth m_componentHealth;

		private readonly bool m_isBreakingBlock;
		public string m_customSound;
		private List<int> m_specificBlockIds;
		private Random m_random = new Random();
		private double m_lastActionTime;
		private Point3? m_blockToBreak;
		private const float ActionCooldown = 0.5f;

		public enum AnimationType
		{
			None,
			Humanoid,
			FourLegged,
			ComboModel
		}
	}
}
