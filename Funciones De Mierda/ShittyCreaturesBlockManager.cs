using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public static class ShittyCreaturesBlockManager
	{
		public static Texture2D BlocksTexture { get; private set; }
		public static Texture2D AnimatedBlocksTexture { get; private set; }

		public static void Initialize()
		{
			try
			{
				// Intenta cargar la textura base
				BlocksTexture = ContentManager.Get<Texture2D>("Textures/ShittyCreaturesTextures", throwOnNotFound: false);
				if (BlocksTexture == null)
				{
					Log.Warning("ShittyCreaturesBlockManager: Textura base no encontrada, usando por defecto.");
					BlocksTexture = BlocksTexturesManager.DefaultBlocksTexture;
				}
			}
			catch (Exception ex)
			{
				Log.Error($"ShittyCreaturesBlockManager: Error cargando texturas - {ex.Message}");
				BlocksTexture = BlocksTexturesManager.DefaultBlocksTexture;
				AnimatedBlocksTexture = null;
			}
		}

		public static void Dispose()
		{
			if (BlocksTexture != null && BlocksTexture != BlocksTexturesManager.DefaultBlocksTexture)
				BlocksTexture.Dispose();
			if (AnimatedBlocksTexture != null)
				AnimatedBlocksTexture.Dispose();
		}
	}
}
