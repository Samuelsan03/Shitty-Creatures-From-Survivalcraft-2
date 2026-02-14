using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Comportamiento de huida para zombis que ANULA la huida genérica.
	/// Los zombis NUNCA huyen cuando tienen poca vida, siguen atacando hasta morir.
	/// La única huida posible es la gestionada por ComponentZombieChaseBehavior (estado Fleeing)
	/// cuando es atacado por un miembro de su misma manada.
	/// </summary>
	public class ComponentZombieRunAwayBehavior : ComponentBehavior, IUpdateable, IComponentEscapeBehavior
	{
		// Propiedad requerida por IComponentEscapeBehavior
		public float LowHealthToEscape { get; set; }

		// Propiedades requeridas por IUpdateable
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// Nivel de importancia: SIEMPRE 0 para que este comportamiento NUNCA se active
		public override float ImportanceLevel => 0f;

		// Referencias a componentes (necesarias aunque no se usen)
		private SubsystemTime m_subsystemTime;
		private ComponentCreature m_componentCreature;
		private ComponentPathfinding m_componentPathfinding;

		// Método de la interfaz IComponentEscapeBehavior - NO HACE NADA
		public void RunAwayFrom(ComponentBody componentBody) { }

		// Método de actualización - VACÍO
		public void Update(float dt) { }

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			// Cargar LowHealthToEscape desde el XML (aunque no lo usaremos)
			LowHealthToEscape = valuesDictionary.GetValue<float>("LowHealthToEscape", 0.2f);

			// Cargar referencias mínimas para evitar errores de nulidad
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true);

			// IMPORTANTE: No añadimos ningún estado ni lógica
			// Este comportamiento permanece INACTIVO para siempre
		}
	}
}
