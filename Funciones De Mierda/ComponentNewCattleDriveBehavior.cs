using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewCattleDriveBehavior : ComponentBehavior, IUpdateable, INoiseListener
	{
		// Campos para compatibilidad con ambos tipos de manada
		public SubsystemTime m_subsystemTime;
		public SubsystemCreatureSpawn m_subsystemCreatureSpawn;
		public ComponentCreature m_componentCreature;
		public ComponentPathfinding m_componentPathfinding;
		public ComponentHerdBehavior m_componentHerdBehavior; // Original
		public ComponentNewHerdBehavior m_componentNewHerdBehavior; // Nuevo

		public StateMachine m_stateMachine = new StateMachine();
		public Random m_random = new Random();
		public float m_importanceLevel;
		public Vector3 m_driveVector;

		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override float ImportanceLevel
		{
			get
			{
				return this.m_importanceLevel;
			}
		}

		public void HearNoise(ComponentBody sourceBody, Vector3 sourcePosition, float loudness)
		{
			if (loudness >= 0.5f)
			{
				Vector3 v = this.m_componentCreature.ComponentBody.Position - sourcePosition;
				this.m_driveVector += Vector3.Normalize(v) * MathUtils.Max(8f - 0.25f * v.Length(), 1f);
				float num = 12f + this.m_random.Float(0f, 3f);
				if (this.m_driveVector.Length() > num)
				{
					this.m_driveVector = num * Vector3.Normalize(this.m_driveVector);
				}
			}
		}

		public void Update(float dt)
		{
			this.m_stateMachine.Update();
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
			this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
			this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);

			// Intentar obtener ambos tipos de componentes de manada
			this.m_componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>(false); // No lanzar error si no existe
			this.m_componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>(false); // No lanzar error si no existe

			// Verificar que al menos uno de los dos exista
			if (this.m_componentHerdBehavior == null && this.m_componentNewHerdBehavior == null)
			{
				throw new Exception("Required component ComponentHerdBehavior or ComponentNewHerdBehavior does not exist in entity.");
			}

			// Configurar la máquina de estados
			this.m_stateMachine.AddState("Inactive", delegate
			{
				this.m_importanceLevel = 0f;
				this.m_driveVector = Vector3.Zero;
			}, delegate
			{
				if (this.IsActive)
				{
					this.m_stateMachine.TransitionTo("Drive");
				}
				if (this.m_driveVector.Length() > 3f)
				{
					this.m_importanceLevel = 7f;
				}
				this.FadeDriveVector();
			}, null);

			this.m_stateMachine.AddState("Drive", delegate
			{
			}, delegate
			{
				if (!this.IsActive)
				{
					this.m_stateMachine.TransitionTo("Inactive");
				}
				if (this.m_driveVector.LengthSquared() < 1f || this.m_componentPathfinding.IsStuck)
				{
					this.m_importanceLevel = 0f;
				}
				if (this.m_random.Float(0f, 1f) < 0.1f * this.m_subsystemTime.GameTimeDelta)
				{
					this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(true);
				}
				if (this.m_random.Float(0f, 1f) < 3f * this.m_subsystemTime.GameTimeDelta)
				{
					Vector3 v = this.CalculateDriveDirectionAndSpeed();
					float speed = MathUtils.Saturate(0.2f * v.Length());
					this.m_componentPathfinding.SetDestination(new Vector3?(this.m_componentCreature.ComponentBody.Position + 15f * Vector3.Normalize(v)), speed, 5f, 0, false, true, false, null);
				}
				this.FadeDriveVector();
			}, null);

			this.m_stateMachine.TransitionTo("Inactive");
		}

		public void FadeDriveVector()
		{
			float num = this.m_driveVector.Length();
			if (num > 0.1f)
			{
				this.m_driveVector -= this.m_subsystemTime.GameTimeDelta * this.m_driveVector / num;
			}
		}

		public Vector3 CalculateDriveDirectionAndSpeed()
		{
			int num = 1;
			Vector3 position = this.m_componentCreature.ComponentBody.Position;
			Vector3 vector = position;
			Vector3 vector2 = this.m_driveVector;

			// Obtener el nombre de la manada del componente que exista
			string herdName = this.GetHerdName();

			if (string.IsNullOrEmpty(herdName))
			{
				return this.m_driveVector; // Sin manada, solo usar el vector de conducción
			}

			foreach (ComponentCreature componentCreature in this.m_subsystemCreatureSpawn.Creatures)
			{
				if (componentCreature != this.m_componentCreature && componentCreature.ComponentHealth.Health > 0f)
				{
					// Verificar ambos tipos de comportamiento de manada
					ComponentNewCattleDriveBehavior otherCattleDrive = componentCreature.Entity.FindComponent<ComponentNewCattleDriveBehavior>();
					ComponentCattleDriveBehavior originalCattleDrive = componentCreature.Entity.FindComponent<ComponentCattleDriveBehavior>();

					string otherHerdName = null;

					if (otherCattleDrive != null)
					{
						otherHerdName = otherCattleDrive.GetHerdName();
					}
					else if (originalCattleDrive != null && originalCattleDrive.m_componentHerdBehavior != null)
					{
						otherHerdName = originalCattleDrive.m_componentHerdBehavior.HerdName;
					}

					// Si son de la misma manada
					if (!string.IsNullOrEmpty(otherHerdName) && herdName.Equals(otherHerdName, StringComparison.OrdinalIgnoreCase))
					{
						Vector3 position2 = componentCreature.ComponentBody.Position;
						if (Vector3.DistanceSquared(position, position2) < 625f)
						{
							vector += position2;

							// Obtener el vector de conducción del otro
							Vector3 otherDriveVector = Vector3.Zero;
							if (otherCattleDrive != null)
							{
								otherDriveVector = otherCattleDrive.m_driveVector;
							}
							else if (originalCattleDrive != null)
							{
								otherDriveVector = originalCattleDrive.m_driveVector;
							}

							vector2 += otherDriveVector;
							num++;
						}
					}
				}
			}

			vector /= (float)num;
			vector2 /= (float)num;
			Vector3 v = vector - position;
			float s = MathUtils.Max(1.5f * v.Length() - 3f, 0f);
			return 0.33f * this.m_driveVector + 0.66f * vector2 + s * Vector3.Normalize(v);
		}

		// Método para obtener el nombre de la manada de cualquier componente disponible
		private string GetHerdName()
		{
			if (this.m_componentNewHerdBehavior != null && !string.IsNullOrEmpty(this.m_componentNewHerdBehavior.HerdName))
			{
				return this.m_componentNewHerdBehavior.HerdName;
			}
			else if (this.m_componentHerdBehavior != null && !string.IsNullOrEmpty(this.m_componentHerdBehavior.HerdName))
			{
				return this.m_componentHerdBehavior.HerdName;
			}

			return null;
		}

		// Método para verificar si una criatura es de la misma manada
		public bool IsSameHerd(ComponentCreature otherCreature)
		{
			string ourHerdName = this.GetHerdName();
			if (string.IsNullOrEmpty(ourHerdName) || otherCreature == null)
			{
				return false;
			}

			// Verificar en el nuevo componente
			ComponentNewHerdBehavior otherNewHerd = otherCreature.Entity.FindComponent<ComponentNewHerdBehavior>();
			if (otherNewHerd != null && !string.IsNullOrEmpty(otherNewHerd.HerdName))
			{
				return ourHerdName.Equals(otherNewHerd.HerdName, StringComparison.OrdinalIgnoreCase);
			}

			// Verificar en el componente original
			ComponentHerdBehavior otherOriginalHerd = otherCreature.Entity.FindComponent<ComponentHerdBehavior>();
			if (otherOriginalHerd != null && !string.IsNullOrEmpty(otherOriginalHerd.HerdName))
			{
				return ourHerdName.Equals(otherOriginalHerd.HerdName, StringComparison.OrdinalIgnoreCase);
			}

			return false;
		}
	}
}
