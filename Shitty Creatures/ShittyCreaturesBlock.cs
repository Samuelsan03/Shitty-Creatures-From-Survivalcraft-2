using System;
using Engine.Graphics;
using Game;

namespace Game
{
	/// <summary>
	/// Clase base abstracta para bloques que utilizan la textura personalizada "Textures/ShittyCreaturesTextures".
	/// </summary>
	public abstract class ShittyCreaturesBlock : Block
	{
		// Textura cargada desde ContentManager (protegida para uso en clases derivadas)
		protected Texture2D m_texture;

		/// <summary>
		/// Inicializa el bloque cargando la textura personalizada.
		/// </summary>
		public override void Initialize()
		{
			m_texture = ContentManager.Get<Texture2D>("Textures/ShittyCreaturesTextures");
			base.Initialize();
		}

		// Aquí se pueden agregar otros métodos virtuales específicos de la temática,
		// por ejemplo GetDefaultWaterValue, GetToxicityValue, etc., según se necesite.
	}
}
