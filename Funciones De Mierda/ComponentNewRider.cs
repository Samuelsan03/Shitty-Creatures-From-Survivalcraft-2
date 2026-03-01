using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewRider : ComponentRider, IUpdateable
	{
		// Almacena el ID de la montura mientras se carga el mundo
		private int? m_mountEntityId;
		private bool m_loadCompleted;
		private Dictionary<int, Entity> m_entityCache;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);

			// Crear caché de entidades por ID para búsqueda rápida
			m_entityCache = new Dictionary<int, Entity>();
			foreach (Entity entity in Project.Entities)
			{
				m_entityCache[entity.Id] = entity;
			}

			// Leer el ID guardado (si existe)
			if (valuesDictionary.ContainsKey("MountEntityId"))
			{
				m_mountEntityId = valuesDictionary.GetValue<int>("MountEntityId");
			}
		}

		public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
		{
			base.Save(valuesDictionary, entityToIdMap);

			// Guardar el ID de la montura si está montado
			if (Mount != null)
			{
				// Usar Entity.Id directamente (según la recomendación)
				int mountId = Mount.Entity.Id;
				valuesDictionary.SetValue("MountEntityId", mountId);
			}
		}

		public void Update(float dt)
		{
			// Solo intentamos restaurar la montura una vez, después de que todas las entidades estén cargadas
			if (!m_loadCompleted && m_mountEntityId.HasValue && Mount == null)
			{
				// Buscar la entidad de la montura usando el caché
				if (m_entityCache != null && m_entityCache.TryGetValue(m_mountEntityId.Value, out Entity mountEntity))
				{
					ComponentMount mount = mountEntity.FindComponent<ComponentMount>();
					if (mount != null)
					{
						// Usar StartMounting para montar
						StartMounting(mount);

						// Cancelar cualquier velocidad vertical que pudiera causar daño por caída
						if (ComponentCreature != null && ComponentCreature.ComponentBody != null)
						{
							ComponentBody body = ComponentCreature.ComponentBody;
							Vector3 vel = body.Velocity;
							vel.Y = 0f;
							body.Velocity = vel;
						}
					}
				}

				m_loadCompleted = true;
				m_mountEntityId = null;
			}
		}
	}
}
