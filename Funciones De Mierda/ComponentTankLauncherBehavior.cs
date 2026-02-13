using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentTankLauncherBehavior : ComponentBehavior, IUpdateable
	{
		// Variable para el item a lanzar
		public string m_itemsToLaunch = "";
		public int m_launchItemIndex = 0;

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
			m_componentTankModel = base.Entity.FindComponent<ComponentTankModel>(true);

			m_componentZombieChaseBehavior = base.Entity.FindComponent<ComponentZombieChaseBehavior>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentNewChaseBehavior = base.Entity.FindComponent<ComponentNewChaseBehavior>(false);
			m_componentNewChaseBehavior2 = base.Entity.FindComponent<ComponentNewChaseBehavior2>(false);

			m_itemsToLaunch = valuesDictionary.GetValue<string>("ItemsToLaunch", "");

			if (!string.IsNullOrEmpty(m_itemsToLaunch))
			{
				if (int.TryParse(m_itemsToLaunch, out int index))
				{
					m_launchItemIndex = index;
				}
				else
				{
					m_launchItemIndex = BlocksManager.GetBlockIndex(m_itemsToLaunch, false);
				}
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

					// ¡¡¡FÓRMULA EXACTA DEL CÓDIGO CHINO QUE SÍ FUNCIONA!!!
					Vector3 targetPos = target.ComponentBody.Position;
					Vector3 v = targetPos - launchPosition;

					// Calcular distancia
					m_distance = v.Length();

					// Calcular dirección NORMALIZADA (esto es clave)
					Vector3 v3 = Vector3.Normalize(v + m_random.Vector3((m_distance < 10f) ? 0f : 1f));

					// Calcular velocidad como el código chino
					float num = MathUtils.Lerp(0f, 40f, MathUtils.Pow((float)m_ChargeTime / 2f, 0.5f));

					// ¡¡¡FÓRMULA CRÍTICA!!! Exactamente como el código chino que funciona
					// v3 * num + new Vector3(1f, m_random.Float(5f, 20f) * m_distance / num, 0f)
					Vector3 velocity = v3 * num + new Vector3(0f, m_random.Float(5f, 20f) * m_distance / Math.Max(num, 0.1f), 0f);

					// ¡¡¡IMPORTANTE!!! Para distancias largas, ajustar la componente Y
					if (m_distance > 15f)
					{
						// Reducir el componente Y para distancias largas
						float distanceFactor = MathUtils.Saturate((m_distance - 15f) / 30f);
						velocity.Y *= MathUtils.Lerp(1f, 0.5f, distanceFactor);
					}

					// LANZAR EL PROYECTIL
					m_subsystemProjectiles.FireProjectile(
						m_launchItemIndex,
						launchPosition,
						velocity,
						Vector3.Zero,  // Sin rotación angular para que no gire
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

				// TIEMPO DE RECARGA
				m_ChargeTime = (double)m_random.Float(2f, 3f);

				bool isClose = m_distance < 10f;
				if (isClose)
				{
					m_ChargeTime *= 1.0;
				}

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
			// Verificar ComponentNewChaseBehavior2 primero (prioridad)
			if (m_componentNewChaseBehavior2 != null && m_componentNewChaseBehavior2.Target != null)
				return m_componentNewChaseBehavior2.Target;

			// Luego ComponentNewChaseBehavior
			if (m_componentNewChaseBehavior != null && m_componentNewChaseBehavior.Target != null)
				return m_componentNewChaseBehavior.Target;

			// Luego ComponentZombieChaseBehavior
			if (m_componentZombieChaseBehavior != null && m_componentZombieChaseBehavior.Target != null)
				return m_componentZombieChaseBehavior.Target;

			// Finalmente ComponentChaseBehavior original
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
				Vector3 v = targetPos - launchPosition;
				float distance = v.Length();

				// Usar fórmula simple para ForceLaunch
				Vector3 direction = Vector3.Normalize(v);
				float speed = 30f;
				Vector3 velocity = direction * speed;

				// Añadir componente vertical basado en distancia
				float verticalBoost = MathUtils.Lerp(3f, 8f, MathUtils.Saturate(distance / 30f));
				velocity.Y += verticalBoost;

				m_subsystemProjectiles.FireProjectile(
					m_launchItemIndex,
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
			// Intentar con los nuevos comportamientos primero
			if (m_componentNewChaseBehavior2 != null)
			{
				m_componentNewChaseBehavior2.Attack(target, 30f, 60f, true);
			}
			else if (m_componentNewChaseBehavior != null)
			{
				m_componentNewChaseBehavior.Attack(target, 30f, 60f, true);
			}
			else if (m_componentZombieChaseBehavior != null)
			{
				m_componentZombieChaseBehavior.Attack(target, 30f, 60f, true);
			}
			else if (m_componentChaseBehavior != null)
			{
				m_componentChaseBehavior.Attack(target, 30f, 60f, true);
			}
		}
	}
}
