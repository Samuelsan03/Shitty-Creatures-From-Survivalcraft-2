using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNoiseAttraction : ComponentBehavior, INoiseAttractListener, IUpdateable
	{
		// ComponentBehavior requirements
		public override float ImportanceLevel { get { return 10f; } }

		// Solo es true DURANTE un evento de ruido activo. Fuera de él, la IA
		// (HandleMountedCombat, persecuciones, etc.) corre exactamente como siempre.
		public bool IsInvestigatingNoise
		{
			get
			{
				return m_noisePosition.HasValue && m_stateMachine.CurrentState != "Idle";
			}
		}

		// OPCIONAL: ponlo en false si algún día quieres que la persecución montada
		// gane al ruido (el ruido no la interrumpirá). Por defecto el ruido SÍ interrumpe.
		public bool NoiseInterruptsChase = true;

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Dependencies
		private SubsystemTime m_subsystemTime;
		private SubsystemAttractNoise m_subsystemAttractNoise;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;
		private ComponentBody m_componentBody;
		private ComponentRider m_componentRider;
		private ComponentZombieChaseBehavior m_componentChaseBehavior;

		// State Machine
		private StateMachine m_stateMachine = new StateMachine();

		// Logic Variables
		private Vector3? m_noisePosition;
		private double m_investigationEndTime;
		private float m_investigationDuration = 4f; // Seconds to look around
		private float m_attractRange = 20f;

		// =====================================================
		// Variables de montura (SOLO se usan durante un evento de ruido)
		// =====================================================
		private bool m_wasMounted;
		private double m_mountedTravelEndTime;
		private float m_mountedStuckTime;
		private double m_nextMountedSpeedOrderTime;
		private double m_mountedStopEndTime;
		private double m_nextMountedBrakeTime;
		private int m_mountedBrakeCount;
		private ComponentMount m_cachedMount;
		private ComponentLocomotion m_mountLocomotion;

		private float m_mountedMaxTravelTime = 25f;      // Segundos máx. de viaje al ruido
		private float m_mountedArriveDistanceSq = 9f;    // Distancia² de llegada (3 m)
		private float m_mountedSlowDownDistanceSq = 49f; // Distancia² para reducir (7 m)
		private float m_mountedTurnSign = 1f;            // Verificado con Vector2.Angle (como ComponentPilot)

		// Load method: No dictionaries for values, just normal initialization
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Find dependencies
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemAttractNoise = Project.FindSubsystem<SubsystemAttractNoise>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);
			m_componentBody = m_componentCreature.ComponentBody;
			m_componentRider = Entity.FindComponent<ComponentRider>(false);
			m_componentChaseBehavior = Entity.FindComponent<ComponentZombieChaseBehavior>(false);

			// --- Define States ---

			// State: Idle (Waiting for noise) — ORIGINAL, SIN TOCAR LA MONTURA NUNCA
			m_stateMachine.AddState("Idle", null, delegate
			{
				// If we have a target, start moving
				if (m_noisePosition.HasValue)
				{
					m_stateMachine.TransitionTo("Attracted");
				}
			}, null);

			// State: Attracted (Moving towards the noise)
			m_stateMachine.AddState("Attracted", delegate
			{
				if (m_noisePosition.HasValue)
				{
					// Montado -> conducir la MONTURA (solo durante este evento de ruido)
					if (IsMounted())
					{
						m_wasMounted = true;
						m_componentPathfinding.Stop();
						m_mountedTravelEndTime = m_subsystemTime.GameTime + m_mountedMaxTravelTime;
						m_mountedStuckTime = 0f;
						m_nextMountedSpeedOrderTime = m_subsystemTime.GameTime;
						m_nextMountedBrakeTime = m_subsystemTime.GameTime;

						ComponentSteedBehavior steed = GetMountSteedBehavior();
						if (steed != null)
						{
							steed.SpeedOrder = 1;
						}
					}
					else
					{
						m_wasMounted = false;

						// Start moving to the source (ORIGINAL)
						float speed = m_componentCreature.ComponentLocomotion.WalkSpeed;
						m_componentPathfinding.SetDestination(
							m_noisePosition,
							speed,
							0.5f,
							100, // Max pathfinding nodes
							false,
							false,
							true, // Raycast destination
							null
						);
					}
				}
				else
				{
					m_stateMachine.TransitionTo("Idle");
				}
			}, delegate
			{
				if (m_noisePosition.HasValue)
				{
					if (IsMounted())
					{
						m_wasMounted = true;
						UpdateMountedMovementToNoise();
					}
					else
					{
						// Se desmontó a mitad de camino -> retomar a pie
						if (m_wasMounted)
						{
							m_wasMounted = false;
							float speed = m_componentCreature.ComponentLocomotion.WalkSpeed;
							m_componentPathfinding.SetDestination(
								m_noisePosition,
								speed,
								0.5f,
								100,
								false,
								false,
								true,
								null
							);
						}

						// ORIGINAL: comprobación de llegada / atasco
						float distanceSquared = Vector3.DistanceSquared(m_componentBody.Position, m_noisePosition.Value);

						if (distanceSquared < 2f || m_componentPathfinding.Destination == null || m_componentPathfinding.IsStuck)
						{
							m_stateMachine.TransitionTo("Investigating");
						}
					}
				}
				else
				{
					m_componentPathfinding.Stop();
					m_stateMachine.TransitionTo("Idle");
				}
			}, delegate
			{
				// Al salir de "Attracted": preparar frenado (lo ejecuta "Investigating")
				BeginMountedStop();
			});

			// State: Investigating (Looking around at the source) — ORIGINAL + frenar montura
			// (aquí es seguro frenar: durante este estado la IA está en modo ruido
			//  y HandleMountedCombat no emite órdenes)
			m_stateMachine.AddState("Investigating", delegate
			{
				// Stop moving and set timer (ORIGINAL)
				m_componentPathfinding.Stop();
				m_investigationEndTime = m_subsystemTime.GameTime + m_investigationDuration;
			}, delegate
			{
				// Mientras investigamos montados, mantener la montura quieta
				if (IsMounted())
				{
					HoldMountedStill();
				}

				// Check if time is up
				if (m_subsystemTime.GameTime >= m_investigationEndTime)
				{
					// Clear the target and go back to idle
					m_noisePosition = null;
					m_stateMachine.TransitionTo("Idle");
				}
			}, null);

			// Start state
			m_stateMachine.TransitionTo("Idle");
		}

		// Implementation of INoiseAttractListener
		public void AttractedToNoise(ComponentBody sourceBody, Vector3 sourcePosition, float lureStrength)
		{
			// Si esta criatura ES una montura y alguien la monta, ignorar (conduce el jinete)
			if (IsBeingRidden())
			{
				return;
			}

			// Opcional (por defecto desactivado): la persecución gana al ruido
			if (!NoiseInterruptsChase && IsMounted() && RiderIsPursuing())
			{
				return;
			}

			// Set the target (ORIGINAL)
			m_noisePosition = sourcePosition;

			// Refrescar tiempo de viaje si ya íbamos montados a otro ruido
			if (IsMounted() && m_stateMachine.CurrentState == "Attracted")
			{
				m_mountedTravelEndTime = m_subsystemTime.GameTime + m_mountedMaxTravelTime;
				m_mountedStuckTime = 0f;
			}

			// If we are doing nothing, start moving immediately
			if (m_stateMachine.CurrentState == "Idle")
			{
				m_stateMachine.TransitionTo("Attracted");
			}
			// If we are already investigating but hear a new noise, interrupt and go to it
			else if (m_stateMachine.CurrentState == "Investigating")
			{
				m_stateMachine.TransitionTo("Attracted");
			}
			// If already Attracted, the Update loop will recalculate path automatically
		}

		// IUpdateable implementation
		public void Update(float dt)
		{
			m_stateMachine.Update();
		}

		// =====================================================
		// Utilidades
		// =====================================================

		private bool IsMounted()
		{
			return m_componentRider != null && m_componentRider.Mount != null;
		}

		private bool IsBeingRidden()
		{
			ComponentMount mount = Entity.FindComponent<ComponentMount>(false);
			return mount != null && mount.Rider != null;
		}

		// ¿El jinete tiene un objetivo de caza vivo? (solo para el interruptor opcional)
		private bool RiderIsPursuing()
		{
			if (m_componentChaseBehavior == null)
			{
				return false;
			}
			ComponentCreature target = m_componentChaseBehavior.Target;
			return target != null && target.ComponentHealth != null && target.ComponentHealth.Health > 0f;
		}

		private ComponentSteedBehavior GetMountSteedBehavior()
		{
			ComponentMount mount = (m_componentRider != null) ? m_componentRider.Mount : null;
			if (mount == null || mount.Entity == null)
			{
				return null;
			}
			return mount.Entity.FindComponent<ComponentSteedBehavior>(false);
		}

		private float GetMountWalkSpeed()
		{
			ComponentMount mount = (m_componentRider != null) ? m_componentRider.Mount : null;
			if (mount == null)
			{
				return 4f;
			}
			if (m_cachedMount != mount || m_mountLocomotion == null)
			{
				m_cachedMount = mount;
				m_mountLocomotion = mount.Entity.FindComponent<ComponentLocomotion>(false);
			}
			return (m_mountLocomotion != null) ? m_mountLocomotion.WalkSpeed : 4f;
		}

		private void BeginMountedStop()
		{
			if (IsMounted())
			{
				m_mountedStopEndTime = m_subsystemTime.GameTime + 2.0;
				m_mountedBrakeCount = 0;
				m_nextMountedBrakeTime = m_subsystemTime.GameTime + 0.15;
			}
		}

		private void HoldMountedStill()
		{
			ComponentSteedBehavior steed = GetMountSteedBehavior();
			if (steed == null)
			{
				return;
			}
			steed.TurnOrder = 0f;
			steed.JumpOrder = 0f;
			ComponentZombieSteedBehavior zombieSteed = steed as ComponentZombieSteedBehavior;
			if (zombieSteed != null)
			{
				zombieSteed.ExternalVerticalInput = 0f;
			}
			ComponentBody mountBody = m_componentRider.Mount.ComponentBody;
			float horizontalSpeed = mountBody.Velocity.XZ.Length();
			if (horizontalSpeed > 0.5f
				&& m_mountedBrakeCount < 3
				&& m_subsystemTime.GameTime >= m_nextMountedBrakeTime
				&& m_subsystemTime.GameTime < m_mountedStopEndTime)
			{
				steed.SpeedOrder = -1;
				m_mountedBrakeCount++;
				m_nextMountedBrakeTime = m_subsystemTime.GameTime + 0.5;
			}
		}

		// Conduce la montura al ruido a VELOCIDAD ALTA (solo durante el evento de ruido)
		private void UpdateMountedMovementToNoise()
		{
			ComponentMount mount = m_componentRider.Mount;
			ComponentBody mountBody = mount.ComponentBody;
			Vector3 noisePos = m_noisePosition.Value;

			float distanceSquared = Vector3.DistanceSquared(mountBody.Position, noisePos);

			if (distanceSquared < m_mountedArriveDistanceSq
				|| m_subsystemTime.GameTime >= m_mountedTravelEndTime
				|| m_mountedStuckTime > 4f)
			{
				m_stateMachine.TransitionTo("Investigating");
				return;
			}

			ComponentSteedBehavior steed = GetMountSteedBehavior();
			if (steed == null)
			{
				m_stateMachine.TransitionTo("Investigating");
				return;
			}

			// ---------- Dirección horizontal ----------
			Vector3 toNoise = noisePos - mountBody.Position;
			Vector2 flat = toNoise.XZ;
			bool hasHorizontalDirection = flat.LengthSquared() > 0.01f;

			float deltaAngle = 0f;
			if (hasHorizontalDirection)
			{
				Vector2 flatForward = mountBody.Matrix.Forward.XZ;
				if (flatForward.LengthSquared() < 0.01f)
				{
					flatForward = Vector2.UnitY;
				}
				deltaAngle = Vector2.Angle(flatForward, flat);
				steed.TurnOrder = m_mountedTurnSign * Math.Clamp(deltaAngle, -0.5f, 0.5f);
			}

			// ---------- Control vertical (monturas voladoras) ----------
			float heightDiff = noisePos.Y - mountBody.Position.Y;
			ComponentZombieSteedBehavior zombieSteed = steed as ComponentZombieSteedBehavior;
			if (zombieSteed != null)
			{
				zombieSteed.ExternalVerticalInput = Math.Clamp(heightDiff / 8f, -1f, 1f);
			}
			else if (heightDiff > 1.2f && mountBody.StandingOnValue != null
				&& hasHorizontalDirection && MathF.Abs(deltaAngle) < 0.5f)
			{
				steed.JumpOrder = 1f;
			}

			// ---------- Velocidad ----------
			float walkSpeed = GetMountWalkSpeed();
			float horizontalSpeed = mountBody.Velocity.XZ.Length();

			if (!hasHorizontalDirection)
			{
				if (m_subsystemTime.GameTime >= m_nextMountedBrakeTime)
				{
					steed.SpeedOrder = -1;
					m_nextMountedBrakeTime = m_subsystemTime.GameTime + 0.5;
				}
			}
			else if (distanceSquared > m_mountedSlowDownDistanceSq)
			{
				// Lejos: acelerar cada frame (como mantener W) -> GALOPE
				steed.SpeedOrder = 1;
			}
			else
			{
				// Cerca: llegar controlado
				if (horizontalSpeed < 0.45f * walkSpeed)
				{
					if (m_subsystemTime.GameTime >= m_nextMountedSpeedOrderTime)
					{
						steed.SpeedOrder = 1;
						m_nextMountedSpeedOrderTime = m_subsystemTime.GameTime + 0.5;
					}
				}
				else if (horizontalSpeed > 0.75f * walkSpeed
					&& m_subsystemTime.GameTime >= m_nextMountedBrakeTime)
				{
					steed.SpeedOrder = -1;
					m_nextMountedBrakeTime = m_subsystemTime.GameTime + 0.5;
				}
			}

			// ---------- Atasco ----------
			if (mountBody.Velocity.Length() > 0.15f * walkSpeed)
			{
				m_mountedStuckTime = 0f;
			}
			else
			{
				m_mountedStuckTime += m_subsystemTime.GameTimeDelta;
			}
		}
	}
}
