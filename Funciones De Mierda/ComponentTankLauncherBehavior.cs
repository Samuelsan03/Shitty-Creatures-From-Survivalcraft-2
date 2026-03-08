using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentTankLauncherBehavior : ComponentBehavior, IUpdateable
	{
		// Constantes de lanzamiento (inspiradas en InvShooter para Throwable)
		private const float MinLaunchSpeed = 20f;
		private const float MaxLaunchSpeed = 35f;
		private const float VerticalBoost = 2f;
		private const float TargetHeightFactor = 0.75f; // Apuntar al 75% de la altura de la caja de stance

		// Variable para los items a lanzar (soporta múltiples items)
		public string m_itemsToLaunch = "";
		public List<int> m_launchItemIndices = new List<int>();
		public int m_currentItemIndex = 0;

		// Temporizador como el código chino
		public double m_nextUpdateTime;
		public double m_ChargeTime;
		public float m_distance = 5f;

		// Para animación de lanzamiento
		public float m_launchAnimationTimer = 0f;
		public bool m_isLaunching = false;

		// Referencias a otros componentes
		public ComponentCreature m_componentCreature;
		public ComponentTankModel m_componentTankModel;
		public ComponentNewGhostTankModel m_componentNewGhostTankModel;
		public ComponentZombieChaseBehavior m_componentZombieChaseBehavior;
		public ComponentChaseBehavior m_componentChaseBehavior;
		public ComponentNewChaseBehavior m_componentNewChaseBehavior;
		public ComponentNewChaseBehavior2 m_componentNewChaseBehavior2;
		public SubsystemProjectiles m_subsystemProjectiles;
		public SubsystemTime m_subsystemTime;
		public Random m_random = new Random();

		public override float ImportanceLevel => 0f;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);

			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentTankModel = base.Entity.FindComponent<ComponentTankModel>(false);
			m_componentNewGhostTankModel = base.Entity.FindComponent<ComponentNewGhostTankModel>(false);

			// TODOS los comportamientos de chase son OPCIONALES
			m_componentZombieChaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>(false);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(false);
			m_componentNewChaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>(false);
			m_componentNewChaseBehavior2 = base.Entity.FindComponent<ComponentNewChaseBehavior2>(false);

			// Cargar items a lanzar (soporta múltiples items separados por coma)
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

			IsActive = true;
		}

		public void Update(float dt)
		{
			bool flag = m_subsystemTime.GameTime >= m_nextUpdateTime;
			if (flag)
			{
				m_distance = 10f;

				ComponentCreature target = GetCurrentTarget();
				bool hasTarget = target != null && m_componentCreature.ComponentHealth.Health > 0f;

				if (hasTarget)
				{
					// Posición de lanzamiento (igual que antes)
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

					// --- NUEVO CÁLCULO DE VELOCIDAD (inspirado en InvShooter) ---
					// Apuntar a la altura del torso del objetivo (como en InvShooter)
					Vector3 targetPos = target.ComponentBody.Position;
					float targetHeight = target.ComponentBody.StanceBoxSize.Y;
					Vector3 aimPoint = targetPos + new Vector3(0f, targetHeight * TargetHeightFactor, 0f);

					Vector3 direction = aimPoint - launchPosition;
					float distance = direction.Length();

					// Velocidad lineal que varía con la distancia (como en Throwable de InvShooter)
					float speed = MathUtils.Lerp(MinLaunchSpeed, MaxLaunchSpeed, distance / 20f); // Ajusta 20f según el rango deseado
					Vector3 velocity = Vector3.Normalize(direction) * speed + new Vector3(0f, VerticalBoost, 0f);
					// -------------------------------------------------------------

					// Seleccionar item a lanzar (rotación circular)
					int itemToLaunch = m_launchItemIndices[m_currentItemIndex];
					m_currentItemIndex = (m_currentItemIndex + 1) % m_launchItemIndices.Count;

					// Lanzar proyectil
					m_subsystemProjectiles.FireProjectile(
						itemToLaunch,
						launchPosition,
						velocity,
						Vector3.Zero,
						m_componentCreature
					);

					// Continuar animación
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
				}

				// Tiempo de recarga fijo 1.0 segundo
				m_ChargeTime = 1.0;
				m_nextUpdateTime = m_subsystemTime.GameTime + m_ChargeTime;
			}

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
		}

		private ComponentCreature GetCurrentTarget()
		{
			if (m_componentNewChaseBehavior2 != null && m_componentNewChaseBehavior2.Target != null)
				return m_componentNewChaseBehavior2.Target;

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

		public void ForceLaunch()
		{
			ComponentCreature target = GetCurrentTarget();
			if (!m_isLaunching && target != null)
			{
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

				// Mismo cálculo de velocidad que en Update
				Vector3 targetPos = target.ComponentBody.Position;
				float targetHeight = target.ComponentBody.StanceBoxSize.Y;
				Vector3 aimPoint = targetPos + new Vector3(0f, targetHeight * TargetHeightFactor, 0f);

				Vector3 direction = aimPoint - launchPosition;
				float distance = direction.Length();

				float speed = MathUtils.Lerp(MinLaunchSpeed, MaxLaunchSpeed, distance / 20f);
				Vector3 velocity = Vector3.Normalize(direction) * speed + new Vector3(0f, VerticalBoost, 0f);

				int itemToLaunch = m_launchItemIndices[m_currentItemIndex];
				m_currentItemIndex = (m_currentItemIndex + 1) % m_launchItemIndices.Count;

				m_subsystemProjectiles.FireProjectile(
					itemToLaunch,
					launchPosition,
					velocity,
					Vector3.Zero,
					m_componentCreature
				);
			}
		}

		public bool CanLaunch()
		{
			return !m_isLaunching &&
				   m_componentCreature != null &&
				   m_componentCreature.ComponentHealth.Health > 0f &&
				   GetCurrentTarget() != null;
		}

		public void SetAttackTarget(ComponentCreature target)
		{
			// Sin límites de distancia
			if (m_componentNewChaseBehavior2 != null)
			{
				m_componentNewChaseBehavior2.Attack(target, 1000f, 1000f, true);
			}
			else if (m_componentNewChaseBehavior != null)
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
