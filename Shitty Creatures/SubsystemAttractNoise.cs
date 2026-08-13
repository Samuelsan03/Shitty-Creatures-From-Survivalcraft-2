using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemAttractNoise : Subsystem
	{
		// Genera un ruido de atracción en una posición estática (ej. una carnada tirada en el suelo)
		public void MakeLureNoise(Vector3 position, float lureStrength, float range)
		{
			this.ProcessLureNoise(null, position, lureStrength, range);
		}

		// Genera un ruido de atracción desde un cuerpo en movimiento (ej. un señuelo activo)
		public void MakeLureNoise(ComponentBody sourceBody, float lureStrength, float range)
		{
			this.ProcessLureNoise(sourceBody, sourceBody.Position, lureStrength, range);
		}

		// Carga de dependencias del motor
		public override void Load(ValuesDictionary valuesDictionary)
		{
			this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
		}

		// Lógica interna que busca a las entidades cercanas y les avisa
		public void ProcessLureNoise(ComponentBody sourceBody, Vector3 position, float lureStrength, float range)
		{
			float num = range * range;
			this.m_componentBodies.Clear();

			// Busca cuerpos en un radio 2D (X, Z) para optimizar rendimiento
			this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), range, this.m_componentBodies);

			for (int i = 0; i < this.m_componentBodies.Count; i++)
			{
				ComponentBody componentBody = this.m_componentBodies.Array[i];

				// Verifica que no sea el que generó el ruido y que esté dentro del radio 3D real
				if (componentBody != sourceBody && Vector3.DistanceSquared(componentBody.Position, position) < num)
				{
					// Busca si este cuerpo tiene el componente de atracción
					foreach (INoiseAttractListener attractListener in componentBody.Entity.FindComponents<INoiseAttractListener>())
					{
						attractListener.AttractedToNoise(sourceBody, position, lureStrength);
					}
				}
			}
		}

		// Variables de caché para no crear basura (Garbage Collection) en cada llamada
		public SubsystemBodies m_subsystemBodies;
		public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
	}
}
