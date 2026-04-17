using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCrossbowShooterBehavior : ComponentBehavior, IUpdateable
	{
		// Componentes necesarios
		private ComponentCreature m_componentCreature;
		private ComponentChaseBehavior m_componentChaseBehavior;
		private ComponentInventory m_componentInventory;
		private SubsystemTime m_subsystemTime;
		private SubsystemProjectiles m_subsystemProjectiles;
		private SubsystemTerrain m_subsystemTerrain;
		private SubsystemBodies m_subsystemBodies;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentCreatureModel m_componentModel;
		private ComponentMiner m_componentMiner;

		// Configuración
		public float MaxDistance = 25f;
		public float AimTime = 0.5f;            // Tiempo de apuntado antes de disparar
		public float Cooldown = 0.8f;            // Tiempo de recarga tras disparo
		public float Accuracy = 0.02f;           // Dispersión (en radianes)
		public float BoltSpeed = 45f;             // Velocidad base del virote

		// Tipos de virotes disponibles
		public ArrowBlock.ArrowType[] AvailableBoltTypes = new ArrowBlock.ArrowType[]
		{
			ArrowBlock.ArrowType.IronBolt,
			ArrowBlock.ArrowType.DiamondBolt,
			ArrowBlock.ArrowType.ExplosiveBolt
		};

		// Estado
		private bool m_isAiming = false;
		private bool m_isFiring = false;
		private double m_aimStartTime;
		private double m_cooldownEndTime;
		private int m_crossbowSlot = -1;
		private Random m_random = new Random();
		private bool m_initialized = false;
		private ArrowBlock.ArrowType? m_nextBoltType = null;

		// Evento para eliminar los pickables de virotes
		private Action<Projectile> m_projectileAddedHandler;

		public int UpdateOrder => 0;
		public override float ImportanceLevel => 0.5f;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Cargar parámetros
			MaxDistance = valuesDictionary.GetValue<float>("MaxDistance", 25f);
			AimTime = valuesDictionary.GetValue<float>("AimTime", 0.5f);
			Cooldown = valuesDictionary.GetValue<float>("Cooldown", 0.8f);
			Accuracy = valuesDictionary.GetValue<float>("Accuracy", 0.02f);
			BoltSpeed = valuesDictionary.GetValue<float>("BoltSpeed", 45f);

			// Componentes
			m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			m_componentChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(true);
			m_componentInventory = base.Entity.FindComponent<ComponentInventory>(true);
			m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
			m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
			m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
			m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(false);
			m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
		}

		public override void OnEntityAdded()
		{
			base.OnEntityAdded();
			m_initialized = true;
			FindCrossbow();

			// Dejar la ballesta sin virote
			if (m_crossbowSlot >= 0)
				SetCrossbowDraw(0);
		}

		// -----------------------------------------------------------------
		// Suscripción para que los virotes desaparezcan al tocar el suelo
		// -----------------------------------------------------------------
		private void SubscribeToProjectileEvents()
		{
			if (m_subsystemProjectiles == null)
				return;

			if (m_projectileAddedHandler == null)
			{
				m_projectileAddedHandler = OnProjectileAdded;
				m_subsystemProjectiles.ProjectileAdded += m_projectileAddedHandler;
			}
		}

		private void UnsubscribeFromProjectileEvents()
		{
			if (m_subsystemProjectiles != null && m_projectileAddedHandler != null)
			{
				m_subsystemProjectiles.ProjectileAdded -= m_projectileAddedHandler;
				m_projectileAddedHandler = null;
			}
		}

		private void OnProjectileAdded(Projectile projectile)
		{
			if (projectile.Owner != m_componentCreature)
				return;

			int contents = Terrain.ExtractContents(projectile.Value);
			if (contents == ArrowBlock.Index)
			{
				projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
			}
		}

		// -----------------------------------------------------------------
		// Métodos auxiliares
		// -----------------------------------------------------------------
		private bool IsStuck() => m_componentPathfinding != null && m_componentPathfinding.IsStuck;

		private bool IsLineOfSightBlocked(ComponentCreature target)
		{
			if (target == null) return true;
			Vector3 start = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 end = target.ComponentCreatureModel.EyePosition;
			float distance = Vector3.Distance(start, end);
			if (distance <= 0.1f) return false;

			TerrainRaycastResult? terrainHit = m_subsystemTerrain.Raycast(start, end, true, true,
				(int value, float d) =>
				{
					int contents = Terrain.ExtractContents(value);
					Block block = BlocksManager.Blocks[contents];
					return block.IsCollidable_(value);
				});
			if (terrainHit != null && terrainHit.Value.Distance < distance) return true;

			BodyRaycastResult? bodyHit = m_subsystemBodies.Raycast(start, end, 0.35f,
				(ComponentBody body, float dist) =>
				{
					if (body == m_componentCreature.ComponentBody) return false;
					if (body == target.ComponentBody) return false;
					return true;
				});
			if (bodyHit != null && bodyHit.Value.Distance < distance) return true;

			return false;
		}

		private ComponentCreature GetChaseTarget()
		{
			if (m_componentChaseBehavior != null && m_componentChaseBehavior.Target != null)
				return m_componentChaseBehavior.Target;
			return null;
		}

		// -----------------------------------------------------------------
		// Gestión del inventario y ballesta
		// -----------------------------------------------------------------
		private void FindCrossbow()
		{
			for (int i = 0; i < m_componentInventory.SlotsCount; i++)
			{
				int slotValue = m_componentInventory.GetSlotValue(i);
				if (slotValue != 0)
				{
					Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
					if (block is CrossbowBlock)
					{
						m_crossbowSlot = i;
						m_componentInventory.ActiveSlotIndex = i;
						break;
					}
				}
			}
		}

		private void SetCrossbowDraw(int drawValue)
		{
			if (m_crossbowSlot < 0)
			{
				FindCrossbow();
				if (m_crossbowSlot < 0) return;
			}

			try
			{
				int currentCrossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
				if (currentCrossbowValue == 0) return;

				int currentData = Terrain.ExtractData(currentCrossbowValue);
				int newData = CrossbowBlock.SetDraw(currentData, MathUtils.Clamp(drawValue, 0, 15));
				int newCrossbowValue = Terrain.ReplaceData(currentCrossbowValue, newData);
				m_componentInventory.RemoveSlotItems(m_crossbowSlot, 1);
				m_componentInventory.AddSlotItems(m_crossbowSlot, newCrossbowValue, 1);
			}
			catch (Exception) { }
		}

		private void SetCrossbowLoaded(ArrowBlock.ArrowType boltType)
		{
			if (m_crossbowSlot < 0)
			{
				FindCrossbow();
				if (m_crossbowSlot < 0) return;
			}

			try
			{
				int currentCrossbowValue = m_componentInventory.GetSlotValue(m_crossbowSlot);
				if (currentCrossbowValue == 0) return;

				int currentData = Terrain.ExtractData(currentCrossbowValue);
				int newData = CrossbowBlock.SetDraw(currentData, 15);
				newData = CrossbowBlock.SetArrowType(newData, boltType);
				int newCrossbowValue = Terrain.ReplaceData(currentCrossbowValue, newData);
				m_componentInventory.RemoveSlotItems(m_crossbowSlot, 1);
				m_componentInventory.AddSlotItems(m_crossbowSlot, newCrossbowValue, 1);
			}
			catch (Exception) { }
		}

		// -----------------------------------------------------------------
		// Lógica de estado
		// -----------------------------------------------------------------
		public void Update(float dt)
		{
			if (!m_initialized || m_componentCreature.ComponentHealth.Health <= 0f)
				return;

			ComponentCreature target = GetChaseTarget();

			if (target == null || IsStuck() || IsLineOfSightBlocked(target))
			{
				ResetState();
				if (m_crossbowSlot >= 0)
					SetCrossbowDraw(0);
				return;
			}

			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				target.ComponentBody.Position
			);

			if (distance > MaxDistance)
			{
				ResetState();
				if (m_crossbowSlot >= 0)
					SetCrossbowDraw(0);
				return;
			}

			// Mirar al objetivo (solo visual)
			if (m_componentModel != null)
				m_componentModel.LookAtOrder = target.ComponentCreatureModel.EyePosition;

			// Máquina de estados
			if (!m_isAiming && !m_isFiring)
			{
				// Comprobar cooldown
				if (m_subsystemTime.GameTime >= m_cooldownEndTime)
				{
					StartAiming(target);
				}
			}

			if (m_isAiming)
			{
				// Mientras apunta, llamar a AimState.InProgress cada frame
				Ray3 ray = GetAimRay(target);
				m_componentMiner.Aim(ray, AimState.InProgress);

				if (m_subsystemTime.GameTime - m_aimStartTime >= AimTime)
				{
					// Terminar apuntado y disparar
					m_componentMiner.Aim(ray, AimState.Completed);
					m_isAiming = false;
					m_isFiring = true;
					m_cooldownEndTime = m_subsystemTime.GameTime + Cooldown;
				}
			}
			else if (m_isFiring)
			{
				// Pequeña pausa tras disparo
				if (m_subsystemTime.GameTime - (m_aimStartTime + AimTime) >= 0.1f)
				{
					m_isFiring = false;
					// Reiniciar para el siguiente ciclo
				}
			}
		}

		// -----------------------------------------------------------------
		// Métodos de apuntado mejorado (balística y predicción)
		// -----------------------------------------------------------------

		/// <summary>
		/// Resuelve la dirección necesaria para que un proyectil con velocidad inicial speed y gravedad gravity
		/// alcance el punto target desde el punto start.
		/// </summary>
		private Vector3 SolveBallisticDirection(Vector3 start, Vector3 target, float speed, float gravity)
		{
			Vector3 delta = target - start;
			float horizontalDist = new Vector2(delta.X, delta.Z).Length();
			float verticalDiff = delta.Y;

			if (horizontalDist < 0.01f)
			{
				// Si el objetivo está casi vertical, apuntar directamente
				return Vector3.Normalize(delta);
			}

			// Ecuación cuadrática para tan(theta)
			float a = gravity * horizontalDist;
			float b = speed * speed;
			float c = gravity * horizontalDist * horizontalDist + 2 * verticalDiff * speed * speed;
			float discriminant = b * b - gravity * c;

			if (discriminant < 0)
			{
				// No solución real, usar dirección directa
				return Vector3.Normalize(delta);
			}

			float sqrtDisc = MathF.Sqrt(discriminant);
			float tanTheta1 = (b + sqrtDisc) / a;
			float tanTheta2 = (b - sqrtDisc) / a;

			// Elegir el ángulo más bajo (trayectoria más plana)
			float tanTheta = MathF.Abs(tanTheta1) < MathF.Abs(tanTheta2) ? tanTheta1 : tanTheta2;
			float angle = MathF.Atan(tanTheta);

			// Dirección horizontal normalizada
			Vector3 horizontalDir = Vector3.Normalize(new Vector3(delta.X, 0, delta.Z));
			// Construir dirección final
			return horizontalDir * MathF.Cos(angle) + Vector3.UnitY * MathF.Sin(angle);
		}

		/// <summary>
		/// Calcula la dirección de apuntado considerando la gravedad, velocidad del proyectil y velocidad del objetivo.
		/// </summary>
		private Vector3 CalculateAimDirection(Vector3 start, Vector3 target, Vector3 targetVelocity, float speed, float gravity)
		{
			// Primer intento: dirección directa
			Vector3 aimDir = Vector3.Normalize(target - start);
			int iterations = 3;

			for (int i = 0; i < iterations; i++)
			{
				// Tiempo estimado de vuelo (distancia / velocidad)
				float time = Vector3.Distance(start, target) / speed;
				Vector3 predictedTarget = target + targetVelocity * time;

				aimDir = SolveBallisticDirection(start, predictedTarget, speed, gravity);
				if (aimDir == Vector3.Zero)
					break; // fallback
			}

			return aimDir;
		}

		private Ray3 GetAimRay(ComponentCreature target)
		{
			Vector3 eyePos = m_componentCreature.ComponentCreatureModel.EyePosition;
			Vector3 targetPos = target.ComponentCreatureModel.EyePosition;
			Vector3 targetVel = target.ComponentBody.Velocity;

			// Calcular dirección corregida
			Vector3 direction = CalculateAimDirection(eyePos, targetPos, targetVel, BoltSpeed, 10f);

			// Aplicar dispersión aleatoria (opcional, basada en Accuracy)
			if (Accuracy > 0f)
			{
				// Generar ángulos aleatorios (yaw y pitch) dentro del cono definido por Accuracy
				float yaw = m_random.Float(-Accuracy, Accuracy);
				float pitch = m_random.Float(-Accuracy, Accuracy);
				// Crear quaternion de rotación
				Quaternion rot = Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0);
				direction = Vector3.Transform(direction, rot);
				direction = Vector3.Normalize(direction);
			}

			return new Ray3(eyePos, direction);
		}

		private void StartAiming(ComponentCreature target)
		{
			// Seleccionar tipo de virote según distancia
			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				target.ComponentBody.Position
			);
			m_nextBoltType = SelectBoltTypeForDistance(distance);
			if (m_nextBoltType == null)
				return;

			// Cargar la ballesta (draw=15 y virote)
			SetCrossbowLoaded(m_nextBoltType.Value);
			// Asegurar que la ballesta está en el slot activo
			m_componentInventory.ActiveSlotIndex = m_crossbowSlot;

			m_isAiming = true;
			m_isFiring = false;
			m_aimStartTime = m_subsystemTime.GameTime;

			// Suscribirse a los eventos de proyectiles para que desaparezcan
			SubscribeToProjectileEvents();
		}

		private void ResetState()
		{
			m_isAiming = false;
			m_isFiring = false;
			m_nextBoltType = null;

			if (m_componentModel != null)
				m_componentModel.LookAtOrder = null;

			UnsubscribeFromProjectileEvents();
		}

		// -----------------------------------------------------------------
		// Selección de tipo de virote (igual que antes)
		// -----------------------------------------------------------------
		private ArrowBlock.ArrowType? SelectBoltTypeForDistance(float distance)
		{
			const float explosiveMinDistance = 20f;

			if (distance >= explosiveMinDistance)
			{
				if (AvailableBoltTypes.Length > 0)
				{
					int index = m_random.Int(0, AvailableBoltTypes.Length - 1);
					return AvailableBoltTypes[index];
				}
			}
			else
			{
				var nonExplosiveTypes = new List<ArrowBlock.ArrowType>();
				foreach (var boltType in AvailableBoltTypes)
				{
					if (boltType != ArrowBlock.ArrowType.ExplosiveBolt)
						nonExplosiveTypes.Add(boltType);
				}
				if (nonExplosiveTypes.Count > 0)
				{
					int index = m_random.Int(0, nonExplosiveTypes.Count - 1);
					return nonExplosiveTypes[index];
				}
			}
			return AvailableBoltTypes.Length > 0 ? AvailableBoltTypes[0] : (ArrowBlock.ArrowType?)null;
		}
	}
}
