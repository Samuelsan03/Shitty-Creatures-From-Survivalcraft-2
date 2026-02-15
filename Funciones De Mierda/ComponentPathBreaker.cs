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

			// Referencias a todos los comportamientos de persecución (NO son obligatorios)
			this.m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>();
			this.m_componentZombieChaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>();
			this.m_componentNewChaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>();
			this.m_componentNewChaseBehavior2 = base.Entity.FindComponent<ComponentNewChaseBehavior2>();

			// Estos componentes SÍ son obligatorios para el PathBreaker
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			this.m_componentLocomotion = base.Entity.FindComponent<ComponentLocomotion>(true);
			this.m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);

			// Estos componentes NO son obligatorios
			this.m_componentInventory = base.Entity.FindComponent<ComponentInventory>();
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
			// Verificar si la criatura está muerta (solo si existe ComponentHealth)
			if (this.m_componentHealth != null && this.m_componentHealth.Health <= 0f)
			{
				this.m_blockToBreak = null;
				return;
			}

			// Verificar si hay cualquier objetivo de persecución
			bool hasTarget = this.HasAnyTarget();
			bool flag = (!this.CanBreakBlocks && !this.CanPlaceBlocks) || !hasTarget;

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

						// Determinar si el objetivo está principalmente abajo
						bool targetIsBelow = false;
						ComponentBody targetBody = this.GetActiveTargetBody();
						if (targetBody != null)
						{
							Vector3 toTarget = targetBody.Position - this.m_componentBody.Position;
							targetIsBelow = toTarget.Y < -1f && Math.Abs(toTarget.Y) > Math.Max(Math.Abs(toTarget.X), Math.Abs(toTarget.Z));
						}

						// Lista de direcciones a verificar, en orden de prioridad
						List<Point3> directionsToCheck = new List<Point3>();

						// Si el objetivo está abajo, incluir dirección hacia abajo
						if (targetIsBelow && this.m_componentPathfinding.IsStuck)
						{
							directionsToCheck.Add(new Point3(0, -1, 0));  // Abajo
						}

						// Siempre verificar la dirección de movimiento actual
						Point3 facingDirection = this.GetMainFacingDirection();
						if (facingDirection != Point3.Zero)
						{
							directionsToCheck.Add(facingDirection);

							// También verificar arriba de la dirección actual
							directionsToCheck.Add(facingDirection + new Point3(0, 1, 0));
						}

						// Si está atascado, verificar todas las direcciones horizontales
						if (this.m_componentPathfinding.IsStuck)
						{
							// Todas las direcciones horizontales
							directionsToCheck.Add(new Point3(1, 0, 0));   // Este
							directionsToCheck.Add(new Point3(-1, 0, 0));  // Oeste
							directionsToCheck.Add(new Point3(0, 0, 1));   // Norte
							directionsToCheck.Add(new Point3(0, 0, -1));  // Sur

							// Arriba de cada dirección horizontal
							directionsToCheck.Add(new Point3(1, 1, 0));   // Arriba-Este
							directionsToCheck.Add(new Point3(-1, 1, 0));  // Arriba-Oeste
							directionsToCheck.Add(new Point3(0, 1, 1));   // Arriba-Norte
							directionsToCheck.Add(new Point3(0, 1, -1));  // Arriba-Sur

							// También verificar directamente arriba
							directionsToCheck.Add(new Point3(0, 1, 0));   // Arriba
						}
						else
						{
							// Si no está atascado, también verificar los lados
							// Esto ayuda con obstáculos laterales
							if (facingDirection.X != 0)
							{
								// Si mira este/oeste, verificar norte/sur
								directionsToCheck.Add(new Point3(0, 0, 1));   // Norte
								directionsToCheck.Add(new Point3(0, 0, -1));  // Sur
								directionsToCheck.Add(new Point3(0, 1, 1));   // Arriba-Norte
								directionsToCheck.Add(new Point3(0, 1, -1));  // Arriba-Sur
							}
							else if (facingDirection.Z != 0)
							{
								// Si mira norte/sur, verificar este/oeste
								directionsToCheck.Add(new Point3(1, 0, 0));   // Este
								directionsToCheck.Add(new Point3(-1, 0, 0));  // Oeste
								directionsToCheck.Add(new Point3(1, 1, 0));   // Arriba-Este
								directionsToCheck.Add(new Point3(-1, 1, 0));  // Arriba-Oeste
							}

							// Siempre verificar directamente arriba
							directionsToCheck.Add(new Point3(0, 1, 0));   // Arriba
						}

						// Eliminar duplicados y mantener orden
						directionsToCheck = directionsToCheck.Distinct().ToList();

						// Verificar bloques en todas las direcciones
						foreach (Point3 direction in directionsToCheck)
						{
							Point3 pointToCheck = p + direction;

							// Verificar el bloque en esta dirección
							int cellValue = this.m_subsystemTerrain.Terrain.GetCellValue(pointToCheck.X, pointToCheck.Y, pointToCheck.Z);
							if (this.IsBlockBreakable(cellValue))
							{
								this.m_blockToBreak = new Point3?(pointToCheck);
								break;
							}
						}
					}
				}
			}
		}

		// MÉTODO MEJORADO: Verificar si hay algún objetivo activo (maneja componentes null)
		private bool HasAnyTarget()
		{
			// Comportamiento de persecución original (opcional)
			if (this.m_componentChaseBehavior != null && this.m_componentChaseBehavior.Target != null)
				return true;

			// Comportamiento de persecución zombie (opcional)
			if (this.m_componentZombieChaseBehavior != null && this.m_componentZombieChaseBehavior.Target != null)
				return true;

			// Nuevo comportamiento de persecución (opcional)
			if (this.m_componentNewChaseBehavior != null && this.m_componentNewChaseBehavior.Target != null)
				return true;

			// Nuevo comportamiento de persecución 2 (opcional)
			if (this.m_componentNewChaseBehavior2 != null && this.m_componentNewChaseBehavior2.Target != null)
				return true;

			return false;
		}

		// MÉTODO MEJORADO: Obtener el cuerpo del objetivo activo (maneja componentes null)
		private ComponentBody GetActiveTargetBody()
		{
			// Comportamiento de persecución original (opcional)
			if (this.m_componentChaseBehavior != null && this.m_componentChaseBehavior.Target != null)
				return this.m_componentChaseBehavior.Target.ComponentBody;

			// Comportamiento de persecución zombie (opcional)
			if (this.m_componentZombieChaseBehavior != null && this.m_componentZombieChaseBehavior.Target != null)
				return this.m_componentZombieChaseBehavior.Target.ComponentBody;

			// Nuevo comportamiento de persecución (opcional)
			if (this.m_componentNewChaseBehavior != null && this.m_componentNewChaseBehavior.Target != null)
				return this.m_componentNewChaseBehavior.Target.ComponentBody;

			// Nuevo comportamiento de persecución 2 (opcional)
			if (this.m_componentNewChaseBehavior2 != null && this.m_componentNewChaseBehavior2.Target != null)
				return this.m_componentNewChaseBehavior2.Target.ComponentBody;

			return null;
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
			int num = Terrain.ExtractContents(cellValue);
			Block block = BlocksManager.Blocks[num];

			// Seguridad extra: no destruir bloques indestructibles (como bedrock)
			if (block.GetExplosionResilience(cellValue) >= float.MaxValue)
				return;

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

		// ===== MÉTODO CLAVE: VERIFICA SI UN BLOQUE PUEDE SER ROTO =====
		private bool IsBlockBreakable(int value)
		{
			int num = Terrain.ExtractContents(value);
			if (num == 0)
				return false;

			Block block = BlocksManager.Blocks[num];

			// --- NUEVA COMPROBACIÓN: bloques indestructibles (como bedrock) ---
			// GetExplosionResilience devuelve float.MaxValue para bloques que no pueden ser destruidos por explosiones.
			// Usamos el mismo criterio para el pathbreaker.
			if (block.GetExplosionResilience(value) >= float.MaxValue)
				return false;

			// Verificar si está en la lista específica (si se definió)
			bool specificOk = this.m_specificBlockIds.Count == 0 || this.m_specificBlockIds.Contains(num);

			// Condiciones originales: colisionable, resiliencia de excavación >= 0 (no negativo) y cumple specificOk
			return block.IsCollidable_(value) && block.DigResilience >= 0f && specificOk;
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

		// Campos
		private SubsystemTime m_subsystemTime;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemAudio m_subsystemAudio;

		// Comportamientos de persecución (opcionales - pueden ser null)
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentZombieChaseBehavior m_componentZombieChaseBehavior;
		private ComponentNewChaseBehavior m_componentNewChaseBehavior;
		private ComponentNewChaseBehavior2 m_componentNewChaseBehavior2;

		// Componentes obligatorios para PathBreaker
		private ComponentPathfinding m_componentPathfinding;
		private ComponentBody m_componentBody;
		private ComponentLocomotion m_componentLocomotion;
		private ComponentCreatureModel m_componentCreatureModel;

		// Componentes opcionales
		private ComponentInventory m_componentInventory;
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
