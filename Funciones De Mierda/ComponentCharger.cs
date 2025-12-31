using System;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentCharger : Component
	{
		// Propiedades configurables
		public float KnockbackForce = 15f;
		public float KnockbackVerticalFactor = 1.2f;
		public float AttackRange = 3f;

		// Referencias
		public ComponentCreature m_componentCreature;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);

			// Cargar valores
			KnockbackForce = valuesDictionary.GetValue<float>("KnockbackForce", 15f);
			KnockbackVerticalFactor = valuesDictionary.GetValue<float>("KnockbackVerticalFactor", 1.2f);
			AttackRange = valuesDictionary.GetValue<float>("AttackRange", 3f);
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);

			valuesDictionary.SetValue<float>("KnockbackForce", KnockbackForce);
			valuesDictionary.SetValue<float>("KnockbackVerticalFactor", KnockbackVerticalFactor);
			valuesDictionary.SetValue<float>("AttackRange", AttackRange);
		}

		/// <summary>
		/// Empuja al objetivo violentamente
		/// </summary>
		public void KnockbackTarget(ComponentBody targetBody)
		{
			if (targetBody == null || m_componentCreature == null) return;

			Vector3 direction = Vector3.Normalize(targetBody.Position - m_componentCreature.ComponentBody.Position);

			// Añadir componente vertical
			direction.Y = KnockbackVerticalFactor;
			direction = Vector3.Normalize(direction);

			// Aplicar impulso
			targetBody.ApplyImpulse(direction * KnockbackForce);
		}

		/// <summary>
		/// Empuje con fuerza personalizada
		/// </summary>
		public void KnockbackTarget(ComponentBody targetBody, float customForce)
		{
			if (targetBody == null || m_componentCreature == null) return;

			Vector3 direction = Vector3.Normalize(targetBody.Position - m_componentCreature.ComponentBody.Position);

			// Añadir componente vertical
			direction.Y = KnockbackVerticalFactor;
			direction = Vector3.Normalize(direction);

			// Aplicar impulso con fuerza personalizada
			targetBody.ApplyImpulse(direction * customForce);
		}

		/// <summary>
		/// Verifica si el objetivo está en rango para empujar
		/// </summary>
		public bool IsTargetInRange(ComponentBody targetBody)
		{
			if (targetBody == null || m_componentCreature == null) return false;

			float distance = Vector3.Distance(
				m_componentCreature.ComponentBody.Position,
				targetBody.Position
			);

			return distance <= AttackRange;
		}
	}
}
