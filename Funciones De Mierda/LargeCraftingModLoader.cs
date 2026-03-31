using System;
using System.Collections.Generic;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class LargeCraftingModLoader : ModLoader
	{
		public override void __ModInitialize()
		{
			// No inicializamos aquí porque los bloques aún no están listos
		}

		// Este método se ejecuta después de que BlocksManager haya inicializado todos los bloques
		public override void BlocksInitalized()
		{
			// Inicializar el gestor de recetas
			LargeCraftingRecipesManager.Initialize();

			// Opcional: registrar el comportamiento del bloque si no se hace automáticamente
			var project = GameManager.Project;
			if (project != null)
			{
				var subsystemBehaviors = project.FindSubsystem<SubsystemBlockBehaviors>(true);
				if (subsystemBehaviors != null)
				{
					// El comportamiento ya debería estar registrado por la base de datos,
					// pero si no, se puede añadir aquí.
				}
			}
		}
	}
}
