using System;
using System.Collections.Generic;
using System.Text;
using Game;  // Referencia al namespace original
using Engine;
using GameEntitySystem;

namespace Funciones_De_Mierda.Game
{
	internal class ComponentNewHerdBehavior2 : ComponentNewHerdBehavior
	{
		// Campos adicionales para la versión extendida
		private float m_extendedHelpRange = 25f;
		private bool m_enableAdvancedGroupAI = true;
		private List<ComponentCreature> m_recentlyHelpedCreatures = new List<ComponentCreature>();
		private float m_lastHelpTime;

		// Constructor opcional
		public ComponentNewHerdBehavior2()
		{
			// Inicializaciones específicas
		}

		// Método para obtener el estado de ayuda automática
		public bool GetAutoHelpStatus()
		{
			return AutoNearbyCreaturesHelp; // Usa la propiedad pública
		}

		// Método para modificar el estado de ayuda
		public void SetAutoHelpStatus(bool status)
		{
			AutoNearbyCreaturesHelp = status;
			Console.WriteLine($"Ayuda automática establecida a: {status}");
		}

		// Método extendido para llamar ayuda
		public void EnhancedCallHelp(ComponentCreature target, float maxRange, float maxChaseTime, bool isPersistent)
		{
			if (target == null || !AutoNearbyCreaturesHelp)
				return;

			// Lógica adicional antes de llamar ayuda
			if (CanUseExtendedHelpRange())
			{
				maxRange = m_extendedHelpRange;
				Console.WriteLine($"Usando rango extendido de ayuda: {maxRange}");
			}

			// Llamar al método base
			CallNearbyCreaturesHelp(target, maxRange, maxChaseTime, isPersistent, true);

			// Registrar la ayuda
			RegisterHelpCall(target);
		}

		// Método para verificar y llamar ayuda con lógica avanzada
		public void CheckAndCallHelp(ComponentCreature target)
		{
			if (AutoNearbyCreaturesHelp && target != null && m_enableAdvancedGroupAI)
			{
				// Verificar si ya ayudamos recientemente a este objetivo
				if (!WasRecentlyHelped(target))
				{
					// Llamar ayuda con parámetros personalizados
					EnhancedCallHelp(target, 20f, 45f, true);

					// También activar nuestro propio comportamiento de persecución
					if (GetChaseBehavior() != null && CanAttackCreature(target))
					{
						GetChaseBehavior().Attack(target, 25f, 60f, true);
					}
				}
				else
				{
					// Usar DisplayName en lugar de Name
					string targetName = target.Entity.ValuesDictionary.DatabaseObject.Name ?? "Entidad Desconocida";
					Console.WriteLine($"Ya ayudamos recientemente a {targetName}");
				}
			}
		}

		// Método personalizado para lógica de manada extendida
		public void CustomHerdLogic()
		{
			if (!AutoNearbyCreaturesHelp)
			{
				Console.WriteLine("Ayuda automática está desactivada");
				return;
			}

			// Ejemplo de lógica personalizada
			Vector3? herdCenter = FindHerdCenter();
			if (herdCenter.HasValue)
			{
				Console.WriteLine($"Centro de manada encontrado en: {herdCenter.Value}");

				// Lógica adicional basada en la posición del centro
				float distance = Vector3.Distance(GetCreatureComponent().ComponentBody.Position, herdCenter.Value);

				if (distance > 15f && m_enableAdvancedGroupAI)
				{
					Console.WriteLine("Demasiado lejos del centro, activando modo de reagrupación");
					ActivateRegroupingMode();
				}
			}
		}

		// Método para verificar miembros de la manada cercanos
		public List<ComponentCreature> GetNearbyHerdMembers(float range = 15f)
		{
			List<ComponentCreature> herdMembers = new List<ComponentCreature>();

			if (string.IsNullOrEmpty(HerdName))
				return herdMembers;

			Vector3 position = GetCreatureComponent().ComponentBody.Position;
			float rangeSquared = range * range;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature != GetCreatureComponent() &&
					Vector3.DistanceSquared(position, creature.ComponentBody.Position) < rangeSquared)
				{
					ComponentNewHerdBehavior herdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
					if (herdBehavior != null && herdBehavior.HerdName == HerdName)
					{
						herdMembers.Add(creature);
					}
				}
			}

			return herdMembers;
		}

		// Método para evaluar amenazas en grupo
		public void EvaluateGroupThreat(ComponentCreature threat)
		{
			if (threat == null || !AutoNearbyCreaturesHelp)
				return;

			List<ComponentCreature> nearbyMembers = GetNearbyHerdMembers();

			// Solo actuar si hay suficientes miembros cercanos
			if (nearbyMembers.Count >= 2 && m_enableAdvancedGroupAI)
			{
				// Usar DisplayName en lugar de Name
				string threatName = threat.Entity.ValuesDictionary.DatabaseObject.Name ?? "Entidad Desconocida";
				Console.WriteLine($"Evaluando amenaza de {threatName} con {nearbyMembers.Count} aliados cercanos");

				// Lógica de decisión grupal
				if (ShouldGroupAttack(threat, nearbyMembers.Count))
				{
					EnhancedCallHelp(threat, 30f, 90f, true);
				}
			}
		}

		// Métodos auxiliares privados
		private bool CanUseExtendedHelpRange()
		{
			// Lógica para determinar si usar rango extendido
			return m_enableAdvancedGroupAI &&
				   GetCreatureComponent().ComponentHealth.Health > 0.5f;
		}

		private void RegisterHelpCall(ComponentCreature target)
		{
			m_recentlyHelpedCreatures.Add(target);
			m_lastHelpTime = (float)m_subsystemTime.GameTime;

			// Limpiar lista periódicamente
			if (m_recentlyHelpedCreatures.Count > 10)
			{
				m_recentlyHelpedCreatures.RemoveAt(0);
			}
		}

		private bool WasRecentlyHelped(ComponentCreature target)
		{
			// Verificar si ayudamos en los últimos 30 segundos
			float timeSinceLastHelp = (float)(m_subsystemTime.GameTime - m_lastHelpTime);
			return m_recentlyHelpedCreatures.Contains(target) && timeSinceLastHelp < 30f;
		}

		private void ActivateRegroupingMode()
		{
			// Implementar lógica de reagrupación
			Vector3? herdCenter = FindHerdCenter();
			if (herdCenter.HasValue && m_componentPathfinding != null)
			{
				m_componentPathfinding.SetDestination(
					herdCenter.Value,
					1.0f, // Velocidad máxima
					10f,  // Radio de llegada
					100,  // Máximo de posiciones
					false, true, false, null
				);
			}
		}

		private bool ShouldGroupAttack(ComponentCreature threat, int allyCount)
		{
			// Lógica simple: atacar si tenemos ventaja numérica
			return allyCount >= 3 ||
				   (allyCount >= 2 && GetCreatureComponent().ComponentHealth.Health > 0.7f);
		}

		// Método para configurar comportamiento avanzado
		public void ConfigureAdvancedAI(bool enableExtendedRange = true, bool enableGroupLogic = true)
		{
			m_enableAdvancedGroupAI = enableGroupLogic;
			m_extendedHelpRange = enableExtendedRange ? 35f : 20f;

			Console.WriteLine($"AI avanzada configurada - Rango: {m_extendedHelpRange}, Lógica grupal: {m_enableAdvancedGroupAI}");
		}

		// Sobrescribir el método Update para agregar funcionalidad
		public override void Update(float dt)
		{
			// Llamar al método base primero
			base.Update(dt);

			// Agregar lógica personalizada
			if (m_enableAdvancedGroupAI)
			{
				UpdateAdvancedAI(dt);
			}
		}

		private void UpdateAdvancedAI(float dt)
		{
			// Lógica de IA avanzada periódica
			// Corregido: usar 2.0f en lugar de 2.0 (float en lugar de double)
			if (m_subsystemTime.PeriodicGameTimeEvent(2.0f, 0.0f))
			{
				// Verificar amenazas cercanas periódicamente
				ScanForThreats();

				// Verificar estado de la manada
				CheckHerdCohesion();
			}
		}

		private void ScanForThreats()
		{
			// Implementar escaneo de amenazas
			// Esta es una implementación de ejemplo
			Vector3 position = GetCreatureComponent().ComponentBody.Position;

			foreach (ComponentCreature creature in m_subsystemCreatureSpawn.Creatures)
			{
				if (creature != GetCreatureComponent() &&
					Vector3.Distance(position, creature.ComponentBody.Position) < 20f &&
					!IsSameHerd(creature))
				{
					// Verificar si es una amenaza
					ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
					if (chase != null && chase.Target != null && IsSameHerd(chase.Target))
					{
						EvaluateGroupThreat(creature);
					}
				}
			}
		}

		private void CheckHerdCohesion()
		{
			List<ComponentCreature> nearbyMembers = GetNearbyHerdMembers(25f);
			if (nearbyMembers.Count < 2 && !string.IsNullOrEmpty(HerdName))
			{
				Console.WriteLine($"Advertencia: Solo {nearbyMembers.Count + 1} miembros de manada cerca");
			}
		}

		// Método para obtener el nombre de una entidad (solución alternativa)
		private string GetEntityName(Entity entity)
		{
			if (entity == null) return "Null";

			// Intentar obtener el nombre de diferentes maneras
			if (entity.ValuesDictionary != null && entity.ValuesDictionary.DatabaseObject != null)
			{
				return entity.ValuesDictionary.DatabaseObject.Name ?? "Sin nombre";
			}

			return entity.GetType().Name;
		}
	}
}
