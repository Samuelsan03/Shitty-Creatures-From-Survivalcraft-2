using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class PoisonBulletBlock : FlatBlock
	{
		public override void Initialize()
		{
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Usar color verde venenoso
			Color poisonColor = new Color(100, 200, 100, 255); // Verde venenoso
			BlocksManager.DrawFlatOrImageExtrusionBlock(primitivesRenderer, value, size, ref matrix, null, poisonColor, false, environmentData);
		}

		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			// Crear partículas verdes
			Color poisonColor = new Color(100, 200, 100, 255);
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, 0.1f, poisonColor, TextureSlot);
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(this.BlockIndex, 0, 0);
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			string displayName;
			if (LanguageControl.TryGetBlock("PoisonBulletBlock:0", "DisplayName", out displayName))
			{
				return displayName;
			}
			return "Poison Bullet"; // Valor por defecto en inglés
		}

		public override string GetDescription(int value)
		{
			string description;
			if (LanguageControl.TryGetBlock("PoisonBulletBlock:0", "Description", out description))
			{
				return description;
			}
			return "Similar to a musket round, but it cannot be used as musket ammunition and is not available for crafting.";
		}

		public override float GetProjectilePower(int value)
		{
			return 0f; // Retornar 0 para que no funcione en mosquete
		}

		public override float GetExplosionPressure(int value)
		{
			return 0f;
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			return TextureSlot;
		}

		public static int Index = 325;
		public static int TextureSlot = 229; // Mismo slot que MusketBall
	}
}
