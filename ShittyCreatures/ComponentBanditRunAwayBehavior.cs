using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	/// <summary>
	/// Comportamiento de huida para bandidos: NUNCA huyen. Siguen atacando incluso con salud baja o al oír ruidos.
	/// </summary>
	public class ComponentBanditRunAwayBehavior : ComponentRunAwayBehavior, IUpdateable
	{
		// Anulación completa del método Update: la importancia siempre es 0, nunca se activa el comportamiento.
		public override void Update(float dt)
		{
			m_importanceLevel = 0f;
			// No llamamos a base.Update para evitar que la máquina de estados haga algo.
			// La máquina de estados se queda en Inactive permanentemente.
		}

		// Ignorar cualquier petición de huir (por ejemplo, al ser herido).
		public override void RunAwayFrom(ComponentBody componentBody)
		{
			// No hacer nada.
		}

		// Ignorar ruidos.
		public override void HearNoise(ComponentBody sourceBody, Vector3 sourcePosition, float loudness)
		{
			// No hacer nada.
		}

		// Al cargar, forzamos LowHealthToEscape a 0 (aunque ya no se usa) y aseguramos que los manejadores de eventos
		// no provoquen comportamientos no deseados.
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			LowHealthToEscape = 0f;   // Por si acaso algún otro código lo consulta.

			// Eliminar cualquier delegado que pudiera causar comportamiento de huida
			ComponentHealth componentHealth = m_componentCreature.ComponentHealth;
			if (componentHealth != null)
			{
				// Eliminar específicamente el delegado de Injured que podría llamar a RunAwayFrom
				// Esto evita que al ser herido intente huir
				componentHealth.Injured = null;
			}
		}

		public new UpdateOrder UpdateOrder => UpdateOrder.Default;
	}
}
