using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentTankLauncherBehavior : ComponentBehavior, IUpdateable
	{
		// Constantes de lanzamiento
		private const float MinLaunchSpeed = 20f;
		private const float MaxLaunchSpeed = 35f;
		private const float VerticalBoost = 2f;
		private const float TargetHeightFactor = 0.75f;

		// Items a lanzar
		public string m_itemsToLaunch = "";
		public List<int> m_launchItemIndices = new List<int>();
		public int m_currentItemIndex = 0;

		// Temporizador
		public double m_nextUpdateTime;
		public double m_ChargeTime;
		public float m_distance = 5f;

		// Animación de lanzamiento
		public float m_launchAnimationTimer = 0f;
		public bool m_isLaunching = false;

		// Referencias a otros componentes
		public ComponentCreature m_componentCreature;
		public ComponentTankModel m_componentTankModel;
		public ComponentNewGhostTankModel m_componentNewGhostTankModel;
		public ComponentZombieChaseBehavior m_componentZombieChaseBehavior;
		public ComponentChaseBehavior m_componentChaseBehavior;
		public ComponentNewChaseBehavior m_componentNewChaseBehavior;
		public SubsystemProjectiles m_subsystemProjectiles;
		public SubsystemTime m_subsystemTime;
		public Random m_random = new Random();

		// Nuevos componentes para control de línea de visión y aturdimiento
		private SubsystemBodies m_subsystemBodies;
		private ComponentLocomotion m_componentLocomotion;

		public override float ImportanceLevel => 0f;
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);

			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentTankModel = base.Entity.FindComponent<ComponentTankModel>(false);
			m_componentNewGhostTankModel = base.Entity.FindComponent<ComponentNewGhostTankModel>(false);

			m_componentZombieChaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>(false);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(false);
			m_componentNewChaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>(false);

			m_componentLocomotion = m_componentCreature.ComponentLocomotion;

			// Cargar items a lanzar
			m_itemsToLaunch = valuesDictionary.GetValue<string>("ItemsToLaunch", "");
			m_launchItemIndices.Clear();

			if (!string.IsNullOrEmpty(m_itemsToLaunch))
			{
				string[] items = m_itemsToLaunch.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string item in items)
				{
					string trimmedItem = item.Trim();
					if (string.IsNullOrEmpty(trimmedItem)) continue;

					if (trimmedItem.Contains(":"))
					{
						string[] parts = trimmedItem.Split(':');
						string blockName = parts[0].Trim();
						string variantStr = parts[1].Trim();

						int blockIndex = BlocksManager.GetBlockIndex(blockName, false);
						if (blockIndex >= 0)
						{
							if (int.TryParse(variantStr, out int variant))
							{
								int blockValue = Terrain.MakeBlockValue(blockIndex, 0, variant);
								m_launchItemIndices.Add(blockValue);
							}
							else
							{
								m_launchItemIndices.Add(blockIndex);
								Log.Warning("ComponentTankLauncherBehavior: Invalid variant format for '" + trimmedItem + "', using default variant");
							}
						}
						else
						{
							Log.Warning("ComponentTankLauncherBehavior: Block '" + blockName + "' not found");
						}
					}
					else
					{
						if (int.TryParse(trimmedItem, out int directIndex))
						{
							m_launchItemIndices.Add(directIndex);
						}
						else
						{
							int blockIndex = BlocksManager.GetBlockIndex(trimmedItem, false);
							if (blockIndex >= 0)
							{
								m_launchItemIndices.Add(blockIndex);
							}
							else
							{
								Log.Warning("ComponentTankLauncherBehavior: Item '" + trimmedItem + "' not found");
							}
						}
					}
				}
			}

			if (m_launchItemIndices.Count == 0)
			{
				m_launchItemIndices.Add(0);
				Log.Warning("ComponentTankLauncherBehavior: No valid items found, using default");
			}

			// Inicializar el cooldown correctamente
			m_ChargeTime = 1.0;
			m_nextUpdateTime = m_subsystemTime.GameTime + 0.5; // Pequeño delay inicial

			IsActive = true;
		}

		public void Update(float dt)
		{
			if (AchievementsManager.IsCelebrationActive) return;

			// Gestión de la animación de lanzamiento (al inicio para que no bloquee)
			if (m_isLaunching)
			{
				m_launchAnimationTimer += dt;
				if (m_launchAnimationTimer >= 0.5f)
				{
					ResetHandsAnimation();
					m_isLaunching = false;
					m_launchAnimationTimer = 0f;
				}
			}

			bool flag = m_subsystemTime.GameTime >= m_nextUpdateTime;
			if (flag)
			{
				m_distance = 10f;

				if (CanLaunch())
				{
					ComponentCreature target = GetCurrentTarget();
					if (target != null)
					{
						// Posición de lanzamiento
						Vector3 launchPosition = m_componentCreature.ComponentCreatureModel.EyePosition
							+ m_componentCreature.ComponentBody.Matrix.Right * 0.3f
							- m_componentCreature.ComponentBody.Matrix.Up * 0.2f
							+ m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;

						// Animación de manos
						var componentHumanModel = m_componentCreature.ComponentCreatureModel as ComponentHumanModel;
						if (componentHumanModel != null)
						{
							componentHumanModel.m_handAngles2 = new Vector2(4f, -5f);
							componentHumanModel.m_handAngles1 = new Vector2(4f, 3f);
							m_isLaunching = true;
							m_launchAnimationTimer = 0f;
						}

						// Cálculo de velocidad
						Vector3 targetPos = target.ComponentBody.Position;
						float targetHeight = target.ComponentBody.StanceBoxSize.Y;
						Vector3 aimPoint = targetPos + new Vector3(0f, targetHeight * TargetHeightFactor, 0f);

						Vector3 direction = aimPoint - launchPosition;
						float distance = direction.Length();

						float speed = MathUtils.Lerp(MinLaunchSpeed, MaxLaunchSpeed, distance / 20f);
						Vector3 velocity = Vector3.Normalize(direction) * speed + new Vector3(0f, VerticalBoost, 0f);

						// Seleccionar item a lanzar (con protección)
						int itemToLaunch = GetNextValidItem();

						// Lanzar proyectil
						m_subsystemProjectiles.FireProjectile(
							itemToLaunch,
							launchPosition,
							velocity,
							Vector3.Zero,
							m_componentCreature
						);

						// Cooldown SOLO después de lanzar exitosamente
						m_ChargeTime = 1.0;
						m_nextUpdateTime = m_subsystemTime.GameTime + m_ChargeTime;
					}
					else
					{
						// Target se perdió, reintentar pronto
						m_nextUpdateTime = m_subsystemTime.GameTime + 0.1;
					}
				}
				else
				{
					// No se puede lanzar ahora, reintentar pronto (no esperar cooldown completo)
					m_nextUpdateTime = m_subsystemTime.GameTime + 0.1;
				}
			}
		}

		/// <summary>
		/// Obtiene el siguiente item válido de la lista, saltando items inválidos.
		/// </summary>
		private int GetNextValidItem()
		{
			if (m_launchItemIndices.Count == 0)
				return 0;

			int attempts = 0;
			int startIndex = m_currentItemIndex;

			do
			{
				int itemToLaunch = m_launchItemIndices[m_currentItemIndex];
				m_currentItemIndex = (m_currentItemIndex + 1) % m_launchItemIndices.Count;
				attempts++;

				// Verificar que el item sea válido
				if (itemToLaunch > 0)
				{
					return itemToLaunch;
				}

				// Si dimos la vuelta completa, devolver lo que haya
				if (m_currentItemIndex == startIndex)
				{
					return m_launchItemIndices[0];
				}
			} while (attempts < m_launchItemIndices.Count);

			return m_launchItemIndices[0];
		}

		private ComponentCreature GetCurrentTarget()
		{
			if (m_componentNewChaseBehavior != null && m_componentNewChaseBehavior.Target != null)
				return m_componentNewChaseBehavior.Target;

			if (m_componentZombieChaseBehavior != null && m_componentZombieChaseBehavior.Target != null)
				return m_componentZombieChaseBehavior.Target;

			if (m_componentChaseBehavior != null && m_componentChaseBehavior.Target != null)
				return m_componentChaseBehavior.Target;

			return null;
		}

		private void ResetHandsAnimation()
		{
			var componentHumanModel = m_componentCreature.ComponentCreatureModel as ComponentHumanModel;
			if (componentHumanModel != null)
			{
				componentHumanModel.m_handAngles2 = Vector2.Zero;
				componentHumanModel.m_handAngles1 = Vector2.Zero;
			}
		}

		private bool HasLineOfSight(ComponentCreature target)
		{
			Vector3 start = m_componentCreature.ComponentCreatureModel.EyePosition;
			float targetHeight = target.ComponentBody.StanceBoxSize.Y;
			Vector3 aimPoint = target.ComponentBody.Position + new Vector3(0f, targetHeight * TargetHeightFactor, 0f);
			float targetDistance = Vector3.Distance(start, aimPoint);

			SubsystemTerrain terrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			if (terrain != null)
			{
				TerrainRaycastResult? terrainHit = terrain.Raycast(start, aimPoint, false, true, (value, dist) =>
				{
					int contents = Terrain.ExtractContents(value);
					if (contents != 0 && BlocksManager.Blocks[contents].IsCollidable_(value))
						return true;
					return false;
				});
				if (terrainHit != null && terrainHit.Value.Distance < targetDistance - 0.1f)
					return false;
			}

			if (m_subsystemBodies != null)
			{
				BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(start, aimPoint, targetDistance, (body, dist) =>
				{
					if (body == m_componentCreature.ComponentBody || body == target.ComponentBody)
						return false;
					return true;
				});
				if (bodyHit != null && bodyHit.Value.Distance < targetDistance - 0.1f)
					return false;
			}

			return true;
		}

		public bool CanLaunch()
		{
			if (m_isLaunching)
				return false;

			if (m_componentCreature == null || m_componentCreature.ComponentHealth.Health <= 0f)
				return false;

			ComponentCreature target = GetCurrentTarget();
			if (target == null)
				return false;

			if (m_componentLocomotion != null && m_componentLocomotion.StunTime > 0f)
				return false;

			if (!HasLineOfSight(target))
				return false;

			return true;
		}

		public void ForceLaunch()
		{
			if (!CanLaunch())
				return;

			ComponentCreature target = GetCurrentTarget();
			if (target == null)
				return;

			var componentHumanModel = m_componentCreature.ComponentCreatureModel as ComponentHumanModel;
			if (componentHumanModel != null)
			{
				componentHumanModel.m_handAngles2 = new Vector2(4f, -5f);
				componentHumanModel.m_handAngles1 = new Vector2(4f, 3f);
				m_isLaunching = true;
				m_launchAnimationTimer = 0f;
			}

			Vector3 launchPosition = m_componentCreature.ComponentCreatureModel.EyePosition
				+ m_componentCreature.ComponentBody.Matrix.Right * 0.3f
				- m_componentCreature.ComponentBody.Matrix.Up * 0.2f
				+ m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;

			Vector3 targetPos = target.ComponentBody.Position;
			float targetHeight = target.ComponentBody.StanceBoxSize.Y;
			Vector3 aimPoint = targetPos + new Vector3(0f, targetHeight * TargetHeightFactor, 0f);

			Vector3 direction = aimPoint - launchPosition;
			float distance = direction.Length();

			float speed = MathUtils.Lerp(MinLaunchSpeed, MaxLaunchSpeed, distance / 20f);
			Vector3 velocity = Vector3.Normalize(direction) * speed + new Vector3(0f, VerticalBoost, 0f);

			int itemToLaunch = GetNextValidItem();

			m_subsystemProjectiles.FireProjectile(
				itemToLaunch,
				launchPosition,
				velocity,
				Vector3.Zero,
				m_componentCreature
			);

			// Aplicar cooldown después de lanzamiento forzado
			m_ChargeTime = 1.0;
			m_nextUpdateTime = m_subsystemTime.GameTime + m_ChargeTime;
		}

		public void SetAttackTarget(ComponentCreature target)
		{
			if (m_componentNewChaseBehavior != null)
			{
				m_componentNewChaseBehavior.Attack(target, 1000f, 1000f, true);
			}
			else if (m_componentZombieChaseBehavior != null)
			{
				m_componentZombieChaseBehavior.Attack(target, 1000f, 1000f, true);
			}
			else if (m_componentChaseBehavior != null)
			{
				m_componentChaseBehavior.Attack(target, 1000f, 1000f, true);
			}
		}
	}
}
