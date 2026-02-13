using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentTankLauncherBehavior : ComponentBehavior, IUpdateable
	{
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

		public override float ImportanceLevel
		{
			get { return 0f; }
		}

		public UpdateOrder UpdateOrder
		{
			get { return UpdateOrder.Default; }
		}

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

					// Verificar si tiene formato "NombreBloque:Variante" (ejemplo: ArrowBlock:4)
					if (trimmedItem.Contains(":"))
					{
						string[] parts = trimmedItem.Split(':');
						string blockName = parts[0].Trim();
						string variantStr = parts[1].Trim();

						int blockIndex = BlocksManager.GetBlockIndex(blockName, false);
						if (blockIndex >= 0)
						{
							// Intentar parsear la variante
							if (int.TryParse(variantStr, out int variant))
							{
								// Crear valor de bloque con variante
								int blockValue = Terrain.MakeBlockValue(blockIndex, 0, variant);
								m_launchItemIndices.Add(blockValue);
							}
							else
							{
								// Si no se puede parsear, usar el bloque sin variante
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
						// Es solo el nombre del bloque sin variante
						if (int.TryParse(trimmedItem, out int directIndex))
						{
							// Es un índice directo
							m_launchItemIndices.Add(directIndex);
						}
						else
						{
							// Es un nombre de bloque
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

			// Si no hay items válidos, usar valor por defecto
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
					// POSICIÓN EXACTA DEL CÓDIGO CHINO
					Vector3 launchPosition = m_componentCreature.ComponentCreatureModel.EyePosition
						+ m_componentCreature.ComponentBody.Matrix.Right * 0.3f
						- m_componentCreature.ComponentBody.Matrix.Up * 0.2f
						+ m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;

					// ANIMACIÓN DE MANOS EXACTA DEL CÓDIGO CHINO
					var componentHumanModel = m_componentCreature.ComponentCreatureModel as ComponentHumanModel;
					if (componentHumanModel != null)
					{
						componentHumanModel.m_handAngles2 = new Vector2(4f, -5f);
						componentHumanModel.m_handAngles1 = new Vector2(4f, 3f);
						m_isLaunching = true;
						m_launchAnimationTimer = 0f;
					}

					// CÁLCULO DE VELOCIDAD - COMBINACIÓN INVSHOOTER + DAYZMOD
					Vector3 targetPos = target.ComponentBody.Position;
					Vector3 direction = targetPos - launchPosition;
					m_distance = direction.Length();

					// NORMALIZAR la dirección (crucial para apuntar)
					Vector3 normalizedDirection = Vector3.Normalize(direction);

					// CALCULAR VELOCIDAD BASE (como InvShooter)
					float baseSpeed = 30f;

					// Factor de distancia (como InvShooter)
					float distanceFactor = MathUtils.Clamp(m_distance / 12f, 0.6f, 1.8f);
					float speed = baseSpeed * distanceFactor;

					// ALTURA INICIAL (como InvShooter) - para compensar gravedad
					float verticalBoost = MathUtils.Lerp(1f, 3f, m_distance / 25f);

					// VELOCIDAD FINAL - dirección * velocidad + componente vertical
					Vector3 velocity = normalizedDirection * speed + new Vector3(0f, verticalBoost, 0f);

					// SELECCIONAR ITEM A LANZAR (rotación circular)
					int itemToLaunch = m_launchItemIndices[m_currentItemIndex];
					m_currentItemIndex = (m_currentItemIndex + 1) % m_launchItemIndices.Count;

					// LANZAR EL PROYECTIL
					m_subsystemProjectiles.FireProjectile(
						itemToLaunch,
						launchPosition,
						velocity,
						Vector3.Zero,
						m_componentCreature
					);

					// Fin de animación
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

				// TIEMPO DE RECARGA FIJO 1.0 SEGUNDO
				m_ChargeTime = 1.0;
				m_nextUpdateTime = m_subsystemTime.GameTime + m_ChargeTime;
			}

			// Si está en animación, continuar
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
			// Prioridad: ComponentNewChaseBehavior2 (nuevo)
			if (m_componentNewChaseBehavior2 != null && m_componentNewChaseBehavior2.Target != null)
				return m_componentNewChaseBehavior2.Target;

			// Prioridad: ComponentNewChaseBehavior (nuevo)
			if (m_componentNewChaseBehavior != null && m_componentNewChaseBehavior.Target != null)
				return m_componentNewChaseBehavior.Target;

			// Prioridad: ComponentZombieChaseBehavior (zombie)
			if (m_componentZombieChaseBehavior != null && m_componentZombieChaseBehavior.Target != null)
				return m_componentZombieChaseBehavior.Target;

			// Prioridad: ComponentChaseBehavior (original)
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

				Vector3 targetPos = target.ComponentBody.Position;
				Vector3 direction = targetPos - launchPosition;
				float distance = direction.Length();

				// MISMA FÓRMULA QUE UPDATE
				Vector3 normalizedDirection = Vector3.Normalize(direction);
				float baseSpeed = 30f;
				float distanceFactor = MathUtils.Clamp(distance / 12f, 0.6f, 1.8f);
				float speed = baseSpeed * distanceFactor;
				float verticalBoost = MathUtils.Lerp(1f, 3f, distance / 25f);
				Vector3 velocity = normalizedDirection * speed + new Vector3(0f, verticalBoost, 0f);

				// Seleccionar item (rotación circular)
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
			// SIN LÍMITES DE DISTANCIA
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
